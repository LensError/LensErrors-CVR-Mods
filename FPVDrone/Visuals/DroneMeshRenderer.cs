using UnityEngine;

namespace FPVDrone
{
    public class DroneMeshRenderer
    {
        public const int DroneMeshLayer = 30;

        private GameObject _root;
        private readonly Transform[] _propHubs = new Transform[4];
        private readonly float[] _propAngles = new float[4];

        private Material _carbonMat;
        private Material _metalMat;
        private Material _lipoMat;
        private Material _propMat;
        private Material _propDiscMat;
        private Material _ductMat;

        private float _cachedWb = -1f;
        private float _cachedProp = -1f;
        private int _cachedBlades = -1;
        private float _cachedStator = -1f;
        private bool _cachedDucted = false;
        private float _cachedCamHeightMm = float.NaN;
        private float _cachedCamFwdMm = float.NaN;

        public static Vector3 GetFpvCameraLocalOffset(DroneHardwareSpec hw)
        {
            return new Vector3(
                0.0f,
                Settings.CameraHeightOffsetMm.Value * 0.001f,
                Settings.CameraForwardOffsetMm.Value * 0.001f);
        }

        public void Initialize()
        {
            if (_root != null)
                return;

            _root = new GameObject("FPVDrone_VisualMesh");
            _root.layer = DroneMeshLayer;
            Object.DontDestroyOnLoad(_root);

            Shader std = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            _carbonMat = new Material(std) { color = new Color(0.11f, 0.12f, 0.14f, 1.0f) };
            _metalMat = new Material(std) { color = new Color(0.70f, 0.74f, 0.78f, 1.0f) };
            _lipoMat = new Material(std) { color = new Color(0.16f, 0.36f, 0.72f, 1.0f) };
            _propMat = new Material(std) { color = new Color(0.15f, 0.78f, 0.92f, 1.0f) };
            _ductMat = new Material(std) { color = new Color(0.88f, 0.62f, 0.16f, 1.0f) };

            Shader unlit = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? std;
            _propDiscMat = new Material(unlit) { color = new Color(0.25f, 0.80f, 0.95f, 0.18f) };

            RebuildMesh(DroneBuilder.EvaluateCurrent());
        }

        public void UpdateVisual(Vector3 pos, Quaternion rot, float[] motorRpms, bool isArmed, float dt)
        {
            if (_root == null)
                return;

            DroneHardwareSpec hw = DroneBuilder.EvaluateCurrent();
            float camH = Settings.CameraHeightOffsetMm.Value;
            float camF = Settings.CameraForwardOffsetMm.Value;
            if (Mathf.Abs(hw.WheelbaseMm - _cachedWb) > 0.5f ||
                Mathf.Abs(hw.PropDiameterM - _cachedProp) > 0.001f ||
                hw.PropBlades != _cachedBlades ||
                Mathf.Abs(hw.StatorDiameterMm - _cachedStator) > 0.5f ||
                hw.IsDucted != _cachedDucted ||
                Mathf.Abs(camH - _cachedCamHeightMm) > 0.05f ||
                Mathf.Abs(camF - _cachedCamFwdMm) > 0.05f)
            {
                RebuildMesh(hw);
            }

            bool showAny = !isArmed || Settings.ShowDroneMeshInFpv.Value || (DroneController.Instance?.Spout?.IsSpoutActive ?? false);
            _root.SetActive(showAny);
            _root.transform.SetPositionAndRotation(pos, rot);

            // Spin propellers [0: RR (CW), 1: FR (CCW), 2: RL (CCW), 3: FL (CW)]
            float[] dirs = { 1f, -1f, -1f, 1f };
            for (int i = 0; i < 4; i++)
            {
                if (_propHubs[i] == null) continue;
                float rpm = (motorRpms != null && i < motorRpms.Length) ? motorRpms[i] : 0f;
                // Cap visual step speed slightly for smooth motion without strobing
                float degPerSec = Mathf.Min(rpm * 6.0f, 7200.0f);
                _propAngles[i] = (_propAngles[i] + degPerSec * dirs[i] * dt) % 360.0f;
                _propHubs[i].localRotation = Quaternion.Euler(0.0f, _propAngles[i], 0.0f);
            }
        }

        private void RebuildMesh(DroneHardwareSpec hw)
        {
            _cachedWb = hw.WheelbaseMm;
            _cachedProp = hw.PropDiameterM;
            _cachedBlades = hw.PropBlades;
            _cachedStator = hw.StatorDiameterMm;
            _cachedDucted = hw.IsDucted;
            _cachedCamHeightMm = Settings.CameraHeightOffsetMm.Value;
            _cachedCamFwdMm = Settings.CameraForwardOffsetMm.Value;

            for (int i = _root.transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_root.transform.GetChild(i).gameObject);
            }

            float armLen = hw.FrameRadiusM;
            float armThick = Mathf.Clamp(hw.FrameRadiusM * 0.04f, 0.002f, 0.007f);
            float armWidth = Mathf.Clamp(hw.FrameRadiusM * 0.11f, 0.005f, 0.018f);

            // Center Stack Plate
            float stackW = Mathf.Clamp(hw.ArmHalfXY * 0.55f, 0.022f, 0.055f);
            float stackL = Mathf.Clamp(hw.ArmHalfXY * 0.90f, 0.030f, 0.105f);
            CreateBox("BottomPlate", Vector3.zero, Quaternion.identity, new Vector3(stackW, armThick, stackL), _carbonMat);
            CreateBox("TopPlate", new Vector3(0f, hw.BellyHeightM * 0.6f, 0f), Quaternion.identity, new Vector3(stackW * 0.85f, armThick, stackL * 0.85f), _carbonMat);

            // LiPo Battery
            float lipoH = Mathf.Clamp(hw.TopHeightM * 0.75f, 0.010f, 0.036f);
            Vector3 lipoPos = hw.IsDucted
                ? new Vector3(0f, -hw.BellyHeightM * 0.45f, 0f)
                : new Vector3(0f, hw.BellyHeightM * 0.6f + lipoH * 0.55f, -stackL * 0.12f);
            CreateBox("Battery", lipoPos, Quaternion.identity, new Vector3(stackW * 0.72f, lipoH, stackL * 0.75f), _lipoMat);

            // FPV Camera Pod
            float camPodSize = Mathf.Clamp(hw.FrameRadiusM * 0.14f, 0.011f, 0.024f);
            Vector3 camLocal = GetFpvCameraLocalOffset(hw);
            CreateBox("CameraPod", camLocal - Vector3.forward * (camPodSize * 0.65f), Quaternion.Euler(-Settings.CameraTiltAngle.Value, 0f, 0f), new Vector3(camPodSize, camPodSize * 0.85f, camPodSize * 1.1f), _carbonMat);

            // 4 Diagonal X-Arms
            Vector3[] motorPos =
            {
                new Vector3( hw.ArmHalfXY, 0f, -hw.ArmHalfXY), // 0: Rear-Right
                new Vector3( hw.ArmHalfXY, 0f,  hw.ArmHalfXY), // 1: Front-Right
                new Vector3(-hw.ArmHalfXY, 0f, -hw.ArmHalfXY), // 2: Rear-Left
                new Vector3(-hw.ArmHalfXY, 0f,  hw.ArmHalfXY)  // 3: Front-Left
            };

            float motorDiamM = Mathf.Clamp(hw.StatorDiameterMm * 0.0012f, 0.008f, 0.038f);
            float motorHeightM = Mathf.Clamp(hw.StatorHeightMm * 0.0015f, 0.004f, 0.024f);
            float propRad = hw.PropDiameterM * 0.5f;
            float bladeChord = Mathf.Clamp(propRad * 0.22f, 0.004f, 0.018f);

            for (int i = 0; i < 4; i++)
            {
                Vector3 mp = motorPos[i];
                Vector3 mid = mp * 0.5f;
                Quaternion armRot = Quaternion.LookRotation(mp.normalized, Vector3.up);
                CreateBox($"Arm_{i}", mid, armRot, new Vector3(armWidth, armThick, armLen), _carbonMat);

                // Motor Stator Bell
                CreateCylinder($"Motor_{i}", mp + Vector3.up * (motorHeightM * 0.4f), new Vector3(motorDiamM, motorHeightM * 0.5f, motorDiamM), _metalMat);

                // Prop Hub + Blades
                GameObject hub = new GameObject($"PropHub_{i}");
                hub.layer = DroneMeshLayer;
                hub.transform.SetParent(_root.transform, false);
                hub.transform.localPosition = mp + Vector3.up * (motorHeightM * 0.85f);
                _propHubs[i] = hub.transform;

                for (int b = 0; b < hw.PropBlades; b++)
                {
                    float ang = b * (360f / hw.PropBlades);
                    GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    blade.name = $"Blade_{i}_{b}";
                    blade.layer = DroneMeshLayer;
                    Object.Destroy(blade.GetComponent<Collider>());
                    blade.transform.SetParent(hub.transform, false);
                    blade.transform.localRotation = Quaternion.Euler(0f, ang, 0f);
                    blade.transform.localPosition = blade.transform.localRotation * new Vector3(0f, 0f, propRad * 0.5f);
                    blade.transform.localScale = new Vector3(bladeChord, 0.0015f, propRad * 0.96f);
                    blade.GetComponent<Renderer>().sharedMaterial = _propMat;
                }

                // Translucent spinning prop disc
                GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = $"PropDisc_{i}";
                disc.layer = DroneMeshLayer;
                Object.Destroy(disc.GetComponent<Collider>());
                disc.transform.SetParent(hub.transform, false);
                disc.transform.localPosition = Vector3.zero;
                disc.transform.localScale = new Vector3(hw.PropDiameterM * 0.98f, 0.0006f, hw.PropDiameterM * 0.98f);
                disc.GetComponent<Renderer>().sharedMaterial = _propDiscMat;

                // Optional Duct / Prop Guard Ring (12 segments)
                if (hw.IsDucted)
                {
                    float ductR = propRad + 0.004f;
                    float ductH = Mathf.Clamp(hw.BellyHeightM * 1.4f, 0.012f, 0.032f);
                    const int segs = 12;
                    float segLen = (2f * Mathf.PI * ductR) / segs * 1.08f;
                    for (int s = 0; s < segs; s++)
                    {
                        float a = s * (360f / segs);
                        Quaternion segRot = Quaternion.Euler(0f, a, 0f);
                        Vector3 segPos = mp + Vector3.up * (motorHeightM * 0.55f) + segRot * new Vector3(0f, 0f, ductR);
                        CreateBox($"Duct_{i}_{s}", segPos, segRot, new Vector3(segLen, ductH, 0.0025f), _ductMat);
                    }
                }
            }
        }

        private void CreateBox(string name, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = DroneMeshLayer;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private void CreateCylinder(string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.layer = DroneMeshLayer;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
            if (_carbonMat != null) Object.Destroy(_carbonMat);
            if (_metalMat != null) Object.Destroy(_metalMat);
            if (_lipoMat != null) Object.Destroy(_lipoMat);
            if (_propMat != null) Object.Destroy(_propMat);
            if (_propDiscMat != null) Object.Destroy(_propDiscMat);
            if (_ductMat != null) Object.Destroy(_ductMat);
        }
    }
}

