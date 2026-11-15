using ABI.CCK.Components;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using UnityEngine;

namespace FPVDrone
{
    [DisallowMultipleComponent]
    public class DroneController : MonoBehaviour
    {
        public static DroneController Instance { get; private set; }

        public bool IsArmed => _isArmed;

        private DronePhysics _physics;
        private DroneInput _input;
        private DroneAudioEngine _audio;
        private DroneCameraManager _cameraManager;
        private DroneVTXSimulation _vtx;
        private DroneNetworkSync _networkSync;
        private DronePropSync _propSync;
        private DroneOSD _osd;
        private DroneConfigMenu _configMenu;
        private DroneMeshRenderer _meshRenderer;
        private Spout.SpoutManager _spout;

        public Spout.SpoutManager Spout => _spout;
        public DronePropSync PropSync => _propSync;

        private bool _isArmed = false;
        private Vector3 _pilotTakeoffPosition = Vector3.zero;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _physics = new DronePhysics();
            _input = new DroneInput();
            _audio = new DroneAudioEngine();
            _cameraManager = new DroneCameraManager();
            _vtx = new DroneVTXSimulation();
            _networkSync = new DroneNetworkSync();
            _propSync = new DronePropSync();
            _osd = new DroneOSD();
            _configMenu = new DroneConfigMenu();
            _meshRenderer = new DroneMeshRenderer();
            _spout = new Spout.SpoutManager(Settings.SpoutSenderName.Value);

            _audio.Initialize();
            _cameraManager.Initialize();
            _osd.Initialize();
            _configMenu.Initialize(_input);
            _meshRenderer.Initialize();
            _meshRenderer.SetVisible(false);

            _spout.OnSpoutStateChanged += HandleSpoutStateChanged;
            if (Settings.ModEnabled.Value && Settings.EnableSpout.Value)
            {
                _spout.StartSpout();
            }

            _configMenu.OnResetDroneRequested += ResetDroneToPlayer;
            _configMenu.OnTurtleFlipRequested += () => _physics.TriggerTurtleFlip();
            _physics.OnCollisionImpact += speed => _audio.PlayImpactSound(speed);
        }

        private void HandleSpoutStateChanged(bool active)
        {
            _cameraManager?.SetSpoutActive(active);
            if (Settings.EnableSpout.Value != active)
            {
                Settings.EnableSpout.Value = active;
                MelonLoader.MelonPreferences.Save();
            }
        }

        private void OnDestroy()
        {
            if (_isArmed)
            {
                Disarm();
            }

            if (_spout != null)
            {
                _spout.OnSpoutStateChanged -= HandleSpoutStateChanged;
                _spout.Dispose();
                _spout = null;
            }

            _input.ReleaseAllLocks();
            _propSync?.Unbind();
            _audio.Destroy();
            _cameraManager.Destroy();
            _osd.Destroy();
            _configMenu.Destroy();
            _meshRenderer?.Destroy();

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!Settings.ModEnabled.Value)
                return;

            // 1. Immediate exit from flight mode via Tab or Escape
            if (_isArmed && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab)))
            {
                Disarm();
                ViewManager.Instance?.UiStateToggle(false);
                return;
            }

            // Close configurator menu if open and Escape is pressed when not flying
            if (!_isArmed && _configMenu.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                _configMenu.SetOpen(false);
                ViewManager.Instance?.UiStateToggle(false);
                return;
            }

            // 2. Process inputs and CVR game input locks (suppresses flight controls while menu is open)
            _input.UpdateInputs(_isArmed, _configMenu.IsOpen);

            // 3. Handle button one-shots
            if (_input.ArmTogglePressed)
            {
                ToggleArm();
            }

            if (_input.MenuTogglePressed)
            {
                _configMenu.ToggleMenu();
            }

            if (_input.ResetPressed)
            {
                ResetDroneToPlayer();
            }

            if (_input.TurtleFlipPressed)
            {
                _physics.TriggerTurtleFlip();
            }

            if (_input.FlightModeTogglePressed)
            {
                Settings.CycleFlightMode();
            }

            // 3. Update physics step
            float dt = Time.deltaTime;
            _physics.StepPhysics(
                dt,
                _isArmed,
                _input.Throttle,
                _input.Pitch,
                _input.Roll,
                _input.Yaw
            );

            // 4. Update bound CVRSpawnable prop (while armed or already bound) or procedural fallback mesh (only while armed / Spout active)
            float wheelbaseMm = DroneBuilder.EvaluateCurrent().WheelbaseMm;
            bool spoutActive = _spout != null && _spout.IsSpoutActive;
            bool propBound = _propSync != null && (_isArmed || _propSync.IsBound) && _propSync.UpdateSync(
                _physics.Position,
                _physics.Rotation,
                _isArmed,
                _physics.CurrentThrottle01,
                wheelbaseMm,
                dt);

            if (propBound || (!_isArmed && !spoutActive))
            {
                _meshRenderer?.SetVisible(false);
            }
            else
            {
                _meshRenderer?.UpdateVisual(_physics.Position, _physics.Rotation, _physics.MotorRpms, _isArmed, dt);
            }

            if (_isArmed || (_spout != null && _spout.IsSpoutActive))
            {
                _cameraManager.UpdateCameraTransform(_physics.Position, _physics.Rotation);
            }

            if (_isArmed)
            {
                _vtx.UpdateSimulation(_physics.Position, _pilotTakeoffPosition, _physics.BatteryVoltage, dt);
                bool suppressIndicator = propBound && Settings.SuppressIndicatorWhenPropBound.Value;
                if (suppressIndicator)
                {
                    if (DroneNetworkSync.IsSyncing)
                    {
                        _networkSync.StopSyncing();
                    }
                }
                else
                {
                    if (!DroneNetworkSync.IsSyncing)
                    {
                        _networkSync.StartSyncing();
                    }
                    _networkSync.UpdateSync(_physics.Position, _physics.Rotation);
                }
                _osd.UpdateFlightStats(dt, _physics);
            }
            else
            {
                _osd.TickPostFlightSummary(dt);
            }

            // 5. Update 3D spatial audio & low-battery beeper (muted if DebugHearPropAudio is active with a bound prop)
            bool muteModAudio = propBound && Settings.DebugHearPropAudio.Value;
            _audio.UpdateAudio(
                _physics.Position,
                _physics.Rotation,
                _physics.MotorThrottles,
                _physics.MotorRpms,
                _physics.CurrentRpmPercent,
                _physics.LocalAngularVelocityDeg,
                _physics.LinearVelocity.magnitude,
                _physics.GForceLoad,
                _physics.BatteryCurrentAmps,
                _physics.IsInPropWash,
                _isArmed && !muteModAudio
            );
            _audio.UpdateLowBatteryBeeper(dt, _physics.BatteryVoltage, Settings.BatteryCells.Value, _isArmed && !muteModAudio);
        }

        private void OnGUI()
        {
            if (!Settings.ModEnabled.Value)
                return;

            bool isVr = MetaPort.Instance != null && MetaPort.Instance.isUsingVr;

            // In Desktop mode, draw the FPV camera feed full-screen (with barrel distortion when enabled)
            if (_isArmed && !isVr)
            {
                GUI.depth = 100;
                _cameraManager.DrawDesktopFpvFeed();
            }

            // Draw technical Betaflight OSD overlay for player
            if (_isArmed)
            {
                _osd.DrawOSD(_physics, _vtx, _input, _isArmed);
            }
            else
            {
                _osd.DrawPostFlightSummary();
            }

            // Broadcast to Spout2 (OBS / Resolume)
            if (Event.current.type == EventType.Repaint && _spout != null && _spout.IsSpoutActive)
            {
                // Graphics.Blit leaves RenderTexture.active on its destination; restore it afterwards,
                // otherwise everything drawn later in this OnGUI (configurator) lands in an offscreen buffer.
                RenderTexture activeBeforeSpout = RenderTexture.active;
                RenderTexture spoutTex = _cameraManager.GetSpoutTexture(
                    _osd,
                    _physics,
                    _vtx,
                    _input,
                    _isArmed,
                    Settings.SpoutIncludeOSD.Value
                );

                if (spoutTex != null)
                {
                    _spout.Send(spoutTex);
                }

                RenderTexture.active = activeBeforeSpout;
            }

            // Draw Pro-FPV Configurator Menu if open
            if (_configMenu.IsOpen)
            {
                _configMenu.DrawMenu();
            }
        }

        public void ToggleSpout()
        {
            _spout?.ToggleSpout();
        }

        public void ToggleSpoutOsd()
        {
            Settings.SpoutIncludeOSD.Value = !Settings.SpoutIncludeOSD.Value;
            MelonLoader.MelonPreferences.Save();
        }

        public void SetModEnabled(bool enabled)
        {
            if (Settings.ModEnabled.Value != enabled)
            {
                Settings.ModEnabled.Value = enabled;
                MelonLoader.MelonPreferences.Save();
            }

            if (!enabled)
            {
                Disarm();
                _propSync?.Unbind();
                _meshRenderer?.SetVisible(false);
                if (_configMenu != null && _configMenu.IsOpen)
                {
                    _configMenu.SetOpen(false);
                }
                _input?.ReleaseAllLocks();
            }
        }

        public void ToggleArm()
        {
            if (!Settings.ModEnabled.Value)
                return;

            if (_isArmed)
            {
                Disarm();
            }
            else
            {
                Arm();
            }
        }

        public void OpenConfigMenu()
        {
            if (!Settings.ModEnabled.Value)
                return;
            _configMenu?.SetOpen(true);
        }

        public void ToggleConfigMenu()
        {
            if (!Settings.ModEnabled.Value)
                return;
            _configMenu?.ToggleMenu();
        }

        public void Arm()
        {
            if (_isArmed || !Settings.ModEnabled.Value)
                return;

            // Automatically close QuickMenu and Main Menu so flight begins cleanly without UI obstruction
            CVR_MenuManager.Instance?.ToggleQuickMenu(false);
            ViewManager.Instance?.UiStateToggle(false);

            RecordPilotLocation();

            // Spawn drone right in front of the pilot on the ground
            ResetDroneToPlayer();

            _isArmed = true;
            if (Settings.AutoBindDroneProp.Value)
            {
                bool bound = _propSync != null && _propSync.TryAutoBind(false);
                if (!bound && Settings.AutoSpawnDroneProp.Value)
                {
                    _propSync?.TrySpawnProp(_physics.Position);
                }
            }
            _cameraManager.SetFlightActive(true);
            if (!(_propSync != null && _propSync.IsBound && Settings.SuppressIndicatorWhenPropBound.Value))
            {
                _networkSync.StartSyncing();
            }
            _osd.ResetTimer();
            _audio.PlayArmBeep();
        }

        public void Disarm()
        {
            if (!_isArmed)
                return;

            _isArmed = false;
            _audio.PlayDisarmBeep();
            _osd.NotifyDisarmSummary(_physics.ConsumedMah);
            _input.ReleaseAllLocks();
            _cameraManager.SetFlightActive(false);
            _networkSync.StopSyncing();

            if (_configMenu != null && _configMenu.IsOpen)
            {
                _configMenu.SetOpen(false);
            }
        }

        public void TriggerTurtleFlip()
        {
            _physics?.TriggerTurtleFlip();
        }

        public void ResetDroneToPlayer()
        {
            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            var player = PlayerSetup.Instance;
            if (player != null && player.activeCam != null)
            {
                Transform camTransform = player.activeCam.transform;
                Vector3 forwardFlat = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
                if (forwardFlat.sqrMagnitude < 0.01f) forwardFlat = Vector3.forward;

                Vector3 targetGround = camTransform.position + forwardFlat * 1.5f;

                // Raycast down to find exact floor height
                if (Physics.Raycast(targetGround + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 15.0f, DronePhysics.WorldCollisionMask, QueryTriggerInteraction.Ignore))
                {
                    spawnPos = hit.point + Vector3.up * _physics.BellyHeightM;
                }
                else
                {
                    spawnPos = targetGround;
                }

                spawnRot = Quaternion.LookRotation(forwardFlat, Vector3.up);
            }
            else
            {
                spawnPos = transform.position + Vector3.up * 0.2f;
            }

            _physics.Reset(spawnPos, spawnRot);
            _input.ResetSmoothing();
            _audio.PlayEscInitBeeps();

            if (_isArmed)
            {
                _cameraManager.UpdateCameraTransform(_physics.Position, _physics.Rotation);
                _networkSync.UpdateSync(_physics.Position, _physics.Rotation);
            }
        }

        private void RecordPilotLocation()
        {
            var player = PlayerSetup.Instance;
            if (player != null && player.activeCam != null)
            {
                _pilotTakeoffPosition = player.activeCam.transform.position;
            }
            else
            {
                _pilotTakeoffPosition = transform.position;
            }
        }

        public void SpawnDroneProp()
        {
            if (_propSync == null)
                return;

            Vector3 spawnPos = _physics.Position;
            if (!_isArmed)
            {
                var player = PlayerSetup.Instance;
                if (player != null && player.activeCam != null)
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(player.activeCam.transform.forward, Vector3.up).normalized;
                    if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                    spawnPos = player.activeCam.transform.position + fwd * 1.5f;
                }
            }

            _propSync.TrySpawnProp(spawnPos, ignoreCooldown: true);
        }

        public void BindPropAuto()
        {
            if (_propSync == null)
                return;

            if (_propSync.TryAutoBind(fallbackToLatestIfNoFpvParams: true))
            {
                MelonLoader.MelonLogger.Msg($"[FPVDrone] Manually bound prop '{_propSync.BoundName}'.");
            }
            else
            {
                MelonLoader.MelonLogger.Warning("[FPVDrone] No spawned CVRSpawnable props owned by you were found.");
            }
        }

        public void UnbindProp()
        {
            _propSync?.Unbind();
        }

        public void OnWorldUnload()
        {
            Disarm();
            _propSync?.Unbind();
        }
    }
}
