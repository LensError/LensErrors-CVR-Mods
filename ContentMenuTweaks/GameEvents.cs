using ABI.CCK.Components;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Player;
using ABI_RC.Core.PropManagement;
using ABI_RC.Core.Networking.IO.Instancing;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
using ABI_RC.Systems.GameEventSystem;
using HarmonyLib;
using MelonLoader;
using System;
using System.Reflection;

namespace ContentMenuTweaks
{
    static class GameEvents
    {
        static readonly FieldInfo AvatarDetailsField = typeof(ViewManager).GetField("_avatarDetails", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo PropDetailsField = typeof(ViewManager).GetField("_propDetails", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo WorldDetailsField = typeof(ViewManager).GetField("_worldDetails", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void Init(HarmonyLib.Harmony harmony)
        {
            try
            {
                harmony.PatchAll(typeof(GameEvents).Assembly);
                CVRGameEventSystem.Avatar.OnLocalAvatarLoad.AddListener(OnLocalAvatarLoad);
                CVRGameEventSystem.Avatar.OnRemoteAvatarLoad.AddListener(OnRemoteAvatarLoad);
                CVRGameEventSystem.Spawnable.OnPropSpawned.AddListener(OnPropSpawned);
                CVRGameEventSystem.World.OnLoad.AddListener(OnWorldLoad);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        internal static void Deinit()
        {
            try
            {
                CVRGameEventSystem.Avatar.OnLocalAvatarLoad.RemoveListener(OnLocalAvatarLoad);
                CVRGameEventSystem.Avatar.OnRemoteAvatarLoad.RemoveListener(OnRemoteAvatarLoad);
                CVRGameEventSystem.Spawnable.OnPropSpawned.RemoveListener(OnPropSpawned);
                CVRGameEventSystem.World.OnLoad.RemoveListener(OnWorldLoad);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static void OnLocalAvatarLoad(CVRAvatar avatar)
        {
            try
            {
                if (!Settings.RecentAvatarsEnabled)
                    return;

                string id = avatar != null && avatar.AssetInfo != null ? avatar.AssetInfo.objectId : null;
                if (string.IsNullOrEmpty(Settings.NormalizeId(id)) && MetaPort.Instance != null)
                    id = MetaPort.Instance.currentAvatarGuid;

                string name = avatar != null ? avatar.name : string.Empty;
                string image = string.Empty;

                var details = GetCurrentAvatarDetails();
                if (details != null && string.Equals(Settings.NormalizeId(details.AvatarId), Settings.NormalizeId(id), StringComparison.OrdinalIgnoreCase))
                {
                    name = details.AvatarName;
                    image = details.AvatarImageUrl;
                }

                Settings.AddAvatar(id, name, image);
                RecentCategoryInjector.MarkDirty();
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static void OnRemoteAvatarLoad(CVRPlayerEntity entity, CVRAvatar avatar)
        {
            try
            {
                if (!Settings.RecentSeenAvatarsEnabled)
                    return;

                string id = avatar != null && avatar.AssetInfo != null ? avatar.AssetInfo.objectId : null;
                if (string.IsNullOrEmpty(Settings.NormalizeId(id)) && entity != null)
                    id = entity.ContentMetadata != null ? entity.ContentMetadata.AssetId : null;

                if (string.IsNullOrEmpty(Settings.NormalizeId(id)))
                    return;

                Settings.AddSeenAvatar(id, string.Empty, string.Empty);
                RecentCategoryInjector.MarkDirty();
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static void OnPropSpawned(string _, PropSoul prop)
        {
            try
            {
                if (prop == null || MetaPort.Instance == null)
                    return;

                if (prop.SpawnedBy == MetaPort.Instance.ownerId)
                {
                    if (!Settings.RecentPropsEnabled)
                        return;

                    string id = prop.ObjectId;
                    string name = string.Empty;
                    string image = string.Empty;

                    var details = GetCurrentPropDetails();
                    if (details != null && string.Equals(Settings.NormalizeId(details.SpawnableId), Settings.NormalizeId(id), StringComparison.OrdinalIgnoreCase))
                    {
                        name = details.SpawnableName;
                        image = details.SpawnableImageUrl;
                    }

                    Settings.AddProp(id, name, image);
                    RecentCategoryInjector.MarkDirty();
                }
                else
                {
                    if (!Settings.RecentSeenPropsEnabled)
                        return;

                    if (string.IsNullOrEmpty(Settings.NormalizeId(prop.ObjectId)))
                        return;

                    Settings.AddSeenProp(prop.ObjectId, string.Empty, string.Empty);
                    RecentCategoryInjector.MarkDirty();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static void OnWorldLoad(string worldId)
        {
            try
            {
                if (!Settings.RecentWorldsEnabled)
                    return;

                if (string.IsNullOrEmpty(Settings.NormalizeId(worldId)))
                    worldId = Instances.CurrentWorldId;

                string name = MetaPort.Instance != null ? MetaPort.Instance.CurrentWorldName : string.Empty;
                string image = string.Empty;

                var details = GetCurrentWorldDetails();
                if (details != null && string.Equals(Settings.NormalizeId(details.WorldId), Settings.NormalizeId(worldId), StringComparison.OrdinalIgnoreCase))
                {
                    name = details.WorldName;
                    image = details.WorldImageUrl;
                }

                Settings.AddWorld(worldId, name, image);
                RecentCategoryInjector.MarkDirty();
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static AvatarDetails_t GetCurrentAvatarDetails()
        {
            var viewManager = ViewManager.Instance;
            return viewManager == null || AvatarDetailsField == null ? null : AvatarDetailsField.GetValue(viewManager) as AvatarDetails_t;
        }

        static SpawnableDetail_t GetCurrentPropDetails()
        {
            var viewManager = ViewManager.Instance;
            return viewManager == null || PropDetailsField == null ? null : PropDetailsField.GetValue(viewManager) as SpawnableDetail_t;
        }

        static WorldDetails_t GetCurrentWorldDetails()
        {
            var viewManager = ViewManager.Instance;
            return viewManager == null || WorldDetailsField == null ? null : WorldDetailsField.GetValue(viewManager) as WorldDetails_t;
        }
    }
}
