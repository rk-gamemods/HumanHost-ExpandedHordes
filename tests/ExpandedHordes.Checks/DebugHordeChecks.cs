using System;
using ExpandedHordes;

internal static class DebugHordeChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var request = new DebugHordeRequest();
        check(request.Poll(0, true, true) == DebugHordeRequestState.None,
            "Night readiness alone cannot start an unrequested horde");
        check(request.TryQueue(10, 10) && !request.TryQueue(11, 10),
            "Repeated shortcut presses cannot queue overlapping starts or extend the deadline");
        check(request.Poll(12, true, false) == DebugHordeRequestState.Waiting && request.Pending,
            "A daylight request waits for native nighttime propagation");
        check(request.Poll(13, true, true) == DebugHordeRequestState.Ready && !request.Pending &&
            request.Poll(14, true, true) == DebugHordeRequestState.None,
            "A ready request is consumed before the native effect and can execute only once");
        request.TryQueue(30, 10);
        check(request.Poll(31, false, true) == DebugHordeRequestState.Cancelled &&
            request.Poll(32, true, true) == DebugHordeRequestState.None,
            "Lost world, player, debug mode or gameplay readiness cancels without deferred execution");
        request.TryQueue(40, 10);
        check(!request.TryQueue(49, 10) && request.Poll(50, true, true) == DebugHordeRequestState.Expired && !request.Pending,
            "A night transition that reaches the deadline cannot fire even if readiness arrives together");
        request.TryQueue(60, 10); request.Clear(); request.Clear();
        check(request.Poll(61, true, true) == DebugHordeRequestState.None && request.TryQueue(62, 10),
            "Repeated cancellation is safe and a later explicit request can start afresh");
        request.Clear();
    }
}
