using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Harmony.ILCopying;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoreFactionInteraction.NoCommsConsole
{
	class SilverInTradeBeaconRangeToSilverInStoragePatcher
	{
		static readonly MethodInfo launchSilverMethod = typeof(TradeUtility).GetMethod(nameof(TradeUtility.LaunchSilver));
		static readonly MethodInfo playerHomeMapWithMostLaunchableSilverMethod =
			typeof(TradeUtility).GetMethod(nameof(TradeUtility.PlayerHomeMapWithMostLaunchableSilver));
		static readonly MethodInfo colonyHasEnoughSilverMethod = typeof(TradeUtility).GetMethod(nameof(TradeUtility.ColonyHasEnoughSilver));
		static readonly List<MethodInfo> tradeBeaconMethods =
			new List<MethodInfo>() { launchSilverMethod, playerHomeMapWithMostLaunchableSilverMethod, colonyHasEnoughSilverMethod };

		static readonly ConstructorInfo silverInStorageTrackerConstructor = typeof(SilverInStorageTracker).GetConstructor(Type.EmptyTypes);
		static readonly MethodInfo silverInStorageTrackerPayFeeMethod =
			typeof(SilverInStorageTracker).GetMethod(nameof(SilverInStorageTracker.PayFee));
		static readonly MethodInfo silverInStorageTrackerPlayerHomeMapWithMostMethod =
			typeof(SilverInStorageTracker).GetMethod(nameof(SilverInStorageTracker.PlayerHomeMapWithMost));
		static readonly MethodInfo silverInStorageTrackerMapHasEnoughMethod =
			typeof(SilverInStorageTracker).GetMethod(nameof(SilverInStorageTracker.MapHasEnough));

		public static bool HasSilverInTradeBeaconRangeMethod(MethodInfo method)
		{
			var instructions = MethodBodyReader.GetInstructions(generator: null, method: method);
			return instructions.Any(instruction => instruction.opcode == OpCodes.Call && tradeBeaconMethods.Contains(instruction.operand as MethodInfo));
		}

		public static IEnumerable<CodeInstruction> ReplaceSilverInTradeBeaconRangeWithSilverInStorageTranspiler(
			IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
		{
			var silverInStorageTrackerVar = ilGenerator.DeclareLocal(typeof(SilverInStorageTracker));
			yield return new CodeInstruction(OpCodes.Newobj, silverInStorageTrackerConstructor);
			yield return new CodeInstruction(OpCodes.Stloc_S, silverInStorageTrackerVar);
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method && tradeBeaconMethods.Contains(method))
				{
					// Assumption: None of the replaced instructions have labels (targets of branches).
					yield return new CodeInstruction(OpCodes.Ldloc_S, silverInStorageTrackerVar);
					if (method == launchSilverMethod)
						yield return new CodeInstruction(OpCodes.Call, silverInStorageTrackerPayFeeMethod);
					else if (method == playerHomeMapWithMostLaunchableSilverMethod)
						yield return new CodeInstruction(OpCodes.Call, silverInStorageTrackerPlayerHomeMapWithMostMethod);
					else if (method == colonyHasEnoughSilverMethod)
						yield return new CodeInstruction(OpCodes.Call, silverInStorageTrackerMapHasEnoughMethod);
					continue;
				}
				yield return instruction;
			}
		}
	}

	class SilverInStorageTracker
	{
		class SilverInStorage
		{
			public readonly IEnumerable<Thing> stacks;
			public readonly int total;

			public SilverInStorage(IEnumerable<Thing> stacks, int total)
			{
				this.stacks = stacks;
				this.total = total;
			}

			public override string ToString() =>
				$"silver in storage (total = {total}):\n\t{stacks.Join(thing => thing.Label, "\n\t")}";
		}

		readonly Dictionary<int, SilverInStorage> silverInStoragePerMapCache = new Dictionary<int, SilverInStorage>();

		delegate bool InSellablePositionDelegate(Thing t, out string reason);

		static readonly InSellablePositionDelegate InSellablePosition =
			(InSellablePositionDelegate)Delegate.CreateDelegate(typeof(InSellablePositionDelegate),
				// InSellablePosition is an instance method of TradeDeal but it doesn't use its TradeDeal instance at all, so null instance is fine.
				firstArgument: null,
				typeof(TradeDeal).GetMethod("InSellablePosition", AccessTools.all));

		SilverInStorage GetSilverInStorage(Map map)
		{
			if (!silverInStoragePerMapCache.TryGetValue(map.uniqueID, out var silverInStorage))
			{
				// Based off TradeDeal.AddAllTradeables and Pawn_TraderTracker.ColonyThingsWillingToBuy.
				var colonists = map.mapPawns.FreeColonists.ToList();
				var silverStacks = map.listerThings.AllThings.Where(thing =>
					thing.def == ThingDefOf.Silver &&
					// TradeUtility.PlayerSellableNow // This is always true for silver.
					!thing.Position.Fogged(thing.Map) &&
					(map.areaManager.Home[thing.Position] || thing.IsInAnyStorage()) &&
					// Based off Pawn_TraderTracker.ReachableForTrade.
					colonists.Any(pawn => map.reachability.CanReach(pawn.Position, thing, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Some)) &&
					InSellablePosition(thing, out _));
				silverInStorage = new SilverInStorage(silverStacks, silverStacks.Sum(silver => silver.stackCount));
				Log.Message("Found " + silverInStorage);
				silverInStoragePerMapCache.Add(map.uniqueID, silverInStorage);
			}
			return silverInStorage;
		}

		IEnumerable<Thing> GetStacks(Map map) => GetSilverInStorage(map).stacks;

		int GetTotal(Map map) => GetSilverInStorage(map).total;

		// Replacement for TradeUtility.LaunchSilver(Map map, int fee).
		// Static with instance as last parameter for transpiler convenience.
		public static void PayFee(Map map, int fee, SilverInStorageTracker silverInStorageTracker)
		{
			foreach (var silverStack in silverInStorageTracker.GetStacks(map))
			{
				if (fee == 0)
					return;
				var silverStackPayment = Math.Min(fee, silverStack.stackCount);
				silverStack.SplitOff(silverStackPayment).Destroy();
				fee -= silverStackPayment;
			}
			Log.Error("Could not find any more " + ThingDefOf.Silver + " to pay fee");
		}

		// Replacement for TradeUtility.PlayerHomeMapWithMostLaunchableSilver().
		// Static with instance as last parameter for transpiler convenience.
		public static Map PlayerHomeMapWithMost(SilverInStorageTracker silverInStorageTracker) =>
			Find.Maps.Where(map => map.IsPlayerHome).MaxBy(silverInStorageTracker.GetTotal);

		// Replacement for TradeUtility.ColonyHasEnoughSilver(Map map, int fee).
		// Static with instance as last parameter for transpiler convenience.
		public static bool MapHasEnough(Map map, int fee, SilverInStorageTracker silverInStorageTracker) =>
			silverInStorageTracker.GetTotal(map) >= fee;
	}
}
