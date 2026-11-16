// IValidatableConfig.cs
// Contract scriptable-object-schema.md 0. ("Validáció" row): every configuration
// ScriptableObject in this folder implements this interface so the asset's own
// OnValidate() can run the SAME checks that the EditMode tests exercise, and log
// them to the Editor console. Validate() never throws and never mutates state —
// it only reports. An empty list means the asset is valid.
using System.Collections.Generic;

namespace MathHorde.Infrastructure.Configuration
{
    public interface IValidatableConfig
    {
        IReadOnlyList<string> Validate();
    }
}
