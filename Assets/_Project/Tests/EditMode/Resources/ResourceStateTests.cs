using System.Collections.Generic;
using MathHorde.Core.Resources;
using NUnit.Framework;

namespace MathHorde.Tests.EditMode.Resources
{
    [TestFixture]
    public class ResourceStateTests
    {
        [Test]
        public void GetCurrent_UnknownType_ReturnsZero()
        {
            var state = new ResourceState(new Dictionary<ResourceType, ResourcePool>());

            Assert.That(state.GetCurrent(ResourceType.Ammo), Is.EqualTo(0));
        }

        [Test]
        public void GetMax_UnknownType_ReturnsZero()
        {
            var state = new ResourceState(new Dictionary<ResourceType, ResourcePool>());

            Assert.That(state.GetMax(ResourceType.Ammo), Is.EqualTo(0));
        }

        [Test]
        public void WithPool_ReturnsNewInstance_OriginalStateUnchanged()
        {
            var original = new ResourceState(new Dictionary<ResourceType, ResourcePool>());
            var updatedPool = new ResourcePool(ResourceType.Ammo, current: 50, max: 200, min: 0);

            var updated = original.WithPool(updatedPool);

            Assert.That(original.GetCurrent(ResourceType.Ammo), Is.EqualTo(0));
            Assert.That(updated.GetCurrent(ResourceType.Ammo), Is.EqualTo(50));
        }

        [Test]
        public void WithPool_ReplacesExistingTypeEntry()
        {
            var initialPool = new ResourcePool(ResourceType.Ammo, current: 20, max: 200, min: 0);
            var state = new ResourceState(new Dictionary<ResourceType, ResourcePool> { [ResourceType.Ammo] = initialPool });

            var replacedPool = new ResourcePool(ResourceType.Ammo, current: 33, max: 200, min: 0);
            var updated = state.WithPool(replacedPool);

            Assert.That(updated.GetCurrent(ResourceType.Ammo), Is.EqualTo(33));
            Assert.That(updated.Pools.Count, Is.EqualTo(1));
        }
    }
}
