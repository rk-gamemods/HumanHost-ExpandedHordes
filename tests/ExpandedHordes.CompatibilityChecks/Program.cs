// Real installed Harmony dispatch; only game/controller/Admin Panel behavior is substituted.
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ExpandedHordes;
using HarmonyLib;

public class C_Controller_Base
{
    public enum Controller_State { Grounded, Falling, Rising }
    public bool _isPlayer;
    public int InputCalls;
    public static implicit operator bool(C_Controller_Base value) => value != null;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public Controller_State DetermineControllerState() => Controller_State.Falling;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Input_WSAD() { InputCalls++; }
}

internal static class AdminFixture
{
    internal static bool Fly = true;
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void DetermineControllerState_Postfix(ref object __result)
    { if (Fly) __result = C_Controller_Base.Controller_State.Grounded; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Input_WSAD_Prefix() => !Fly;
}

internal static class Program
{
    private static int assertions, observerCalls;
    private static readonly MethodInfo State = AccessTools.Method(typeof(C_Controller_Base), "DetermineControllerState");
    private static readonly MethodInfo Input = AccessTools.Method(typeof(C_Controller_Base), "Input_WSAD");
    private static readonly MethodInfo OldState = AccessTools.Method(typeof(AdminFixture), "DetermineControllerState_Postfix");
    private static readonly MethodInfo OldInput = AccessTools.Method(typeof(AdminFixture), "Input_WSAD_Prefix");
    private static readonly Harmony Admin = new Harmony(AdminPanelMovementGuard.AdminOwner);
    private static readonly Harmony Other = new Harmony("fixture.other");
    private static void Check(bool ok, string label)
    { assertions++; if (!ok) throw new Exception(label); }
    private static bool Install(string version = "1.1.9", string hash = AdminPanelMovementGuard.KnownHash) =>
        AdminPanelMovementGuard.Install(typeof(AdminFixture), version, hash);
    private static void Register()
    {
        Admin.Patch(State, postfix: new HarmonyMethod(OldState) { priority = 321, before = new[] { "fixture.other" } });
        Admin.Patch(Input, prefix: new HarmonyMethod(OldInput) { priority = 321, before = new[] { "fixture.other" } });
    }
    private static void Observe(C_Controller_Base __instance) { observerCalls++; }
    private static void FixedState(C_Controller_Base __instance, ref C_Controller_Base.Controller_State __result)
    { if (!__instance._isPlayer) __result = C_Controller_Base.Controller_State.Rising; }
    private static void PatchObserver() { }
    private static void Main()
    {
        var npc = new C_Controller_Base();
        var player = new C_Controller_Base { _isPlayer = true };
        Check(!Install(), "Absent registrations must be a no-op");
        Register();
        npc.Input_WSAD();
        Check(npc.DetermineControllerState() == C_Controller_Base.Controller_State.Grounded && npc.InputCalls == 0,
            "Baseline must reproduce NPC fly state and suppressed input");
        Check(!Install("1.2.0"), "Updated Admin Panel version must be untouched");
        Check(!Install(hash: "different"), "Same-version changed DLL must be untouched");
        Check(Install(), "Known bug must install");
        npc.Input_WSAD(); player.Input_WSAD();
        Check(npc.DetermineControllerState() == C_Controller_Base.Controller_State.Falling && npc.InputCalls == 1,
            "NPC gravity state and input must be restored");
        Check(player.DetermineControllerState() == C_Controller_Base.Controller_State.Grounded && player.InputCalls == 0,
            "Player fly behavior must remain intact");
        var registered = Harmony.GetPatchInfo(State).Postfixes;
        Check(registered.Count == 1 && registered[0].owner == AdminPanelMovementGuard.AdminOwner &&
            registered[0].priority == 321 && registered[0].before.SequenceEqual(new[] { "fixture.other" }),
            "Owner and ordering metadata must survive");
        Check(Install() && Harmony.GetPatchInfo(State).Postfixes.Count == 1, "Repeat install must not duplicate hooks");
        AdminFixture.Fly = false;
        player.Input_WSAD(); npc.Input_WSAD();
        Check(player.InputCalls == 1 && npc.InputCalls == 2 && player.DetermineControllerState() == C_Controller_Base.Controller_State.Falling,
            "Fly disabled preserves ordinary behavior");
        Other.Patch(State, postfix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(FixedState)))
            { after = new[] { AdminPanelMovementGuard.AdminOwner } });
        Other.Patch(Input, postfix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(Observe))));
        npc.Input_WSAD();
        Check(npc.DetermineControllerState() == C_Controller_Base.Controller_State.Rising && observerCalls == 1,
            "Another mod's result and input hook must survive");
        AdminPanelMovementGuard.Stop(); AdminPanelMovementGuard.Stop();
        Check(Harmony.GetPatchInfo(State).Postfixes.Count(p => p.PatchMethod == OldState) == 1,
            "Unload restores the original once");
        Check(Harmony.GetPatchInfo(State).Postfixes.Any(p => p.owner == "fixture.other"), "Unload retains other mods");
        Other.UnpatchSelf();
        Other.Patch(OldState, prefix: new HarmonyMethod(AccessTools.Method(typeof(Program), nameof(PatchObserver))));
        Check(!Install(), "Already-patched Admin Panel implementation must be left alone");
        Other.UnpatchSelf();
        Admin.Unpatch(Input, OldInput);
        Check(!Install() && Harmony.GetPatchInfo(State).Postfixes.Single().PatchMethod == OldState,
            "One replaced hook means no partial install");
        Admin.Patch(Input, prefix: new HarmonyMethod(OldInput));
        Check(Install(), "Reinstall after clean unload");
        var stateWrapper = Harmony.GetPatchInfo(State).Postfixes.Single().PatchMethod;
        Admin.Unpatch(State, stateWrapper);
        AdminPanelMovementGuard.Stop();
        Check(Harmony.GetPatchInfo(State)?.Postfixes.Count == 0,
            "Unload must not resurrect a hook another mod removed");
        Admin.UnpatchSelf();
        Console.WriteLine($"PASS: {assertions} Admin Panel compatibility assertions with real Harmony dispatch; game boundaries substituted.");
    }
}
