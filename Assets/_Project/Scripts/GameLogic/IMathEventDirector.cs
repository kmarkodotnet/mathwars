using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.Core.Skills;
using MathHorde.MathEngine;

namespace MathHorde.GameLogic
{
    public interface IMathEventDirector
    {
        void Initialize(LevelDefinition level, DifficultyProfile difficulty, IMathProblemGenerator generator, IRandomSource random);
        Result<bool> PrepareUpcomingEvents(double gameTime);
        void Tick(double gameTime);
        void OnPlayerLaneSelected(LaneIndex lane, double gameTime);
        void OnSessionEnded(double gameTime);
        void OnRewardProcessed(in RewardApplication application, double gameTime);
        IReadOnlyList<ActiveMathEvent> ActiveEvents { get; }
        IReadOnlyList<ResolveCommand> PendingResolutions { get; }
        void ClearPendingResolutions();
        void ApplyTimePressure(float thinkTimeMultiplier, float gateSpeedMultiplier);
    }

    /// <summary>Explicit empty-schedule adapter for the F1 scene until F3-12 composition.</summary>
    public class NullMathEventDirector : IMathEventDirector
    {
        public IReadOnlyList<ActiveMathEvent> ActiveEvents => Array.Empty<ActiveMathEvent>();
        public IReadOnlyList<ResolveCommand> PendingResolutions => Array.Empty<ResolveCommand>();
        public virtual void Initialize(LevelDefinition level, DifficultyProfile difficulty, IMathProblemGenerator generator, IRandomSource random) { }
        public virtual Result<bool> PrepareUpcomingEvents(double gameTime) => Result<bool>.Ok(true);
        public virtual void Tick(double gameTime) { }
        public virtual void OnPlayerLaneSelected(LaneIndex lane, double gameTime) { }
        public virtual void OnSessionEnded(double gameTime) { }
        public virtual void OnRewardProcessed(in RewardApplication application, double gameTime) { }
        public virtual void ClearPendingResolutions() { }
        public virtual void ApplyTimePressure(float thinkTimeMultiplier, float gateSpeedMultiplier) { }
    }
}
