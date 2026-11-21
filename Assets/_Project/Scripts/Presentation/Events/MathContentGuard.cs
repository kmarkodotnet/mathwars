using MathHorde.Core.Resources;
using MathHorde.GameLogic;

namespace MathHorde.Presentation.Events
{
    internal static class MathContentGuard
    {
        internal static void RequireActive(ActiveMathEvent item)
        {
            ResourceRules.RequireActive(item.RewardResource);
            if (item.Problem != null) ResourceRules.RequireActive(item.Problem.Reward.Resource);
            if (item.ResourceGate.HasValue)
                ResourceRules.RequireActive(item.ResourceGate.Value.Prepared.Reward.Resource);
        }
    }
}
