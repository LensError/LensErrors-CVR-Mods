using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.IO.Global;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.UI;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace ContentMenuTweaks
{
    static class RecentCategoryInjector
    {
        internal const string AvatarCategoryKey = "recent-content-categories-avatars";
        internal const string PropCategoryKey = "recent-content-categories-props";
        internal const string WorldCategoryKey = "recent-content-categories-worlds";
        internal const string SeenAvatarCategoryKey = "recent-content-categories-seen-avatars";
        internal const string SeenPropCategoryKey = "recent-content-categories-seen-props";

        const int RecentSortIndex = -100000;

        static readonly FieldInfo CategoriesField = AccessTools.Field(typeof(ViewManager), "_categories");
        static readonly FieldInfo LastAvatarCategoryField = AccessTools.Field(typeof(ViewManager), "_lastAvatarCategory");
        static readonly FieldInfo LastWorldCategoryField = AccessTools.Field(typeof(ViewManager), "_lastWorldCategory");
        static readonly FieldInfo LastSpawnableCategoryField = AccessTools.Field(typeof(ViewManager), "_lastSpawnableCategory");
        static readonly FieldInfo LastAvatarCategoryIsSystemField = AccessTools.Field(typeof(ViewManager), "_lastAvatarCategoryIsSystem");
        static readonly FieldInfo LastWorldCategoryIsSystemField = AccessTools.Field(typeof(ViewManager), "_lastWorldCategoryIsSystem");
        static readonly FieldInfo LastSpawnableCategoryIsSystemField = AccessTools.Field(typeof(ViewManager), "_lastSpawnableCategoryIsSystem");
        static readonly FieldInfo AvatarsPagedField = AccessTools.Field(typeof(ViewManager), "_avatarsPaged");
        static readonly FieldInfo WorldsPagedField = AccessTools.Field(typeof(ViewManager), "_worldsPaged");
        static readonly FieldInfo SpawnablesPagedField = AccessTools.Field(typeof(ViewManager), "_spawnablesPaged");
        static readonly FieldInfo CurrentAvatarPageField = AccessTools.Field(typeof(ViewManager), "_currentAvatarPage");
        static readonly FieldInfo CurrentWorldPageField = AccessTools.Field(typeof(ViewManager), "_currentWorldPage");
        static readonly FieldInfo CurrentSpawnablePageField = AccessTools.Field(typeof(ViewManager), "_currentSpawnablePage");
        static readonly FieldInfo CurrentAvatarPagesField = AccessTools.Field(typeof(ViewManager), "_currentAvatarResultPagesCount");
        static readonly FieldInfo CurrentWorldPagesField = AccessTools.Field(typeof(ViewManager), "_currentWorldResultPagesCount");
        static readonly FieldInfo CurrentSpawnablePagesField = AccessTools.Field(typeof(ViewManager), "_currentSpawnableResultPagesCount");
        static readonly FieldInfo AvatarSortOrderField = AccessTools.Field(typeof(ViewManager), "_avatarSortOrder");
        static readonly FieldInfo WorldSortOrderField = AccessTools.Field(typeof(ViewManager), "_worldSortOrder");
        static readonly FieldInfo PropSortOrderField = AccessTools.Field(typeof(ViewManager), "_propSortOrder");
        static readonly FieldInfo AvatarAscendingField = AccessTools.Field(typeof(ViewManager), "_avatarAscending");
        static readonly FieldInfo WorldAscendingField = AccessTools.Field(typeof(ViewManager), "_worldAscending");
        static readonly FieldInfo PropAscendingField = AccessTools.Field(typeof(ViewManager), "_propAscending");
        static readonly FieldInfo CohtmlViewField = AccessTools.Field(typeof(ABI_RC.Core.UI.UIRework.CVRUIManagerBase), "cohtmlView");

        static bool _dirty;

        internal static void MarkDirty()
        {
            _dirty = true;
        }

        internal static void RefreshCategories()
        {
            var viewManager = ViewManager.Instance;
            if (viewManager == null)
                return;

            InjectCategories(viewManager, pushToUi: true);
        }

        internal static void InjectCategories(ViewManager viewManager, bool pushToUi)
        {
            var categories = CategoriesField.GetValue(viewManager) as Categories_t;
            if (categories == null)
                return;

            bool changed = false;
            changed |= Settings.RecentAvatarsEnabled
                ? EnsureCategory(categories.Avatars, AvatarCategoryKey, "Recently Used", CategoryTypes.Avatars)
                : RemoveCategory(categories.Avatars, AvatarCategoryKey);
            changed |= Settings.RecentPropsEnabled
                ? EnsureCategory(categories.Spawnables, PropCategoryKey, "Recently Spawned", CategoryTypes.Spawnables)
                : RemoveCategory(categories.Spawnables, PropCategoryKey);
            changed |= Settings.RecentWorldsEnabled
                ? EnsureCategory(categories.Worlds, WorldCategoryKey, "Recently Visited", CategoryTypes.Worlds)
                : RemoveCategory(categories.Worlds, WorldCategoryKey);
            changed |= Settings.RecentSeenAvatarsEnabled
                ? EnsureCategory(categories.Avatars, SeenAvatarCategoryKey, "Recently Seen", CategoryTypes.Avatars)
                : RemoveCategory(categories.Avatars, SeenAvatarCategoryKey);
            changed |= Settings.RecentSeenPropsEnabled
                ? EnsureCategory(categories.Spawnables, SeenPropCategoryKey, "Recently Seen", CategoryTypes.Spawnables)
                : RemoveCategory(categories.Spawnables, SeenPropCategoryKey);

            if ((changed || _dirty) && pushToUi)
                TriggerCategories(viewManager, categories);

            _dirty = false;
        }

        internal static bool IsRecentAvatarCategory(string category, bool isSystem)
        {
            return Settings.RecentAvatarsEnabled && isSystem && string.Equals(category, AvatarCategoryKey, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRecentPropCategory(string category, bool isSystem)
        {
            return Settings.RecentPropsEnabled && isSystem && string.Equals(category, PropCategoryKey, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRecentWorldCategory(string category, bool isSystem)
        {
            return Settings.RecentWorldsEnabled && isSystem && string.Equals(category, WorldCategoryKey, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRecentSeenAvatarCategory(string category, bool isSystem)
        {
            return Settings.RecentSeenAvatarsEnabled && isSystem && string.Equals(category, SeenAvatarCategoryKey, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsRecentSeenPropCategory(string category, bool isSystem)
        {
            return Settings.RecentSeenPropsEnabled && isSystem && string.Equals(category, SeenPropCategoryKey, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsViewingRecentAvatars(ViewManager viewManager)
        {
            return IsRecentAvatarCategory(LastAvatarCategoryField.GetValue(viewManager) as string, (bool)LastAvatarCategoryIsSystemField.GetValue(viewManager));
        }

        internal static bool IsViewingRecentProps(ViewManager viewManager)
        {
            return IsRecentPropCategory(LastSpawnableCategoryField.GetValue(viewManager) as string, (bool)LastSpawnableCategoryIsSystemField.GetValue(viewManager));
        }

        internal static bool IsViewingRecentWorlds(ViewManager viewManager)
        {
            return IsRecentWorldCategory(LastWorldCategoryField.GetValue(viewManager) as string, (bool)LastWorldCategoryIsSystemField.GetValue(viewManager));
        }

        internal static bool IsViewingRecentSeenAvatars(ViewManager viewManager)
        {
            return IsRecentSeenAvatarCategory(LastAvatarCategoryField.GetValue(viewManager) as string, (bool)LastAvatarCategoryIsSystemField.GetValue(viewManager));
        }

        internal static bool IsViewingRecentSeenProps(ViewManager viewManager)
        {
            return IsRecentSeenPropCategory(LastSpawnableCategoryField.GetValue(viewManager) as string, (bool)LastSpawnableCategoryIsSystemField.GetValue(viewManager));
        }

        internal static void LoadRecentAvatars(ViewManager viewManager)
        {
            if (!Settings.RecentAvatarsEnabled)
                return;

            LastAvatarCategoryField.SetValue(viewManager, AvatarCategoryKey);
            LastAvatarCategoryIsSystemField.SetValue(viewManager, true);
            RecentContentResolver.ResolveAvatars(viewManager);

            var avatars = new List<Avatar_t>();
            for (int i = 0; i < Settings.Avatars.Count; i++)
            {
                RecentEntry entry = Settings.Avatars[i];
                avatars.Add(new Avatar_t
                {
                    AvatarId = entry.Id,
                    AvatarName = entry.Name,
                    AvatarDesc = "Recently used avatar",
                    AvatarImageUrl = entry.ImageUrl,
                    AvatarImageCoui = string.Empty
                });
            }

            AvatarsPagedField.SetValue(viewManager, avatars);
            CurrentAvatarPageField.SetValue(viewManager, (uint)0);
            CurrentAvatarPagesField.SetValue(viewManager, (uint)0);
            TriggerPaged(viewManager, "LoadAvatarsPaged", avatars, AvatarSortOrderField, AvatarAscendingField);
        }

        internal static void LoadRecentProps(ViewManager viewManager)
        {
            if (!Settings.RecentPropsEnabled)
                return;

            LastSpawnableCategoryField.SetValue(viewManager, PropCategoryKey);
            LastSpawnableCategoryIsSystemField.SetValue(viewManager, true);
            RecentContentResolver.ResolveProps(viewManager);

            var props = new List<Spawnable_t>();
            for (int i = 0; i < Settings.Props.Count; i++)
            {
                RecentEntry entry = Settings.Props[i];
                props.Add(new Spawnable_t
                {
                    SpawnableId = entry.Id,
                    SpawnableName = entry.Name,
                    SpawnableImageUrl = entry.ImageUrl,
                    SpawnableImageCoui = string.Empty
                });
            }

            SpawnablesPagedField.SetValue(viewManager, props);
            CurrentSpawnablePageField.SetValue(viewManager, (uint)0);
            CurrentSpawnablePagesField.SetValue(viewManager, (uint)0);
            TriggerPaged(viewManager, "LoadSpawnablesPaged", props, PropSortOrderField, PropAscendingField);
        }

        internal static void LoadRecentWorlds(ViewManager viewManager)
        {
            if (!Settings.RecentWorldsEnabled)
                return;

            LastWorldCategoryField.SetValue(viewManager, WorldCategoryKey);
            LastWorldCategoryIsSystemField.SetValue(viewManager, true);
            RecentContentResolver.ResolveWorlds(viewManager);

            var worlds = new List<World_t>();
            for (int i = 0; i < Settings.Worlds.Count; i++)
            {
                RecentEntry entry = Settings.Worlds[i];
                worlds.Add(new World_t
                {
                    WorldId = entry.Id,
                    WorldName = entry.Name,
                    WorldImageUrl = entry.ImageUrl,
                    WorldImageCoui = string.Empty,
                    UsersInPublic = 0
                });
            }

            WorldsPagedField.SetValue(viewManager, worlds);
            CurrentWorldPageField.SetValue(viewManager, (uint)0);
            CurrentWorldPagesField.SetValue(viewManager, (uint)0);
            TriggerPaged(viewManager, "LoadWorldsPaged", worlds, WorldSortOrderField, WorldAscendingField);
        }

        static bool EnsureCategory(List<Category_t> list, string key, string name, CategoryTypes parent)
        {
            if (list == null)
                return false;

            bool found = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(list[i].CategoryKey, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!found)
                {
                    list[i].CategoryClearTextName = name;
                    list[i].CategorySortingIndex = RecentSortIndex;
                    list[i].CategoryParent = parent;
                    list[i].IsSystemCategory = true;
                    found = true;
                    continue;
                }

                list.RemoveAt(i);
            }

            if (!found)
            {
                list.Insert(0, new Category_t
                {
                    CategoryKey = key,
                    CategoryClearTextName = name,
                    CategorySortingIndex = RecentSortIndex,
                    CategoryParent = parent,
                    IsSystemCategory = true
                });
                return true;
            }

            int index = list.FindIndex(category => string.Equals(category.CategoryKey, key, StringComparison.OrdinalIgnoreCase));
            if (index > 0)
            {
                Category_t category = list[index];
                list.RemoveAt(index);
                list.Insert(0, category);
                return true;
            }

            return false;
        }

        static bool RemoveCategory(List<Category_t> list, string key)
        {
            if (list == null)
                return false;

            bool removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(list[i].CategoryKey, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                list.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        static void TriggerCategories(ViewManager viewManager, Categories_t categories)
        {
            CohtmlControlledView cohtmlView = GetCohtmlView(viewManager);
            if (cohtmlView == null || cohtmlView.View == null)
                return;

            cohtmlView.View.TriggerEvent("LoadCategories", categories);
        }

        static void TriggerPaged<T>(ViewManager viewManager, string eventName, List<T> entries, FieldInfo sortField, FieldInfo ascendingField)
        {
            CohtmlControlledView cohtmlView = GetCohtmlView(viewManager);
            if (cohtmlView == null || cohtmlView.View == null)
                return;

            cohtmlView.View.TriggerEvent(eventName, entries, (uint)0, (uint)0, sortField.GetValue(viewManager), (bool)ascendingField.GetValue(viewManager));
        }

        internal static void LoadRecentSeenAvatars(ViewManager viewManager)
        {
            if (!Settings.RecentSeenAvatarsEnabled)
                return;

            LastAvatarCategoryField.SetValue(viewManager, SeenAvatarCategoryKey);
            LastAvatarCategoryIsSystemField.SetValue(viewManager, true);
            RecentContentResolver.ResolveSeenAvatars(viewManager);

            var avatars = new List<Avatar_t>();
            for (int i = 0; i < Settings.SeenAvatars.Count; i++)
            {
                RecentEntry entry = Settings.SeenAvatars[i];
                if (Settings.SeenAvatarsHidePrivate && entry.IsPublic == false)
                    continue;
                avatars.Add(new Avatar_t
                {
                    AvatarId = entry.Id,
                    AvatarName = entry.Name,
                    AvatarDesc = "Recently seen avatar",
                    AvatarImageUrl = entry.ImageUrl,
                    AvatarImageCoui = string.Empty
                });
            }

            AvatarsPagedField.SetValue(viewManager, avatars);
            CurrentAvatarPageField.SetValue(viewManager, (uint)0);
            CurrentAvatarPagesField.SetValue(viewManager, (uint)0);
            TriggerPaged(viewManager, "LoadAvatarsPaged", avatars, AvatarSortOrderField, AvatarAscendingField);
        }

        internal static void LoadRecentSeenProps(ViewManager viewManager)
        {
            if (!Settings.RecentSeenPropsEnabled)
                return;

            LastSpawnableCategoryField.SetValue(viewManager, SeenPropCategoryKey);
            LastSpawnableCategoryIsSystemField.SetValue(viewManager, true);
            RecentContentResolver.ResolveSeenProps(viewManager);

            var props = new List<Spawnable_t>();
            for (int i = 0; i < Settings.SeenProps.Count; i++)
            {
                RecentEntry entry = Settings.SeenProps[i];
                if (Settings.SeenPropsHidePrivate && entry.IsPublic == false)
                    continue;
                props.Add(new Spawnable_t
                {
                    SpawnableId = entry.Id,
                    SpawnableName = entry.Name,
                    SpawnableImageUrl = entry.ImageUrl,
                    SpawnableImageCoui = string.Empty
                });
            }

            SpawnablesPagedField.SetValue(viewManager, props);
            CurrentSpawnablePageField.SetValue(viewManager, (uint)0);
            CurrentSpawnablePagesField.SetValue(viewManager, (uint)0);
            TriggerPaged(viewManager, "LoadSpawnablesPaged", props, PropSortOrderField, PropAscendingField);
        }

        internal static CohtmlControlledView GetCohtmlView(ViewManager viewManager)
        {
            return CohtmlViewField == null ? null : CohtmlViewField.GetValue(viewManager) as CohtmlControlledView;
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.RequestCategories))]
    static class RequestCategoriesPatch
    {
        static void Postfix(ViewManager __instance)
        {
            try
            {
                RecentCategoryInjector.InjectCategories(__instance, pushToUi: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.RequestCategoriesTask))]
    static class RequestCategoriesTaskPatch
    {
        static void Postfix(ViewManager __instance, ref Task __result)
        {
            __result = AwaitAndInject(__instance, __result);
        }

        static async Task AwaitAndInject(ViewManager viewManager, Task original)
        {
            await original;
            RecentCategoryInjector.InjectCategories(viewManager, pushToUi: false);
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.GetFilteredAvatarsPaged))]
    static class GetFilteredAvatarsPagedPatch
    {
        static bool Prefix(ViewManager __instance, string category, bool isSystem)
        {
            if (!RecentCategoryInjector.IsRecentAvatarCategory(category, isSystem))
                return true;

            try
            {
                RecentCategoryInjector.LoadRecentAvatars(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.GetFilteredSpawnablePaged))]
    static class GetFilteredSpawnablePagedPatch
    {
        static bool Prefix(ViewManager __instance, string category, bool isSystem)
        {
            if (!RecentCategoryInjector.IsRecentPropCategory(category, isSystem))
                return true;

            try
            {
                RecentCategoryInjector.LoadRecentProps(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.GetFilteredWorldsPaged))]
    static class GetFilteredWorldsPagedPatch
    {
        static bool Prefix(ViewManager __instance, string category, bool isSystem)
        {
            if (!RecentCategoryInjector.IsRecentWorldCategory(category, isSystem))
                return true;

            try
            {
                RecentCategoryInjector.LoadRecentWorlds(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.GetFilteredAvatarsPaged))]
    static class GetFilteredSeenAvatarsPagedPatch
    {
        static bool Prefix(ViewManager __instance, string category, bool isSystem)
        {
            if (!RecentCategoryInjector.IsRecentSeenAvatarCategory(category, isSystem))
                return true;

            try
            {
                RecentCategoryInjector.LoadRecentSeenAvatars(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.GetFilteredSpawnablePaged))]
    static class GetFilteredSeenSpawnablePagedPatch
    {
        static bool Prefix(ViewManager __instance, string category, bool isSystem)
        {
            if (!RecentCategoryInjector.IsRecentSeenPropCategory(category, isSystem))
                return true;

            try
            {
                RecentCategoryInjector.LoadRecentSeenProps(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }

            return false;
        }
    }
}
