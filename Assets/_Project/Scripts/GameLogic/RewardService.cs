using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MathHorde.Core.Common;
using MathHorde.Core.Resources;
using MathHorde.GameLogic.Events;

namespace MathHorde.GameLogic
{
    /// <summary>
    /// Contract game-logic.md 7. Journals terminal math outcomes before publishing any
    /// resource events, so even a synchronous replay cannot credit the same event twice.
    /// </summary>
    public sealed class RewardService : IRewardService
    {
        private readonly IResourceService _resources;
        private readonly IGameEventSink _events;
        private readonly IGameClock _clock;
        private readonly int _eventCapacity;
        private HashSet<MathEventId> _appliedRewards;
        private List<Guid> _eventIds;
        private ReadOnlyCollection<Guid> _eventIdsView;
        private int _reservedCount;

        /// <param name="eventCapacity">
        /// Maximum additional distinct event IDs between restores, normally the level's
        /// full schedule count. Reservation and restore must run outside the session Tick.
        /// </param>
        public RewardService(IResourceService resources, IGameEventSink events, IGameClock clock,
            int eventCapacity)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (eventCapacity < 0) throw new ArgumentOutOfRangeException(nameof(eventCapacity));
            _eventCapacity = eventCapacity;
            _reservedCount = eventCapacity;
            _appliedRewards = new HashSet<MathEventId>(eventCapacity);
            _eventIds = new List<Guid>(eventCapacity);
            _eventIdsView = _eventIds.AsReadOnly();
        }

        // Includes skipped terminal outcomes: a later contradictory replay cannot award them.
        public IReadOnlyCollection<Guid> AppliedRewardEventIds => _eventIdsView;

        public void RestoreAppliedRewards(IReadOnlyCollection<Guid> eventIds)
        {
            if (eventIds == null) throw new ArgumentNullException(nameof(eventIds));
            int capacity = checked(eventIds.Count + _eventCapacity);
            var restored = new HashSet<MathEventId>(capacity);
            var ids = new List<Guid>(capacity);
            foreach (var id in eventIds)
                if (restored.Add(new MathEventId(id))) ids.Add(id);
            _appliedRewards = restored;
            _eventIds = ids;
            _eventIdsView = ids.AsReadOnly();
            _reservedCount = capacity;
        }

        public RewardApplication ApplyReward(in GateResolution resolution)
        {
            ResourceRules.RequireActive(resolution.Resource);
            if (_appliedRewards.Contains(resolution.EventId))
                return Skip(resolution, RewardApplicationStatus.AlreadyApplied, "ALREADY_APPLIED");
            if (resolution.Outcome < GateResolutionOutcome.CorrectRewarded
                || resolution.Outcome > GateResolutionOutcome.Duplicate)
                throw new ArgumentOutOfRangeException(nameof(resolution));
            if (_appliedRewards.Count >= _reservedCount)
                throw new InvalidOperationException("Reward capacity exhausted; reserve the full schedule before Tick.");

            _appliedRewards.Add(resolution.EventId);
            _eventIds.Add(resolution.EventId.Value);
            switch (resolution.Outcome)
            {
                case GateResolutionOutcome.IncorrectNoReward:
                    return Skip(resolution, RewardApplicationStatus.SkippedIncorrect, "INCORRECT");
                case GateResolutionOutcome.Expired:
                    return Skip(resolution, RewardApplicationStatus.SkippedExpired, "EXPIRED");
                case GateResolutionOutcome.Duplicate:
                    return Skip(resolution, RewardApplicationStatus.AlreadyApplied, "ALREADY_APPLIED");
            }

            // Keep the raw mathematical delta. Only the resource service clamps against
            // the current live pool; the captured gate snapshot is never written back.
            var change = _resources.ApplyDelta(resolution.Resource, resolution.RewardDelta, "MATH_GATE");
            _events.Publish(new RewardApplied(resolution.EventId, resolution.Resource,
                change.AppliedDelta, _clock.GameTime));
            return new RewardApplication(resolution.EventId, RewardApplicationStatus.Applied,
                resolution.RewardDelta, change.AppliedDelta, resolution.Resource);
        }

        public ResourceChange ApplyCombatReward(ResourceType type, int amount, string reasonCode)
        {
            ResourceRules.RequireActive(type);
            return _resources.ApplyDelta(type, amount, reasonCode);
        }

        private RewardApplication Skip(in GateResolution resolution, RewardApplicationStatus status,
            string reasonCode)
        {
            _events.Publish(new RewardSkipped(resolution.EventId, reasonCode, _clock.GameTime));
            return new RewardApplication(resolution.EventId, status, resolution.RewardDelta, 0,
                resolution.Resource);
        }
    }
}
