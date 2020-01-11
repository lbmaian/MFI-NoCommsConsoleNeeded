using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using MoreFactionInteraction;
using MoreFactionInteraction.More_Flavour;
using MoreFactionInteraction.MoreFactionWar;
using RimWorld;
using Verse;

namespace NoCommsConsoleRequiredForIncidents
{
	using static NoCommsConsoleNeededPatcher;
	using static SilverInTradeBeaconRangeToSilverInStoragePatcher;
	using static TechLevelComparisonPatcher;

	static class ModAssemblies
	{
		public static readonly Assembly MoreFactionInteraction =
			AccessTools.TypeByName("MoreFactionInteraction.MoreFactionInteractionMod")?.Assembly;
	}

	[StaticConstructorOnStartup]
	static class HarmonyPatches
	{
		const bool DEBUG = false;

		static HarmonyPatches()
		{
			HarmonyInstance.DEBUG = DEBUG;
			try
			{
				var harmony = HarmonyInstance.Create("NoCommsConsoleRequiredForIncidents");
				harmony.PatchAll(typeof(VanillaHarmonyPatches));
				if (!(ModAssemblies.MoreFactionInteraction is null))
					harmony.PatchAll(typeof(MoreFactionInteractionHarmonyPatches));
			}
			finally
			{
				HarmonyInstance.DEBUG = false;
			}
		}

		static void PatchAll(this HarmonyInstance harmony, Type parentType)
		{
			foreach (var type in parentType.GetNestedTypes(AccessTools.all))
			{
				// Following copied from HarmonyInstance.PatchAll(Assembly).
				var harmonyMethods = type.GetHarmonyMethods();
				if (harmonyMethods != null && harmonyMethods.Count > 0)
				{
					var attributes = HarmonyMethod.Merge(harmonyMethods);
					var patchProcessor = new PatchProcessor(harmony, type, attributes);
					patchProcessor.Patch();
				}
			}
		}
	}

	// TODO: Move to own file.
	static class VanillaHarmonyPatches
	{
		[HarmonyPatch]
		static class IncidentWorker_RansomDemand_CanFireNowSub_Patch
		{
			[HarmonyTargetMethod]
			static MethodInfo CalculateMethod(HarmonyInstance _) =>
				typeof(IncidentWorker_RansomDemand).GetMethod("CanFireNowSub", AccessTools.all);

			[HarmonyTranspiler]
			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
				FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
		}

		[HarmonyPatch]
		static class ChoiceLetter_RansomDemand_SilverInTradeBeaconRange_Patch
		{
			[HarmonyTargetMethods]
			static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance _) =>
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
	}

	// TODO: Move to own file.
	static class MoreFactionInteractionHarmonyPatches
	{
		[HarmonyPatch]
		static class IncidentWorker_MysticalShaman_CanFireNowSub_Patch
		{
			static readonly Type targetType =
				ModAssemblies.MoreFactionInteraction.GetType("MoreFactionInteraction.IncidentWorker_MysticalShaman");

			[HarmonyTargetMethod]
			static MethodInfo CalculateMethod(HarmonyInstance _) => targetType.GetMethod("CanFireNowSub", AccessTools.all);

			[HarmonyTranspiler]
			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
				FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: false);
		}

		[HarmonyPatch]
		static class IncidentWorker_MysticalShaman_TechLevel_Patch
		{
			static readonly Type targetType =
				ModAssemblies.MoreFactionInteraction.GetType("MoreFactionInteraction.IncidentWorker_MysticalShaman");

			[HarmonyTargetMethods]
			static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance _) =>
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
				ModAssemblies.MoreFactionInteraction.GetType("MoreFactionInteraction.IncidentWorker_MysticalShaman");

			[HarmonyTargetMethods]
			static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance _) =>
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
			static readonly MethodInfo methodof_ThingCompUtility_TryGetComp =
				typeof(ThingCompUtility).GetMethod(nameof(ThingCompUtility.TryGetComp)).MakeGenericMethod(typeof(CompUseEffect_FixWorstHealthCondition));
			static readonly MethodInfo methodof_MysticalShaman_Notify_CaravanArrived_Patch_GetHealWorstHealthConditionCompUseEffect =
				typeof(MysticalShaman_Notify_CaravanArrived_Patch).GetMethod(nameof(GetHealWorstHealthConditionCompUseEffect), AccessTools.all);

			[HarmonyTranspiler]
			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
			{
				var instructionList = instructions as List<CodeInstruction> ?? new List<CodeInstruction>(instructions);
				var replaceStartIndex = instructionList.FindIndex(instruction =>
					instruction.opcode == OpCodes.Ldstr && instruction.operand is "MechSerumHealer");
				var replaceEndIndex = instructionList.FindIndex(replaceStartIndex + 1, instruction =>
					instruction.opcode == OpCodes.Call && instruction.operand == methodof_ThingCompUtility_TryGetComp);
				instructionList[replaceEndIndex] = new CodeInstruction(OpCodes.Call,
					methodof_MysticalShaman_Notify_CaravanArrived_Patch_GetHealWorstHealthConditionCompUseEffect);
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
			static MethodInfo CalculateMethod(HarmonyInstance _) => targetType.GetMethod("CanFireNowSub", AccessTools.all);

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
			static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance _) =>
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
			static MethodInfo CalculateMethod(HarmonyInstance _) =>
				typeof(IncidentWorker_ReverseTradeRequest).GetMethod("CanFireNowSub", AccessTools.all);

			[HarmonyTranspiler]
			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
				FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
		}

		[HarmonyPatch]
		static class IncidentWorker_ReverseTradeRequest_SilverInTradeBeaconRange_Patch
		{
			[HarmonyTargetMethods]
			static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance _) =>
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
			static MethodInfo CalculateMethod(HarmonyInstance _) =>
				typeof(IncidentWorker_Extortion).GetMethod("CanFireNowSub", AccessTools.all);

			[HarmonyTranspiler]
			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
				FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
		}

		[HarmonyPatch]
		static class ChoiceLetter_ExtortionDemand_SilverInTradeBeaconRange_Patch
		{
			[HarmonyTargetMethods]
			static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance _) =>
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
			static MethodInfo CalculateMethod(HarmonyInstance _) =>
				typeof(IncidentWorker_WoundedCombatants).GetMethod("CanFireNowSub", AccessTools.all);

			[HarmonyTranspiler]
			static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
				FakeAlwaysHaveCommsConsoleTranspiler(instructions, hasMapParam: true);
		}

		[HarmonyPatch]
		static class IncidentWorker_WoundedCombatants_TechLevel_Patch
		{
			[HarmonyTargetMethods]
			static IEnumerable<MethodBase> CalculateMethods(HarmonyInstance _) =>
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
}
