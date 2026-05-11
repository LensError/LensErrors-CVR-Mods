using ABI.CCK.Components;
using ABI_RC.Core.UI;
using ABI_RC.Systems.Movement;
using System.Globalization;
using UnityEngine;

namespace BetterScrollFlight
{
    [DisallowMultipleComponent]
    class FlightComponent : MonoBehaviour
    {
        bool _wasFlying;
        float _worldDefaultMultiplier = 5f;
        float _lastMiddleClickTime = -1f;

        void Awake()
        {
            Object.DontDestroyOnLoad(gameObject);
            CVRWorld.GameRulesUpdated += OnGameRulesUpdated;
        }

        void OnDestroy()
        {
            CVRWorld.GameRulesUpdated -= OnGameRulesUpdated;
        }

        void OnGameRulesUpdated()
        {
            var controller = BetterBetterCharacterController.Instance;
            if (controller != null)
                _worldDefaultMultiplier = controller.worldFlightSpeedMultiplier;
        }

        void Update()
        {
            if (!Settings.Enabled)
                return;

            var controller = BetterBetterCharacterController.Instance;
            if (controller == null)
                return;

            bool isFlying = controller.IsFlying() && controller.FlightAllowedInWorld;

            if (isFlying && !_wasFlying)
            {
                _worldDefaultMultiplier = controller.worldFlightSpeedMultiplier;
                ApplySpeedScale(controller);
            }
            else if (!isFlying && _wasFlying)
            {
                if (Settings.ResetOnExitFlight)
                    Settings.ResetSpeedScale();
                controller.worldFlightSpeedMultiplier = _worldDefaultMultiplier;
            }

            if (isFlying && Cursor.lockState == CursorLockMode.Locked)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (scroll != 0f)
                {
                    bool modifierOk = !Settings.RequireModifier || Input.GetKey(Settings.ModifierKey);
                    if (modifierOk)
                    {
                        float step = 1f + Settings.SpeedStep;
                        Settings.AdjustSpeedScale(scroll > 0f ? step : (1f / step));
                        ApplySpeedScale(controller);
                        ShowHudToast();
                    }
                }

                if (Input.GetMouseButtonDown(2))
                {
                    float now = Time.unscaledTime;
                    if (now - _lastMiddleClickTime < 0.4f)
                    {
                        Settings.ResetSpeedScale();
                        ApplySpeedScale(controller);
                        ShowHudToast();
                        _lastMiddleClickTime = -1f;
                    }
                    else
                    {
                        _lastMiddleClickTime = now;
                    }
                }
            }

            _wasFlying = isFlying;
        }

        void ShowHudToast()
        {
            if (!Settings.ShowHud || !CohtmlHud.IsReady)
                return;

            CohtmlHud.Instance.ViewDropTextImmediate(
                "BetterScrollFlight",
                Settings.SpeedScale.ToString("F2", CultureInfo.InvariantCulture) + "x",
                "Flight speed",
                "bsf-speed",
                false);
        }

        void ApplySpeedScale(BetterBetterCharacterController controller)
        {
            controller.worldFlightSpeedMultiplier = _worldDefaultMultiplier * Settings.SpeedScale;
        }
    }
}
