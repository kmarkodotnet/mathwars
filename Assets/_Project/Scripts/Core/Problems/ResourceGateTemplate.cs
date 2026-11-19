using System;
using System.Collections.Generic;

namespace MathHorde.Core.Problems
{
    public sealed class ResourceGateTemplate
    {
        public IReadOnlyList<ResourceGateOptionTemplate> Options { get; }
        public IReadOnlyList<SkillTag> SkillTags { get; }

        public ResourceGateTemplate(IReadOnlyList<ResourceGateOptionTemplate> options, IReadOnlyList<SkillTag> skillTags)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (skillTags == null) throw new ArgumentNullException(nameof(skillTags));
            if (options.Count < 2 || options.Count > 4) throw new ArgumentOutOfRangeException(nameof(options));
            Options = new List<ResourceGateOptionTemplate>(options).AsReadOnly();
            SkillTags = new List<SkillTag>(skillTags).AsReadOnly();
        }
    }
}
