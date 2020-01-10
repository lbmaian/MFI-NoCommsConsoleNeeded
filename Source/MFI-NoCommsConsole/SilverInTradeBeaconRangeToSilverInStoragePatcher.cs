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

namespace NoCommsConsoleRequiredForIncidents
{
	class SilverInTradeBeaconRangeToSilverInStoragePatcher
	{
		static readonly MethodInfo methodof_TradeUtility_LaunchSilver =
			typeof(TradeUtility).GetMethod(nameof(TradeUtility.LaunchSilver));
		static readonly MethodInfo methodof_TradeUtility_PlayerHomeMapWithMostLaunchableSilver =
			typeof(TradeUtility).GetMethod(nameof(TradeUtility.PlayerHomeMapWithMostLaunchableSilver));
		static readonly MethodInfo methodof_TradeUtility_ColonyHasEnoughSilver =
			typeof(TradeUtility).GetMethod(nameof(TradeUtility.ColonyHasEnoughSilver));

		static readonly ConstructorInfo methodof_SilverInStorageTracker_ctor =
			typeof(SilverInStorageTracker).GetConstructor(Type.EmptyTypes);
		static readonly MethodInfo methodof_SilverInStorageTracker_PayFee =
			typeof(SilverInStorageTracker).GetMethod(nameof(SilverInStorageTracker.PayFee));
		static readonly MethodInfo methodof_SilverInStorageTracker_PlayerHomeMapWithMost =
			typeof(SilverInStorageTracker).GetMethod(nameof(SilverInStorageTracker.PlayerHomeMapWithMost));
		static readonly MethodInfo methodof_SilverInStorageTracker_MapHasEnough =
			typeof(SilverInStorageTracker).GetMethod(nameof(SilverInStorageTracker.MapHasEnough));

		public static bool HasSilverInTradeBeaconRangeMethod(MethodInfo method)
		{
			var instructions = MethodReader.GetInstructions(method);
			return instructions.Any(instruction => instruction.opcode == OpCodes.Call && IsTradeBeaconMethod(instruction.operand as MethodInfo));
		}

		public static IEnumerable<CodeInstruction> ReplaceSilverInTradeBeaconRangeWithSilverInStorageTranspiler(
			IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
		{
			var silverInStorageTrackerVar = ilGenerator.DeclareLocal(typeof(SilverInStorageTracker));
			yield return new CodeInstruction(OpCodes.Newobj, methodof_SilverInStorageTracker_ctor);
			yield return new CodeInstruction(OpCodes.Stloc_S, silverInStorageTrackerVar);
			foreach (var instruction in instructions)
			{
				if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method && IsTradeBeaconMethod(method))
				{
					// Assumption: None of the replaced instructions have labels (targets of branches).
					yield return new CodeInstruction(OpCodes.Ldloc_S, silverInStorageTrackerVar);
					if (method == methodof_TradeUtility_LaunchSilver)
						yield return new CodeInstruction(OpCodes.Call, methodof_SilverInStorageTracker_PayFee);
					else if (method == methodof_TradeUtility_PlayerHomeMapWithMostLaunchableSilver)
						yield return new CodeInstruction(OpCodes.Call, methodof_SilverInStorageTracker_PlayerHomeMapWithMost);
					else if (method == methodof_TradeUtility_ColonyHasEnoughSilver)
						yield return new CodeInstruction(OpCodes.Call, methodof_SilverInStorageTracker_MapHasEnough);
				}
				else if (instruction.opcode == OpCodes.Ldstr && instruction.operand is "NeedSilverLaunchable")
				{
					yield return new CodeInstruction(OpCodes.Ldstr, "NeedSilverInStorage");
				}
				else
				{
					yield return instruction;
				}
			}
		}

		static bool IsTradeBeaconMethod(MethodInfo method) =>
			method == methodof_TradeUtility_LaunchSilver ||
			method == methodof_TradeUtility_PlayerHomeMapWithMostLaunchableSilver ||
			method == methodof_TradeUtility_ColonyHasEnoughSilver;
	}

	class MethodReader
	{
		static readonly FieldInfo fieldof_MethodBodyReader_locals = typeof(MethodBodyReader).GetField("locals", AccessTools.all);
		static readonly FieldInfo fieldof_MethodBodyReader_variables = typeof(MethodBodyReader).GetField("variables", AccessTools.all);
		static readonly FieldInfo fieldof_MethodBodyReader_ilInstructions = typeof(MethodBodyReader).GetField("ilInstructions", AccessTools.all);

		public static List<ILInstruction> GetInstructions(MethodInfo method)
		{
			var reader = new MethodBodyReader(method, generator: null);
			// Workaround for MethodBodyReader bug where opcodes that have (Short)InlineVar operand type (such as ldloc.s)
			// can result in a NullReferenceException due to MethodBodyReader.variables being null when generator is null.
			var locals = (IList<LocalVariableInfo>)fieldof_MethodBodyReader_locals.GetValue(reader);
			fieldof_MethodBodyReader_variables.SetValue(reader, new LocalBuilder[locals.Count]);
			reader.ReadInstructions();
			var instructions = (List<ILInstruction>)fieldof_MethodBodyReader_ilInstructions.GetValue(reader);
			foreach (var instruction in instructions)
			{
				var operandType = instruction.opcode.OperandType;
				if ((operandType == OperandType.ShortInlineVar || operandType == OperandType.InlineVar) &&
					instruction.opcode.Name.Contains("loc") && instruction.operand is LocalVariableInfo local)
				{
					// We can't construct a LocalBuilder without an ILGenerator, so just use the LocalVariableInfo for argument.
					instruction.argument = local;
				}
			}
			return instructions;
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

		delegate bool InSellablePositionDelegate(TradeDeal tradeDeal, Thing t, out string reason);

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
					ReachableForTrade(map, colonists, thing) &&
					InSellablePosition(thing));
				silverInStorage = new SilverInStorage(silverStacks, silverStacks.Sum(silver => silver.stackCount));
				//Log.Message("Found " + silverInStorage);
				silverInStoragePerMapCache.Add(map.uniqueID, silverInStorage);
			}
			return silverInStorage;
		}

		// Based off Pawn_TraderTracker.ReachableForTrade,
		// generalized so that any arbitrary free colonist on the map can meet the requirement.
		static bool ReachableForTrade(Map map, List<Pawn> colonists, Thing thing) =>
			colonists.Any(pawn => map.reachability.CanReach(pawn.Position, thing, PathEndMode.Touch, TraverseMode.PassDoors, Danger.Some));

		// Based off TradeDeal.InSellablePosition, which can't be called because it's an instance method,
		// and we can't construct a TradeDeal instance since its constructor ends up calling AddAllTradeables,
		// which is unwanted. The method doesn't even use its instance (it could be a static method),\
		// but in RimWorld's Mono/Unity version, MethodInfo.Invoke for an instance method requires non-null target.
		static bool InSellablePosition(Thing thing)
		{
			if (!thing.Spawned)
				return false;
			// Don't need to check whether thing.Position.Fogged(thing.Map), since it's already checked in caller.
			if (thing.GetRoom() is var room)
			{
				var map = thing.Map;
				var radialCellCount = GenRadial.NumCellsInRadius(RoofCollapseUtility.RoofMaxSupportDistance);
				for (var radialCellIndex = 0; radialCellIndex < radialCellCount; radialCellIndex++)
				{
					var position = thing.Position + GenRadial.RadialPattern[radialCellIndex];
					if (position.InBounds(map) && position.GetRoom(map) == room)
					{
						foreach (var positionThing in position.GetThingList(map))
						{
							if (positionThing.PreventPlayerSellingThingsNearby(out _))
								return false;
						}
					}
				}
			}
			return true;
		}

		IEnumerable<Thing> GetStacks(Map map) => GetSilverInStorage(map).stacks;

		int GetTotal(Map map) => GetSilverInStorage(map).total;

		// Replacement for TradeUtility.LaunchSilver(Map map, int fee).
		// Static with instance as last parameter for transpiler convenience.
		public static void PayFee(Map map, int fee, SilverInStorageTracker silverInStorageTracker)
		{
			if (fee == 0)
				return;
			// Need to evaluate the stacks enumerable into a list first, since splitting silver stacks modifies the stacks enumerable itself,
			// and if enumerated directly, would have caused an InvalidOperationException.
			var silverStacks = silverInStorageTracker.GetStacks(map).ToList();
			foreach (var silverStack in silverStacks)
			{
				var silverStackPayment = Math.Min(fee, silverStack.stackCount);
				silverStack.SplitOff(silverStackPayment).Destroy();
				fee -= silverStackPayment;
				if (fee == 0)
					return;
			}
			Log.Error($"Could not find any more {ThingDefOf.Silver} to pay remaining fee {fee}.");
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
