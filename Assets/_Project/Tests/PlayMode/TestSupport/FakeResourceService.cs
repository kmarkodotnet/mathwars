// FakeResourceService.cs
// F1-15 — minimal IResourceService test double for Presentation-layer tests
// (GameplayHudModelTests). Only GetCurrent/GetMax are exercised by
// GameplayHudModel (contract game-logic.md 2.1: Presentation only ever
// reads through this interface, never mutates) — every mutation method
// deliberately throws so a test would fail loudly if the HUD model ever
// tried to call one.
using System;
using System.Collections.Generic;
using MathHorde.Core.Levels;
using MathHorde.Core.Resources;
using MathHorde.GameLogic;

namespace MathHorde.Tests.PlayMode.TestSupport
{
    public sealed class FakeResourceService : IResourceService
    {
        private readonly Dictionary<ResourceType, int> _current = new Dictionary<ResourceType, int>();
        private readonly Dictionary<ResourceType, int> _max = new Dictionary<ResourceType, int>();

        public void SetPool(ResourceType type, int current, int max)
        {
            _current[type] = current;
            _max[type] = max;
        }

        public ResourceState State => throw new NotSupportedException("GameplayHudModel never reads .State directly.");

        public int GetCurrent(ResourceType type) => _current.TryGetValue(type, out int value) ? value : 0;

        public int GetMax(ResourceType type) => _max.TryGetValue(type, out int value) ? value : 0;

        public ResourceChange ApplyDelta(ResourceType type, int delta, string reasonCode) =>
            throw new NotSupportedException("Presentation nem hívhat ApplyDelta-t (contract game-logic.md 2.1).");

        public bool TryConsume(ResourceType type, int amount, string reasonCode, out ResourceChange change) =>
            throw new NotSupportedException("Presentation nem hívhat TryConsume-ot (contract game-logic.md 2.1).");

        public ResourceChange SetMax(ResourceType type, int newMax, string reasonCode) =>
            throw new NotSupportedException("Presentation nem hívhat SetMax-ot (contract game-logic.md 2.1).");

        public int CaptureSnapshot(ResourceType type) =>
            throw new NotSupportedException("Presentation nem hívhat CaptureSnapshot-ot.");

        public void Initialize(LevelDefinition level) =>
            throw new NotSupportedException("Csak a GameSessionService hívhatja (contract game-logic.md 2.).");
    }
}
