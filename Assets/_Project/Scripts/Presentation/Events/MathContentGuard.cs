using MathHorde.Core.Resources;
using MathHorde.GameLogic;

namespace MathHorde.Presentation.Events
{
    internal static class MathContentGuard
    {
        internal static void RequireActive(ActiveMathEvent item)
        {
            string error = ResourceRules.ValidateRewardPair(item.RewardResource, item.Problem?.Reward.Resource ?? item.RewardResource);
            if (error != null) throw new System.ArgumentException(error);
        }
    }
}
