// LevelTemplate.cs
// Contract scriptable-object-schema.md 3. (LevelDefinitionSO.TemplateType). Explicit
// numeric values (core-domain.md 0. enum rule — Unity serializes enums as int, so a
// reordering would silently rewrite every already-authored LevelDefinitionSO asset).
// This enum is SO-editor-only for now: no GameLogic/Core contract currently reads it
// (LevelDefinition's Core DTO has no TemplateType field yet), so it lives in the
// Infrastructure/Configuration namespace instead of MathHorde.Core.Levels.
namespace MathHorde.Infrastructure.Configuration
{
    public enum LevelTemplate
    {
        Tutorial = 0,
        Standard = 1,
        BossLevel = 2
    }
}
