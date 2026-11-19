using System;
using System.Collections.Generic;
using MathHorde.Core.Resources;

namespace MathHorde.GameLogic
{
    public interface IRewardService
    {
        RewardApplication ApplyReward(in GateResolution resolution);
        ResourceChange ApplyCombatReward(ResourceType type, int amount, string reasonCode);
        IReadOnlyCollection<Guid> AppliedRewardEventIds { get; }
        void RestoreAppliedRewards(IReadOnlyCollection<Guid> eventIds);
    }
}
