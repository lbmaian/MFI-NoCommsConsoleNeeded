using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using RimWorld;

namespace NoCommsConsoleRequiredForIncidents
{
	class TechLevelComparisonPatcher
	{
		// This is sufficient for the TechLevel enum, which has a max int value of 7.
		static OpCode Ldc_I4_x(int x) => x switch
		{
			0 => OpCodes.Ldc_I4_0,
			1 => OpCodes.Ldc_I4_1,
			2 => OpCodes.Ldc_I4_2,
			3 => OpCodes.Ldc_I4_3,
			4 => OpCodes.Ldc_I4_4,
			5 => OpCodes.Ldc_I4_5,
			6 => OpCodes.Ldc_I4_6,
			7 => OpCodes.Ldc_I4_7,
			8 => OpCodes.Ldc_I4_8,
			_ => throw new ArgumentOutOfRangeException($"{x} must be >= 0 and <= 8"),
		};

		static readonly FieldInfo fieldof_FactionDef_techLevel = typeof(FactionDef).GetField(nameof(FactionDef.techLevel));

		public static IEnumerable<CodeInstruction> TechLevelComparisonTranspiler(IEnumerable<CodeInstruction> instructions,
			TechLevel origTechLevel, TechLevel newTechLevel)
		{
			var origTechLevelOpcode = Ldc_I4_x((int)origTechLevel);
			var newTechLevelOpCode = Ldc_I4_x((int)newTechLevel);
			bool accessedTechLevelField = false;
			foreach (var instruction in instructions)
			{
				if (accessedTechLevelField)
				{
					accessedTechLevelField = false;
					if (instruction.opcode == origTechLevelOpcode)
					{
						yield return new CodeInstruction(newTechLevelOpCode);
						continue;
					}
				}
				if (instruction.opcode == OpCodes.Ldfld && instruction.operand == fieldof_FactionDef_techLevel)
					accessedTechLevelField = true;
				yield return instruction;
			}
		}
	}
}
