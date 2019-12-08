using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using MoreFactionInteraction.MoreFactionWar;
using RimWorld;
using Verse;

namespace MoreFactionInteraction.NoCommsConsole
{
	[StaticConstructorOnStartup]
	static class HarmonyPatches
	{
		const bool DEBUG = false;

		static HarmonyPatches()
		{
			HarmonyInstance.DEBUG = DEBUG;
			try
			{
				HarmonyInstance.Create("MoreFactionInteraction.NoCommsConsole").PatchAll();
			}
			finally
			{
				HarmonyInstance.DEBUG = false;
			}
		}
	}

	// TODO: Patch TradeUtility.PlayerHomeMapWithMostLaunchableSilver/ColonyHasEnoughSilver/LaunchSilver calls
	// to consider silver in any reachable storage/stockpile, not just within orbital trade beacons.
	// TODO: Patch "NeedSilverLaunchable".Translate to "NotEnoughSilver".Translate.
	// TODO: Patch vanilla IncidentWorker_RansomDemand/ChoiceLetter_RansomDemand as well?

	[HarmonyPatch]
	static class IncidentWorker_MysticalShaman_CanFireNowSub_Patch
	{
		static readonly Type targetType =
			typeof(MoreFactionInteractionMod).Assembly.GetType("MoreFactionInteraction.IncidentWorker_MysticalShaman");

		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) => targetType.GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			NoCommsConsoleNeededPatcher.FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: false);
	}

	[HarmonyPatch]
	static class IncidentWorker_MysticalShaman_TechLevel_Patch
	{
		static readonly Type targetType =
			typeof(MoreFactionInteractionMod).Assembly.GetType("MoreFactionInteraction.IncidentWorker_MysticalShaman");

		[HarmonyTargetMethods]
		static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance harmony)
		{
			// Lambda code is in compiler-generated non-public instance methods on compiler-generated non-public nested classes.
			foreach (var type in targetType.GetNestedTypes(BindingFlags.NonPublic))
			{
				foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
				{
					if (MatchMethod(method))
						yield return method;
				}
			}
		}

		static bool MatchMethod(MethodInfo method) =>
			method.ReturnType == typeof(bool) &&
			method.GetParameters() is ParameterInfo[] parameters &&
			parameters.Length == 1 && parameters[0].ParameterType == typeof(Faction) &&
			(method.Name.Contains("CanFireNowSub") || method.Name.Contains("TryExecuteWorker"));

		// Effectively removes the faction.def.TechLevel <= TechLevel.Neolithic check.
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			TechLevelComparisonPatcher.TechLevelComparisonTranspiler(instructions, TechLevel.Neolithic, TechLevel.Archotech);

		// MFI bugfix: Ensure that the mystical shaman's faction isn't the player faction.
		// Note: The Faction parameter must match that of the method being patched; hence "Faction f".
		[HarmonyPrefix]
		static bool Prefix(Faction f) => f != Faction.OfPlayer;
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
			// Lambda code is in compiler-generated non-public instance methods on compiler-generated non-public nested classes.
			foreach (var type in typeof(IncidentWorker_WoundedCombatants).GetNestedTypes(BindingFlags.NonPublic))
			{
				foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
				{
					if (MatchMethod(method))
						yield return method;
				}
			}
		}

		static bool MatchMethod(MethodInfo method) =>
			method.ReturnType == typeof(bool) &&
			method.GetParameters() is ParameterInfo[] parameters &&
			parameters.Length == 1 && parameters[0].ParameterType == typeof(Faction) &&
			method.Name.Contains("FindAlliedWarringFaction");

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			TechLevelComparisonPatcher.TechLevelComparisonTranspiler(instructions, TechLevel.Industrial, TechLevel.Neolithic);
	}
}
