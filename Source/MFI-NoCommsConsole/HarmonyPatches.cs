using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using MoreFactionInteraction.MoreFactionWar;
using RimWorld;
using Verse;

namespace MoreFactionInteraction.NoCommsConsole
{
	using static NoCommsConsoleNeededPatcher;
	using static TechLevelComparisonPatcher;
	using static SilverInTradeBeaconRangeToSilverInStoragePatcher;

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
			FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: false);
	}

	[HarmonyPatch]
	static class IncidentWorker_MysticalShaman_TechLevel_Patch
	{
		static readonly Type targetType =
			typeof(MoreFactionInteractionMod).Assembly.GetType("MoreFactionInteraction.IncidentWorker_MysticalShaman");

		[HarmonyTargetMethods]
		static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance harmony) =>
			targetType.FindLambdaMethods(method =>
				method.ReturnType == typeof(bool) &&
				method.GetParameters() is ParameterInfo[] parameters &&
				parameters.Length == 1 && parameters[0].ParameterType == typeof(Faction) &&
				(method.Name.Contains("CanFireNowSub") || method.Name.Contains("TryExecuteWorker")));

		// Effectively removes the faction.def.TechLevel <= TechLevel.Neolithic check.
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			TechLevelComparisonTranspiler(instructions, TechLevel.Neolithic, TechLevel.Archotech);

		// MFI bugfix: Ensure that the mystical shaman's faction isn't the player faction.
		// Note: The Faction parameter must match that of the method being patched; hence "Faction f".
		[HarmonyPrefix]
		static bool Prefix(Faction f) => f != Faction.OfPlayer;
	}

	[HarmonyPatch]
	static class IncidentWorker_MysticalShaman_SilverInTradeBeaconRange_Patch
	{
		static readonly Type targetType =
			typeof(MoreFactionInteractionMod).Assembly.GetType("MoreFactionInteraction.IncidentWorker_MysticalShaman");

		[HarmonyTargetMethods]
		static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance harmony) =>
			targetType.FindLambdaMethods(method =>
				method.ReturnType == typeof(void) &&
				method.GetParameters().Length == 0 &&
				method.Name.Contains("TryExecuteWorker") &&
				HasSilverInTradeBeaconRangeMethod(method))
			.Prepend(targetType.GetMethod("TryExecuteWorker", AccessTools.all));

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) =>
			ReplaceSilverInTradeBeaconRangeWithSilverInStorageTranspiler(instructions, ilGenerator);
	}

	[HarmonyPatch]
	static class IncidentWorker_RoadWorks_CanFireNowSub_Patch
	{
		static readonly Type targetType =
			typeof(MoreFactionInteractionMod).Assembly.GetType("MoreFactionInteraction.IncidentWorker_RoadWorks");

		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) => targetType.GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: false);
	}

	[HarmonyPatch]
	static class IncidentWorker_RoadWorks_SilverInTradeBeaconRange_Patch
	{
		static readonly Type targetType =
			typeof(MoreFactionInteractionMod).Assembly.GetType("MoreFactionInteraction.IncidentWorker_RoadWorks");

		[HarmonyTargetMethods]
		static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance harmony) =>
			targetType.FindLambdaMethods(method =>
				method.ReturnType == typeof(void) &&
				method.GetParameters().Length == 0 &&
				method.Name.Contains("TryExecuteWorker") &&
				HasSilverInTradeBeaconRangeMethod(method))
			.Prepend(targetType.GetMethod("TryExecuteWorker", AccessTools.all));

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) =>
			ReplaceSilverInTradeBeaconRangeWithSilverInStorageTranspiler(instructions, ilGenerator);
	}

	[HarmonyPatch]
	static class IncidentWorker_ReverseTradeRequest_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_ReverseTradeRequest).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
	}

	[HarmonyPatch]
	static class IncidentWorker_ReverseTradeRequest_SilverInTradeBeaconRange_Patch
	{
		[HarmonyTargetMethods]
		static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance harmony) =>
			typeof(IncidentWorker_ReverseTradeRequest).FindLambdaMethods(method =>
				method.ReturnType == typeof(void) &&
				method.GetParameters().Length == 0 &&
				method.Name.Contains("TryExecuteWorker") &&
				HasSilverInTradeBeaconRangeMethod(method))
			.Prepend(typeof(IncidentWorker_ReverseTradeRequest).GetMethod("TryExecuteWorker", AccessTools.all));

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) =>
			ReplaceSilverInTradeBeaconRangeWithSilverInStorageTranspiler(instructions, ilGenerator);
	}

	[HarmonyPatch]
	static class IncidentWorker_Extortion_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_Extortion).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
	}

	[HarmonyPatch]
	static class ChoiceLetter_ExtortionDemand_SilverInTradeBeaconRange_Patch
	{
		[HarmonyTargetMethods]
		static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance harmony) =>
			typeof(ChoiceLetter_ExtortionDemand).FindLambdaMethods(method =>
				method.ReturnType == typeof(void) &&
				method.GetParameters().Length == 0 &&
				method.Name.Contains("get_Choices") &&
				HasSilverInTradeBeaconRangeMethod(method))
			.Prepend(typeof(ChoiceLetter_ExtortionDemand).FindIteratorMethod(enumeratorType => enumeratorType.Name.Contains("get_Choices")));

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) =>
			ReplaceSilverInTradeBeaconRangeWithSilverInStorageTranspiler(instructions, ilGenerator);
	}

	[HarmonyPatch]
	static class IncidentWorker_WoundedCombatants_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_WoundedCombatants).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
	}

	[HarmonyPatch]
	static class IncidentWorker_WoundedCombatants_TechLevel_Patch
	{
		[HarmonyTargetMethods]
		static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance harmony) =>
			typeof(IncidentWorker_WoundedCombatants).FindLambdaMethods(method =>
				method.ReturnType == typeof(bool) &&
				method.GetParameters() is ParameterInfo[] parameters &&
				parameters.Length == 1 && parameters[0].ParameterType == typeof(Faction) &&
				method.Name.Contains("FindAlliedWarringFaction"))
			.Prepend(typeof(IncidentWorker_WoundedCombatants).GetMethod("TryExecuteWorker", AccessTools.all));

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			TechLevelComparisonTranspiler(instructions, TechLevel.Industrial, TechLevel.Neolithic);
	}
}
