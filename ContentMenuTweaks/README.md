# Content Menu Tweaks

Small improvements for ChilloutVR's native content menus:

- Avatars: **Recently Used**
- Avatars: **Recently Seen**
- Props: **Recently Spawned**
- Props: **Recently Seen**
- Worlds: **Recently Visited**
- Your own and friends' joinable Friends, Friends of Friends, Friends of Group,
  Group-only, and missing Public instances show up in Active Worlds

Recent categories are inserted at the top of their matching category lists and
show the most recent 48 entries recorded locally.

## Settings

Install UIExpansionKit to manage these toggles in CVR's Settings menu:

- Recent Avatar Category
- Recent Prop Category
- Recent World Category
- Friend Active Instances
- Recently Seen Avatar Category
- Recently Seen Prop Category
- Hide Private Seen Avatars
- Hide Private Seen Props

## Notes

- Recent data is stored locally in `UserData\MelonPreferences.cfg`.
- This mod does not create or modify CVR cloud categories.
- Prop history only records props spawned by you.
- Recently Seen histories record avatars worn by other players and props spawned
  by other players.
- Recent categories have a local Clear button in the native content menu.
- Friend active instances are derived from CVR's own online friend state and
  your current instance state.

## Known Problems

- Names and images are best-effort. If CVR has not exposed details for an item
  yet, the category may initially show a shortened content ID.
