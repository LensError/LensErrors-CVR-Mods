using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using UnityEngine;

namespace FPVDrone
{
    public class DroneCameraManager
    {
        private const int VrLeftGoggleLayer = CVRLayers.CVRReserved2;  // Layer 13
        private const int VrRightGoggleLayer = CVRLayers.CVRReserved3; // Layer 14

        private GameObject _cameraRigObject;
        private Camera _fpvCamera;
        private AudioListener _fpvAudioListener;
        private AudioListener _cachedPlayerListener;
        private RenderTexture _fpvRenderTexture;

        // Stereo 3D right-eye drone camera (Option 2b)
        private GameObject _fpvRightRigObject;
        private Camera _fpvRightCamera;
        private RenderTexture _fpvRightRenderTexture;

        // Full 3D VR cockpit camera rig (native XR stereo view mounted at drone camera)
        private GameObject _vrCockpitRigObject;
        private GameObject _vrCockpitCamObject;
        private Camera _vrCockpitCamera;
        private Camera _vrCockpitOsdCam;
        private GameObject _vrCockpitOsdObject;
        private bool _vrWorldCamSuppressed = false;
        private int _savedVrCamCullingMask = 0;
        private CameraClearFlags _savedVrCamClearFlags = CameraClearFlags.Skybox;

        // VR head-mounted goggle rig (black surround backdrop + left/right eye quads + OSD overlay)
        private Camera _vrBackdropCamera;
        private Camera _vrDisplayLeftCam;
        private Camera _vrDisplayRightCam;
        private GameObject _vrGogglesLeftObject;
        private GameObject _vrGogglesRightObject;
        private GameObject _vrLeftOsdObject;
        private GameObject _vrRightOsdObject;
        private MeshFilter _vrGogglesLeftFilter;
        private MeshFilter _vrGogglesRightFilter;
        private Material _displayMaterial;
        private Material _rightDisplayMaterial;
        private Material _osdOverlayMaterial;
        private RenderTexture _vrOsdRenderTexture;
        private Mesh _distortionMesh;

        private float _cachedDistortionK = -999.0f;
        private bool _cachedDistortionEnabled = false;

        private bool _isArmed = false;
        private bool _isSpoutActive = false;
        private bool _isConfigMenuOpen = false;
        private RenderTexture _spoutOsdRenderTexture;

        // Dedicated gimbal camera for stabilized clean Spout2 output
        private GameObject _gimbalRigObject;
        private Camera _gimbalCamera;
        private RenderTexture _gimbalRenderTexture;
        private Quaternion _gimbalSmoothedRot = Quaternion.identity;
        private Quaternion _gimbalSmoothedYaw = Quaternion.identity;
        private float _gimbalSmoothedPitch = 0.0f;
        private float _gimbalSmoothedRoll = 0.0f;
        private Vector3 _lastValidFlatFwd = Vector3.forward;
        private bool _gimbalRotInitialized = false;

        public Camera FpvCamera => _fpvCamera;
        public RenderTexture FpvRenderTexture => _fpvRenderTexture;

        public void Initialize()
        {
            if (_cameraRigObject != null)
                return;

            _cameraRigObject = new GameObject("FPVDrone_CameraRig");
            Object.DontDestroyOnLoad(_cameraRigObject);

            _fpvCamera = CreateFpvCameraComponent(_cameraRigObject, depth: 10);

            _fpvAudioListener = _cameraRigObject.AddComponent<AudioListener>();
            _fpvAudioListener.enabled = false;

            Settings.GetResolutionDimensions(Settings.FpvCameraResolution.Value, out int rtWidth, out int rtHeight);
            CreateRenderTexture(rtWidth, rtHeight);

            Shader unlit = Shader.Find("Unlit/Texture") ?? Shader.Find("UI/Default") ?? Shader.Find("Standard");
            _displayMaterial = new Material(unlit)
            {
                mainTexture = _fpvRenderTexture
            };
            _displayMaterial.SetInt("_Cull", 0);

            _rightDisplayMaterial = new Material(unlit)
            {
                mainTexture = _fpvRightRenderTexture
            };
            _rightDisplayMaterial.SetInt("_Cull", 0);

            Shader transparentShader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
            _osdOverlayMaterial = new Material(transparentShader);
            _osdOverlayMaterial.SetInt("_Cull", 0);
            _osdOverlayMaterial.renderQueue = 3100;

            EnsureDistortionMesh();
            Settings.EnableOnboardAudio.OnEntryValueChanged.Subscribe(OnOnboardAudioSettingChanged);
            Settings.FpvCameraResolution.OnEntryValueChanged.Subscribe(OnFpvResolutionChanged);
            Settings.SpoutGimbal.OnEntryValueChanged.Subscribe(OnGimbalSettingChanged);
            Settings.SpoutGimbalResolution.OnEntryValueChanged.Subscribe(OnGimbalResolutionChanged);
            Settings.SpoutIncludeOSD.OnEntryValueChanged.Subscribe(OnSpoutOsdSettingChanged);
            Settings.VrView.OnEntryValueChanged.Subscribe(OnVrViewChanged);
            Settings.CockpitVrShowOsd.OnEntryValueChanged.Subscribe(OnCockpitVrShowOsdChanged);
            Settings.LosFlyMode.OnEntryValueChanged.Subscribe(OnLosFlyModeChanged);
            Settings.ShowDroneMeshInFpv.OnEntryValueChanged.Subscribe(OnDroneMeshVisibilityChanged);
            Settings.SpoutShowDroneMesh.OnEntryValueChanged.Subscribe(OnDroneMeshVisibilityChanged);
            Application.onBeforeRender += OnBeforeRenderCockpitVr;
            UpdateCameraCullingMasks();
        }

        private static Camera CreateFpvCameraComponent(GameObject host, float depth)
        {
            var cam = host.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 105.0f; // Wide FPV lens
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1500.0f;
            cam.depth = depth;
            cam.enabled = false;
            // Offscreen drone cameras must stay monocular; leaving StereoTargetEyeMask.Both causes XR RT Y-flip
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            // Exclude UI (5), PlayerClone (9), VR Goggle layers (13, 14), and UIInternal (15)
            cam.cullingMask = ~((1 << 5) | (1 << 9) | (1 << VrLeftGoggleLayer) | (1 << VrRightGoggleLayer) | (1 << 15));
            return cam;
        }

        private void OnOnboardAudioSettingChanged(bool oldValue, bool newValue)
        {
            UpdateCameraActiveState();
        }

        private void OnFpvResolutionChanged(VideoResolution oldValue, VideoResolution newValue)
        {
            Settings.GetResolutionDimensions(newValue, out int w, out int h);
            CreateRenderTexture(w, h);
        }

        private void OnGimbalSettingChanged(SpoutGimbalMode oldValue, SpoutGimbalMode newValue)
        {
            _gimbalRotInitialized = false;
            UpdateCameraActiveState();
        }

        private void OnGimbalResolutionChanged(VideoResolution oldValue, VideoResolution newValue)
        {
            if (IsGimbalActive())
            {
                Settings.GetResolutionDimensions(newValue, out int w, out int h);
                EnsureRenderTexture(ref _gimbalRenderTexture, "FPVDrone_GimbalRenderTexture", w, h, 2, _gimbalCamera);
            }
        }

        private void OnSpoutOsdSettingChanged(bool oldValue, bool newValue)
        {
            _gimbalRotInitialized = false;
            UpdateCameraActiveState();
        }

        private void OnVrViewChanged(VrViewMode oldValue, VrViewMode newValue)
        {
            UpdateCameraActiveState();
        }

        private void OnCockpitVrShowOsdChanged(bool oldValue, bool newValue)
        {
            UpdateCameraActiveState();
        }

        private void OnLosFlyModeChanged(bool oldValue, bool newValue)
        {
            UpdateCameraActiveState();
        }

        private void OnDroneMeshVisibilityChanged(bool oldValue, bool newValue)
        {
            UpdateCameraActiveState();
            UpdateCameraCullingMasks();
        }

        private static int GetCullingMaskForDroneMesh(bool showDrone)
        {
            const int baseMask = ~((1 << 5) | (1 << 9) | (1 << VrLeftGoggleLayer) | (1 << VrRightGoggleLayer) | (1 << 15) | (1 << DroneMeshRenderer.DroneDiscardLayer));
            int droneBit = 1 << DroneMeshRenderer.DroneMeshLayer;
            return showDrone ? baseMask : (baseMask & ~droneBit);
        }

        private void UpdateCameraCullingMasks()
        {
            int fpvMask = GetCullingMaskForDroneMesh(Settings.ShowDroneMeshInFpv.Value);
            if (_fpvCamera != null) _fpvCamera.cullingMask = fpvMask;
            if (_fpvRightCamera != null) _fpvRightCamera.cullingMask = fpvMask;
            if (_vrCockpitCamera != null) _vrCockpitCamera.cullingMask = fpvMask;
            if (_gimbalCamera != null) _gimbalCamera.cullingMask = GetCullingMaskForDroneMesh(Settings.SpoutShowDroneMesh.Value);
        }

        private static void EnsureRenderTexture(ref RenderTexture rt, string rtName, int width, int height, int antiAliasing, Camera targetCam)
        {
            if (rt != null && rt.width == width && rt.height == height)
            {
                if (targetCam != null) targetCam.targetTexture = rt;
                return;
            }

            if (rt != null)
            {
                if (rt.IsCreated()) rt.Release();
                Object.Destroy(rt);
            }

            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = rtName,
                antiAliasing = antiAliasing,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();

            if (targetCam != null)
                targetCam.targetTexture = rt;
        }

        private void CreateRenderTexture(int width, int height)
        {
            EnsureRenderTexture(ref _fpvRenderTexture, "FPVDrone_RenderTexture", width, height, 2, _fpvCamera);

            if (_displayMaterial != null)
                _displayMaterial.mainTexture = _fpvRenderTexture;

            if (_fpvRightCamera != null || _fpvRightRenderTexture != null)
            {
                EnsureRenderTexture(ref _fpvRightRenderTexture, "FPVDrone_RightRenderTexture", width, height, 2, _fpvRightCamera);
                if (_rightDisplayMaterial != null)
                    _rightDisplayMaterial.mainTexture = _fpvRightRenderTexture;
            }
        }

        private bool IsGimbalActive()
        {
            if (!_isSpoutActive || Settings.SpoutIncludeOSD.Value)
                return false;
            return Settings.SpoutGimbal.Value != SpoutGimbalMode.Off ||
                   Settings.SpoutShowDroneMesh.Value != Settings.ShowDroneMeshInFpv.Value;
        }

        private void UpdateGimbalCameraState()
        {
            if (IsGimbalActive())
            {
                if (_gimbalRigObject == null)
                {
                    _gimbalRigObject = new GameObject("FPVDrone_GimbalCameraRig");
                    Object.DontDestroyOnLoad(_gimbalRigObject);
                    _gimbalCamera = CreateFpvCameraComponent(_gimbalRigObject, depth: 9);
                    UpdateCameraCullingMasks();
                }

                Settings.GetResolutionDimensions(Settings.SpoutGimbalResolution.Value, out int gw, out int gh);
                EnsureRenderTexture(ref _gimbalRenderTexture, "FPVDrone_GimbalRenderTexture", gw, gh, 2, _gimbalCamera);
                _gimbalCamera.enabled = true;
            }
            else
            {
                if (_gimbalCamera != null)
                {
                    _gimbalCamera.enabled = false;
                    _gimbalCamera.targetTexture = null;
                }
                if (_gimbalRenderTexture != null)
                {
                    if (_gimbalRenderTexture.IsCreated()) _gimbalRenderTexture.Release();
                    Object.Destroy(_gimbalRenderTexture);
                    _gimbalRenderTexture = null;
                }
                _gimbalRotInitialized = false;
            }
        }

        public void SetFlightActive(bool active)
        {
            _isArmed = active;
            UpdateCameraActiveState();
        }

        public void SetConfigMenuOpen(bool open)
        {
            if (_isConfigMenuOpen == open)
                return;
            _isConfigMenuOpen = open;
            UpdateCameraActiveState();
        }

        public void SetSpoutActive(bool active)
        {
            _isSpoutActive = active;
            UpdateCameraActiveState();
        }

        private bool ShouldShowVrGoggles()
        {
            if (!_isArmed || _isConfigMenuOpen || Settings.LosFlyMode.Value)
                return false;

            bool qmOpen = CVR_MenuManager.Instance != null && CVR_MenuManager.Instance.IsViewShown;
            bool mmOpen = ViewManager.Instance != null && ViewManager.Instance.IsAnyMenuOpen;
            return !qmOpen && !mmOpen;
        }

        private void UpdateCameraActiveState()
        {
            if (_fpvCamera == null)
                return;

            UpdateGimbalCameraState();

            bool isVr = MetaPort.Instance != null && MetaPort.Instance.isUsingVr;
            bool losMode = Settings.LosFlyMode.Value;
            bool isCockpitVr = isVr && Settings.VrView.Value == VrViewMode.CockpitVR3D;
            bool needFpvFeed = _isArmed && !losMode && !isCockpitVr;
            bool gimbalOnly = _isSpoutActive && !needFpvFeed && IsGimbalActive();
            bool shouldRenderFpv = (needFpvFeed || _isSpoutActive) && !gimbalOnly;
            _fpvCamera.enabled = shouldRenderFpv;

            if (isVr)
            {
                UpdateVrDisplayState();
                RestorePlayerAudioListener();
            }
            else
            {
                DisableVrGogglesRig();

                if (!losMode && Settings.EnableOnboardAudio.Value && _isArmed)
                {
                    UpdateOnboardAudioListener(true);
                }
                else
                {
                    RestorePlayerAudioListener();
                }
            }
        }

        private void SetVrWorldCamSuppressed(bool suppress)
        {
            var playerSetup = PlayerSetup.Instance;
            Camera vrCam = playerSetup != null ? playerSetup.vrCam : null;

            if (suppress)
            {
                if (vrCam == null)
                    return;

                if (!_vrWorldCamSuppressed)
                {
                    _savedVrCamCullingMask = vrCam.cullingMask & ~((1 << VrLeftGoggleLayer) | (1 << VrRightGoggleLayer));
                    _savedVrCamClearFlags = vrCam.clearFlags;
                    _vrWorldCamSuppressed = true;
                }

                // Keep vrCam enabled so CVR camera/menu code stays intact while zeroing GPU world render cost.
                vrCam.cullingMask = 0;
                vrCam.clearFlags = CameraClearFlags.Nothing;
            }
            else if (_vrWorldCamSuppressed)
            {
                if (vrCam != null)
                {
                    vrCam.cullingMask = _savedVrCamCullingMask;
                    vrCam.clearFlags = _savedVrCamClearFlags;
                }
                _vrWorldCamSuppressed = false;
            }
        }

        private void DisableVrCockpitRig()
        {
            if (_vrCockpitCamera != null) _vrCockpitCamera.enabled = false;
            if (_vrCockpitOsdCam != null) _vrCockpitOsdCam.enabled = false;
            if (_vrCockpitOsdObject != null) _vrCockpitOsdObject.SetActive(false);
            SetVrWorldCamSuppressed(false);
        }

        private void DisableVrGogglesQuadRig()
        {
            if (_vrBackdropCamera != null) _vrBackdropCamera.enabled = false;
            if (_vrDisplayLeftCam != null) _vrDisplayLeftCam.enabled = false;
            if (_vrDisplayRightCam != null) _vrDisplayRightCam.enabled = false;
            if (_vrGogglesLeftObject != null) _vrGogglesLeftObject.SetActive(false);
            if (_vrGogglesRightObject != null) _vrGogglesRightObject.SetActive(false);
            if (_fpvRightCamera != null) _fpvRightCamera.enabled = false;
        }

        private void DisableVrGogglesRig()
        {
            DisableVrGogglesQuadRig();
            DisableVrCockpitRig();
        }

        private void UpdateVrDisplayState()
        {
            bool showFeed = ShouldShowVrGoggles();
            VrViewMode viewMode = Settings.VrView.Value;

            if (!showFeed)
            {
                DisableVrGogglesRig();
                return;
            }

            if (viewMode == VrViewMode.CockpitVR3D)
            {
                DisableVrGogglesQuadRig();
                EnsureVrCockpitRig();
                SetVrWorldCamSuppressed(true);

                var playerSetup = PlayerSetup.Instance;
                float baseDepth = (playerSetup != null && playerSetup.vrCam != null) ? playerSetup.vrCam.depth : 0.0f;
                float uiDepth = (playerSetup != null && playerSetup.vrUiCam != null && playerSetup.vrUiCam.depth > baseDepth + 0.05f)
                    ? playerSetup.vrUiCam.depth
                    : baseDepth + 2.0f;

                if (playerSetup != null && playerSetup.vrUiCam != null)
                {
                    playerSetup.vrUiCam.cullingMask &= ~((1 << VrLeftGoggleLayer) | (1 << VrRightGoggleLayer));
                }

                if (_vrCockpitCamera != null)
                {
                    _vrCockpitCamera.depth = Mathf.Lerp(baseDepth, uiDepth, 0.40f);
                    _vrCockpitCamera.cullingMask = GetCullingMaskForDroneMesh(Settings.ShowDroneMeshInFpv.Value);
                    if (playerSetup != null && playerSetup.vrCam != null)
                    {
                        _vrCockpitCamera.farClipPlane = playerSetup.vrCam.farClipPlane;
                    }
                    _vrCockpitCamera.enabled = true;
                }

                bool showOsd = Settings.CockpitVrShowOsd.Value;
                if (_vrCockpitOsdCam != null)
                {
                    _vrCockpitOsdCam.depth = Mathf.Lerp(baseDepth, uiDepth, 0.65f);
                    _vrCockpitOsdCam.cullingMask = 1 << VrLeftGoggleLayer;
                    _vrCockpitOsdCam.enabled = showOsd;
                }
                if (_vrCockpitOsdObject != null)
                {
                    _vrCockpitOsdObject.SetActive(showOsd);
                }
                return;
            }

            DisableVrCockpitRig();
            bool stereo = viewMode == VrViewMode.Stereo3D;

            EnsureVrGogglesDisplay();
            if (stereo)
            {
                EnsureVrRightDroneCamera();
            }

            if (_fpvRightCamera != null)
            {
                _fpvRightCamera.enabled = stereo;
            }
            if (_vrBackdropCamera != null)
            {
                _vrBackdropCamera.enabled = true;
            }
            if (_vrDisplayLeftCam != null)
            {
                _vrDisplayLeftCam.enabled = true;
                _vrDisplayLeftCam.stereoTargetEye = stereo ? StereoTargetEyeMask.Left : StereoTargetEyeMask.Both;
                _vrDisplayLeftCam.cullingMask = 1 << VrLeftGoggleLayer;
            }
            if (_vrDisplayRightCam != null)
            {
                _vrDisplayRightCam.enabled = stereo;
                _vrDisplayRightCam.stereoTargetEye = StereoTargetEyeMask.Right;
                _vrDisplayRightCam.cullingMask = 1 << VrRightGoggleLayer;
            }
            if (_vrGogglesLeftObject != null)
            {
                _vrGogglesLeftObject.SetActive(true);
            }
            if (_vrGogglesRightObject != null)
            {
                _vrGogglesRightObject.SetActive(stereo);
            }

            UpdateVrGogglesTransformAndScale();
        }

        public RenderTexture GetSpoutTexture(
            DroneOSD osd,
            DronePhysics physics,
            DroneVTXSimulation vtx,
            DroneInput input,
            bool isArmed,
            bool includeOsd)
        {
            if (!includeOsd && IsGimbalActive() && _gimbalRenderTexture != null)
            {
                return _gimbalRenderTexture;
            }

            if (_fpvRenderTexture == null)
            {
                return null;
            }

            if (!includeOsd || osd == null)
            {
                return _fpvRenderTexture;
            }

            int w = _fpvRenderTexture.width;
            int h = _fpvRenderTexture.height;

            if (_spoutOsdRenderTexture == null || _spoutOsdRenderTexture.width != w || _spoutOsdRenderTexture.height != h)
            {
                if (_spoutOsdRenderTexture != null)
                {
                    if (_spoutOsdRenderTexture.IsCreated()) _spoutOsdRenderTexture.Release();
                    Object.Destroy(_spoutOsdRenderTexture);
                }

                _spoutOsdRenderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
                {
                    name = "FPVDrone_SpoutOsdBuffer",
                    filterMode = FilterMode.Bilinear
                };
                _spoutOsdRenderTexture.Create();
            }

            bool prevSrgb = GL.sRGBWrite;
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(_fpvRenderTexture, _spoutOsdRenderTexture);
            GL.sRGBWrite = prevSrgb;

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = _spoutOsdRenderTexture;

            Matrix4x4 prevMatrix = GUI.matrix;
            float scaleX = (float)w / Mathf.Max(1, Screen.width);
            float scaleY = (float)h / Mathf.Max(1, Screen.height);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1.0f));

            osd.DrawOSD(physics, vtx, input, isArmed, Screen.width, Screen.height, skipStatic: true, skipNotification: true);

            GUI.matrix = prevMatrix;
            RenderTexture.active = prevActive;

            return _spoutOsdRenderTexture;
        }

        private AudioListener ResolvePlayerAudioListener()
        {
            var player = PlayerSetup.Instance;
            if (player != null)
            {
                if (player.activeAudioListener != null && player.activeAudioListener != _fpvAudioListener)
                    return player.activeAudioListener;
                if (player.activeCam != null)
                {
                    var camListener = player.activeCam.GetComponent<AudioListener>();
                    if (camListener != null && camListener != _fpvAudioListener)
                        return camListener;
                }
            }
            return _cachedPlayerListener;
        }

        private void UpdateOnboardAudioListener(bool active)
        {
            if (active)
            {
                AudioListener playerListener = ResolvePlayerAudioListener();
                if (_cachedPlayerListener != null && _cachedPlayerListener != playerListener)
                {
                    _cachedPlayerListener.enabled = true;
                }
                _cachedPlayerListener = playerListener;

                if (_cachedPlayerListener != null && _cachedPlayerListener != _fpvAudioListener)
                {
                    _cachedPlayerListener.enabled = false;
                }

                if (_fpvAudioListener != null)
                {
                    _fpvAudioListener.enabled = true;
                }
            }
            else
            {
                RestorePlayerAudioListener();
            }
        }

        public void RestorePlayerAudioListener()
        {
            if (_fpvAudioListener != null)
            {
                _fpvAudioListener.enabled = false;
            }

            AudioListener playerListener = ResolvePlayerAudioListener();
            if (_cachedPlayerListener != null && _cachedPlayerListener != playerListener)
            {
                _cachedPlayerListener.enabled = true;
            }
            if (playerListener != null)
            {
                playerListener.enabled = true;
            }
            _cachedPlayerListener = null;
        }

        public void UpdateCameraTransform(Vector3 dronePosition, Quaternion droneRotation)
        {
            DroneHardwareSpec hw = DroneBuilder.EvaluateCurrent();
            Vector3 camWorldPos = dronePosition + droneRotation * DroneMeshRenderer.GetFpvCameraLocalOffset(hw);
            float fov = Mathf.Clamp(Settings.FpvCameraFov.Value, 70.0f, 160.0f);
            float nearClip = Mathf.Clamp(hw.BellyHeightM * 0.75f, 0.012f, 0.05f);

            // Drone mechanical camera uptilt angle (negative X in Unity rotates nose up towards sky)
            Quaternion uptilt = Quaternion.Euler(-Settings.CameraTiltAngle.Value, 0.0f, 0.0f);
            Quaternion fpvRot = droneRotation * uptilt;

            bool isVr = MetaPort.Instance != null && MetaPort.Instance.isUsingVr;
            bool stereoVr = isVr && _isArmed && !Settings.LosFlyMode.Value && Settings.VrView.Value == VrViewMode.Stereo3D;
            float halfIpd = stereoVr ? Mathf.Clamp(Settings.VrStereoIpdMm.Value, 40.0f, 85.0f) * 0.0005f : 0.0f;
            Vector3 fpvRight = fpvRot * Vector3.right;

            if (_cameraRigObject != null && _fpvCamera != null && _fpvCamera.enabled)
            {
                _fpvCamera.fieldOfView = fov;
                _fpvCamera.nearClipPlane = nearClip;
                _cameraRigObject.transform.position = camWorldPos - fpvRight * halfIpd;
                _cameraRigObject.transform.rotation = fpvRot;

                EnsureDistortionMesh();
                if (_vrGogglesLeftFilter != null && _vrGogglesLeftFilter.sharedMesh != _distortionMesh)
                    _vrGogglesLeftFilter.sharedMesh = _distortionMesh;
                if (_vrGogglesRightFilter != null && _vrGogglesRightFilter.sharedMesh != _distortionMesh)
                    _vrGogglesRightFilter.sharedMesh = _distortionMesh;
            }

            if (isVr)
            {
                UpdateVrDisplayState();

                if (_vrCockpitRigObject != null && _vrCockpitCamera != null && _vrCockpitCamera.enabled)
                {
                    _vrCockpitCamera.nearClipPlane = nearClip;
                    _vrCockpitRigObject.transform.position = camWorldPos;
                    _vrCockpitRigObject.transform.rotation = fpvRot;
                    SyncCockpitVrHeadRotation();
                }

                if (_fpvRightRigObject != null && _fpvRightCamera != null && _fpvRightCamera.enabled)
                {
                    _fpvRightCamera.fieldOfView = fov;
                    _fpvRightCamera.nearClipPlane = nearClip;
                    _fpvRightRigObject.transform.position = camWorldPos + fpvRight * halfIpd;
                    _fpvRightRigObject.transform.rotation = fpvRot;
                }
            }

            if (IsGimbalActive() && _gimbalRigObject != null && _gimbalCamera != null && _gimbalCamera.enabled)
            {
                _gimbalCamera.fieldOfView = Mathf.Clamp(Settings.SpoutGimbalFov.Value, 70.0f, 160.0f);
                _gimbalCamera.nearClipPlane = nearClip;
                _gimbalRigObject.transform.position = camWorldPos;
                _gimbalRigObject.transform.rotation = ComputeGimbalRotation(droneRotation, fpvRot, Settings.SpoutGimbal.Value, Time.deltaTime);
            }
        }

        private Quaternion ComputeGimbalRotation(Quaternion droneRotation, Quaternion fpvRot, SpoutGimbalMode mode, float dt)
        {
            if (mode == SpoutGimbalMode.Off)
                return fpvRot;

            float smoothSpeed = Mathf.Clamp(Settings.SpoutGimbalSmoothSpeed.Value, 0.5f, 30.0f);
            float alpha = 1.0f - Mathf.Exp(-smoothSpeed * Mathf.Max(0.0f, dt));

            // Full 3D quaternion smoothing (Gyroflow / ReelSteady style, zero Euler flip on loops)
            if (mode == SpoutGimbalMode.ThreeAxisSmooth)
            {
                if (!_gimbalRotInitialized)
                {
                    _gimbalSmoothedRot = fpvRot;
                    _gimbalRotInitialized = true;
                }
                else
                {
                    _gimbalSmoothedRot = Quaternion.Slerp(_gimbalSmoothedRot, fpvRot, alpha);
                }
                return _gimbalSmoothedRot;
            }

            // Horizon-relative decomposition
            Vector3 fwd = fpvRot * Vector3.forward;
            Vector3 flatFwd = Vector3.ProjectOnPlane(fwd, Vector3.up);
            if (flatFwd.sqrMagnitude > 0.01f)
            {
                flatFwd.Normalize();
                _lastValidFlatFwd = flatFwd;
            }
            else
            {
                flatFwd = _lastValidFlatFwd;
            }

            Quaternion yawOnly = Quaternion.LookRotation(flatFwd, Vector3.up);
            Vector3 yawRight = yawOnly * Vector3.right;
            Vector3 yawUp = Vector3.up;

            float rawPitchDeg = -Mathf.Asin(Mathf.Clamp(fwd.y, -0.9999f, 0.9999f)) * Mathf.Rad2Deg;
            Vector3 camRight = fpvRot * Vector3.right;
            float rawRollDeg = Mathf.Atan2(Vector3.Dot(camRight, yawUp), Vector3.Dot(camRight, yawRight)) * Mathf.Rad2Deg;

            GimbalAxisMode rollMode;
            GimbalAxisMode pitchMode;
            GimbalYawMode yawMode;

            switch (mode)
            {
                case SpoutGimbalMode.OneAxisRoll:
                    rollMode = GimbalAxisMode.Lock;
                    pitchMode = GimbalAxisMode.Raw;
                    yawMode = GimbalYawMode.Raw;
                    break;
                case SpoutGimbalMode.TwoAxisHorizon:
                    rollMode = GimbalAxisMode.Lock;
                    pitchMode = GimbalAxisMode.Lock;
                    yawMode = GimbalYawMode.Raw;
                    break;
                case SpoutGimbalMode.Custom:
                default:
                    rollMode = Settings.SpoutGimbalRollMode.Value;
                    pitchMode = Settings.SpoutGimbalPitchMode.Value;
                    yawMode = Settings.SpoutGimbalYawMode.Value;
                    break;
            }

            // If Custom has all 3 axes Smooth, use 3D quaternion Slerp to avoid vertical loop flip
            if (mode == SpoutGimbalMode.Custom && rollMode == GimbalAxisMode.Smooth && pitchMode == GimbalAxisMode.Smooth && yawMode == GimbalYawMode.Smooth)
            {
                if (!_gimbalRotInitialized)
                {
                    _gimbalSmoothedRot = fpvRot;
                    _gimbalRotInitialized = true;
                }
                else
                {
                    _gimbalSmoothedRot = Quaternion.Slerp(_gimbalSmoothedRot, fpvRot, alpha);
                }
                return _gimbalSmoothedRot;
            }

            float targetLockedPitch = -Settings.SpoutGimbalPitchOffset.Value;
            float targetPitch = pitchMode == GimbalAxisMode.Lock ? targetLockedPitch : rawPitchDeg;
            float targetRoll = rollMode == GimbalAxisMode.Lock ? 0.0f : rawRollDeg;

            if (!_gimbalRotInitialized)
            {
                _gimbalSmoothedYaw = yawOnly;
                _gimbalSmoothedPitch = targetPitch;
                _gimbalSmoothedRoll = targetRoll;
                _gimbalSmoothedRot = fpvRot;
                _gimbalRotInitialized = true;
            }
            else
            {
                _gimbalSmoothedYaw = yawMode == GimbalYawMode.Smooth
                    ? Quaternion.Slerp(_gimbalSmoothedYaw, yawOnly, alpha)
                    : yawOnly;

                _gimbalSmoothedPitch = pitchMode == GimbalAxisMode.Smooth
                    ? Mathf.LerpAngle(_gimbalSmoothedPitch, targetPitch, alpha)
                    : targetPitch;

                _gimbalSmoothedRoll = rollMode == GimbalAxisMode.Smooth
                    ? Mathf.LerpAngle(_gimbalSmoothedRoll, targetRoll, alpha)
                    : targetRoll;
            }

            return _gimbalSmoothedYaw * Quaternion.Euler(_gimbalSmoothedPitch, 0.0f, _gimbalSmoothedRoll);
        }

        public void DrawDesktopFpvFeed()
        {
            if (_fpvCamera == null || !_fpvCamera.enabled)
                return;

            Settings.GetResolutionDimensions(Settings.FpvCameraResolution.Value, out int targetW, out int targetH);
            if (_fpvRenderTexture == null || _fpvRenderTexture.width != targetW || _fpvRenderTexture.height != targetH)
            {
                CreateRenderTexture(targetW, targetH);
            }

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _fpvRenderTexture, ScaleMode.StretchToFill, false);
        }

        private void EnsureVrGogglesDisplay()
        {
            if (_vrGogglesLeftObject != null && _vrBackdropCamera != null && _vrDisplayLeftCam != null)
                return;

            var playerSetup = PlayerSetup.Instance;
            if (playerSetup == null || playerSetup.vrCamera == null)
                return;

            Transform vrHead = playerSetup.vrCamera.transform;

            if (playerSetup.vrCam != null)
            {
                playerSetup.vrCam.cullingMask &= ~((1 << VrLeftGoggleLayer) | (1 << VrRightGoggleLayer));
            }
            if (playerSetup.vrUiCam != null)
            {
                playerSetup.vrUiCam.cullingMask &= ~((1 << VrLeftGoggleLayer) | (1 << VrRightGoggleLayer));
            }

            if (_vrBackdropCamera == null)
            {
                var bgObj = new GameObject("FPV_VR_BackdropCam");
                bgObj.transform.SetParent(vrHead, false);
                _vrBackdropCamera = bgObj.AddComponent<Camera>();
                _vrBackdropCamera.clearFlags = CameraClearFlags.SolidColor;
                _vrBackdropCamera.backgroundColor = Color.black;
                _vrBackdropCamera.cullingMask = 0;
                _vrBackdropCamera.stereoTargetEye = StereoTargetEyeMask.Both;
                _vrBackdropCamera.depthTextureMode = DepthTextureMode.None;
                _vrBackdropCamera.nearClipPlane = 0.05f;
                _vrBackdropCamera.farClipPlane = 2.0f;
                _vrBackdropCamera.allowHDR = false;
                _vrBackdropCamera.allowMSAA = false;
                _vrBackdropCamera.useOcclusionCulling = false;
                _vrBackdropCamera.enabled = false;
            }

            if (_vrDisplayLeftCam == null)
            {
                var leftCamObj = new GameObject("FPV_VR_DisplayCam_L");
                leftCamObj.transform.SetParent(vrHead, false);
                _vrDisplayLeftCam = leftCamObj.AddComponent<Camera>();
                _vrDisplayLeftCam.clearFlags = CameraClearFlags.Depth;
                _vrDisplayLeftCam.cullingMask = 1 << VrLeftGoggleLayer;
                _vrDisplayLeftCam.stereoTargetEye = StereoTargetEyeMask.Both;
                _vrDisplayLeftCam.depthTextureMode = DepthTextureMode.None;
                _vrDisplayLeftCam.nearClipPlane = 0.05f;
                _vrDisplayLeftCam.farClipPlane = 5.0f;
                _vrDisplayLeftCam.allowHDR = false;
                _vrDisplayLeftCam.allowMSAA = false;
                _vrDisplayLeftCam.useOcclusionCulling = false;
                _vrDisplayLeftCam.enabled = false;
            }

            if (_vrDisplayRightCam == null)
            {
                var rightCamObj = new GameObject("FPV_VR_DisplayCam_R");
                rightCamObj.transform.SetParent(vrHead, false);
                _vrDisplayRightCam = rightCamObj.AddComponent<Camera>();
                _vrDisplayRightCam.clearFlags = CameraClearFlags.Depth;
                _vrDisplayRightCam.cullingMask = 1 << VrRightGoggleLayer;
                _vrDisplayRightCam.stereoTargetEye = StereoTargetEyeMask.Right;
                _vrDisplayRightCam.depthTextureMode = DepthTextureMode.None;
                _vrDisplayRightCam.nearClipPlane = 0.05f;
                _vrDisplayRightCam.farClipPlane = 5.0f;
                _vrDisplayRightCam.allowHDR = false;
                _vrDisplayRightCam.allowMSAA = false;
                _vrDisplayRightCam.useOcclusionCulling = false;
                _vrDisplayRightCam.enabled = false;
            }

            EnsureDistortionMesh();

            if (_vrGogglesLeftObject == null)
            {
                _vrGogglesLeftObject = CreateGoggleQuad(
                    "FPV_VR_Goggles_Display_L",
                    vrHead,
                    VrLeftGoggleLayer,
                    _displayMaterial,
                    _distortionMesh,
                    out _vrGogglesLeftFilter,
                    out _vrLeftOsdObject);
            }

            if (_vrGogglesRightObject == null)
            {
                _vrGogglesRightObject = CreateGoggleQuad(
                    "FPV_VR_Goggles_Display_R",
                    vrHead,
                    VrRightGoggleLayer,
                    _rightDisplayMaterial,
                    _distortionMesh,
                    out _vrGogglesRightFilter,
                    out _vrRightOsdObject);
            }

            UpdateVrGogglesTransformAndScale();
        }

        private GameObject CreateGoggleQuad(
            string name,
            Transform parent,
            int layer,
            Material videoMat,
            Mesh mesh,
            out MeshFilter filter,
            out GameObject osdOverlayObj)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.layer = layer;

            var col = quad.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);

            filter = quad.GetComponent<MeshFilter>();
            if (filter != null && mesh != null)
                filter.sharedMesh = mesh;

            var mr = quad.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.material = videoMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.allowOcclusionWhenDynamic = false;
            }

            osdOverlayObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            osdOverlayObj.name = name + "_OSD";
            osdOverlayObj.transform.SetParent(quad.transform, false);
            osdOverlayObj.transform.localPosition = new Vector3(0.0f, 0.0f, -0.002f);
            osdOverlayObj.transform.localRotation = Quaternion.identity;
            osdOverlayObj.transform.localScale = Vector3.one;
            osdOverlayObj.layer = layer;

            var osdCol = osdOverlayObj.GetComponent<Collider>();
            if (osdCol != null)
                Object.Destroy(osdCol);

            var osdMr = osdOverlayObj.GetComponent<MeshRenderer>();
            if (osdMr != null)
            {
                osdMr.material = _osdOverlayMaterial;
                osdMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                osdMr.receiveShadows = false;
                osdMr.allowOcclusionWhenDynamic = false;
            }

            quad.SetActive(false);
            return quad;
        }

        private void UpdateVrGogglesTransformAndScale()
        {
            var playerSetup = PlayerSetup.Instance;
            if (playerSetup == null || playerSetup.vrCamera == null)
                return;

            int goggleMaskExclude = ~((1 << VrLeftGoggleLayer) | (1 << VrRightGoggleLayer));
            if (playerSetup.vrCam != null)
                playerSetup.vrCam.cullingMask &= goggleMaskExclude;
            if (playerSetup.vrUiCam != null)
                playerSetup.vrUiCam.cullingMask &= goggleMaskExclude;

            float baseDepth = playerSetup.vrCam != null ? playerSetup.vrCam.depth : 0.0f;
            float uiDepth = (playerSetup.vrUiCam != null && playerSetup.vrUiCam.depth > baseDepth + 0.05f)
                ? playerSetup.vrUiCam.depth
                : baseDepth + 2.0f;

            if (_vrBackdropCamera != null)
                _vrBackdropCamera.depth = Mathf.Lerp(baseDepth, uiDepth, 0.25f);
            if (_vrDisplayLeftCam != null)
            {
                _vrDisplayLeftCam.depth = Mathf.Lerp(baseDepth, uiDepth, 0.50f);
                if (playerSetup.vrCam != null)
                    _vrDisplayLeftCam.fieldOfView = playerSetup.vrCam.fieldOfView;
            }
            if (_vrDisplayRightCam != null)
            {
                _vrDisplayRightCam.depth = Mathf.Lerp(baseDepth, uiDepth, 0.50f);
                if (playerSetup.vrCam != null)
                    _vrDisplayRightCam.fieldOfView = playerSetup.vrCam.fieldOfView;
            }

            // Compensate for CVR avatar height scaling (_avatarScaleRelation) on vrCameraRig
            float headScale = Mathf.Max(0.05f, playerSetup.vrCamera.transform.lossyScale.y);
            const float worldDist = 0.80f;
            float goggleFovDeg = Mathf.Clamp(Settings.VrGoggleScreenFov.Value, 35.0f, 110.0f);
            float worldWidth = 2.0f * worldDist * Mathf.Tan(goggleFovDeg * 0.5f * Mathf.Deg2Rad);
            float aspect = (_fpvRenderTexture != null && _fpvRenderTexture.height > 0)
                ? (float)_fpvRenderTexture.width / _fpvRenderTexture.height
                : (16.0f / 9.0f);
            float worldHeight = worldWidth / aspect;

            Vector3 localPos = new Vector3(0.0f, 0.0f, worldDist / headScale);
            Vector3 localScale = new Vector3(worldWidth / headScale, worldHeight / headScale, 1.0f / headScale);

            if (_vrGogglesLeftObject != null)
            {
                _vrGogglesLeftObject.transform.localPosition = localPos;
                _vrGogglesLeftObject.transform.localRotation = Quaternion.identity;
                _vrGogglesLeftObject.transform.localScale = localScale;
            }
            if (_vrGogglesRightObject != null)
            {
                _vrGogglesRightObject.transform.localPosition = localPos;
                _vrGogglesRightObject.transform.localRotation = Quaternion.identity;
                _vrGogglesRightObject.transform.localScale = localScale;
            }
        }

        private void EnsureVrCockpitRig()
        {
            if (_vrCockpitRigObject != null && _vrCockpitCamera != null && _vrCockpitOsdCam != null && _vrCockpitOsdObject != null)
                return;

            var playerSetup = PlayerSetup.Instance;

            if (_vrCockpitRigObject == null)
            {
                _vrCockpitRigObject = new GameObject("FPVDrone_VrCockpitRig");
                Object.DontDestroyOnLoad(_vrCockpitRigObject);
            }

            if (_vrCockpitCamObject == null)
            {
                _vrCockpitCamObject = new GameObject("FPVDrone_VrCockpitHead");
                _vrCockpitCamObject.transform.SetParent(_vrCockpitRigObject.transform, false);
                _vrCockpitCamObject.transform.localPosition = Vector3.zero;
                _vrCockpitCamObject.transform.localRotation = Quaternion.identity;
            }

            if (_vrCockpitCamera == null)
            {
                _vrCockpitCamera = _vrCockpitCamObject.AddComponent<Camera>();
                _vrCockpitCamera.clearFlags = CameraClearFlags.Skybox;
                _vrCockpitCamera.nearClipPlane = 0.02f;
                _vrCockpitCamera.farClipPlane = (playerSetup != null && playerSetup.vrCam != null) ? playerSetup.vrCam.farClipPlane : 1500.0f;
                _vrCockpitCamera.stereoTargetEye = StereoTargetEyeMask.Both;
                _vrCockpitCamera.allowHDR = playerSetup != null && playerSetup.vrCam != null && playerSetup.vrCam.allowHDR;
                _vrCockpitCamera.allowMSAA = playerSetup == null || playerSetup.vrCam == null || playerSetup.vrCam.allowMSAA;
                _vrCockpitCamera.cullingMask = GetCullingMaskForDroneMesh(Settings.ShowDroneMeshInFpv.Value);
                _vrCockpitCamera.enabled = false;
            }

            if (_vrCockpitOsdCam == null)
            {
                var osdCamObj = new GameObject("FPVDrone_VrCockpitOsdCam");
                osdCamObj.transform.SetParent(_vrCockpitCamObject.transform, false);
                osdCamObj.transform.localPosition = Vector3.zero;
                osdCamObj.transform.localRotation = Quaternion.identity;

                _vrCockpitOsdCam = osdCamObj.AddComponent<Camera>();
                _vrCockpitOsdCam.clearFlags = CameraClearFlags.Depth;
                _vrCockpitOsdCam.cullingMask = 1 << VrLeftGoggleLayer;
                _vrCockpitOsdCam.stereoTargetEye = StereoTargetEyeMask.Both;
                _vrCockpitOsdCam.depthTextureMode = DepthTextureMode.None;
                _vrCockpitOsdCam.nearClipPlane = 0.05f;
                _vrCockpitOsdCam.farClipPlane = 5.0f;
                _vrCockpitOsdCam.allowHDR = false;
                _vrCockpitOsdCam.allowMSAA = false;
                _vrCockpitOsdCam.useOcclusionCulling = false;
                _vrCockpitOsdCam.enabled = false;
            }

            if (_vrCockpitOsdObject == null)
            {
                _vrCockpitOsdObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _vrCockpitOsdObject.name = "FPVDrone_VrCockpit_OSD";
                _vrCockpitOsdObject.transform.SetParent(_vrCockpitCamObject.transform, false);
                _vrCockpitOsdObject.transform.localPosition = new Vector3(0.0f, 0.0f, 1.35f);
                _vrCockpitOsdObject.transform.localRotation = Quaternion.identity;
                _vrCockpitOsdObject.transform.localScale = new Vector3(1.60f, 0.90f, 1.0f);
                _vrCockpitOsdObject.layer = VrLeftGoggleLayer;

                var osdCol = _vrCockpitOsdObject.GetComponent<Collider>();
                if (osdCol != null)
                    Object.Destroy(osdCol);

                var osdMr = _vrCockpitOsdObject.GetComponent<MeshRenderer>();
                if (osdMr != null)
                {
                    osdMr.material = _osdOverlayMaterial;
                    osdMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    osdMr.receiveShadows = false;
                    osdMr.allowOcclusionWhenDynamic = false;
                }

                _vrCockpitOsdObject.SetActive(false);
            }
        }

        private void SyncCockpitVrHeadRotation()
        {
            if (_vrCockpitCamObject == null)
                return;

            var playerSetup = PlayerSetup.Instance;
            if (playerSetup == null || playerSetup.vrCamera == null)
                return;

            _vrCockpitCamObject.transform.localPosition = Vector3.zero;
            _vrCockpitCamObject.transform.localRotation = playerSetup.vrCamera.transform.localRotation;
        }

        private void OnBeforeRenderCockpitVr()
        {
            if (_vrCockpitCamera == null || !_vrCockpitCamera.enabled)
                return;

            SyncCockpitVrHeadRotation();
        }

        private void EnsureVrRightDroneCamera()
        {
            if (_fpvRightCamera != null)
                return;

            _fpvRightRigObject = new GameObject("FPVDrone_RightEyeCameraRig");
            Object.DontDestroyOnLoad(_fpvRightRigObject);

            _fpvRightCamera = CreateFpvCameraComponent(_fpvRightRigObject, depth: 10);
            UpdateCameraCullingMasks();

            int rw = _fpvRenderTexture != null ? _fpvRenderTexture.width : 1280;
            int rh = _fpvRenderTexture != null ? _fpvRenderTexture.height : 720;
            EnsureRenderTexture(ref _fpvRightRenderTexture, "FPVDrone_RightRenderTexture", rw, rh, 2, _fpvRightCamera);

            if (_rightDisplayMaterial != null)
                _rightDisplayMaterial.mainTexture = _fpvRightRenderTexture;
        }

        public void PrepareVrGogglesOsd(DroneOSD osd, DronePhysics physics, DroneVTXSimulation vtx, DroneInput input)
        {
            if (osd == null || !ShouldShowVrGoggles())
                return;

            bool isCockpitVr = Settings.VrView.Value == VrViewMode.CockpitVR3D;
            if (isCockpitVr && !Settings.CockpitVrShowOsd.Value)
                return;

            const int osdW = 1280;
            const int osdH = 720;

            if (_vrOsdRenderTexture == null || _vrOsdRenderTexture.width != osdW || _vrOsdRenderTexture.height != osdH)
            {
                if (_vrOsdRenderTexture != null)
                {
                    if (_vrOsdRenderTexture.IsCreated()) _vrOsdRenderTexture.Release();
                    Object.Destroy(_vrOsdRenderTexture);
                }

                _vrOsdRenderTexture = new RenderTexture(osdW, osdH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "FPVDrone_VrOsdOverlay",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _vrOsdRenderTexture.Create();
            }

            if (_osdOverlayMaterial != null && _osdOverlayMaterial.mainTexture != _vrOsdRenderTexture)
                _osdOverlayMaterial.mainTexture = _vrOsdRenderTexture;

            RenderTexture prevActive = RenderTexture.active;
            Matrix4x4 prevMatrix = GUI.matrix;
            bool prevSrgb = GL.sRGBWrite;

            RenderTexture.active = _vrOsdRenderTexture;
            GL.Clear(true, true, Color.clear);
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            GUI.matrix = Matrix4x4.identity;

            osd.DrawOSD(physics, vtx, input, _isArmed, osdW, osdH, skipStatic: isCockpitVr, skipNotification: false);

            GUI.matrix = prevMatrix;
            GL.sRGBWrite = prevSrgb;
            RenderTexture.active = prevActive;
        }

        private void EnsureDistortionMesh()
        {
            bool enabled = Settings.EnableLensDistortion.Value;
            float k = enabled ? Settings.LensDistortionK.Value : 0.0f;

            if (_distortionMesh != null && Mathf.Abs(_cachedDistortionK - k) < 0.001f && _cachedDistortionEnabled == enabled)
                return;

            _cachedDistortionK = k;
            _cachedDistortionEnabled = enabled;

            if (_distortionMesh == null)
            {
                _distortionMesh = new Mesh { name = "FPV_Distortion_Grid" };
            }

            const int cols = 24;
            const int rows = 24;
            int vertexCount = (cols + 1) * (rows + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[cols * rows * 6];

            float normDMax = 1.0f + k * 2.0f;

            int vertIdx = 0;
            for (int r = 0; r <= rows; r++)
            {
                float fy = (float)r / rows;
                float cy = (fy - 0.5f) * 2.0f;

                for (int c = 0; c <= cols; c++)
                {
                    float fx = (float)c / cols;
                    float cx = (fx - 0.5f) * 2.0f;

                    vertices[vertIdx] = new Vector3(fx - 0.5f, fy - 0.5f, 0.0f);

                    float rSq = cx * cx + cy * cy;
                    float distScale = (1.0f + k * rSq) / normDMax;
                    float uDist = 0.5f + (cx * distScale) * 0.5f;
                    float vDist = 0.5f + (cy * distScale) * 0.5f;

                    uvs[vertIdx] = new Vector2(uDist, vDist);
                    vertIdx++;
                }
            }

            int triIdx = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int i0 = r * (cols + 1) + c;
                    int i1 = i0 + 1;
                    int i2 = (r + 1) * (cols + 1) + c;
                    int i3 = i2 + 1;

                    triangles[triIdx++] = i0;
                    triangles[triIdx++] = i2;
                    triangles[triIdx++] = i1;

                    triangles[triIdx++] = i1;
                    triangles[triIdx++] = i2;
                    triangles[triIdx++] = i3;
                }
            }

            _distortionMesh.Clear();
            _distortionMesh.vertices = vertices;
            _distortionMesh.uv = uvs;
            _distortionMesh.triangles = triangles;
            _distortionMesh.RecalculateBounds();
        }

        public void Destroy()
        {
            Application.onBeforeRender -= OnBeforeRenderCockpitVr;
            Settings.EnableOnboardAudio.OnEntryValueChanged.Unsubscribe(OnOnboardAudioSettingChanged);
            Settings.FpvCameraResolution.OnEntryValueChanged.Unsubscribe(OnFpvResolutionChanged);
            Settings.SpoutGimbal.OnEntryValueChanged.Unsubscribe(OnGimbalSettingChanged);
            Settings.SpoutGimbalResolution.OnEntryValueChanged.Unsubscribe(OnGimbalResolutionChanged);
            Settings.SpoutIncludeOSD.OnEntryValueChanged.Unsubscribe(OnSpoutOsdSettingChanged);
            Settings.VrView.OnEntryValueChanged.Unsubscribe(OnVrViewChanged);
            Settings.CockpitVrShowOsd.OnEntryValueChanged.Unsubscribe(OnCockpitVrShowOsdChanged);
            Settings.LosFlyMode.OnEntryValueChanged.Unsubscribe(OnLosFlyModeChanged);
            Settings.ShowDroneMeshInFpv.OnEntryValueChanged.Unsubscribe(OnDroneMeshVisibilityChanged);
            Settings.SpoutShowDroneMesh.OnEntryValueChanged.Unsubscribe(OnDroneMeshVisibilityChanged);
            SetVrWorldCamSuppressed(false);
            RestorePlayerAudioListener();
            if (_vrCockpitRigObject != null)
            {
                Object.Destroy(_vrCockpitRigObject);
                _vrCockpitRigObject = null;
                _vrCockpitCamObject = null;
                _vrCockpitCamera = null;
                _vrCockpitOsdCam = null;
                _vrCockpitOsdObject = null;
            }
            if (_cameraRigObject != null)
            {
                Object.Destroy(_cameraRigObject);
                _cameraRigObject = null;
            }
            if (_fpvRightRigObject != null)
            {
                Object.Destroy(_fpvRightRigObject);
                _fpvRightRigObject = null;
                _fpvRightCamera = null;
            }
            if (_gimbalRigObject != null)
            {
                Object.Destroy(_gimbalRigObject);
                _gimbalRigObject = null;
                _gimbalCamera = null;
            }
            if (_vrBackdropCamera != null)
            {
                Object.Destroy(_vrBackdropCamera.gameObject);
                _vrBackdropCamera = null;
            }
            if (_vrDisplayLeftCam != null)
            {
                Object.Destroy(_vrDisplayLeftCam.gameObject);
                _vrDisplayLeftCam = null;
            }
            if (_vrDisplayRightCam != null)
            {
                Object.Destroy(_vrDisplayRightCam.gameObject);
                _vrDisplayRightCam = null;
            }
            if (_vrGogglesLeftObject != null)
            {
                Object.Destroy(_vrGogglesLeftObject);
                _vrGogglesLeftObject = null;
                _vrLeftOsdObject = null;
            }
            if (_vrGogglesRightObject != null)
            {
                Object.Destroy(_vrGogglesRightObject);
                _vrGogglesRightObject = null;
                _vrRightOsdObject = null;
            }
            if (_displayMaterial != null)
            {
                Object.Destroy(_displayMaterial);
                _displayMaterial = null;
            }
            if (_rightDisplayMaterial != null)
            {
                Object.Destroy(_rightDisplayMaterial);
                _rightDisplayMaterial = null;
            }
            if (_osdOverlayMaterial != null)
            {
                Object.Destroy(_osdOverlayMaterial);
                _osdOverlayMaterial = null;
            }
            if (_distortionMesh != null)
            {
                Object.Destroy(_distortionMesh);
                _distortionMesh = null;
            }
            if (_fpvRenderTexture != null)
            {
                if (_fpvRenderTexture.IsCreated())
                    _fpvRenderTexture.Release();
                Object.Destroy(_fpvRenderTexture);
                _fpvRenderTexture = null;
            }
            if (_fpvRightRenderTexture != null)
            {
                if (_fpvRightRenderTexture.IsCreated())
                    _fpvRightRenderTexture.Release();
                Object.Destroy(_fpvRightRenderTexture);
                _fpvRightRenderTexture = null;
            }
            if (_vrOsdRenderTexture != null)
            {
                if (_vrOsdRenderTexture.IsCreated())
                    _vrOsdRenderTexture.Release();
                Object.Destroy(_vrOsdRenderTexture);
                _vrOsdRenderTexture = null;
            }
            if (_gimbalRenderTexture != null)
            {
                if (_gimbalRenderTexture.IsCreated())
                    _gimbalRenderTexture.Release();
                Object.Destroy(_gimbalRenderTexture);
                _gimbalRenderTexture = null;
            }
            if (_spoutOsdRenderTexture != null)
            {
                if (_spoutOsdRenderTexture.IsCreated())
                    _spoutOsdRenderTexture.Release();
                Object.Destroy(_spoutOsdRenderTexture);
                _spoutOsdRenderTexture = null;
            }
        }
    }
}
