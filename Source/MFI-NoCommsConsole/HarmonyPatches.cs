using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using MoreFactionInteraction.More_Flavour;
using MoreFactionInteraction.MoreFactionWar;
using RimWorld;
using Verse;

namespace MoreFactionInteraction.NoCommsConsole
{
	using static NoCommsConsoleNeededPatcher;
	using static SilverInTradeBeaconRangeToSilverInStoragePatcher;
	using static TechLevelComparisonPatcher;

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

	// Note: RansomDemand is a vanilla incident rather than a MFI incident being patched.
	[HarmonyPatch]
	static class IncidentWorker_RansomDemand_CanFireNowSub_Patch
	{
		[HarmonyTargetMethod]
		static MethodInfo CalculateMethod(HarmonyInstance harmony) =>
			typeof(IncidentWorker_RansomDemand).GetMethod("CanFireNowSub", AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
	}

	// Note: Part of the vanilla RansomDemand incident.
	[HarmonyPatch]
	static class ChoiceLetter_RansomDemand_SilverInTradeBeaconRange_Patch
	{
		[HarmonyTargetMethods]
		static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance harmony) =>
			typeof(ChoiceLetter_RansomDemand).FindLambdaMethods(method =>
				method.ReturnType == typeof(void) &&
				method.GetParameters().Length == 0 &&
				// Note: RimWorld was compiled in a way such that the lambda methods are called "<>m__x" where x is a number.
				// In case RimWorld is ever recompiled in a different way, only going to assume that lambda methods start with "<".
				method.Name.StartsWith("<") &&
				HasSilverInTradeBeaconRangeMethod(method))
			.Prepend(typeof(ChoiceLetter_RansomDemand).FindIteratorMethod(enumeratorType =>
				typeof(IEnumerable<DiaOption>).IsAssignableFrom(enumeratorType)));

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) =>
			ReplaceSilverInTradeBeaconRangeWithSilverInStorageTranspiler(instructions, ilGenerator);
	}

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
				(method.Name.StartsWith("<CanFireNowSub>") || method.Name.StartsWith("<TryExecuteWorker>")));

		// Effectively removes the faction.def.TechLevel <= TechLevel.Neolithic check.
		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			TechLevelComparisonTranspiler(instructions, TechLevel.Neolithic, TechLevel.Archotech);

		// Fix for MFI oversight: Ensure that the mystical shaman's faction isn't the player faction.
		// It was technically possible for the player faction to have neolithic tech and have access to powered comms console
		// and trade beacons, especially when other mods are in play.
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
				method.Name.StartsWith("<TryExecuteWorker>") &&
				HasSilverInTradeBeaconRangeMethod(method))
			.Prepend(targetType.GetMethod("TryExecuteWorker", AccessTools.all));

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) =>
			ReplaceSilverInTradeBeaconRangeWithSilverInStorageTranspiler(instructions, ilGenerator);
	}

	// MysticalShaman.Notify_CaravanArrived uses the MechHealSerum ThingDef, which may not be available if removed by another mod
	// (such as "Lord of the Rims - The Third Age" or "Medieval - Vanilla").
	// Workaround is to instead instantiate CompUseEffect_FixWorstHealthCondition directly.
	[HarmonyPatch(typeof(MysticalShaman), nameof(MysticalShaman.Notify_CaravanArrived))]
	static class MysticalShaman_Notify_CaravanArrived_Patch
	{
		static readonly MethodInfo tryGetCompMethod =
			typeof(ThingCompUtility).GetMethod(nameof(ThingCompUtility.TryGetComp)).MakeGenericMethod(typeof(CompUseEffect_FixWorstHealthCondition));
		static readonly MethodInfo getHealWorstHealthConditionCompUseEffectMethod =
			typeof(MysticalShaman_Notify_CaravanArrived_Patch).GetMethod(nameof(GetHealWorstHealthConditionCompUseEffect), AccessTools.all);

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var instructionList = instructions as List<CodeInstruction> ?? new List<CodeInstruction>(instructions);
			var replaceStartIndex = instructionList.FindIndex(instruction =>
				instruction.opcode == OpCodes.Ldstr && instruction.operand is "MechSerumHealer");
			var replaceEndIndex = instructionList.FindIndex(replaceStartIndex + 1, instruction =>
				instruction.opcode == OpCodes.Call && instruction.operand == tryGetCompMethod);
			instructionList[replaceEndIndex] = new CodeInstruction(OpCodes.Call, getHealWorstHealthConditionCompUseEffectMethod);
			instructionList.RemoveRange(replaceStartIndex, replaceEndIndex - replaceStartIndex);
			return instructionList;
		}

		static CompUseEffect GetHealWorstHealthConditionCompUseEffect()
		{
			var compUseEffect = new CompUseEffect_FixWorstHealthCondition();
			compUseEffect.Initialize(new CompProperties_UseEffect());
			return compUseEffect;
		}
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
				method.Name.StartsWith("<TryExecuteWorker>") &&
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
				method.Name.StartsWith("<TryExecuteWorker>") &&
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
				method.Name.StartsWith("<get_Choices>") &&
				HasSilverInTradeBeaconRangeMethod(method))
			.Prepend(typeof(ChoiceLetter_ExtortionDemand).FindIteratorMethod(enumeratorType =>
				typeof(IEnumerable<DiaOption>).IsAssignableFrom(enumeratorType)));

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
				method.Name.StartsWith("<FindAlliedWarringFaction>"))
			.Prepend(typeof(IncidentWorker_WoundedCombatants).GetMethod("TryExecuteWorker", AccessTools.all));

		[HarmonyTranspiler]
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
			TechLevelComparisonTranspiler(instructions, TechLevel.Industrial, TechLevel.Neolithic);
	}
}
