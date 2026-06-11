using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Savior;
using ABI_RC.Core.UI;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.Movement;
using cohtml.Net;
using MelonLoader;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ThirdPersonOptions
{
    [DisallowMultipleComponent]
    class ThirdPersonOptionsComponent : MonoBehaviour
    {
        static readonly Vector2 LeftThirdPersonOffset = new Vector2(-0.45f, 0f);
        static readonly Vector2 RightThirdPersonOffset = new Vector2(0.45f, 0f);

        static readonly MethodInfo EnterThirdPersonMethod = typeof(BetterCharacterLook).GetMethod(
            "EnterThirdPerson",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly MethodInfo ExitThirdPersonMethod = typeof(BetterCharacterLook).GetMethod(
            "ExitThirdPerson",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly FieldInfo DesiredThirdPersonDistanceField = typeof(BetterCharacterLook).GetField(
            "_desiredThirdPersonDistance",
            BindingFlags.Instance | BindingFlags.NonPublic);

        static readonly FieldInfo InternalViewField = typeof(CohtmlControlledViewWrapper).GetField(
            "_view",
            BindingFlags.Instance | BindingFlags.NonPublic);

        CohtmlControlledView _cohtmlView;
        bool _destroyed;
        bool _cameraOffsetApplied;
        bool _restoreThirdPersonAfterMiddleClickZoom;
        bool _warnedMissingToggleMethods;
        bool _warnedMissingDistanceField;
        bool _ctrlTWasPressed;
        bool _ctrlYWasPressed;
        float _savedThirdPersonDistance;
        Vector2 _savedThirdPersonLocalOffset;
        float _nextInjectPass;

        void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            MelonCoroutines.Start(WaitForCohtmlView());
        }

        void OnDestroy()
        {
            _destroyed = true;
            try
            {
                if (_cohtmlView != null && _cohtmlView.Listener != null)
                {
                    _cohtmlView.Listener.ReadyForBindings -= OnReadyForBindings;
                    _cohtmlView.Listener.FinishLoad -= OnFinishLoad;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }

            var look = GetCharacterLook();
            if (look != null)
            {
                RestoreThirdPersonAfterMiddleClickZoom(look);
                RestoreThirdPersonLocalOffset(look);
                look.ScrollToToggleThirdPerson = GetNativeScrollToToggleThirdPerson();
            }
        }

        void Update()
        {
            var look = GetCharacterLook();
            if (look == null)
                return;

            if (Time.unscaledTime >= _nextInjectPass)
            {
                _nextInjectPass = Time.unscaledTime + 2f;
                ExecuteNativeSettingsInjection();
            }

            look.ScrollToToggleThirdPerson = GetNativeScrollToToggleThirdPerson() && IsScrollModifierPressed();
            UpdateThirdPersonLocalOffset(look);

            if (!Settings.Enabled || !Settings.MiddleClickZoom)
                RestoreThirdPersonAfterMiddleClickZoom(look);
            else if (ShouldHandleGameplayInput() && Input.GetMouseButtonDown(2))
                ToggleMiddleClickZoom(look);

            bool ctrlTHeld = IsCtrlTHeld();
            if (ctrlTHeld && !_ctrlTWasPressed)
            {
                ConsumeNativeCtrlTToggle();

                if (Settings.Enabled && Settings.CtrlTToggle && ShouldHandleGameplayInput())
                    ToggleThirdPerson(look);
            }
            _ctrlTWasPressed = ctrlTHeld;

            bool ctrlYHeld = IsCtrlYHeld();
            if (ctrlYHeld && !_ctrlYWasPressed && ShouldHandleGameplayInput())
                Settings.CycleCameraPosition();
            _ctrlYWasPressed = ctrlYHeld;
        }

        IEnumerator WaitForCohtmlView()
        {
            while (!_destroyed && ViewManager.Instance == null)
                yield return null;

            while (!_destroyed && (ViewManager.Instance == null || ViewManager.Instance.cohtmlView == null || ViewManager.Instance.cohtmlView.Listener == null))
                yield return null;

            if (_destroyed)
                yield break;

            _cohtmlView = ViewManager.Instance.cohtmlView;
            _cohtmlView.Listener.ReadyForBindings += OnReadyForBindings;
            _cohtmlView.Listener.FinishLoad += OnFinishLoad;

            if (_cohtmlView.FinishedLoading)
            {
                OnReadyForBindings();
                OnFinishLoad(null);
            }
        }

        void OnReadyForBindings()
        {
            try
            {
                if (_cohtmlView == null || _cohtmlView.View == null)
                    return;

                _cohtmlView.View.BindCall("TPO_GetNativeSettingsJson", new Func<string>(Settings.GetNativeSettingsJson));
                _cohtmlView.View.RegisterForEvent("TPO_SetEnabled", new Action<bool>(Settings.SetEnabled));
                _cohtmlView.View.RegisterForEvent("TPO_SetCtrlTToggle", new Action<bool>(Settings.SetCtrlTToggle));
                _cohtmlView.View.RegisterForEvent("TPO_SetMiddleClickZoom", new Action<bool>(Settings.SetMiddleClickZoom));
                _cohtmlView.View.RegisterForEvent("TPO_SetCameraPosition", new Action<int>(Settings.SetCameraPosition));
                _cohtmlView.View.RegisterForEvent("TPO_SetModifierKey", new Action<int>(Settings.SetModifierKey));
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        void OnFinishLoad(string url)
        {
            ExecuteNativeSettingsInjection();
        }

        void ExecuteNativeSettingsInjection()
        {
            try
            {
                var wrapper = ViewManager.Instance != null && ViewManager.Instance.cohtmlView != null
                    ? ViewManager.Instance.cohtmlView.View
                    : null;

                GetInternalView(wrapper)?.ExecuteScript(NativeSettingsScript);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static View GetInternalView(CohtmlControlledViewWrapper wrapper)
        {
            return InternalViewField?.GetValue(wrapper) as View;
        }

        static BetterCharacterLook GetCharacterLook()
        {
            var controller = BetterBetterCharacterController.Instance;
            return controller == null ? null : controller.CharacterLook;
        }

        static bool GetNativeScrollToToggleThirdPerson()
        {
            var metaPort = MetaPort.Instance;
            if (metaPort == null || metaPort.settings == null)
                return true;

            return metaPort.settings.GetSettingsBool("ScrollToToggleThirdPerson", true);
        }

        static bool IsScrollModifierPressed()
        {
            if (!Settings.Enabled)
                return true;

            if (Settings.ScrollModifier == Settings.ModifierKeyOption.None)
                return true;

            return Input.GetKey(Settings.ModifierKey);
        }

        internal static bool IsCtrlTHeld()
        {
            return Input.GetKey(KeyCode.T)
                && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        }

        static void ConsumeNativeCtrlTToggle()
        {
            var inputManager = CVRInputManager.Instance;
            if (inputManager != null)
                inputManager.toggleThirdPerson = false;
        }

        static bool ShouldHandleGameplayInput()
        {
            var viewManager = ViewManager.Instance;
            if (viewManager != null && viewManager.IsAnyMenuOpen)
                return false;

            return true;
        }

        static bool IsCtrlYHeld()
        {
            return Input.GetKey(KeyCode.Y)
                && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
        }

        void UpdateThirdPersonLocalOffset(BetterCharacterLook look)
        {
            bool shouldApply = Settings.Enabled
                && look.IsInThirdPerson
                && Settings.CameraPosition != Settings.CameraPositionOption.Back;
            if (!shouldApply)
            {
                RestoreThirdPersonLocalOffset(look);
                return;
            }

            if (!_cameraOffsetApplied)
            {
                _savedThirdPersonLocalOffset = look.ThirdPersonLocalOffset;
                _cameraOffsetApplied = true;
            }

            switch (Settings.CameraPosition)
            {
                case Settings.CameraPositionOption.Left:
                    look.ThirdPersonLocalOffset = _savedThirdPersonLocalOffset + LeftThirdPersonOffset;
                    break;
                case Settings.CameraPositionOption.Right:
                    look.ThirdPersonLocalOffset = _savedThirdPersonLocalOffset + RightThirdPersonOffset;
                    break;
                default:
                    look.ThirdPersonLocalOffset = _savedThirdPersonLocalOffset;
                    break;
            }
        }

        void RestoreThirdPersonLocalOffset(BetterCharacterLook look)
        {
            if (!_cameraOffsetApplied)
                return;

            look.ThirdPersonLocalOffset = _savedThirdPersonLocalOffset;
            _cameraOffsetApplied = false;
        }

        void ToggleMiddleClickZoom(BetterCharacterLook look)
        {
            if (_restoreThirdPersonAfterMiddleClickZoom)
            {
                RestoreThirdPersonAfterMiddleClickZoom(look);
                return;
            }

            if (!look.IsInThirdPerson || !look.IsThirdPersonAllowed)
                return;

            if (DesiredThirdPersonDistanceField == null)
            {
                WarnMissingDistanceField();
                return;
            }

            _savedThirdPersonDistance = (float)DesiredThirdPersonDistanceField.GetValue(look);
            ToggleThirdPerson(look);
            _restoreThirdPersonAfterMiddleClickZoom = !look.IsInThirdPerson;
        }

        void RestoreThirdPersonAfterMiddleClickZoom(BetterCharacterLook look)
        {
            if (!_restoreThirdPersonAfterMiddleClickZoom)
                return;

            _restoreThirdPersonAfterMiddleClickZoom = false;
            if (!look.IsThirdPersonAllowed)
                return;

            if (!look.IsInThirdPerson)
                ToggleThirdPerson(look);

            if (!look.IsInThirdPerson)
                return;

            if (DesiredThirdPersonDistanceField == null)
            {
                WarnMissingDistanceField();
                return;
            }

            DesiredThirdPersonDistanceField.SetValue(look, _savedThirdPersonDistance);
        }

        void WarnMissingDistanceField()
        {
            if (_warnedMissingDistanceField)
                return;

            MelonLogger.Warning("Could not find BetterCharacterLook third-person distance field.");
            _warnedMissingDistanceField = true;
        }

        void ToggleThirdPerson(BetterCharacterLook look)
        {
            if (!look.IsThirdPersonAllowed)
                return;

            MethodInfo method = look.IsInThirdPerson ? ExitThirdPersonMethod : EnterThirdPersonMethod;
            if (method != null)
            {
                method.Invoke(look, null);
                return;
            }

            if (!_warnedMissingToggleMethods)
            {
                MelonLogger.Warning("Could not find BetterCharacterLook third-person toggle methods.");
                _warnedMissingToggleMethods = true;
            }
        }

        const string NativeSettingsScript = @"
(function() {
    if (window._tpoInjecting)
        return;

    window._tpoInjecting = true;

    function finish() {
        window._tpoInjecting = false;
    }

    function boolText(value) {
        return value ? 'True' : 'False';
    }

    function ensureStyle() {
        if (document.getElementById('tpo-native-settings-style'))
            return;

        var style = document.createElement('style');
        style.id = 'tpo-native-settings-style';
        style.textContent = '#settings-input .tpo-native-row{margin-top:.25em;}';
        document.head.appendChild(style);
    }

    function makeCaption(text, tooltip) {
        var caption = document.createElement('div');
        caption.className = 'option-caption';
        caption.textContent = text;
        if (tooltip)
            caption.setAttribute('data-tooltip', tooltip);
        return caption;
    }

    function makeInput(child) {
        var input = document.createElement('div');
        input.className = 'option-input';
        input.appendChild(child);
        return input;
    }

    function makeSpacerCaption() {
        var caption = document.createElement('div');
        caption.className = 'option-caption';
        return caption;
    }

    function makeSpacerInput() {
        var input = document.createElement('div');
        input.className = 'option-input';
        return input;
    }

    function makeRow(caption, input) {
        var row = document.createElement('div');
        row.className = 'row-wrapper tpo-native-row';
        row.setAttribute('platform-specific', 'pc');
        row.appendChild(caption);
        row.appendChild(input);
        row.appendChild(makeSpacerCaption());
        row.appendChild(makeSpacerInput());
        return row;
    }

    function makeToggle(id, current) {
        var toggle = document.createElement('div');
        toggle.id = id;
        toggle.className = 'inp_toggle no-scroll';
        toggle.setAttribute('data-current', boolText(current));
        toggle.setAttribute('data-saveOnChange', 'false');
        return toggle;
    }

    function makeDropdown(id, current, options) {
        var dropdown = document.createElement('div');
        dropdown.id = id;
        dropdown.className = 'inp_dropdown';
        dropdown.setAttribute('data-options', options);
        dropdown.setAttribute('data-current', String(current));
        dropdown.setAttribute('data-saveOnChange', 'false');
        return dropdown;
    }

    function findSetting(name) {
        if (!window.settings)
            return null;

        for (var i = 0; i < window.settings.length; i++) {
            if (window.settings[i].name === name)
                return window.settings[i];
        }

        return null;
    }

    function addSetting(setting) {
        if (!window.settings)
            window.settings = [];

        if (!findSetting(setting.name))
            window.settings[window.settings.length] = setting;
    }

    function build(settingsData) {
        ensureStyle();

        var category = document.getElementById('settings-input');
        if (!category) {
            finish();
            return;
        }

        var oldBlock = document.getElementById('tpo-native-settings');
        if (oldBlock)
            oldBlock.parentNode.removeChild(oldBlock);

        var block = document.createElement('div');
        block.id = 'tpo-native-settings';

        var ctrlT = makeToggle('TPOCtrlTToggle', settingsData.ctrlTToggle);
        var middleClickZoom = makeToggle('TPOMiddleClickZoom', settingsData.middleClickZoom);
        var cameraPosition = makeDropdown('TPOCameraPosition', settingsData.cameraPosition, '0:Back,1:Left,2:Right,3:Front');
        var modifier = makeDropdown('TPOModifierKey', settingsData.modifierKey, '0:Left Ctrl,1:Right Ctrl,2:Left Shift,3:Right Shift,4:Left Alt,5:Right Alt,6:F,7:G,8:T,9:None');

        block.appendChild(makeRow(
            makeCaption('Ctrl+T Toggle', 'Toggles third person while normal gameplay input is active'),
            makeInput(ctrlT)));
        block.appendChild(makeRow(
            makeCaption('3P Middle Click Zoom', 'Middle click from third person to zoom in, then middle click again to restore the previous third-person distance'),
            makeInput(middleClickZoom)));
        block.appendChild(makeRow(
            makeCaption('3P Camera Position', 'Selects a back, left, right, or front-facing third-person camera'),
            makeInput(cameraPosition)));
        block.appendChild(makeRow(
            makeCaption('3P Scroll Modifier', 'Key required for Scroll To Toggle 3P. Set to None to use the native behavior.'),
            makeInput(modifier)));

        var anchor = document.getElementById('ScrollToToggleThirdPerson');
        var anchorRow = anchor ? anchor.closest('.row-wrapper') : null;
        if (anchorRow && anchorRow.parentNode === category)
            category.insertBefore(block, anchorRow.nextSibling);
        else
            category.appendChild(block);

        var ctrlTControl = new inp_toggle(ctrlT);
        var middleClickZoomControl = new inp_toggle(middleClickZoom);
        var cameraPositionControl = new inp_dropdown(cameraPosition);
        var modifierControl = new inp_dropdown(modifier);

        addSetting(ctrlTControl);
        addSetting(middleClickZoomControl);
        addSetting(cameraPositionControl);
        addSetting(modifierControl);

        ctrlT.addEventListener('mousedown', function() {
            window.setTimeout(function() {
                engine.trigger('TPO_SetCtrlTToggle', ctrlTControl.value() === 'True');
            }, 0);
        });

        middleClickZoom.addEventListener('mousedown', function() {
            window.setTimeout(function() {
                engine.trigger('TPO_SetMiddleClickZoom', middleClickZoomControl.value() === 'True');
            }, 0);
        });

        cameraPosition.addEventListener('dropdownSelect', function() {
            engine.trigger('TPO_SetCameraPosition', parseInt(cameraPositionControl.value(), 10));
        });

        modifier.addEventListener('dropdownSelect', function() {
            engine.trigger('TPO_SetModifierKey', parseInt(modifierControl.value(), 10));
        });
        finish();
    }

    if (!window.engine || !engine.call) {
        finish();
        return;
    }

    engine.call('TPO_GetNativeSettingsJson').then(function(json) {
        try {
            build(JSON.parse(json));
        } catch (e) {
            console.log('ThirdPersonOptions settings injection failed: ' + e);
            finish();
        }
    }, function(err) {
        console.log('ThirdPersonOptions settings data request failed: ' + err);
        finish();
    });
})();";
    }
}
