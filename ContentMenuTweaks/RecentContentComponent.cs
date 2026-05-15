using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.UI;
using cohtml.Net;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ContentMenuTweaks
{
    [DisallowMultipleComponent]
    class RecentContentComponent : MonoBehaviour
    {
        static readonly FieldInfo InternalViewField = typeof(CohtmlControlledViewWrapper).GetField("_view", BindingFlags.Instance | BindingFlags.NonPublic);

        static View GetInternalView(CohtmlControlledViewWrapper wrapper) =>
            InternalViewField?.GetValue(wrapper) as View;

        CohtmlControlledView _cohtmlView;
        bool _destroyed;
        float _nextCheckTime;

        void Awake()
        {
            MelonLoader.MelonCoroutines.Start(WaitForCohtmlView());
        }

        void Update()
        {
            if (Time.unscaledTime < _nextCheckTime)
                return;

            _nextCheckTime = Time.unscaledTime + 2f;

            try
            {
                var viewManager = ViewManager.Instance;
                if (viewManager != null)
                    RecentCategoryInjector.InjectCategories(viewManager, pushToUi: false);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error(ex);
            }
        }

        void OnDestroy()
        {
            _destroyed = true;
            try
            {
                if (_cohtmlView?.Listener == null)
                    return;

                _cohtmlView.Listener.ReadyForBindings -= OnReadyForBindings;
                _cohtmlView.Listener.FinishLoad -= OnFinishLoad;
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error(ex);
            }
        }

        IEnumerator WaitForCohtmlView()
        {
            while (!_destroyed && ViewManager.Instance == null)
                yield return null;

            if (_destroyed)
                yield break;

            var cv = RecentCategoryInjector.GetCohtmlView(ViewManager.Instance);
            while (!_destroyed && cv == null)
            {
                yield return null;
                cv = RecentCategoryInjector.GetCohtmlView(ViewManager.Instance);
            }

            while (!_destroyed && cv.Listener == null)
                yield return null;

            if (_destroyed)
                yield break;

            _cohtmlView = cv;
            cv.Listener.ReadyForBindings += OnReadyForBindings;
            cv.Listener.FinishLoad += OnFinishLoad;

            if (cv.FinishedLoading)
            {
                OnReadyForBindings();
                OnFinishLoad(null);
            }
        }

        void OnReadyForBindings()
        {
            try
            {
                var cv = RecentCategoryInjector.GetCohtmlView(ViewManager.Instance);
                cv?.View.BindCall("CMT_ClearCategory", new Action<string>(OnClearCategory));
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error(ex);
            }
        }

        void OnFinishLoad(string url)
        {
            try
            {
                var cv = RecentCategoryInjector.GetCohtmlView(ViewManager.Instance);
                if (cv?.View != null)
                    GetInternalView(cv.View)?.ExecuteScript(ClearButtonScript);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error(ex);
            }
        }

        void OnClearCategory(string categoryKey)
        {
            try
            {
                var vm = ViewManager.Instance;
                if (vm == null) return;

                switch (categoryKey)
                {
                    case RecentCategoryInjector.AvatarCategoryKey:
                        Settings.ClearAvatars();
                        RecentCategoryInjector.LoadRecentAvatars(vm);
                        break;
                    case RecentCategoryInjector.PropCategoryKey:
                        Settings.ClearProps();
                        RecentCategoryInjector.LoadRecentProps(vm);
                        break;
                    case RecentCategoryInjector.WorldCategoryKey:
                        Settings.ClearWorlds();
                        RecentCategoryInjector.LoadRecentWorlds(vm);
                        break;
                    case RecentCategoryInjector.SeenAvatarCategoryKey:
                        Settings.ClearSeenAvatars();
                        RecentCategoryInjector.LoadRecentSeenAvatars(vm);
                        break;
                    case RecentCategoryInjector.SeenPropCategoryKey:
                        Settings.ClearSeenProps();
                        RecentCategoryInjector.LoadRecentSeenProps(vm);
                        break;
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error(ex);
            }
        }

        const string ClearButtonScript = @"(function() {
    const RECENT_KEYS = {
        'recent-content-categories-avatars': true,
        'recent-content-categories-seen-avatars': true,
        'recent-content-categories-props': true,
        'recent-content-categories-seen-props': true,
        'recent-content-categories-worlds': true
    };

    function currentKey(sectionId) {
        if (sectionId === 'avatars') return avatarCategory;
        if (sectionId === 'worlds') return worldCategory;
        if (sectionId === 'props') return propCategory;
        return '';
    }

    function currentIsSystem(sectionId) {
        if (sectionId === 'avatars') return avatarCategoryIsSystem;
        if (sectionId === 'worlds') return worldCategoryIsSystem;
        if (sectionId === 'props') return propCategoryIsSystem;
        return false;
    }

    function isRecentSection(sectionId) {
        const key = currentKey(sectionId);
        return !!currentIsSystem(sectionId) && !!RECENT_KEYS[key];
    }

    function injectClearBtn(sectionId) {
        const section = document.getElementById(sectionId);
        if (!section) return;

        let clearBtn = document.getElementById('cmt-clear-' + sectionId);
        if (!clearBtn) {
            const host = section.querySelector('.list-filter .border');
            if (!host) return;

            clearBtn = document.createElement('div');
            clearBtn.id = 'cmt-clear-' + sectionId;
            clearBtn.className = 'content-btn button color-primary cmt-clear-button';
            clearBtn.textContent = 'Clear';
            clearBtn.style.display = 'none';
            clearBtn.setAttribute('data-tooltip', 'Clear this local Content Menu Tweaks history');
            clearBtn.addEventListener('click', function() {
                const key = currentKey(sectionId);
                if (RECENT_KEYS[key])
                    engine.call('CMT_ClearCategory', key);
            });
            host.appendChild(clearBtn);
        }

        clearBtn.style.display = isRecentSection(sectionId) ? '' : 'none';
    }

    function updateClearButtons() {
        injectClearBtn('avatars');
        injectClearBtn('worlds');
        injectClearBtn('props');
    }

    if (!document.getElementById('cmt-clear-button-style')) {
        const style = document.createElement('style');
        style.id = 'cmt-clear-button-style';
        style.textContent = '.list-filter .border .cmt-clear-button{position:relative;margin-top:.5em;}';
        document.head.appendChild(style);
    }

    if (!window._cmtClearButtonsInjected) {
        window._cmtClearButtonsInjected = true;

        const originalFilterContent = filterContent;
        filterContent = function(ident, filter, isSystemCategory) {
            originalFilterContent(ident, filter, isSystemCategory);
            updateClearButtons();
        };

        engine.on('LoadCategories', updateClearButtons);
        engine.on('LoadAvatarsPaged', updateClearButtons);
        engine.on('LoadWorldsPaged', updateClearButtons);
        engine.on('LoadSpawnablesPaged', updateClearButtons);

        if (typeof MutationObserver !== 'undefined') {
            const observer = new MutationObserver(updateClearButtons);
            observer.observe(document.body, { childList: true, subtree: true });
        }
    }

    updateClearButtons();
})();";
    }
}
