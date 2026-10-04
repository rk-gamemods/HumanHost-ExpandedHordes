using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using ExpandedHordes;

internal static partial class Program
{
    private sealed partial class AssemblyContract
    {
        internal void Arrival()
        {
            var manager = Type("NPC_Horde_Mgr");
            var placement = manager.GetMethods().Select(reader.GetMethodDefinition)
                .Single(m => reader.GetString(m.Name) == "GetValidSpawnPosition");
            var placementShape = Describe(placement);
            Check(placementShape.PlacementSignature, "Installed arrival helper is instance Vector3 GetValidSpawnPosition(Vector3, Single, Single, Int32)");
            var iterator = manager.GetNestedTypes().Select(reader.GetTypeDefinition)
                .Single(t => reader.GetString(t.Name).StartsWith("<Spawn_Horde_NPCs>", StringComparison.Ordinal));
            var moveNext = iterator.GetMethods().Select(reader.GetMethodDefinition)
                .Single(m => reader.GetString(m.Name) == "MoveNext");
            var instructions = ReadArrivalInstructions(moveNext);
            var shape = ArrivalShape.Read(instructions, placementShape);
            Check(shape.WaitMatches, "Installed native iterator has one WaitForSeconds(float) fed by ldc.r4 0.1");
            Check(shape.PlacementMatches, "Installed native iterator has exactly two placement calls with attempts 1000 and 100");
            Check(shape.RateEnabled(400) && shape.DirectionsEnabled(4), "Verified installed shape enables both requested arrival options");
            Check(!shape.RateEnabled(100) && !shape.DirectionsEnabled(1), "Default settings enable no arrival hooks");

            var badWait = instructions.ToArray();
            int waitIndex = Array.FindIndex(badWait, i => i.Code == OpCodes.Ldc_R4 && i.Operand is float f && f == 0.1f);
            Check(waitIndex >= 0, "Negative wait fixture starts from the installed native constant");
            badWait[waitIndex] = new ArrivalInstruction(OpCodes.Ldc_R4, 0.2f);
            var changedWait = ArrivalShape.Read(badWait, placementShape);
            Check(!changedWait.RateEnabled(400) && changedWait.DirectionsEnabled(4), "Mismatched installed wait disables rate while retaining directions");

            var badAttempts = instructions.ToArray();
            int zombieIndex = Array.FindIndex(badAttempts, i =>
                (i.Code == OpCodes.Ldc_I4 || i.Code == OpCodes.Ldc_I4_S) && Convert.ToInt32(i.Operand) == 100);
            Check(zombieIndex >= 0, "Negative placement fixture starts from installed attempts 100");
            badAttempts[zombieIndex] = new ArrivalInstruction(OpCodes.Ldc_I4, 99);
            var changedPlacement = ArrivalShape.Read(badAttempts, placementShape);
            Check(changedPlacement.RateEnabled(400) && !changedPlacement.DirectionsEnabled(4), "Mismatched installed placement disables directions while retaining rate");
            var wrongReturn = new ArrivalMethod("NPC_Horde_Mgr", "GetValidSpawnPosition", "Void", false,
                "Vector3", "Single", "Single", "Int32");
            Check(!ArrivalShape.Read(instructions, wrongReturn).DirectionsEnabled(4), "Changed placement return type disables directions");
            Check(!ArrivalShape.Read(instructions.Concat(instructions).ToArray(), placementShape).RateEnabled(400) &&
                !ArrivalShape.Read(instructions.Concat(instructions).ToArray(), placementShape).DirectionsEnabled(4),
                "Duplicate native waits and placement calls reject an ambiguous iterator");
        }

        private ArrivalMethod Describe(MethodDefinition method)
        {
            var signature = method.DecodeSignature(new TypeNames(), (object)null);
            return new ArrivalMethod(reader.GetString(reader.GetTypeDefinition(method.GetDeclaringType()).Name),
                reader.GetString(method.Name), signature.ReturnType, (method.Attributes & MethodAttributes.Static) != 0,
                signature.ParameterTypes.ToArray());
        }

        private ArrivalMethod Describe(EntityHandle handle)
        {
            if (handle.Kind == HandleKind.MethodSpecification)
                return Describe(reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method);
            if (handle.Kind == HandleKind.MethodDefinition) return Describe(reader.GetMethodDefinition((MethodDefinitionHandle)handle));
            var method = reader.GetMemberReference((MemberReferenceHandle)handle);
            var signature = method.DecodeMethodSignature(new TypeNames(), (object)null);
            string owner = method.Parent.Kind switch
            {
                HandleKind.TypeReference => reader.GetString(reader.GetTypeReference((TypeReferenceHandle)method.Parent).Name),
                HandleKind.TypeDefinition => reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)method.Parent).Name),
                HandleKind.TypeSpecification => reader.GetTypeSpecification((TypeSpecificationHandle)method.Parent).DecodeSignature(new TypeNames(), (object)null),
                _ => "unknown"
            };
            return new ArrivalMethod(owner, reader.GetString(method.Name), signature.ReturnType, !signature.Header.IsInstance,
                signature.ParameterTypes.ToArray());
        }

        private ArrivalInstruction[] ReadArrivalInstructions(MethodDefinition method)
        {
            byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
            var codes = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode))
                .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => unchecked((ushort)o.Value));
            var instructions = new List<ArrivalInstruction>();
            int offset = 0;
            while (offset < il.Length)
            {
                ushort key = il[offset++];
                if (key == 0xfe) key = (ushort)(0xfe00 | il[offset++]);
                OpCode code = codes[key];
                object operand = code.OperandType switch
                {
                    OperandType.InlineMethod => Describe(MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset))),
                    OperandType.InlineI => BitConverter.ToInt32(il, offset),
                    OperandType.ShortInlineI => (int)(sbyte)il[offset],
                    OperandType.ShortInlineR => BitConverter.ToSingle(il, offset),
                    _ => null
                };
                instructions.Add(new ArrivalInstruction(code, operand));
                offset += code.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                    _ => 4
                };
            }
            return instructions.ToArray();
        }
    }
}
