using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.API.Responses.DetailsV2;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace ContentMenuTweaks
{
    static class RecentContentResolver
    {
        const int ResolveFrameDelay = 12;
        static readonly TimeSpan FailedResolveRetryDelay = TimeSpan.FromMinutes(10);

        static bool _resolvingAvatars;
        static bool _resolvingProps;
        static bool _resolvingWorlds;
        static bool _resolvingSeenAvatars;
        static bool _resolvingSeenProps;
        static readonly Dictionary<string, DateTime> AvatarRetryAfter = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, DateTime> PropRetryAfter = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, DateTime> WorldRetryAfter = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        static readonly FieldInfo AvatarDetailsField = AccessTools.Field(typeof(ViewManager), "_avatarDetails");
        static readonly FieldInfo PropDetailsField = AccessTools.Field(typeof(ViewManager), "_propDetails");
        static readonly FieldInfo WorldDetailsField = AccessTools.Field(typeof(ViewManager), "_worldDetails");

        internal static void ResolveAvatars(ViewManager viewManager)
        {
            if (_resolvingAvatars || !HasUnresolved(Settings.Avatars))
                return;

            MelonCoroutines.Start(ResolveAvatarsCoroutine(viewManager));
        }

        internal static void ResolveProps(ViewManager viewManager)
        {
            if (_resolvingProps || !HasUnresolved(Settings.Props))
                return;

            MelonCoroutines.Start(ResolvePropsCoroutine(viewManager));
        }

        internal static void ResolveWorlds(ViewManager viewManager)
        {
            if (_resolvingWorlds || !HasUnresolved(Settings.Worlds))
                return;

            MelonCoroutines.Start(ResolveWorldsCoroutine(viewManager));
        }

        internal static void ResolveSeenAvatars(ViewManager viewManager)
        {
            if (_resolvingSeenAvatars || !HasUnresolved(Settings.SeenAvatars))
                return;

            MelonCoroutines.Start(ResolveSeenAvatarsCoroutine(viewManager));
        }

        internal static void ResolveSeenProps(ViewManager viewManager)
        {
            if (_resolvingSeenProps || !HasUnresolved(Settings.SeenProps))
                return;

            MelonCoroutines.Start(ResolveSeenPropsCoroutine(viewManager));
        }

        static IEnumerator ResolveAvatarsCoroutine(ViewManager viewManager)
        {
            _resolvingAvatars = true;
            try
            {
                for (int i = 0; i < Settings.Avatars.Count; i++)
                {
                    RecentEntry entry = Settings.Avatars[i];
                    if (!NeedsResolve(entry))
                        continue;

                    bool changed = false;
                    ContentAvatarResponse cached;
                    if (!ContentAvatarResponse.Cache.TryGetValue(entry.Id, out cached))
                    {
                        if (IsResolveCoolingDown(AvatarRetryAfter, entry.Id))
                            continue;

                        Task task = viewManager.RequestAvatarDetailsPageTask(entry.Id);
                        while (!task.IsCompleted)
                            yield return null;

                        if (task.IsFaulted)
                        {
                            MelonLogger.Warning("Failed to resolve recent avatar " + Settings.ShortId(entry.Id));
                            MarkResolveFailed(AvatarRetryAfter, entry.Id);
                        }

                        ContentAvatarResponse.Cache.TryGetValue(entry.Id, out cached);
                    }

                    bool resolved = false;
                    if (cached != null)
                    {
                        changed |= Settings.UpdateAvatar(entry.Id, cached.Name, UriToString(cached.Image));
                        resolved = true;
                    }
                    else
                    {
                        var details = AvatarDetailsField.GetValue(viewManager) as AvatarDetails_t;
                        if (details != null && string.Equals(Settings.NormalizeId(details.AvatarId), entry.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            changed |= Settings.UpdateAvatar(entry.Id, details.AvatarName, details.AvatarImageUrl);
                            resolved = true;
                        }
                    }

                    if (resolved)
                        ClearResolveFailure(AvatarRetryAfter, entry.Id);
                    else
                        MarkResolveFailed(AvatarRetryAfter, entry.Id);

                    if (changed && viewManager != null && RecentCategoryInjector.IsViewingRecentAvatars(viewManager))
                        RecentCategoryInjector.LoadRecentAvatars(viewManager);

                    yield return DelayNextResolve();
                }
            }
            finally
            {
                _resolvingAvatars = false;
            }
        }

        static IEnumerator ResolvePropsCoroutine(ViewManager viewManager)
        {
            _resolvingProps = true;
            try
            {
                for (int i = 0; i < Settings.Props.Count; i++)
                {
                    RecentEntry entry = Settings.Props[i];
                    if (!NeedsResolve(entry))
                        continue;

                    bool changed = false;
                    ContentSpawnableResponse cached;
                    if (!ContentSpawnableResponse.Cache.TryGetValue(entry.Id, out cached))
                    {
                        if (IsResolveCoolingDown(PropRetryAfter, entry.Id))
                            continue;

                        Task task = viewManager.GetPropDetailsTask(entry.Id);
                        while (!task.IsCompleted)
                            yield return null;

                        if (task.IsFaulted)
                        {
                            MelonLogger.Warning("Failed to resolve recent prop " + Settings.ShortId(entry.Id));
                            MarkResolveFailed(PropRetryAfter, entry.Id);
                        }

                        ContentSpawnableResponse.Cache.TryGetValue(entry.Id, out cached);
                    }

                    bool resolved = false;
                    if (cached != null)
                    {
                        changed |= Settings.UpdateProp(entry.Id, cached.Name, UriToString(cached.Image));
                        resolved = true;
                    }
                    else
                    {
                        var details = PropDetailsField.GetValue(viewManager) as SpawnableDetail_t;
                        if (details != null && string.Equals(Settings.NormalizeId(details.SpawnableId), entry.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            changed |= Settings.UpdateProp(entry.Id, details.SpawnableName, details.SpawnableImageUrl);
                            resolved = true;
                        }
                    }

                    if (resolved)
                        ClearResolveFailure(PropRetryAfter, entry.Id);
                    else
                        MarkResolveFailed(PropRetryAfter, entry.Id);

                    if (changed && viewManager != null && RecentCategoryInjector.IsViewingRecentProps(viewManager))
                        RecentCategoryInjector.LoadRecentProps(viewManager);

                    yield return DelayNextResolve();
                }
            }
            finally
            {
                _resolvingProps = false;
            }
        }

        static IEnumerator ResolveWorldsCoroutine(ViewManager viewManager)
        {
            _resolvingWorlds = true;
            try
            {
                for (int i = 0; i < Settings.Worlds.Count; i++)
                {
                    RecentEntry entry = Settings.Worlds[i];
                    if (!NeedsResolve(entry))
                        continue;

                    bool changed = false;
                    ContentWorldResponse cached;
                    if (!ContentWorldResponse.Cache.TryGetValue(entry.Id, out cached))
                    {
                        if (IsResolveCoolingDown(WorldRetryAfter, entry.Id))
                            continue;

                        Task task = viewManager.GetWorldDetailsTask(entry.Id);
                        while (!task.IsCompleted)
                            yield return null;

                        if (task.IsFaulted)
                        {
                            MelonLogger.Warning("Failed to resolve recent world " + Settings.ShortId(entry.Id));
                            MarkResolveFailed(WorldRetryAfter, entry.Id);
                        }

                        ContentWorldResponse.Cache.TryGetValue(entry.Id, out cached);
                    }

                    bool resolved = false;
                    if (cached != null)
                    {
                        changed |= Settings.UpdateWorld(entry.Id, cached.Name, UriToString(cached.Image));
                        resolved = true;
                    }
                    else
                    {
                        var details = WorldDetailsField.GetValue(viewManager) as WorldDetails_t;
                        if (details != null && string.Equals(Settings.NormalizeId(details.WorldId), entry.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            changed |= Settings.UpdateWorld(entry.Id, details.WorldName, details.WorldImageUrl);
                            resolved = true;
                        }
                    }

                    if (resolved)
                        ClearResolveFailure(WorldRetryAfter, entry.Id);
                    else
                        MarkResolveFailed(WorldRetryAfter, entry.Id);

                    if (changed && viewManager != null && RecentCategoryInjector.IsViewingRecentWorlds(viewManager))
                        RecentCategoryInjector.LoadRecentWorlds(viewManager);

                    yield return DelayNextResolve();
                }
            }
            finally
            {
                _resolvingWorlds = false;
            }
        }

        static IEnumerator ResolveSeenAvatarsCoroutine(ViewManager viewManager)
        {
            _resolvingSeenAvatars = true;
            try
            {
                for (int i = 0; i < Settings.SeenAvatars.Count; i++)
                {
                    RecentEntry entry = Settings.SeenAvatars[i];
                    if (!NeedsResolve(entry))
                        continue;

                    bool changed = false;
                    ContentAvatarResponse cached;
                    if (!ContentAvatarResponse.Cache.TryGetValue(entry.Id, out cached))
                    {
                        if (IsResolveCoolingDown(AvatarRetryAfter, entry.Id))
                            continue;

                        Task task = viewManager.RequestAvatarDetailsPageTask(entry.Id);
                        while (!task.IsCompleted)
                            yield return null;

                        if (task.IsFaulted)
                        {
                            MelonLogger.Warning("Failed to resolve seen avatar " + Settings.ShortId(entry.Id));
                            MarkResolveFailed(AvatarRetryAfter, entry.Id);
                        }

                        ContentAvatarResponse.Cache.TryGetValue(entry.Id, out cached);
                    }

                    bool resolved = false;
                    if (cached != null)
                    {
                        changed |= Settings.UpdateSeenAvatar(entry.Id, cached.Name, UriToString(cached.Image));
                        changed |= Settings.UpdateSeenAvatarPublic(entry.Id, cached.Public);
                        resolved = true;
                    }
                    else
                    {
                        var details = AvatarDetailsField.GetValue(viewManager) as AvatarDetails_t;
                        if (details != null && string.Equals(Settings.NormalizeId(details.AvatarId), entry.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            changed |= Settings.UpdateSeenAvatar(entry.Id, details.AvatarName, details.AvatarImageUrl);
                            changed |= Settings.UpdateSeenAvatarPublic(entry.Id, details.IsPublic);
                            resolved = true;
                        }
                    }

                    if (resolved)
                        ClearResolveFailure(AvatarRetryAfter, entry.Id);
                    else
                        MarkResolveFailed(AvatarRetryAfter, entry.Id);

                    if (changed && viewManager != null && RecentCategoryInjector.IsViewingRecentSeenAvatars(viewManager))
                        RecentCategoryInjector.LoadRecentSeenAvatars(viewManager);

                    yield return DelayNextResolve();
                }
            }
            finally
            {
                _resolvingSeenAvatars = false;
            }
        }

        static IEnumerator ResolveSeenPropsCoroutine(ViewManager viewManager)
        {
            _resolvingSeenProps = true;
            try
            {
                for (int i = 0; i < Settings.SeenProps.Count; i++)
                {
                    RecentEntry entry = Settings.SeenProps[i];
                    if (!NeedsResolve(entry))
                        continue;

                    bool changed = false;
                    ContentSpawnableResponse cached;
                    if (!ContentSpawnableResponse.Cache.TryGetValue(entry.Id, out cached))
                    {
                        if (IsResolveCoolingDown(PropRetryAfter, entry.Id))
                            continue;

                        Task task = viewManager.GetPropDetailsTask(entry.Id);
                        while (!task.IsCompleted)
                            yield return null;

                        if (task.IsFaulted)
                        {
                            MelonLogger.Warning("Failed to resolve seen prop " + Settings.ShortId(entry.Id));
                            MarkResolveFailed(PropRetryAfter, entry.Id);
                        }

                        ContentSpawnableResponse.Cache.TryGetValue(entry.Id, out cached);
                    }

                    bool resolved = false;
                    if (cached != null)
                    {
                        changed |= Settings.UpdateSeenProp(entry.Id, cached.Name, UriToString(cached.Image));
                        changed |= Settings.UpdateSeenPropPublic(entry.Id, cached.Public);
                        resolved = true;
                    }
                    else
                    {
                        var details = PropDetailsField.GetValue(viewManager) as SpawnableDetail_t;
                        if (details != null && string.Equals(Settings.NormalizeId(details.SpawnableId), entry.Id, StringComparison.OrdinalIgnoreCase))
                        {
                            changed |= Settings.UpdateSeenProp(entry.Id, details.SpawnableName, details.SpawnableImageUrl);
                            changed |= Settings.UpdateSeenPropPublic(entry.Id, details.IsPublic);
                            resolved = true;
                        }
                    }

                    if (resolved)
                        ClearResolveFailure(PropRetryAfter, entry.Id);
                    else
                        MarkResolveFailed(PropRetryAfter, entry.Id);

                    if (changed && viewManager != null && RecentCategoryInjector.IsViewingRecentSeenProps(viewManager))
                        RecentCategoryInjector.LoadRecentSeenProps(viewManager);

                    yield return DelayNextResolve();
                }
            }
            finally
            {
                _resolvingSeenProps = false;
            }
        }

        static bool HasUnresolved(System.Collections.Generic.List<RecentEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (NeedsResolve(entries[i]))
                    return true;
            }

            return false;
        }

        static bool NeedsResolve(RecentEntry entry)
        {
            return Settings.IsPlaceholderName(entry) || string.IsNullOrWhiteSpace(entry.ImageUrl);
        }

        static bool IsResolveCoolingDown(Dictionary<string, DateTime> retryAfter, string id)
        {
            DateTime nextTry;
            if (!retryAfter.TryGetValue(id, out nextTry))
                return false;

            if (DateTime.UtcNow < nextTry)
                return true;

            retryAfter.Remove(id);
            return false;
        }

        static void MarkResolveFailed(Dictionary<string, DateTime> retryAfter, string id)
        {
            retryAfter[id] = DateTime.UtcNow.Add(FailedResolveRetryDelay);
        }

        static void ClearResolveFailure(Dictionary<string, DateTime> retryAfter, string id)
        {
            retryAfter.Remove(id);
        }

        static IEnumerator DelayNextResolve()
        {
            for (int i = 0; i < ResolveFrameDelay; i++)
                yield return null;
        }

        static string UriToString(Uri uri)
        {
            return uri == null ? string.Empty : uri.ToString();
        }
    }
}
