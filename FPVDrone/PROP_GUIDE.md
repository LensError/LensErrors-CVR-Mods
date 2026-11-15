# Custom Drone Prop Guide

Create your own ChilloutVR `CVRSpawnable` drone prop that auto-binds to `FPVDrone` and syncs size, arming, props, and audio to other players.

## 1. Hierarchy & Orientation
- **Orientation**: Forward = `+Z`, Up = `+Y`, centered at the drone's center of mass.
- **Root vs Visual Child**: Put `CVRSpawnable` and `Animator` on the root, and place your drone mesh under a child object (e.g. `VisualRoot`) so scale animations do not fight the `CVRSpawnable` root transform.
- **Colliders**: Avoid adding physics colliders to the prop (your own prop's colliders are disabled while bound, but other players' props could collide with yours in mid-air).

## 2. `CVRSpawnable` Sync Parameters
Add any of these float parameters to `CVRSpawnable -> Sync Values` (having at least one lets `FPVDrone` auto-detect and bind the prop):

| Parameter | Range | Description |
|---|---|---|
| `FPV_Size` | `0.0` - `1.0` | Frame wheelbase mapped linearly from `65 mm` (`0.0`) to `400 mm` (`1.0`). |
| `FPV_Armed` | `0.0` / `1.0` | `1.0` while armed, `0.0` when disarmed. |
| `FPV_Throttle` | `0.0` - `1.0` | Current throttle (`0.0` when disarmed). |

### Recommended Animator Setup
- **Size (`FPV_Size`)**: Animate `VisualRoot` uniform scale (`X = Y = Z`) with a 1D Motion Time clip from `65 / refMm` at `0.0` to `400 / refMm` at `1.0`. For example, if your model is `225 mm` motor-to-motor diagonal (corner-to-corner), set `VisualRoot` scale to `0.289` (`65 / 225`) at `FPV_Size = 0.0` and `1.778` (`400 / 225`) at `FPV_Size = 1.0`.
- **Props (`FPV_Armed` / `FPV_Throttle`)**: Spin the 4 prop/motor meshes on `Y`, using `FPV_Throttle` (or `FPV_Armed` for idle spin) to drive speed/motion.
- **Audio (Optional)**: Add a 3D `AudioSource` driven by `FPV_Armed` and `FPV_Throttle` (`volume` and `pitch`). You can use the included [Drone_PropBuzz.wav](resources/Drone_PropBuzz.wav) loop. `FPVDrone` mutes your prop's `AudioSource` locally while bound so you hear the mod's 4-motor audio, while other players hear the prop's 3D audio.

## 3. Hiding Parts from Your FPV Camera (`Discard`)
- Any renderer whose GameObject name contains `Discard` (case-insensitive, e.g. `Canopy_Discard`) is moved to the hidden camera layer while armed, so it does not block your FPV / Spout view while remaining visible when disarmed and to other players.

## 4. Using It In-Game
1. Upload the prop with the CVR CCK.
2. Spawn it manually with `Auto-Bind Drone Prop` enabled, **or** paste its prop GUID into `Drone Prop GUID` in the `FPV Drone` Quick Menu tab and enable `Auto-Spawn Prop`.

