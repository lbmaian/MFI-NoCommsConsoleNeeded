using Harmony;
using Verse;

namespace NoCommsConsoleRequiredForIncidents
{
	public class NoCommsConsoleNeededMod : Mod
	{
		public NoCommsConsoleNeededMod(ModContentPack content) : base(content)
		{
			var harmony = HarmonyInstance.Create("NoCommsConsoleRequiredForIncidents.early");
			harmony.Patch(typeof(BackupIncidents));
		}
	}
}
