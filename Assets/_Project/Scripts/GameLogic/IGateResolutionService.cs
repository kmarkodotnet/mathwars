using System;
using System.Collections.Generic;

namespace MathHorde.GameLogic
{
    public interface IGateResolutionService
    {
        GateResolution Resolve(in ResolveCommand command, ActiveMathEvent mathEvent);
        bool IsResolved(MathEventId eventId);
        void RestoreResolvedEvents(IReadOnlyCollection<Guid> resolvedEventIds);
    }
}
