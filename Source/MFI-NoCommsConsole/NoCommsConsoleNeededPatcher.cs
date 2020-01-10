using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using RimWorld;
using Verse;

namespace NoCommsConsoleRequiredForIncidents
{
	class NoCommsConsoleNeededPatcher
	{
		static readonly MethodInfo methodof_CommsConsoleUtility_PlayerHasPoweredCommsConsole =
			typeof(CommsConsoleUtility).GetMethod(nameof(CommsConsoleUtility.PlayerHasPoweredCommsConsole), Type.EmptyTypes);
		static readonly MethodInfo methodof_CommsConsoleUtility_PlayerHasPoweredCommsConsole_Map =
			typeof(CommsConsoleUtility).GetMethod(nameof(CommsConsoleUtility.PlayerHasPoweredCommsConsole), new[] { typeof(Map) });

		public static IEnumerable<CodeInstruction> FakeAlwaysHaveCommsConsoleTranspiler(IEnumerable<CodeInstruction> instructions, bool hasMapParam)
		{
			var commsConsoleUtilityMethod = hasMapParam ?
				methodof_CommsConsoleUtility_PlayerHasPoweredCommsConsole_Map :
				methodof_CommsConsoleUtility_PlayerHasPoweredCommsConsole;
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Call && instruction.operand == commsConsoleUtilityMethod)
				{
					// Assumption: None of the replaced instructions have labels (targets of branches).
					if (hasMapParam)
					{
						// Remove the map value on the stack, since we're no longer calling a method that consumes it.
						yield return new CodeInstruction(OpCodes.Pop);
					}
					yield return new CodeInstruction(OpCodes.Ldc_I4_1); // true
					continue;
				}
				yield return instruction;
			}
		}
	}
}
