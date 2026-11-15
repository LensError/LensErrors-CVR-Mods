# FPVDrone Spawnable Prop Connectivity Plan

## Goal
Allow `FPVDrone` to bind to a player-spawned `CVRSpawnable` drone prop in ChilloutVR so the mod drives its world position, rotation, scale, and prop spin in real time, making the physical drone visible to all players in the instance.

---

## Architecture & Integration

### 1. Prop Discovery & Binding (`FPVDrone/Network/DronePropSync.cs`)
- **Discovery**: Find active `CVRSpawnable` instances with `SpawnedByMe == true` (via reflection on `SpawnableManager.spawnables` or `UnityEngine.Object.FindObjectsOfType<CVRSpawnable>()`).
- **Auto-match / Manual Bind**:
  - Auto-detect a prop that exposes expected `CVRSpawnableValue` parameter names (e.g. `FPV_Throttle`, `FPV_Size`, `FPV_Armed`), or
  - Allow the user to pick/bind any of their currently spawned `CVRSpawnable` props from `DroneConfigMenu` / `DroneQuickMenu`.
- **Fallback**: Keep `DroneMeshRenderer` (procedural local mesh) as fallback when no `CVRSpawnable` is bound, and hide `DroneMeshRenderer` when a `CVRSpawnable` is actively bound.

### 2. Driving Position, Rotation & Network Sync
Each frame in `DroneController.Update()` while a prop is bound:
- **Physics Guard**: If the bound `CVRSpawnable` has a `Rigidbody`, set `rb.isKinematic = true` while flying/bound so Unity gravity, collisions, and `CVRSharedPhysicsController` do not fight `DronePhysics`. Restore its original `isKinematic` state on unbind/unload.
- **Transform Sync**: Copy `DronePhysics.Position` and `DronePhysics.Rotation` to `spawnable.transform.SetPositionAndRotation(pos, rot)`.
- **Network Broadcast**: Call `spawnable.ForceUpdate()` so `CVRSpawnable.sendUpdate()` transmits `position`, `eulerAngles`, and `syncValues` at `MetaPort.SystemTickRate`.
- **Parameter Sync (`CVRSpawnableValue`)**:
  - Push `FPV_Armed` (`0` or `1`) and `FPV_Throttle` (`0..1`) via `spawnable.SetValue(idx, val)` to drive prop spin speed in the prop's Animator for all players.
  - Push `FPV_Size` (`0..1` mapped from `65 mm .. 400 mm` relative to `225 mm` base) to drive a child `VisualRoot` scale animation (because `PropNetwork.SendUpdate` hardcodes `Vector3.one` for the root `CVRSpawnable` scale).
- **Local Camera**: Optionally disable/exclude the bound prop's renderers from the FPV camera (or keep them visible if the user wants props/frame in the FPV view).
- **Camera Indicator Toggle**: Allow keeping or suppressing the native CVR camera indicator (`DroneNetworkSync`) when a `CVRSpawnable` prop is already representing the drone in multiplayer.

---

## Possible Rotation & Axis Problems

1. **Blender `.blend` `X = -90` Root Rotation**:
   - Unity imports `.blend` roots with `X = -90` to convert Blender Z-up (`+Z` up, `-Y` forward) to Unity Y-up (`+Y` up, `+Z` forward).
   - **Fix**: Place `CVRSpawnable` on an empty `DroneRoot` parent at rotation `(0, 0, 0)`, with the `.blend` model as a child. If `CVRSpawnable` is on the `.blend` object directly, the drone will pitch 90 degrees nose-up/down or roll sideways in flight.
2. **Motor / Prop Spin Axis (`Z` vs `Y`)**:
   - Because the `.blend` mesh object has `X = -90`, child motor objects (`motor 1..4`) with `(0,0,0)` rotation in Blender will have their **local `Z` axis** pointing up in Unity instead of `Y`.
   - **Fix**: Check local gizmos in Unity before creating the spin animation clips. Either animate local `Z` rotation on `motor 1..4`, or wrap each motor under an unrotated empty pivot (`0,0,0`) in Unity and animate `Y`.
3. **Negative Scale / Mirrored Prop Normals**:
   - Duplicating and mirroring a prop in Blender with scale `-1` flips spin direction and normals.
   - **Fix**: Apply Rotation & Scale (`Ctrl+A`) in Blender on all meshes so every object has `(1, 1, 1)` scale before exporting/importing.
4. **Euler Wraparound & Fast-Flip Slerp Artifacts**:
   - `CVRSpawnable.sendUpdate()` sends `transform.eulerAngles` over the network at `MetaPort.SystemTickRate`, and remote clients run `CVRTools.Slerp` between incoming orientations.
   - During extreme snap flips (> 180 degrees within a single network tick, e.g. > 3600 deg/s or during network hitching), remote players may see the drone slerp the opposite short arc for one tick.
   - At `+-90` deg pitch (vertical climbs/dives), Euler representation can jump across `0`/`180`, though `Quaternion.Euler` on the receiving end reconstructs the correct orientation.
5. **Pickup / Attachment Interference**:
   - If another player grabs the prop with `CVRPickupObject`, `CVRSpawnable.Update()` / `FixedUpdate()` can override or fight the transform.
   - **Fix**: Check `pickup.IsGrabbedByMe` / `SyncType` or unbind/force-drop while armed, and consider setting `CVRSpawnable.propPrivacy` to `owner` on the prefab so others cannot grab it mid-flight.
6. **Child `VisualRoot` Scale vs Root Scale**:
   - Scaling `spawnable.transform.localScale` directly is ignored by `PropNetwork.SendUpdate` (hardcoded `Vector3.one`).
   - **Fix**: Always scale a child `VisualRoot` via `CVRSpawnableValue` + Animator so remote clients see the `65 mm - 400 mm` size match.

---

## Verification Plan
1. Build `FPVDrone.csproj` (`dotnet build FPVDrone\FPVDrone.csproj -c Release`).
2. Test in ChilloutVR with a local `CVRSpawnable` prop:
   - Verify forward (`+Z`), up (`+Y`), and right (`+X`) match `DronePhysics` during pitch, roll, yaw, and inverted flips.
   - Verify landed height rests flush on the floor (`BellyHeightM` offset).
   - Verify `FPV_Throttle` and `FPV_Size` Animator parameters update smoothly and sync to a second client / mirror.

