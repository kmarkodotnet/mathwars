using System;

namespace MathHorde.Core.Resources
{
    /// <summary>ADR-017: the persisted Ammo ordinal is a tombstone, never a live resource.</summary>
    public static class ResourceRules
    {
        public const string AmmoNotAllowed = "AMMO_NOT_ALLOWED";
        public const string InvalidRequest = "INVALID_REQUEST";

        public static void RequireActive(ResourceType type)
        {
            if (type == ResourceType.Ammo)
                throw new ArgumentException(AmmoNotAllowed, nameof(type));
        }

        // Shared by both generation paths and validation; check both independently
        // before the non-ammo consistency check, so Ammo always has the same error.
        public static string ValidateRewardPair(ResourceType resource, ResourceType rewardResource)
        {
            if (resource == ResourceType.Ammo || rewardResource == ResourceType.Ammo)
                return AmmoNotAllowed;
            // Explicit, stable ordinals; avoid Enum.IsDefined boxing on the Bind hot path.
            if (resource != rewardResource || resource < ResourceType.Health || resource > ResourceType.Energy)
                return InvalidRequest;
            return null;
        }
    }
}
