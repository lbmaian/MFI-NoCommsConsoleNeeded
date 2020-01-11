using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using RimWorld;
using Verse;

namespace NoCommsConsoleRequiredForIncidents
{
	using static NoCommsConsoleNeededPatcher;
	using static SilverInTradeBeaconRangeToSilverInStoragePatcher;

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
}
