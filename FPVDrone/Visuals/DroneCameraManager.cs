using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using UnityEngine;

namespace FPVDrone
{
    public class DroneCameraManager
    {
        private GameObject _cameraRigObject;
        private Camera _fpvCamera;
        private AudioListener _fpvAudioListener;
        private AudioListener _cachedPlayerListener;
        private RenderTexture _fpvRenderTexture;
        private GameObject _vrGogglesObject;
        private MeshRenderer _vrGogglesRenderer;
        private MeshFilter _vrGogglesFilter;
        private GameObject _vrStereoRigObject;
        private Camera _vrStereoCamera;
        private Material _displayMaterial;
        private Mesh _distortionMesh;

        private float _cachedDistortionK = -999.0f;
        private bool _cachedDistortionEnabled = false;

        private bool _isArmed = false;
        private bool _isSpoutActive = false;
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

            EnsureDistortionMesh();
            Settings.EnableOnboardAudio.OnEntryValueChanged.Subscribe(OnOnboardAudioSettingChanged);
            Settings.FpvCameraResolution.OnEntryValueChanged.Subscribe(OnFpvResolutionChanged);
            Settings.SpoutGimbal.OnEntryValueChanged.Subscribe(OnGimbalSettingChanged);
            Settings.SpoutGimbalResolution.OnEntryValueChanged.Subscribe(OnGimbalResolutionChanged);
            Settings.SpoutIncludeOSD.OnEntryValueChanged.Subscribe(OnSpoutOsdSettingChanged);
            Settings.VrView.OnEntryValueChanged.Subscribe(OnVrViewChanged);
            Settings.ShowDroneMeshInFpv.OnEntryValueChanged.Subscribe(OnDroneMeshVisibilityChanged);
            Settings.SpoutShowDroneMesh.OnEntryValueChanged.Subscribe(OnDroneMeshVisibilityChanged);
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
            // Exclude UI (5), PlayerClone (9), and UIInternal (15) layers
            cam.cullingMask = ~((1 << 5) | (1 << 9) | (1 << 15));
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

        private void OnDroneMeshVisibilityChanged(bool oldValue, bool newValue)
        {
            UpdateCameraActiveState();
            UpdateCameraCullingMasks();
        }

        private static int GetCullingMaskForDroneMesh(bool showDrone)
        {
            const int baseMask = ~((1 << 5) | (1 << 9) | (1 << 15) | (1 << DroneMeshRenderer.DroneDiscardLayer));
            int droneBit = 1 << DroneMeshRenderer.DroneMeshLayer;
            return showDrone ? baseMask : (baseMask & ~droneBit);
        }

        private void UpdateCameraCullingMasks()
        {
            int fpvMask = GetCullingMaskForDroneMesh(Settings.ShowDroneMeshInFpv.Value);
            if (_fpvCamera != null) _fpvCamera.cullingMask = fpvMask;
            if (_vrStereoCamera != null) _vrStereoCamera.cullingMask = fpvMask;
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
                filterMode = FilterMode.Bilinear
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

        public void SetSpoutActive(bool active)
        {
            _isSpoutActive = active;
            UpdateCameraActiveState();
        }

        private void UpdateCameraActiveState()
        {
            if (_fpvCamera == null)
                return;

            UpdateGimbalCameraState();

            bool gimbalOnly = _isSpoutActive && !_isArmed && IsGimbalActive();
            bool shouldRenderFpv = (_isArmed || _isSpoutActive) && !gimbalOnly;
            _fpvCamera.enabled = shouldRenderFpv;

            bool isVr = MetaPort.Instance != null && MetaPort.Instance.isUsingVr;
            if (isVr)
            {
                UpdateVrDisplayState();
                RestorePlayerAudioListener();
            }
            else
            {
                if (_vrGogglesObject != null)
                {
                    _vrGogglesObject.SetActive(false);
                }
                if (_vrStereoCamera != null)
                {
                    _vrStereoCamera.enabled = false;
                }

                if (Settings.EnableOnboardAudio.Value && _isArmed)
                {
                    UpdateOnboardAudioListener(true);
                }
                else
                {
                    RestorePlayerAudioListener();
                }
            }
        }

        private void UpdateVrDisplayState()
        {
            bool qmOpen = CVR_MenuManager.Instance != null && CVR_MenuManager.Instance.IsViewShown;
            bool showFeed = _isArmed && !qmOpen;
            bool stereo = Settings.VrView.Value == VrViewMode.Stereo3D;

            if (showFeed && !stereo)
            {
                EnsureVrGogglesDisplay();
            }
            if (_vrGogglesObject != null)
            {
                _vrGogglesObject.SetActive(showFeed && !stereo);
            }

            if (showFeed && stereo)
            {
                EnsureVrStereoCamera();
            }
            if (_vrStereoCamera != null)
            {
                _vrStereoCamera.enabled = showFeed && stereo;
            }
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

            osd.DrawOSD(physics, vtx, input, isArmed, Screen.width, Screen.height, skipStatic: true);

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

            if (_cameraRigObject != null && _fpvCamera != null && _fpvCamera.enabled)
            {
                _fpvCamera.fieldOfView = fov;
                _fpvCamera.nearClipPlane = nearClip;
                _cameraRigObject.transform.position = camWorldPos;
                _cameraRigObject.transform.rotation = fpvRot;

                EnsureDistortionMesh();
                if (_vrGogglesFilter != null && _vrGogglesFilter.sharedMesh != _distortionMesh)
                {
                    _vrGogglesFilter.sharedMesh = _distortionMesh;
                }
            }

            if (MetaPort.Instance != null && MetaPort.Instance.isUsingVr)
            {
                UpdateVrDisplayState();

                if (_vrStereoRigObject != null && _vrStereoCamera != null && _vrStereoCamera.enabled)
                {
                    _vrStereoCamera.fieldOfView = fov;
                    _vrStereoCamera.nearClipPlane = nearClip;
                    var vrCam = PlayerSetup.Instance?.vrCamera;
                    if (vrCam != null)
                    {
                        Transform vrCamT = vrCam.transform;
                        Quaternion invHeadRot = Quaternion.Inverse(vrCamT.localRotation);
                        _vrStereoRigObject.transform.rotation = fpvRot * invHeadRot;
                        _vrStereoRigObject.transform.position = camWorldPos - (_vrStereoRigObject.transform.rotation * vrCamT.localPosition);
                    }
                    else
                    {
                        _vrStereoRigObject.transform.position = camWorldPos;
                        _vrStereoRigObject.transform.rotation = fpvRot;
                    }
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
            if (_vrGogglesObject != null)
                return;

            var playerSetup = PlayerSetup.Instance;
            if (playerSetup == null || playerSetup.vrCamera == null)
                return;

            Transform vrHead = playerSetup.vrCamera.transform;

            _vrGogglesObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _vrGogglesObject.name = "FPV_VR_Goggles_Display";
            _vrGogglesObject.transform.SetParent(vrHead, false);

            // Anchor in front of eyes: 0.55m distance, 16:9 aspect ratio (Quad front face already faces -Z toward camera)
            _vrGogglesObject.transform.localPosition = new Vector3(0.0f, 0.0f, 0.55f);
            _vrGogglesObject.transform.localRotation = Quaternion.identity;
            _vrGogglesObject.transform.localScale = new Vector3(0.85f, 0.48f, 1.0f);
            // UIInternal (15): rendered by vrCamera, excluded from _fpvCamera and world mirrors
            _vrGogglesObject.layer = 15;

            var collider = _vrGogglesObject.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            EnsureDistortionMesh();
            _vrGogglesFilter = _vrGogglesObject.GetComponent<MeshFilter>();
            if (_vrGogglesFilter != null)
            {
                _vrGogglesFilter.sharedMesh = _distortionMesh;
            }

            _vrGogglesRenderer = _vrGogglesObject.GetComponent<MeshRenderer>();
            _vrGogglesRenderer.material = _displayMaterial;
            _vrGogglesObject.SetActive(false);
        }

        private void EnsureVrStereoCamera()
        {
            if (_vrStereoCamera != null)
                return;

            _vrStereoRigObject = new GameObject("FPVDrone_VRStereoRig");
            Object.DontDestroyOnLoad(_vrStereoRigObject);

            var camHost = new GameObject("FPVDrone_VRStereoCam");
            camHost.transform.SetParent(_vrStereoRigObject.transform, false);

            _vrStereoCamera = CreateFpvCameraComponent(camHost, depth: 10);
            UpdateCameraCullingMasks();
            _vrStereoCamera.targetTexture = null;
            _vrStereoCamera.stereoTargetEye = StereoTargetEyeMask.Both;
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
            Settings.EnableOnboardAudio.OnEntryValueChanged.Unsubscribe(OnOnboardAudioSettingChanged);
            Settings.FpvCameraResolution.OnEntryValueChanged.Unsubscribe(OnFpvResolutionChanged);
            Settings.SpoutGimbal.OnEntryValueChanged.Unsubscribe(OnGimbalSettingChanged);
            Settings.SpoutGimbalResolution.OnEntryValueChanged.Unsubscribe(OnGimbalResolutionChanged);
            Settings.SpoutIncludeOSD.OnEntryValueChanged.Unsubscribe(OnSpoutOsdSettingChanged);
            Settings.VrView.OnEntryValueChanged.Unsubscribe(OnVrViewChanged);
            Settings.ShowDroneMeshInFpv.OnEntryValueChanged.Unsubscribe(OnDroneMeshVisibilityChanged);
            Settings.SpoutShowDroneMesh.OnEntryValueChanged.Unsubscribe(OnDroneMeshVisibilityChanged);
            RestorePlayerAudioListener();
            if (_cameraRigObject != null)
            {
                Object.Destroy(_cameraRigObject);
                _cameraRigObject = null;
            }
            if (_vrStereoRigObject != null)
            {
                Object.Destroy(_vrStereoRigObject);
                _vrStereoRigObject = null;
                _vrStereoCamera = null;
            }
            if (_gimbalRigObject != null)
            {
                Object.Destroy(_gimbalRigObject);
                _gimbalRigObject = null;
                _gimbalCamera = null;
            }
            if (_vrGogglesObject != null)
            {
                Object.Destroy(_vrGogglesObject);
                _vrGogglesObject = null;
            }
            if (_displayMaterial != null)
            {
                Object.Destroy(_displayMaterial);
                _displayMaterial = null;
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
