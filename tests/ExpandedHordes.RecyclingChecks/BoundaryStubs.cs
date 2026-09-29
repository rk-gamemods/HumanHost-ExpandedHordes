// Only engine/game/Harmony boundaries are substituted. Production hooks run unchanged.
using System;
using System.Collections.Generic;
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
}
namespace UnityEngine
{
    internal class Object
    {
        internal bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed;
    }
    internal sealed class GameObject : Object
    {
        internal bool activeSelf = true;
        internal NPC_Input Controller;
        internal bool ThrowRead;
        public T GetComponent<T>() where T : class => ThrowRead ? throw new InvalidOperationException("read failed") : Controller as T;
    }
}
internal class NPC_Input : UnityEngine.Object
{
    internal UnityEngine.GameObject gameObject;
    internal Char_Status char_Status = new Char_Status();
    internal int _npcSpawnSource = 2;
    private bool movement = true;
    internal int EnableWrites;
    internal bool enabled { get => movement; set { movement = value; if (value) EnableWrites++; } }
    public void On_Char_Died() { }
    internal NPC_Input() { gameObject = new UnityEngine.GameObject { Controller = this }; }
}
internal sealed class Zombie_Input : NPC_Input { }
internal sealed class Char_Status : UnityEngine.Object { internal float _CurrHP = 100; }
internal sealed class NPC_Horde_Mgr : UnityEngine.Object
{
    internal static NPC_Horde_Mgr ins = new NPC_Horde_Mgr();
    internal readonly Dictionary<UnityEngine.GameObject, object> spawned_Horde_NPCs = new Dictionary<UnityEngine.GameObject, object>();
    public void Put_NPC_Back_To_Pool() { }
}
internal sealed class NPC_Spawner_Mgr : UnityEngine.Object
{
    internal static NPC_Spawner_Mgr ins = new NPC_Spawner_Mgr();
    internal readonly Dictionary<UnityEngine.GameObject, object> _waitBackPoolDead_NPCs = new Dictionary<UnityEngine.GameObject, object>();
}
namespace ExpandedHordes
{
    internal static class FeatureRuntime
    {
        internal static readonly HashSet<string> Warnings = new HashSet<string>();
        internal static void WarnOnce(string key, string message) { Warnings.Add(key); }
    }
}
