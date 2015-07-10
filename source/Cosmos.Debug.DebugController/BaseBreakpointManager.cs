using System.Collections.Generic;

namespace Cosmos.Debug
{
    public abstract class BaseBreakpointManager
    {
        public abstract uint[] GetPendingBreakpointAddresses();

        public abstract IEnumerable<KeyValuePair<uint, object>> GetBoundBreakpoints();
    }
}
