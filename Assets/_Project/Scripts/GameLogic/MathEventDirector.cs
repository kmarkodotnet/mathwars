using System;
using System.Collections.Generic;
using System.Globalization;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Problems;
using MathHorde.Core.Skills;
using MathHorde.GameLogic.Events;
using MathHorde.MathEngine;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Owns only math scheduling and lifecycle. Prepare is an external host
    /// phase; Tick never generates, formats, classifies or applies rewards.
    /// All mutable storage is reserved for the entire schedule at Initialize.
    /// </summary>
    public sealed class MathEventDirector : IMathEventDirector
    {
        private readonly IResourceService _resources;
        private readonly IGameEventSink _events;
        private readonly MathEventDirectorSettings _settings;
        private LevelDefinition _level;
        private DifficultyProfile _difficulty;
        private IMathProblemGenerator _generator;
        private ScheduledEvent[] _schedule;
        private List<ActiveMathEvent> _active;
        private List<ResolveCommand> _pending;
        private bool _ended;
        private double _lastTime;
        private float _thinkMultiplier = 1f, _speedMultiplier = 1f;

        public IReadOnlyList<ActiveMathEvent> ActiveEvents { get; private set; } = Array.Empty<ActiveMathEvent>();
        public IReadOnlyList<ResolveCommand> PendingResolutions { get; private set; } = Array.Empty<ResolveCommand>();

        public MathEventDirector(IResourceService resources, IGameEventSink events, MathEventDirectorSettings settings)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize(LevelDefinition level, DifficultyProfile difficulty, IMathProblemGenerator generator, IRandomSource random)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            if (random == null) throw new ArgumentNullException(nameof(random));
            _ended = false; _lastTime = 0; _thinkMultiplier = 1f; _speedMultiplier = 1f;
            _schedule = new ScheduledEvent[level.MathEvents.Count];
            _active = new List<ActiveMathEvent>(_schedule.Length);
            _pending = new List<ResolveCommand>(_schedule.Length);
            ActiveEvents = _active.AsReadOnly(); PendingResolutions = _pending.AsReadOnly();
            for (int index = 0; index < _schedule.Length; index++)
            {
                var entry = level.MathEvents[index] ?? throw new ArgumentException("Null math schedule entry.", nameof(level));
                if (!Finite(entry.TriggerGameTime) || !Positive(entry.PreviewDurationSeconds)
                    || float.IsNaN(entry.ThinkTimeSecondsOverride) || float.IsInfinity(entry.ThinkTimeSecondsOverride))
                    throw new ArgumentException("Non-finite math schedule time.", nameof(level));
                int? target = _settings.TargetsByScheduleIndex.TryGetValue(index, out int value) ? value : (int?)null;
                _schedule[index] = new ScheduledEvent(index, entry,
                    new ActiveMathEvent(MathEventId.Create(random.Seed, index), entry.ProblemType, entry.RewardResource, entry.Goal, target),
                    random.Derive("math-event:" + index.ToString(CultureInfo.InvariantCulture)).Seed,
                    entry.ThinkTimeSecondsOverride > 0 ? entry.ThinkTimeSecondsOverride : difficulty.BaseThinkTimeSeconds);
            }
            Array.Sort(_schedule, (a, b) =>
            {
                int time = a.Entry.TriggerGameTime.CompareTo(b.Entry.TriggerGameTime);
                return time != 0 ? time : a.Index.CompareTo(b.Index);
            });
        }

        public Result<bool> PrepareUpcomingEvents(double gameTime)
        {
            RequireInitialized(); CheckTime(gameTime);
            if (_ended) return Result<bool>.Ok(true);
            string firstError = null;
            for (int i = 0; i < _schedule.Length; i++)
            {
                var s = _schedule[i];
                if (s.View.Machine.IsTerminal || s.View.State == MathEventState.Resolving) continue;
                if (!s.Attempted)
                {
                    s.Attempted = true;
                    s.Error = Prepare(s);
                }
                if (s.Error != null && firstError == null) firstError = s.Error;
            }
            return firstError == null ? Result<bool>.Ok(true) : Result<bool>.Fail(firstError);
        }

        private string Prepare(ScheduledEvent s)
        {
            var e = s.Entry;
            bool gate = e.ProblemType == ProblemType.ResourceOperationComparison;
            if (!_settings.RewardsByScheduleIndex.TryGetValue(s.Index, out var reward) || reward == null
                || reward.IncorrectAmount != 0 || reward.FlatAmount <= 0
                || !Enum.IsDefined(typeof(ProblemType), e.ProblemType) || !Enum.IsDefined(typeof(GateGoal), e.Goal))
                return "MATH_EVENT_CONFIG_INVALID";
            string rewardError = Core.Resources.ResourceRules.ValidateRewardPair(e.RewardResource, reward.Resource);
            if (rewardError != null) return rewardError;
            bool needsTarget = e.ProblemType == ProblemType.TargetValueSelection || (gate && e.Goal == GateGoal.ReachTarget);
            if (needsTarget && !s.View.TargetValue.HasValue) return "GATE_TARGET_MISSING";
            if (!needsTarget && s.View.TargetValue.HasValue) return "MATH_EVENT_CONFIG_INVALID";
            var result = _generator.Generate(new ProblemGenerationRequest
            {
                Seed = s.Seed, Type = e.ProblemType, Difficulty = _difficulty, LaneCount = _level.LaneCount,
                ThinkTimeSeconds = s.BaseThinkTime, RewardResource = e.RewardResource, Reward = reward,
                Goal = e.Goal, TargetValue = s.View.TargetValue,
                GateLimits = gate ? _settings.GateLimits : (GateGenerationLimits?)null,
            });
            if (!result.IsSuccess) return result.ErrorCode;
            s.Problem = result.Value;
            s.View.RewardAmount = reward.FlatAmount;
            return null;
        }

        public void Tick(double gameTime)
        {
            RequireInitialized(); CheckTime(gameTime); _lastTime = gameTime;
            RemoveOldFeedback(gameTime);
            if (_ended) return;
            // Running-session preparation failures also expire waiting events,
            // without producing a child's timeout or a resolution command.
            for (int i = 0; i < _schedule.Length; i++)
            {
                var s = _schedule[i];
                if (!s.View.Machine.IsTerminal && s.Error != null) PreparationFailed(s, s.Error, gameTime);
            }
            int occupied = 0;
            for (int i = 0; i < _active.Count; i++) if (!_active[i].Machine.IsTerminal) occupied++;
            for (int i = 0; i < _schedule.Length; i++)
            {
                var s = _schedule[i]; var v = s.View;
                if (v.State == MathEventState.Inactive && s.Entry.TriggerGameTime <= gameTime && occupied < _settings.MaxConcurrentMathEvents)
                {
                    if (!s.Attempted || s.Problem == null)
                    { PreparationFailed(s, "MATH_EVENT_NOT_PREPARED", gameTime); continue; }
                    v.PreviewStartedAt = gameTime;
                    s.EffectiveThinkTime = (double)s.BaseThinkTime * _thinkMultiplier;
                    s.EffectiveSpeed = (double)_settings.GateApproachSpeed * _speedMultiplier;
                    v.Problem = v.Type == ProblemType.ResourceOperationComparison ? null : s.Problem;
                    v.Machine.Fire(MathEventTrigger.Schedule, gameTime);
                    _active.Add(v); occupied++;
                    _events.Publish(new MathEventPreviewStarted(v.EventId, v.Type, v.ProblemId,
                        v.Problem?.DisplayExpression ?? string.Empty, v.RewardResource, v.Goal, v.TargetValue, gameTime));
                }
                if (v.State == MathEventState.Preview && gameTime >= v.PreviewStartedAt + s.Entry.PreviewDurationSeconds)
                {
                    v.Problem = s.Problem;
                    s.SpawnedAt = gameTime;
                    FillCarriers(s, _settings.GateSpawnDepth);
                    v.Machine.Fire(MathEventTrigger.PreviewElapsed, gameTime);
                    _events.Publish(new MathEventGatesSpawned(v.EventId, v.GateBaseValue, v.RewardResource, gameTime));
                }
                if (v.State == MathEventState.Approaching)
                {
                    double arrival = s.SpawnedAt + (_settings.GateSpawnDepth - _settings.DecisionZoneDepth) / s.EffectiveSpeed;
                    float depth = (float)Math.Max(_settings.DecisionZoneDepth,
                        _settings.GateSpawnDepth - (gameTime - s.SpawnedAt) * s.EffectiveSpeed);
                    MoveCarriers(v, depth);
                    if (gameTime >= arrival)
                    {
                        v.DecisionOpenedAt = arrival; v.DeadlineGameTime = arrival + s.EffectiveThinkTime;
                        v.Machine.Fire(MathEventTrigger.DecisionZoneEntered, gameTime);
                        _events.Publish(new MathEventDecisionWindowOpened(v.EventId, v.DeadlineGameTime, gameTime));
                    }
                }
                if (v.State == MathEventState.AwaitingDecision && gameTime >= v.DeadlineGameTime)
                { Expire(s, "DEADLINE", gameTime); Enqueue(v, default, true, gameTime); occupied--; }
            }
        }

        public void OnPlayerLaneSelected(LaneIndex lane, double gameTime)
        {
            RequireInitialized(); CheckTime(gameTime); _lastTime = gameTime;
            if (_ended || lane.Value >= _level.LaneCount) return;
            for (int i = 0; i < _schedule.Length; i++)
            {
                var s = _schedule[i]; var v = s.View;
                if (v.State != MathEventState.AwaitingDecision) continue;
                if (gameTime >= v.DeadlineGameTime)
                { Expire(s, "DEADLINE", gameTime); Enqueue(v, default, true, gameTime); continue; }
                bool present = false;
                for (int c = 0; c < v.Carriers.Count; c++) present |= v.Carriers[c].Lane == lane;
                if (!present) continue;
                v.Machine.Fire(MathEventTrigger.LaneSelected, gameTime);
                Enqueue(v, lane, false, gameTime);
            }
        }

        public void OnRewardProcessed(in RewardApplication application, double gameTime)
        {
            RequireInitialized(); CheckTime(gameTime); _lastTime = gameTime;
            if (_ended || (application.Status != RewardApplicationStatus.Applied && application.Status != RewardApplicationStatus.SkippedIncorrect)) return;
            for (int i = 0; i < _schedule.Length; i++)
            {
                var s = _schedule[i]; var v = s.View;
                if (v.EventId != application.EventId || v.State != MathEventState.Resolving) continue;
                v.Machine.Fire(MathEventTrigger.RewardApplied, gameTime); s.TerminalAt = gameTime;
                _events.Publish(new MathEventCompleted(v.EventId,
                    application.Status == RewardApplicationStatus.SkippedIncorrect ? 0 : application.AppliedDelta, application.Resource, gameTime));
                return;
            }
        }

        public void OnSessionEnded(double gameTime)
        {
            RequireInitialized(); CheckTime(gameTime); _lastTime = gameTime;
            if (_ended) return;
            _ended = true;
            for (int i = 0; i < _schedule.Length; i++)
                if (!_schedule[i].View.Machine.IsTerminal) Expire(_schedule[i], "SESSION_ENDED", gameTime);
            // Undrained choices cannot be rewarded after session end.
            _pending.Clear();
        }

        public void ClearPendingResolutions() => _pending?.Clear();

        public void ApplyTimePressure(float thinkTimeMultiplier, float gateSpeedMultiplier)
        {
            if (!Positive(thinkTimeMultiplier)) throw new ArgumentOutOfRangeException(nameof(thinkTimeMultiplier));
            if (!Positive(gateSpeedMultiplier)) throw new ArgumentOutOfRangeException(nameof(gateSpeedMultiplier));
            _thinkMultiplier = thinkTimeMultiplier; _speedMultiplier = gateSpeedMultiplier;
        }

        private void FillCarriers(ScheduledEvent s, float depth)
        {
            var v = s.View;
            for (int i = 0; i < v.Problem.Options.Count; i++)
            {
                var o = v.Problem.Options[i];
                v.CarrierBuffer.Add(new GateCarrier(o.OptionIndex, o.Lane, new Vec2(o.Lane.Value, depth), o.DisplayLabel, o.IsCorrect));
            }
        }

        private static void MoveCarriers(ActiveMathEvent v, float depth)
        {
            for (int i = 0; i < v.CarrierBuffer.Count; i++)
            {
                var c = v.CarrierBuffer[i];
                v.CarrierBuffer[i] = new GateCarrier(c.OptionIndex, c.Lane, new Vec2(c.Lane.Value, depth), c.DisplayLabel, c.IsCorrect);
            }
        }

        private void Enqueue(ActiveMathEvent v, LaneIndex lane, bool timedOut, double time)
            => _pending.Add(new ResolveCommand(v.EventId, lane, timedOut, time));

        private void PreparationFailed(ScheduledEvent s, string code, double time)
        {
            _events.Publish(new MathEventPreparationFailed(s.View.EventId, code, time));
            Expire(s, "PREPARATION_FAILED", time);
        }

        private void Expire(ScheduledEvent s, string reason, double time)
        {
            if (s.View.State == MathEventState.Inactive) _active.Add(s.View);
            s.View.Machine.Fire(reason == "DEADLINE" ? MathEventTrigger.DeadlineReached : MathEventTrigger.SessionEnded, time);
            s.TerminalAt = time;
            _events.Publish(new MathEventExpired(s.View.EventId, reason, time));
        }

        private void RemoveOldFeedback(double time)
        {
            for (int i = 0; i < _schedule.Length; i++)
            {
                var s = _schedule[i];
                if (s.View.Machine.IsTerminal && time - s.TerminalAt >= _settings.FeedbackDisplaySeconds) _active.Remove(s.View);
            }
        }

        private static bool Positive(float value) => value > 0 && !float.IsInfinity(value);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private void CheckTime(double time)
        {
            if (!Finite(time) || time < _lastTime) throw new ArgumentOutOfRangeException(nameof(time));
        }
        private void RequireInitialized()
        {
            if (_schedule == null) throw new InvalidOperationException("Initialize must precede director use.");
        }

        private sealed class ScheduledEvent
        {
            internal readonly int Index, Seed;
            internal readonly MathEventScheduleEntry Entry;
            internal readonly ActiveMathEvent View;
            internal readonly float BaseThinkTime;
            internal MathProblem Problem;
            internal bool Attempted;
            internal string Error;
            internal double EffectiveThinkTime, EffectiveSpeed;
            internal double SpawnedAt, TerminalAt;
            internal ScheduledEvent(int index, MathEventScheduleEntry entry, ActiveMathEvent view, int seed, float baseThinkTime)
            { Index = index; Entry = entry; View = view; Seed = seed; BaseThinkTime = baseThinkTime; }
        }
    }
}
