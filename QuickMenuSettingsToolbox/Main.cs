using System;
using System.Collections;
using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.UI;
using ABI_RC.Systems.UI.UILib.UIObjects;
using cohtml.Net;
using MelonLoader;

[assembly: MelonInfo(typeof(QuickMenuSettingsToolbox.Main), "Quick Menu Settings Toolbox", "1.0.2", "LensError")]
[assembly: MelonGame(null, "ChilloutVR")]

namespace QuickMenuSettingsToolbox
{
    public class Main : MelonMod
    {
        static readonly FieldInfo InternalViewField = typeof(CohtmlControlledViewWrapper).GetField(
            "_view", BindingFlags.Instance | BindingFlags.NonPublic);

        const string CategoryIdToken = "__QUICK_MENU_SETTINGS_CATEGORY__";

        const string AdapterScript = @"
(function () {
    if (window.quickMenuSettingsToolboxInstalled)
        return;

    window.quickMenuSettingsToolboxInstalled = true;
    var settingsNodes = [];

    function applyChanges() {
        var source = document.getElementById('CVRUI-QMUI-SettingsPage-Content');
        var category = document.getElementById('__QUICK_MENU_SETTINGS_CATEGORY__');

        if (source) {
            for (var i = 0; i < settingsNodes.length; i++) {
                if (!document.documentElement.contains(settingsNodes[i]))
                    source.appendChild(settingsNodes[i]);
            }
        }

        if (source && category) {
            while (source.firstChild) {
                if (settingsNodes.indexOf(source.firstChild) === -1)
                    settingsNodes.push(source.firstChild);
                category.appendChild(source.firstChild);
            }
        }

        var settingsButtons = document.querySelectorAll(
            '[data-page=""CVRUI-QMUI-SettingsPage""]');
        for (var buttonIndex = 0; buttonIndex < settingsButtons.length; buttonIndex++)
            settingsButtons[buttonIndex].setAttribute('data-tooltip', 'Open Settings');
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        while (target && target !== document) {
            if (target.getAttribute &&
                target.getAttribute('data-page') === 'CVRUI-QMUI-SettingsPage') {
                event.preventDefault();
                event.stopImmediatePropagation();
                engine.trigger('QuickMenuSettingsToolbox-OpenMainMenu');
                return;
            }
            target = target.parentNode;
        }
    }, true);

    new MutationObserver(applyChanges).observe(document.body, {
        childList: true,
        subtree: true
    });

    applyChanges();
})();";

        CohtmlControlledView _quickMenuView;
        Category _quickMenuSettingsCategory;

        public override void OnLateInitializeMelon()
        {
            var toolboxPage = Page.GetOrCreatePage(
                "CVRUILib",
                "CVRUtils",
                isRootPage: true,
                tabIcon: "Toolbox");
            _quickMenuSettingsCategory = toolboxPage.AddCategory("Quick Menu Settings");

            MelonCoroutines.Start(WaitForQuickMenu());
        }

        public override void OnDeinitializeMelon()
        {
            if (_quickMenuView?.Listener != null)
            {
                _quickMenuView.Listener.ReadyForBindings -= OnReadyForBindings;
                _quickMenuView.Listener.FinishLoad -= OnFinishLoad;
            }

            _quickMenuView = null;
        }

        IEnumerator WaitForQuickMenu()
        {
            while (CVR_MenuManager.Instance == null ||
                   CVR_MenuManager.Instance.cohtmlView == null ||
                   CVR_MenuManager.Instance.cohtmlView.Listener == null)
                yield return null;

            _quickMenuView = CVR_MenuManager.Instance.cohtmlView;
            _quickMenuView.Listener.ReadyForBindings += OnReadyForBindings;
            _quickMenuView.Listener.FinishLoad += OnFinishLoad;

            if (_quickMenuView.FinishedLoading)
            {
                OnReadyForBindings();
                OnFinishLoad(null);
            }
        }

        void OnReadyForBindings()
        {
            try
            {
                _quickMenuView?.View?.RegisterForEvent(
                    "QuickMenuSettingsToolbox-OpenMainMenu",
                    new Action(OpenMainMenu));
            }
            catch (Exception exception)
            {
                MelonLogger.Error(exception);
            }
        }

        static void OpenMainMenu()
        {
            if (CVR_MenuManager.Instance != null)
                CVR_MenuManager.Instance.ToggleQuickMenu(false);

            if (ViewManager.Instance != null)
                ViewManager.Instance.UiStateToggle(true);
        }

        void OnFinishLoad(string _)
        {
            try
            {
                var view = InternalViewField?.GetValue(_quickMenuView?.View) as View;
                view?.ExecuteScript(AdapterScript.Replace(
                    CategoryIdToken,
                    _quickMenuSettingsCategory.ElementID));
            }
            catch (Exception exception)
            {
                MelonLogger.Error(exception);
            }
        }
    }
}
