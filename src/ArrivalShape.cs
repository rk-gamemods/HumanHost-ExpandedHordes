using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace ExpandedHordes
{
    // Both Harmony's original instructions and the installed-assembly metadata
    // checks feed this matcher. It never loads or invokes Unity.
    internal sealed class ArrivalMethod
    {
        internal readonly string Owner, Name, ReturnType;
        internal readonly string[] Parameters;
        internal readonly bool IsStatic;
        internal ArrivalMethod(string owner, string name, string returnType, bool isStatic, params string[] parameters)
        { Owner = owner; Name = name; ReturnType = returnType; IsStatic = isStatic; Parameters = parameters; }

        internal bool PlacementSignature => Owner == "NPC_Horde_Mgr" && Name == "GetValidSpawnPosition" &&
            !IsStatic && ReturnType == "Vector3" && Parameters.SequenceEqual(new[] { "Vector3", "Single", "Single", "Int32" });
        internal bool WaitConstructor => Owner == "WaitForSeconds" && Name == ".ctor" && !IsStatic &&
            Parameters.SequenceEqual(new[] { "Single" });
    }

    internal readonly struct ArrivalInstruction
    {
        internal readonly OpCode Code;
        internal readonly object Operand;
        internal ArrivalInstruction(OpCode code, object operand = null) { Code = code; Operand = operand; }
    }

    internal readonly struct ArrivalShape
    {
        internal readonly bool WaitMatches, PlacementMatches;
        private ArrivalShape(bool wait, bool placement) { WaitMatches = wait; PlacementMatches = placement; }
        internal bool RateEnabled(int percentage) => percentage != 100 && WaitMatches;
        internal bool DirectionsEnabled(int directions) => directions != 1 && PlacementMatches;

        internal static ArrivalShape Read(IReadOnlyList<ArrivalInstruction> instructions, ArrivalMethod placement)
        {
            int waits = 0, nativeWaits = 0, calls = 0, anchors = 0, zombies = 0;
            ArrivalInstruction previous = default;
            foreach (var instruction in instructions)
            {
                if (instruction.Code == OpCodes.Nop) continue;
                if (instruction.Operand is ArrivalMethod method)
                {
                    if (instruction.Code == OpCodes.Newobj && method.WaitConstructor)
                    {
                        waits++;
                        if (previous.Code == OpCodes.Ldc_R4 && previous.Operand is float seconds && seconds == ArrivalRules.NativeWaitSeconds)
                            nativeWaits++;
                    }
                    if ((instruction.Code == OpCodes.Call || instruction.Code == OpCodes.Callvirt) &&
                        method.Owner == "NPC_Horde_Mgr" && method.Name == "GetValidSpawnPosition")
                    {
                        calls++;
                        if (method.PlacementSignature && Integer(previous, 1000)) anchors++;
                        if (method.PlacementSignature && Integer(previous, 100)) zombies++;
                    }
                }
                previous = instruction;
            }
            return new ArrivalShape(waits == 1 && nativeWaits == 1,
                placement != null && placement.PlacementSignature && calls == 2 && anchors == 1 && zombies == 1);
        }

        private static bool Integer(ArrivalInstruction instruction, int expected) =>
            (instruction.Code == OpCodes.Ldc_I4 || instruction.Code == OpCodes.Ldc_I4_S) &&
            Convert.ToInt32(instruction.Operand) == expected;
    }
}
