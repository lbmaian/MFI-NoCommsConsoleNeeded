# More Faction Interaction Patch - No Comms Console Needed
Patch/submod for the RimWorld [More Faction Interaction (MFI) mod](https://github.com/Mehni/MoreFactionInteraction) to both:
1. Remove the powered comms console requirement for some of its incidents.
2. Allow the silver that is needed for some of its incidents to be in any storage/stockpile rather than within the range of trade beacons. That is, it uses the same "what items are reachable to be sold" logic as in-map trading with visiting trade caravans.

This is primarily to allow medieval/tribal-only playthroughs to have full access to MFI features.

The following MFI incidents are patched to both remove the need for a powered comms console and allow the silver fee to paid from any storage/stockpile (rather than near trade beacons):
* Mystical shaman  
  Also patches the requirement that there must exist a non-hostile neolithic (tribal) faction (that the shaman belongs to) such that only a non-hostile non-player faction of any tech level is required.
* Roadworks
* Reverse trade request
* Pirate extortion
* Wounded combatants (allied faction involved in faction war requests permission to arrive with wounded)  
  Note: Pawns still arrive in drop pods, and the dialog still talks about transport pods, radios, mortars, etc.
* (Vanilla incident) Ransom demand

## Compatibility
Should be safe to add and remove from existing save games.

Should be compatible with other mods, as long as they don't patch the same incidents (and even then, it's still possible they're compatible).

Note: Any mod, which patches the behavior of "what items are reachable to be sold" code of in-map trading with visiting trade caravans, will not patch this mod's "silver in storage" code. This is due to the latter code having to be copied and modified from the former, for various reasons, to make it work for this mod.

## Credits
* lbmaian - author
* pardeike - [Harmony library](https://github.com/pardeike/Harmony) that's used for patching at runtime
* Mehni - [More Faction Interaction mod](https://github.com/Mehni/MoreFactionInteraction) that this is a submod of
* Ludeon - [RimWorld](https://rimworldgame.com/)
