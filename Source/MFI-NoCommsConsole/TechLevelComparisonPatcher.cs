using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using RimWorld;

namespace MoreFactionInteraction.NoCommsConsole
{
	class TechLevelComparisonPatcher
	{
		// This is sufficient for the TechLevel enum, which has a max int value of 7.
		static OpCode Ldc_I4_x(int x)
		{
			switch (x)
			{
			case 0:
				return OpCodes.Ldc_I4_0;
			case 1:
				return OpCodes.Ldc_I4_1;
			case 2:
				return OpCodes.Ldc_I4_2;
			case 3:
				return OpCodes.Ldc_I4_3;
			case 4:
				return OpCodes.Ldc_I4_4;
			case 5:
				return OpCodes.Ldc_I4_5;
			case 6:
				return OpCodes.Ldc_I4_6;
			case 7:
				return OpCodes.Ldc_I4_7;
			case 8:
				return OpCodes.Ldc_I4_8;
			default:
				throw new ArgumentOutOfRangeException($"{x} must be >= 0 and <= 8");
			}
		}

		static readonly FieldInfo factionDefTechLevelField = typeof(FactionDef).GetField(nameof(FactionDef.techLevel));

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
				if (instruction.opcode == OpCodes.Ldfld && instruction.operand == factionDefTechLevelField)
					accessedTechLevelField = true;
				yield return instruction;
			}
		}
	}
}
