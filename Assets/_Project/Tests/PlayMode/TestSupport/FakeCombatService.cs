// FakeCombatService.cs
// F1-13 — minimal ICombatService test double for PlayerControllerTests.
// PlayerController only ever reads PlayerLane and calls MovePlayerToLane
// (it must never drive combat itself — game-logic.md 3., "harc a
// mateseménytől függetlenül" applies symmetrically: input never touches
// combat simulation state). Every other member deliberately throws, so a
// test fails loudly if PlayerController ever grows a call it should not
// make (same pattern as TestSupport/FakeResourceService.cs).
using System;
using System.Collections.Generic;
using MathHorde.Core.Common;
using MathHorde.Core.Levels;
using MathHorde.GameLogic;

namespace MathHorde.Tests.PlayMode.TestSupport
{
    public sealed class FakeCombatService : ICombatService
    {
        public LaneIndex PlayerLane { get; set; } = new LaneIndex(0);

        public int MoveCallCount { get; private set; }

        public LaneIndex? LastRequestedLane { get; private set; }

        public void MovePlayerToLane(LaneIndex lane)
        {
            MoveCallCount++;
            LastRequestedLane = lane;
            PlayerLane = lane;
        }

        public bool IsPlayerAlive => throw new NotSupportedException("PlayerController nem olvassa az IsPlayerAlive-ot.");

        public IReadOnlyList<EnemySnapshot> ActiveEnemies => throw new NotSupportedException("PlayerController nem olvassa az ActiveEnemies-t.");

        public void Initialize(LevelDefinition level, WeaponDefinition weapon, IReadOnlyDictionary<string, EnemyDefinition> enemyCatalog) =>
            throw new NotSupportedException("PlayerController nem hívhatja az Initialize-ot (csak a GameSessionService).");

        public void Tick(double gameTime, float deltaTime) =>
            throw new NotSupportedException("PlayerController nem hívhatja a Tick-et (csak a GameSessionService).");

        public int SpawnEnemy(string enemyId, LaneIndex lane, float spawnDepth) =>
            throw new NotSupportedException("PlayerController nem spawnol ellenséget.");

        public void ApplyCombatModifiers(CombatModifiers modifiers) =>
            throw new NotSupportedException("PlayerController nem módosítja a harci szorzókat.");
    }
}
