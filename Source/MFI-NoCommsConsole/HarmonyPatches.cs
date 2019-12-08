using System.Collections.Generic;
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

	// TODO: Patch TradeUtility.PlayerHomeMapWithMostLaunchableSilver/ColonyHasEnoughSilver/LaunchSilver calls
	// to consider silver in any reachable storage/stockpile, not just within orbital trade beacons.
	// TODO: Patch "NeedSilverLaunchable".Translate to "NotEnoughSilver".Translate.
	// TODO: Patch vanilla IncidentWorker_RansomDemand/ChoiceLetter_RansomDemand as well?

	[HarmonyPatch]
	static class IncidentWorker_MysticalShaman_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(MoreFactionInteractionMod).Assembly.GetType("MoreFactionInteraction.IncidentWorker_MysticalShaman").GetMethod("CanFireNowSub", AccessTools.all);

		// TODO: Patch out non-hostile neolithic faction check.
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			NoCommsConsoleNeededPatcher.FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: false);
	}

	[HarmonyPatch]
	static class IncidentWorker_RoadWorks_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(MoreFactionInteractionMod).Assembly.GetType("MoreFactionInteraction.IncidentWorker_RoadWorks").GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			NoCommsConsoleNeededPatcher.FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: false);
	}

	[HarmonyPatch]
	static class IncidentWorker_ReverseTradeRequest_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_ReverseTradeRequest).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			NoCommsConsoleNeededPatcher.FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
	}

	[HarmonyPatch]
	static class IncidentWorker_Extortion_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_Extortion).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			NoCommsConsoleNeededPatcher.FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
	}

	[HarmonyPatch]
	static class IncidentWorker_WoundedCombatants_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_WoundedCombatants).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			NoCommsConsoleNeededPatcher.FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
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
