# No Comms Console Needed For Incidents
Patch/submod for RimWorld to both:
1. Remove the powered comms console requirement for some of its incidents.
2. Allow the silver that is needed for some of its incidents to be in any storage/stockpile rather than within the range of trade beacons. That is, it uses the same "what items are reachable to be sold" logic as in-map trading with visiting trade caravans.

This was originally a submod for the [More Faction Interaction (MFI) mod] but is now being generalized to patch vanilla incidents and potentially other mods. It no longer requires MFI to be active.

This is primarily to allow medieval/tribal-only playthroughs to have full access to incidents that normally require comms consoles.

The following incidents are patched to both remove the need for a powered comms console and allow the silver fee to paid from any storage/stockpile (rather than near trade beacons):
* [Vanilla] Ransom demand
* [MFI] Mystical shaman
  * Now also works even if the healer mech serum item definition is removed by a mod (such as the [Lord of the Rims - The Third Age mod]).
  * Also patches the requirement that there must exist a non-hostile neolithic (tribal) faction (that the shaman belongs to) such that only a non-hostile non-player faction of any tech level is required.
* [MFI] Roadworks
* [MFI] Reverse trade request
* [MFI] Pirate extortion
* [MFI] Wounded combatants (allied faction involved in faction war requests permission to arrive with wounded)
  * Note: Pawns still arrive in drop pods, and the dialog still talks about transport pods, radios, mortars, etc.

This mod also currently patches meat determination logic (`ThingDef.IsMeat`) to take into account meat subcategories, which fixes the MFI bumper crop incident sometimes rewarding meats in such subcategories (e.g. salted meats in the Lord of the Rims - The Third Age mod). As this particular patch is only tangentially related to this mod's purpose, it may eventually be moved to another mod (ideally to the mods that introduce such meat subcategories).

## Compatibility
Should be safe to add and remove from existing save games.

Should be compatible with other mods, as long as they don't patch the same incidents (and even then, it's still possible they're compatible).

Note: Any mod, which patches the behavior of "what items are reachable to be sold" code of in-map trading with visiting trade caravans, will not patch this mod's "silver in storage" code. This is due to the latter code having to be copied and modified from the former, for various reasons, to make it work for this mod.

## Credits
* lbmaian - author
* pardeike - [Harmony library] that's used for patching at runtime
* Mehni - [More Faction Interaction (MFI) mod] that this was originally a submod of
* Ludeon - [RimWorld]

## Links
* Steam: https://steamcommunity.com/workshop/filedetails/?id=1933275277
* GitHub: https://github.com/lbmaian/RimWorld-NoCommsConsoleNeededForIncidents

[Harmony library]: https://github.com/pardeike/Harmony
[More Faction Interaction (MFI) mod]: https://github.com/Mehni/MoreFactionInteraction
[Lord of the Rims - The Third Age mod]: https://github.com/Lord-of-the-Rims-DevTeam/Lord-of-the-Rims---The-Third-Age
[RimWorld]: https://rimworldgame.com/
