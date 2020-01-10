using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Harmony;
using RimWorld;
using Verse;

namespace NoCommsConsoleRequiredForIncidents
{
	[DefOf]
	static class BackupIncidentDefOf
	{
#pragma warning disable
		public static IncidentDef RansomDemand;
#pragma warning restore

		// This will be called the moment DefOfHelper sets an above field value.
		// This provides us with de-facto DefOf binding hook.
		static BackupIncidentDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(BackupIncidentDefOf));
			BackupIncidentDefOfTypes = new List<Type>();
			if (!(ModAssemblies.MoreFactionInteraction is null))
				BackupIncidentDefOfTypes.Add(typeof(BackupMoreFactionInteractionIncidentDefOf));
			foreach (var backupIncidentDefOfType in BackupIncidentDefOfTypes)
				DefOfHelper_BindDefsFor(backupIncidentDefOfType);
			BackupIncidentDefOfTypes.Add(typeof(BackupIncidentDefOf));
		}

		internal static readonly List<Type> BackupIncidentDefOfTypes;

		static readonly Action<Type> DefOfHelper_BindDefsFor = (Action<Type>)Delegate.CreateDelegate(typeof(Action<Type>),
			typeof(DefOfHelper).GetMethod("BindDefsFor", AccessTools.all));
	}

	static class BackupMoreFactionInteractionIncidentDefOf
	{
#pragma warning disable
		public static IncidentDef MFI_MysticalShaman;
		public static IncidentDef MFI_RoadWorks;
		public static IncidentDef MFI_ReverseTradeRequest;
		public static IncidentDef MFI_PirateExtortion;
		public static IncidentDef MFI_WoundedCombatants;
#pragma warning restore

		static BackupMoreFactionInteractionIncidentDefOf() =>
			DefOfHelper.EnsureInitializedInCtor(typeof(BackupMoreFactionInteractionIncidentDefOf));
	}

	// LotR Third Age and Medieval Vanilla mods remove the RansomDemand incident since it requires a comms console.
	// Now that that requirement is patched out, ensure that all patched incidents are added back in,
	// including the patched non-vanilla incidents (in case mods removed them for similar reasons).
	[StaticConstructorOnStartup]
	static class ReaddRemovedIncidents
	{
		static ReaddRemovedIncidents()
		{
			var thirdAgeRemoveModernStuff = AccessTools.TypeByName("TheThirdAge.RemoveModernStuff");
			if (!(thirdAgeRemoveModernStuff is null))
			{
				RuntimeHelpers.RunClassConstructor(thirdAgeRemoveModernStuff.TypeHandle);
			}

			var medievalVanillaRemovePostMedieval = AccessTools.TypeByName("MedievalVanilla.RemovePostMedieval");
			if (!(medievalVanillaRemovePostMedieval is null))
			{
				RuntimeHelpers.RunClassConstructor(medievalVanillaRemovePostMedieval.TypeHandle);
			}

			var readdedIncidentDefNames = new List<string>();
			foreach (var backupIncidentDefOfType in BackupIncidentDefOf.BackupIncidentDefOfTypes)
			{
				foreach (var backupIncidentDefOfField in backupIncidentDefOfType.GetFields(BindingFlags.Public | BindingFlags.Static))
				{
					var incidentDefName = backupIncidentDefOfField.Name;
					var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incidentDefName);
					if (incidentDef is null)
					{
						var backupIncidentDef = (IncidentDef)backupIncidentDefOfField.GetValue(null);
						if (backupIncidentDef is null)
						{
							Log.Error("Unexpectedly could not find incident " + incidentDefName + " in " + backupIncidentDefOfType);
						}
						else
						{
							readdedIncidentDefNames.Add(incidentDefName);
							DefDatabase<IncidentDef>.Add(backupIncidentDef);
						}
					}
				}
			}
			if (readdedIncidentDefNames.Count > 0)
				Log.Message("Readded removed Incidents: " + readdedIncidentDefNames.Join());
		}
	}
}
