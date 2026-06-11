# Bunnyhop

Bunnyhop increases the local player's horizontal speed when jumps are chained
without staying on the ground. It preserves CVR's normal jump height and clamps
the boost relative to the current world's movement speed.


## Settings

Settings are available under the shared `LensError's Mods` Quick Menu page:

- **Enabled**: Enables bunnyhop speed boosts.
- **Disable For Current Avatar**: Persistently disables Bunnyhop whenever the
  currently worn avatar is active.
- **Speed Per Jump**: Horizontal speed multiplier applied per chained jump.
- **Maximum Speed**: Maximum multiple of CVR's world-adjusted movement speed.
- **Reset Delay**: Time spent grounded before the jump chain resets.

All settings and per-avatar exclusions are saved between sessions using
MelonPreferences.
