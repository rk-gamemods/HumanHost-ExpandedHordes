// Only native object and catalog boundaries are substituted. The production
// HordeSpecialLimits membership scan and admission guard execute unchanged.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    internal class Object
    {
        internal bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed;
    }
    internal sealed class GameObject : Object
    {
        internal NPC_Input Zombie;
        public T GetComponent<T>() where T : class => Zombie as T;
    }
}

namespace HarmonyLib
{
    internal static class AccessTools
    {
        // These linked production accessors only read fields. Reflection is the
        // test boundary; native field names/types are checked against game DLLs.
        internal delegate TValue FieldRef<T, TValue>(T instance);
        internal static FieldRef<T, TValue> FieldRefAccess<T, TValue>(string name)
        {
            var field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(typeof(T).Name, name);
            return instance => (TValue)field.GetValue(instance);
        }
    }
}

internal sealed partial class NPC_Horde_Mgr : UnityEngine.Object
{
    internal Dictionary<UnityEngine.GameObject, Horde_NPC_Info> _aliveHordeNPCs = new Dictionary<UnityEngine.GameObject, Horde_NPC_Info>();
    internal bool _inAsyncSpawnNPC;
    internal sealed class Horde_NPC_Info
    {
        internal ExpandedHordes.ZombieKind Kind;
        internal bool Known = true;
    }
}
internal sealed class NPC_Input : UnityEngine.Object { internal bool is_Boss; }

namespace ExpandedHordes
{
    internal static class CreatureCatalog
    {
        internal static bool TryClassifyDeath(NPC_Horde_Mgr.Horde_NPC_Info identity, out ZombieKind kind)
        {
            kind = identity?.Kind ?? ZombieKind.Regular;
            return identity != null && identity.Known;
        }
    }
}
