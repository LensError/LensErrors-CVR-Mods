# CVR Trainer

A tiny just-for-fun ChilloutVR trainer-style overlay inspired by GTA V mod menus.

Current state:

- Press `F4` to open or close the menu
- Uses GTA-style numpad controls: `8`/`2` move, `4`/`6` change options, `5` select, `0` back
- Also supports non-numpad controls: arrow keys move/change options, `Enter` selects, `Backspace`/`Escape` goes back
- Locks ChilloutVR player input while the trainer menu is open so those keys do not also move/control the game
- Shows a top-right trainer panel with a `MENU` header, subheaders, highlighted rows, option values, and footer hints
- Includes a `Player` page with respawn, save position, teleport to saved position, and built-in ChilloutVR `Flight`, `Noclip`, and `Clip Flight` controls
- Includes a `World` page with reload world, go home, copy world ID, copy instance ID, and drop current instance portal
- Includes a `Vehicle Spawner` spawnables browser with `Vehicles`, `Props`, `Favorites`, and `Recent` folders
- Spawnable entries support GUID add/paste, API-resolved names/authors, select-to-spawn, immediate spawn, delete-last, favorite, move up/down, local rename, and remove-saved actions
- Includes a `Props` page with delete all my props, delete all props locally, prop delete mode, and clear prop mode
- Add entries manually by typing a GUID or by using `Paste GUID` / `Paste Clipboard`
- Saved vehicle, prop, favorite, recent, and local label data persists between sessions

Spawner actions use ChilloutVR's normal prop spawning path. Vehicle entries are treated as spawnable GUIDs too, so vehicle-like spawnables can be saved and spawned the same way. `Select To Spawn`, `Prop Delete Mode`, and `Drop Portal` close the trainer so game controls are available.
