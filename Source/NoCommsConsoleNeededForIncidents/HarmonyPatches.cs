using System;
using System.Reflection;
using Harmony;
using Verse;

namespace NoCommsConsoleRequiredForIncidents
{
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
	}

	static class HarmonyPatcher
	{
		public static void Patch(this HarmonyInstance harmony, Type type)
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

		public static void PatchAll(this HarmonyInstance harmony, Type containerType)
		{
			foreach (var type in containerType.GetNestedTypes(AccessTools.all))
			{
				harmony.Patch(type);
			}
		}
	}
}
