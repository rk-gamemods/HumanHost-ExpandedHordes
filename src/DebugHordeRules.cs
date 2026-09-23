namespace ExpandedHordes
{
    internal enum DebugHordeRequestState { None, Waiting, Ready, Cancelled, Expired }

    // A request is consumed before invoking game code, so repeated frames cannot start another horde.
    internal sealed class DebugHordeRequest
    {
        internal bool Pending { get; private set; }
        private double deadline;

        internal bool TryQueue(double now, double timeout)
        {
            if (Pending) return false;
            Pending = true;
            deadline = now + timeout;
            return true;
        }

        internal DebugHordeRequestState Poll(double now, bool valid, bool nightReady)
        {
            if (!Pending) return DebugHordeRequestState.None;
            if (!valid) { Clear(); return DebugHordeRequestState.Cancelled; }
            if (now >= deadline) { Clear(); return DebugHordeRequestState.Expired; }
            if (!nightReady) return DebugHordeRequestState.Waiting;
            Clear();
            return DebugHordeRequestState.Ready;
        }

        internal void Clear() => Pending = false;
    }
}
