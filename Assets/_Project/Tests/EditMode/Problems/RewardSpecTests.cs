using MathHorde.Core.Problems;
using MathHorde.Core.Resources;
using NUnit.Framework;
namespace MathHorde.Tests.EditMode.Problems
{
    public class RewardSpecTests
    {
        [Test]
        public void FixedRewardStoresHealthAmountAndIncorrectAmount()
        {
            var reward = new RewardSpec(ResourceType.Health, 30, 2);
            Assert.That(reward.Resource, Is.EqualTo(ResourceType.Health));
            Assert.That(reward.FlatAmount, Is.EqualTo(30));
            Assert.That(reward.IncorrectAmount, Is.EqualTo(2));
        }
    }
}
