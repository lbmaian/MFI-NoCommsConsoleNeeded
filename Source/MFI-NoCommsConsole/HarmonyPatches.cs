using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using MoreFactionInteraction.MoreFactionWar;
using RimWorld;
using Verse;

namespace MoreFactionInteraction.NoCommsConsole
{
	[StaticConstructorOnStartup]
	static class HarmonyPatches
	{
		internal static readonly Assembly MFIAssembly =
			LoadedModManager.GetMod<MoreFactionInteractionMod>().Content.assemblies.loadedAssemblies
				.First(assembly => assembly.GetName().Name == "MoreFactionInteraction");

		internal static IEnumerable<CodeInstruction> FakeAlwaysHaveCommsConsole(IEnumerable<CodeInstruction> instructions, bool hasMapParam)
		{
			var playerHasPoweredCommsConsoleMethod =
				typeof(CommsConsoleUtility).GetMethod(nameof(CommsConsoleUtility.PlayerHasPoweredCommsConsole),
					hasMapParam ? new[] { typeof(Map) } : Type.EmptyTypes);
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Call && instruction.operand == playerHasPoweredCommsConsoleMethod)
				{
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

		static HarmonyPatches()
		{
			//HarmonyInstance.DEBUG = true;
			//try
			//{
				HarmonyInstance.Create("MoreFactionInteraction.NoCommsConsole").PatchAll();
			//}
			//finally
			//{
			//	HarmonyInstance.DEBUG = false;
			//}
		}
	}

	[HarmonyPatch]
	static class IncidentWorker_MysticalShaman_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			HarmonyPatches.MFIAssembly.GetType("MoreFactionInteraction.IncidentWorker_MysticalShaman").GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			HarmonyPatches.FakeAlwaysHaveCommsConsole(instructions, hasMapParam: false);
	}

	[HarmonyPatch]
	static class IncidentWorker_RoadWorks_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			HarmonyPatches.MFIAssembly.GetType("MoreFactionInteraction.IncidentWorker_RoadWorks").GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			HarmonyPatches.FakeAlwaysHaveCommsConsole(instructions, hasMapParam: false);
	}

	[HarmonyPatch]
	static class IncidentWorker_ReverseTradeRequest_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_ReverseTradeRequest).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			HarmonyPatches.FakeAlwaysHaveCommsConsole(instructions, hasMapParam: true);
	}

	[HarmonyPatch]
	static class IncidentWorker_Extortion_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_Extortion).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			HarmonyPatches.FakeAlwaysHaveCommsConsole(instructions, hasMapParam: true);
	}

	[HarmonyPatch]
	static class IncidentWorker_WoundedCombatants_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_WoundedCombatants).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			HarmonyPatches.FakeAlwaysHaveCommsConsole(instructions, hasMapParam: true);
	}

	[HarmonyPatch]
	static class IncidentWorker_WoundedCombatants_TechLevel_Patch
	{
		[HarmonyTargetMethods]
		static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance harmony)
		{
			yield return typeof(IncidentWorker_WoundedCombatants).GetMethod("TryExecuteWorker", AccessTools.all);
			foreach (var type in typeof(IncidentWorker_WoundedCombatants).GetNestedTypes(BindingFlags.NonPublic))
			{
				foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
				{
					if (method.Name.Contains("FindAlliedWarringFaction") && method.ReturnType == typeof(bool))
						yield return method;
				}
			}
		}

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var techLevelField = typeof(FactionDef).GetField(nameof(FactionDef.techLevel));
			bool accessedTechLevelField = false;
			foreach (var instruction in instructions)
			{
				if (accessedTechLevelField)
				{
					accessedTechLevelField = false;
					if (instruction.opcode == OpCodes.Ldc_I4_4) // TechLevel.Industrial
					{
						yield return new CodeInstruction(OpCodes.Ldc_I4_2); // TechLevel.Neolithic
						continue;
					}
				}
				if (instruction.opcode == OpCodes.Ldfld && instruction.operand == techLevelField)
					accessedTechLevelField = true;
				yield return instruction;
			}
		}
	}
}
