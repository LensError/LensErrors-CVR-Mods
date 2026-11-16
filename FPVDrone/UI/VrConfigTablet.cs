using ABI_RC.Core;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Systems.InputManagement.XR;
using UnityEngine;

namespace FPVDrone.UI
{
    /// <summary>
    /// Renders DroneConfigMenu onto an interactive world-space VR tablet (Layer 15 UIInternal)
    /// driven by ChilloutVR's controller laser rays, trigger clicks, and thumbstick scrolling.
    /// </summary>
    public class VrConfigTablet
    {
        public static bool IsPointingAtTablet { get; private set; }

        private GameObject _tabletObject;
        private Collider _tabletCollider;
        private MeshRenderer _tabletRenderer;
        private Material _tabletMaterial;
        private RenderTexture _tabletRt;

        private GameObject _laserObject;
        private LineRenderer _laserLine;
        private Material _laserMaterial;

        private bool _wasVisible;
        private bool _isHovering;
        private Vector2 _pointerPos;
        private bool _triggerHeld;
        private bool _prevTriggerHeld;
        private int _activeHand; // 0 = right, 1 = left

        private bool _pendingDown;
        private bool _pendingUp;
        private float _pendingScroll;

        public void Initialize()
        {
            if (_tabletObject != null)
                return;

            int w = Mathf.RoundToInt(DroneConfigMenu.TabletWidth);
            int h = Mathf.RoundToInt(DroneConfigMenu.TabletHeight);
            _tabletRt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "FPVDrone_VrConfigTabletRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _tabletRt.Create();

            Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            _tabletMaterial = new Material(shader)
            {
                mainTexture = _tabletRt
            };
            _tabletMaterial.SetInt("_Cull", 0);

            _tabletObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _tabletObject.name = "FPVDrone_VrConfigTablet";
            Object.DontDestroyOnLoad(_tabletObject);
            _tabletObject.layer = CVRLayers.UIInternal;

            _tabletCollider = _tabletObject.GetComponent<Collider>();
            _tabletRenderer = _tabletObject.GetComponent<MeshRenderer>();
            if (_tabletRenderer != null)
            {
                _tabletRenderer.material = _tabletMaterial;
                _tabletRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _tabletRenderer.receiveShadows = false;
                _tabletRenderer.allowOcclusionWhenDynamic = false;
            }
            _tabletObject.SetActive(false);

            _laserObject = new GameObject("FPVDrone_VrTabletLaser");
            Object.DontDestroyOnLoad(_laserObject);
            _laserObject.layer = CVRLayers.UIInternal;

            Shader laserShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            _laserMaterial = new Material(laserShader)
            {
                color = new Color(0.25f, 0.90f, 0.72f, 0.9f)
            };

            _laserLine = _laserObject.AddComponent<LineRenderer>();
            _laserLine.useWorldSpace = true;
            _laserLine.positionCount = 2;
            _laserLine.startWidth = 0.004f;
            _laserLine.endWidth = 0.002f;
            _laserLine.material = _laserMaterial;
            _laserLine.startColor = new Color(0.25f, 0.95f, 0.75f, 0.9f);
            _laserLine.endColor = new Color(0.85f, 1.0f, 0.95f, 0.95f);
            _laserLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _laserLine.receiveShadows = false;
            _laserLine.enabled = false;
        }

        public void UpdateTablet(bool isConfigOpen)
        {
            bool isVr = MetaPort.Instance != null && MetaPort.Instance.isUsingVr;
            bool cvrMenuOpen = (ViewManager.Instance != null && ViewManager.Instance.IsAnyMenuOpen) ||
                               (CVR_MenuManager.Instance != null && CVR_MenuManager.Instance.IsViewShown);

            bool shouldShow = isVr && isConfigOpen && !cvrMenuOpen;
            if (!shouldShow)
            {
                HideTablet();
                return;
            }

            if (_tabletObject == null)
                Initialize();

            if (!_wasVisible)
            {
                PlaceInFrontOfPlayer();
                _pendingDown = false;
                _pendingUp = false;
                _pendingScroll = 0f;
                _triggerHeld = false;
                _prevTriggerHeld = false;
            }

            _tabletObject.SetActive(true);
            _wasVisible = true;

            UpdatePointerAndInput();
        }

        private void HideTablet()
        {
            if (_tabletObject != null && _tabletObject.activeSelf)
                _tabletObject.SetActive(false);

            if (_laserLine != null)
                _laserLine.enabled = false;

            _wasVisible = false;
            _isHovering = false;
            _triggerHeld = false;
            _prevTriggerHeld = false;
            IsPointingAtTablet = false;
        }

        private void PlaceInFrontOfPlayer()
        {
            var player = PlayerSetup.Instance;
            Transform head = (player != null && player.vrCamera != null) ? player.vrCamera.transform : null;
            if (head == null)
                return;

            float scale = Mathf.Max(0.15f, head.lossyScale.y);
            Vector3 flatFwd = new Vector3(head.forward.x, 0.0f, head.forward.z);
            if (flatFwd.sqrMagnitude < 0.001f)
                flatFwd = Vector3.forward;
            flatFwd.Normalize();

            float dist = 1.05f * scale;
            Vector3 pos = head.position + flatFwd * dist + Vector3.down * (0.08f * scale);
            Quaternion rot = Quaternion.LookRotation(flatFwd, Vector3.up) * Quaternion.Euler(10.0f, 0.0f, 0.0f);

            float width = 1.15f * scale;
            float height = width * (DroneConfigMenu.TabletHeight / DroneConfigMenu.TabletWidth);

            _tabletObject.transform.SetPositionAndRotation(pos, rot);
            _tabletObject.transform.localScale = new Vector3(width, height, 1.0f);
        }

        private void UpdatePointerAndInput()
        {
            _isHovering = false;
            IsPointingAtTablet = false;

            if (_tabletCollider == null)
                return;

            DroneInput.GetXrModules(out CVRXRModule leftMod, out CVRXRModule rightMod);
            Transform leftRayT = ResolveRayTransform(isLeft: true);
            Transform rightRayT = ResolveRayTransform(isLeft: false);

            bool hitRight = TryHitTablet(rightRayT, out RaycastHit rightHit);
            bool hitLeft = TryHitTablet(leftRayT, out RaycastHit leftHit);

            int chosenHand = -1;
            RaycastHit chosenHit = default;
            Transform chosenRayT = null;

            if (_triggerHeld)
            {
                if (_activeHand == 0 && hitRight)
                {
                    chosenHand = 0;
                    chosenHit = rightHit;
                    chosenRayT = rightRayT;
                }
                else if (_activeHand == 1 && hitLeft)
                {
                    chosenHand = 1;
                    chosenHit = leftHit;
                    chosenRayT = leftRayT;
                }
            }

            if (chosenHand == -1)
            {
                if (hitRight && (!hitLeft || _activeHand == 0))
                {
                    chosenHand = 0;
                    chosenHit = rightHit;
                    chosenRayT = rightRayT;
                }
                else if (hitLeft)
                {
                    chosenHand = 1;
                    chosenHit = leftHit;
                    chosenRayT = leftRayT;
                }
            }

            if (chosenHand >= 0)
            {
                _activeHand = chosenHand;
                _isHovering = true;
                IsPointingAtTablet = true;

                _pointerPos = new Vector2(
                    chosenHit.textureCoord.x * DroneConfigMenu.TabletWidth,
                    (1.0f - chosenHit.textureCoord.y) * DroneConfigMenu.TabletHeight);

                if (_laserLine != null && chosenRayT != null)
                {
                    _laserLine.enabled = true;
                    _laserLine.SetPosition(0, chosenRayT.position);
                    _laserLine.SetPosition(1, chosenHit.point);
                }
            }
            else if (_laserLine != null)
            {
                _laserLine.enabled = false;
            }

            CVRXRModule activeMod = _activeHand == 1 ? leftMod : rightMod;
            bool modValid = activeMod != null && activeMod.IsValid;

            float triggerVal = modValid ? activeMod.Trigger : 0.0f;
            if (_triggerHeld)
                _triggerHeld = triggerVal > 0.35f;
            else
                _triggerHeld = _isHovering && triggerVal > 0.70f;

            if (!_prevTriggerHeld && _triggerHeld)
                _pendingDown = true;
            if (_prevTriggerHeld && !_triggerHeld)
                _pendingUp = true;

            _prevTriggerHeld = _triggerHeld;

            if (_isHovering && modValid)
            {
                float stickY = activeMod.Primary2DAxis.y;
                if (Mathf.Abs(stickY) > 0.25f)
                    _pendingScroll += -stickY * Time.unscaledDeltaTime * 8.0f;
            }
        }

        private bool TryHitTablet(Transform rayTransform, out RaycastHit hit)
        {
            hit = default;
            if (rayTransform == null || _tabletCollider == null)
                return false;

            Ray ray = new Ray(rayTransform.position, rayTransform.forward);
            return _tabletCollider.Raycast(ray, out hit, 8.0f);
        }

        private static Transform ResolveRayTransform(bool isLeft)
        {
            var player = PlayerSetup.Instance;
            if (player != null)
            {
                ControllerRay cr = isLeft ? player.vrRayLeft : player.vrRayRight;
                if (cr != null && cr.rayDirectionTransform != null)
                    return cr.rayDirectionTransform;
            }

            var menuMgr = CVR_MenuManager.Instance;
            if (menuMgr != null)
            {
                ControllerRay cr = isLeft ? menuMgr.leftVrControllerRay : menuMgr.rightVrControllerRay;
                if (cr != null && cr.rayDirectionTransform != null)
                    return cr.rayDirectionTransform;
            }

            if (player != null)
            {
                GameObject tracker = isLeft ? player.vrLeftHandTracker : player.vrRightHandTracker;
                if (tracker != null)
                    return tracker.transform;
            }

            return null;
        }

        public void Render(DroneConfigMenu menu)
        {
            if (!_wasVisible || _tabletObject == null || !_tabletObject.activeSelf || menu == null || !menu.IsOpen)
                return;

            menu.DrawMenuToRenderTexture(
                _tabletRt,
                _pointerPos,
                _isHovering,
                _pendingDown,
                _pendingUp,
                _triggerHeld,
                _pendingScroll);

            _pendingDown = false;
            _pendingUp = false;
            _pendingScroll = 0.0f;
        }

        public void Destroy()
        {
            IsPointingAtTablet = false;
            if (_laserObject != null)
            {
                Object.Destroy(_laserObject);
                _laserObject = null;
                _laserLine = null;
            }
            if (_tabletObject != null)
            {
                Object.Destroy(_tabletObject);
                _tabletObject = null;
                _tabletCollider = null;
                _tabletRenderer = null;
            }
            if (_tabletMaterial != null)
            {
                Object.Destroy(_tabletMaterial);
                _tabletMaterial = null;
            }
            if (_laserMaterial != null)
            {
                Object.Destroy(_laserMaterial);
                _laserMaterial = null;
            }
            if (_tabletRt != null)
            {
                if (_tabletRt.IsCreated())
                    _tabletRt.Release();
                Object.Destroy(_tabletRt);
                _tabletRt = null;
            }
        }
    }
}

