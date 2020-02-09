using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Harmony;
using RimWorld;
using Verse;

namespace NoCommsConsoleRequiredForIncidents
{
	// LotR Third Age and Medieval Vanilla mods remove certain incidents, presumably because they require a comms console.
	// Now that that requirement is patched out, ensure that all patched incidents are added back in,
	// including the patched non-vanilla incidents (in case mods removed them for similar reasons).
	// Note: Not using [DefOf] for any of the below DefOf classes,
	// since the field setting is done manually (and with custom logic) via BackupIncidents.

	static class BackupVanillaIncidentDefOf
	{
#pragma warning disable
		public static IncidentDef RansomDemand;
#pragma warning restore
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
	}

	[HarmonyPatch(typeof(DefOfHelper), nameof(DefOfHelper.RebindAllDefOfs))]
	static class BackupIncidents
	{
		[HarmonyPrefix]
		static void Prefix(bool earlyTryMode)
		{
			if (!earlyTryMode)
				return;

			var backupIncidentDefOfTypes = new List<Type> { typeof(BackupVanillaIncidentDefOf) };
			if (!(ModAssemblies.MoreFactionInteraction is null))
				backupIncidentDefOfTypes.Add(typeof(BackupMoreFactionInteractionIncidentDefOf));

			foreach (var backupIncidentDefOfType in backupIncidentDefOfTypes)
			{
				foreach (var backupIncidentDefOfField in backupIncidentDefOfType.GetFields(BindingFlags.Static | BindingFlags.Public))
				{
					var defName = backupIncidentDefOfField.Name;
					var origDef = DefDatabase<IncidentDef>.GetNamed(defName);

					// Need to non-shallow copy the def, since some mods (such as LotR Third Age) also mutate incidents that they remove.
					// Also, not using AccessTools.MakeDeepCopy since it deep copies too far (such as deep copying Type fields),
					// to the point of somehow causing a crash.
					// For our purposes, we'll just shallow copy fields and single-depth copy non-array lists.
					var copiedDef = new IncidentDef();
					foreach (var field in fieldofs_IncidentDef)
					{
						var value = field.GetValue(origDef);
						if (value is IList list && value.GetType() is var listType && !listType.IsArray)
						{
							var copiedList = (IList)Activator.CreateInstance(listType);
							foreach (var item in list)
							{
								copiedList.Add(item);
							}
							field.SetValue(copiedDef, copiedList);
						}
						else
						{
							field.SetValue(copiedDef, value);
						}
					}

					// We still want to store the original def, since other code may compare against the defs via reference equality.
					Backups.Add(new BackupIncident(defName, origDef, copiedDef));
				}
			}
		}

		internal static readonly FieldInfo[] fieldofs_IncidentDef =
			typeof(IncidentDef).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		internal readonly struct BackupIncident
		{
			public readonly string defName;
			public readonly IncidentDef origDef;
			public readonly IncidentDef copiedDef;

			public BackupIncident(string defName, IncidentDef origDef, IncidentDef copiedDef)
			{
				this.defName = defName;
				this.origDef = origDef;
				this.copiedDef = copiedDef;
			}
		}

		internal static readonly List<BackupIncident> Backups = new List<BackupIncident>();

		// For debugging
		public static string ToString(IncidentDef incidentDef)
		{
			var sb = new System.Text.StringBuilder();
			foreach (var field in fieldofs_IncidentDef)
			{
				sb.Append("\t" + field.Name + ": ");
				var value = field.GetValue(incidentDef);
				if (value is string str)
					sb.AppendLine(str);
				else if (value is IEnumerable enumerable)
					sb.AppendLine(enumerable.ToStringSafeEnumerable());
				else
					sb.AppendLine(value.ToStringSafe());
			}
			return sb.ToString();
		}
	}

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
			foreach (var backupIncident in BackupIncidents.Backups)
			{
				var defName = backupIncident.defName;
				var def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);

				if (def is null)
				{
					var origDef = backupIncident.origDef;
					var copiedDef = backupIncident.copiedDef;
					//Log.Message("backupIncident.origDef for " + defName + "\n" + BackupIncidents.ToString(origDef));
					//Log.Message("backupIncident.copiedDef for " + defName + "\n" + BackupIncidents.ToString(copiedDef));

					foreach (var field in BackupIncidents.fieldofs_IncidentDef)
					{
						// Skip copying shortHash since they were assigned after DefOfHelper.RebindAllDefOfs.
						if (field.Name is nameof(Def.shortHash))
							continue;
						var curValue = field.GetValue(origDef);
						var copiedValue = field.GetValue(copiedDef);
						if (!Equals(curValue, copiedValue))
						{
							if (curValue is IList curList && copiedValue is IList copiedList)
							{
								if (!Enumerable.SequenceEqual(curList.Cast<object>(), copiedList.Cast<object>()))
								{
									curList.Clear();
									foreach (var origItem in copiedList)
									{
										curList.Add(origItem);
									}
								}
							}
							else
							{
								field.SetValue(origDef, copiedValue);
							}
						}
					}

					readdedIncidentDefNames.Add(defName);
					DefDatabase<IncidentDef>.Add(origDef);
					//Log.Message("Restored " + defName + "\n" + BackupIncidents.ToString(origDef));
				}
			}

			if (readdedIncidentDefNames.Count > 0)
				Log.Message("Readded removed Incidents: " + readdedIncidentDefNames.Join());
		}
	}
}
