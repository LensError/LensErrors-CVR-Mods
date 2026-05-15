using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.API.UserWebsocket;
using ABI_RC.Core.Networking.IO.Global;
using ABI_RC.Core.Networking.IO.Instancing;
using ABI_RC.Core.Networking.IO.UserGeneratedContent;
using ABI_RC.Core.UI;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ContentMenuTweaks
{
    static class FriendInstanceInjector
    {
        static readonly FieldInfo CategoriesField = AccessTools.Field(typeof(ViewManager), "_categories");
        static readonly FieldInfo WorldDetailsField = AccessTools.Field(typeof(ViewManager), "_worldDetails");
        static readonly FieldInfo FriendsOnlineStateField = AccessTools.Field(typeof(ViewManager), "_friendsOnlineState");
        static readonly FieldInfo WorldsPagedField = AccessTools.Field(typeof(ViewManager), "_worldsPaged");
        static readonly FieldInfo CurrentWorldPageField = AccessTools.Field(typeof(ViewManager), "_currentWorldPage");
        static readonly FieldInfo CurrentWorldPagesField = AccessTools.Field(typeof(ViewManager), "_currentWorldResultPagesCount");
        static readonly FieldInfo WorldSortOrderField = AccessTools.Field(typeof(ViewManager), "_worldSortOrder");
        static readonly FieldInfo WorldAscendingField = AccessTools.Field(typeof(ViewManager), "_worldAscending");
        static readonly FieldInfo CohtmlViewField = AccessTools.Field(typeof(ABI_RC.Core.UI.UIRework.CVRUIManagerBase), "cohtmlView");
        static readonly Regex GuidRegex = new Regex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);

        internal static void MergeFriendInstances(ViewManager viewManager)
        {
            if (!Settings.FriendInstancesEnabled || viewManager == null)
                return;

            var worldDetails = WorldDetailsField.GetValue(viewManager) as WorldDetails_t;
            if (worldDetails == null || string.IsNullOrEmpty(Settings.NormalizeId(worldDetails.WorldId)))
                return;

            if (worldDetails.Instances == null)
                return;

            var friends = FriendsOnlineStateField.GetValue(viewManager) as List<UserOnlineChangeCohtml>;
            List<FriendInstanceInfo> friendInstances = CollectJoinableInstances(viewManager, friends);
            if (friendInstances.Count == 0)
                return;

            var knownInstanceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < worldDetails.Instances.Count; i++)
            {
                InstanceData_t instance = worldDetails.Instances[i];
                if (instance != null && !string.IsNullOrEmpty(instance.InstanceId))
                    knownInstanceIds.Add(instance.InstanceId);
            }

            for (int i = 0; i < friendInstances.Count; i++)
            {
                FriendInstanceInfo friendInstance = friendInstances[i];
                if (!SameGuid(friendInstance.WorldId, worldDetails.WorldId) || !knownInstanceIds.Add(friendInstance.InstanceId))
                    continue;

                worldDetails.Instances.Add(friendInstance.ToInstanceData());
            }
        }

        internal static void MergeFriendWorlds(ViewManager viewManager, string category, int start)
        {
            if (!Settings.FriendInstancesEnabled || viewManager == null || start != 0 || !IsActiveWorldCategory(viewManager, category))
                return;

            var worlds = WorldsPagedField.GetValue(viewManager) as List<World_t>;
            if (worlds == null)
                return;

            var friends = FriendsOnlineStateField.GetValue(viewManager) as List<UserOnlineChangeCohtml>;
            List<FriendInstanceInfo> friendInstances = CollectJoinableInstances(viewManager, friends);
            if (friendInstances.Count == 0)
                return;

            var knownWorldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < worlds.Count; i++)
            {
                World_t world = worlds[i];
                if (world != null && !string.IsNullOrEmpty(Settings.NormalizeId(world.WorldId)))
                    knownWorldIds.Add(Settings.NormalizeId(world.WorldId));
            }

            bool changed = false;
            for (int i = 0; i < friendInstances.Count; i++)
            {
                FriendInstanceInfo friendInstance = friendInstances[i];
                if (!knownWorldIds.Add(friendInstance.WorldId))
                    continue;

                worlds.Add(friendInstance.ToWorld());
                changed = true;
            }

            if (changed)
                TriggerWorldsPaged(viewManager, worlds);
        }

        internal static bool HasWorldPage(ViewManager viewManager)
        {
            return viewManager != null && WorldsPagedField.GetValue(viewManager) is List<World_t>;
        }

        static List<FriendInstanceInfo> CollectJoinableInstances(ViewManager viewManager, List<UserOnlineChangeCohtml> friends)
        {
            var instances = new Dictionary<string, FriendInstanceInfo>(StringComparer.OrdinalIgnoreCase);

            FriendInstanceInfo currentInstance = TryBuildCurrentInstance(viewManager);
            if (currentInstance != null)
                instances.Add(currentInstance.InstanceId, currentInstance);

            if (friends == null)
                return new List<FriendInstanceInfo>(instances.Values);

            for (int i = 0; i < friends.Count; i++)
            {
                FriendInstanceInfo instance = TryBuildFriendInstance(friends[i]);
                if (instance == null)
                    continue;

                FriendInstanceInfo existing;
                if (instances.TryGetValue(instance.InstanceId, out existing))
                {
                    existing.FriendCount++;
                    existing.FillMissingFrom(instance);
                    continue;
                }

                instances.Add(instance.InstanceId, instance);
            }

            return new List<FriendInstanceInfo>(instances.Values);
        }

        static FriendInstanceInfo TryBuildCurrentInstance(ViewManager viewManager)
        {
            string instanceId = Instances.CurrentInstanceId;
            string worldId = Instances.CurrentWorldId;
            string privacy = Instances.CurrentInstancePrivacyType.ToString();
            string name = Instances.CurrentInstanceName;

            if (string.Equals(privacy, nameof(Instances.InstancePrivacyType.Invalid), StringComparison.OrdinalIgnoreCase))
            {
                privacy = ExtractCurrentInstancePrivacy(instanceId, name);
                if (string.IsNullOrWhiteSpace(privacy))
                    return null;
            }

            if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(Settings.NormalizeId(worldId)) || !IsActiveListInstancePrivacy(privacy, name))
                return null;

            if (string.IsNullOrWhiteSpace(name) || name == "Offline Instance" || name == "Private Instance")
                name = "Current Instance";

            string worldName = string.Empty;
            string worldImageUrl = string.Empty;
            string worldImageCoui = string.Empty;

            var worldDetails = viewManager == null || WorldDetailsField == null ? null : WorldDetailsField.GetValue(viewManager) as WorldDetails_t;
            if (worldDetails != null && SameGuid(worldDetails.WorldId, worldId))
            {
                worldName = worldDetails.WorldName;
                worldImageUrl = worldDetails.WorldImageUrl;
                worldImageCoui = worldDetails.WorldImageCoui;
            }

            if (string.IsNullOrWhiteSpace(worldName) && ABI_RC.Core.Savior.MetaPort.Instance != null)
                worldName = ABI_RC.Core.Savior.MetaPort.Instance.CurrentWorldName;

            if (string.IsNullOrWhiteSpace(worldName))
                worldName = "Current World";

            return new FriendInstanceInfo
            {
                InstanceId = instanceId,
                InstanceName = name,
                WorldId = Settings.NormalizeId(worldId),
                WorldName = worldName,
                WorldImageUrl = worldImageUrl,
                WorldImageCoui = worldImageCoui,
                FriendCount = 1
            };
        }

        static FriendInstanceInfo TryBuildFriendInstance(UserOnlineChangeCohtml friend)
        {
            if (friend == null || !friend.IsOnline || !friend.IsConnected || friend.Instance == null)
                return null;

            Dictionary<string, string> data = friend.Instance;
            string instanceId = FirstValue(data, "Id", "InstanceId", "InstanceID", "InstanceGuid", "Guid");
            string worldId = FirstValue(data, "WorldId", "WorldID", "WorldGuid", "WorldGuidId", "World", "World.id", "World.Id", "World.Id.Value");
            if (string.IsNullOrEmpty(Settings.NormalizeId(worldId)))
                worldId = ExtractWorldId(instanceId);

            string privacy = FirstValue(data, "Privacy", "InstancePrivacy", "InstanceSettingPrivacy", "Type", "AccessType", "InstanceType", "PrivacyType");
            string name = FirstValue(data, "Name", "InstanceName");

            if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(Settings.NormalizeId(worldId)) || !IsActiveListInstancePrivacy(privacy, name))
                return null;

            if (string.IsNullOrWhiteSpace(name) || name == "Offline Instance" || name == "Private Instance")
                name = "Friends Instance";

            string worldName = FirstValue(data, "WorldName", "World.Name", "WorldNameText", "ContentName");
            if (string.IsNullOrWhiteSpace(worldName))
                worldName = name;
            if (string.IsNullOrWhiteSpace(worldName) || worldName == "Offline Instance" || worldName == "Private Instance")
                worldName = "Friend's World";

            return new FriendInstanceInfo
            {
                InstanceId = instanceId,
                InstanceName = name,
                WorldId = Settings.NormalizeId(worldId),
                WorldName = worldName,
                WorldImageUrl = FirstValue(data, "WorldImageUrl", "WorldImage", "ImageUrl", "Image"),
                WorldImageCoui = FirstValue(data, "WorldImageCoui", "WorldImageCohtmlCache", "ImageCoui", "ImageCohtmlCache"),
                InstanceRegion = FirstValue(data, "Region", "InstanceRegion"),
                CurrentPlayerCount = FirstValue(data, "CurrentPlayerCount", "CurrentPlayer", "PlayerCount", "Players"),
                MaxPlayerCount = FirstValue(data, "MaxPlayerCount", "MaxPlayer", "MaxPlayers"),
                FriendCount = 1
            };
        }

        static bool IsActiveWorldCategory(ViewManager viewManager, string category)
        {
            if (string.IsNullOrWhiteSpace(category) || category.IndexOf("discover", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            string normalized = category.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
            if (string.Equals(normalized, "wrldactive", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "activeworlds", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "worldsactive", StringComparison.OrdinalIgnoreCase))
                return true;

            var categories = viewManager == null || CategoriesField == null ? null : CategoriesField.GetValue(viewManager) as Categories_t;
            if (categories == null || categories.Worlds == null)
                return false;

            for (int i = 0; i < categories.Worlds.Count; i++)
            {
                Category_t worldCategory = categories.Worlds[i];
                if (worldCategory == null || !string.Equals(worldCategory.CategoryKey, category, StringComparison.OrdinalIgnoreCase))
                    continue;

                string name = worldCategory.CategoryClearTextName ?? string.Empty;
                string normalizedName = name.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
                return string.Equals(normalizedName, "activeworlds", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        static bool IsActiveListInstancePrivacy(string privacy, string instanceName)
        {
            if (string.Equals(instanceName, "Offline Instance", StringComparison.OrdinalIgnoreCase)
                || string.Equals(instanceName, "Private Instance", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(privacy))
                return true;

            string normalized = privacy.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
            return string.Equals(normalized, nameof(Instances.InstancePrivacyType.Public), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, nameof(Instances.InstancePrivacyType.Friends), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, nameof(Instances.InstancePrivacyType.FriendsOfFriends), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "FriendsPlus", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, nameof(Instances.InstancePrivacyType.Group), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, nameof(Instances.InstancePrivacyType.GroupPlus), StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "FriendsOfGroup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "GroupOnly", StringComparison.OrdinalIgnoreCase);
        }

        static string ExtractCurrentInstancePrivacy(string instanceId, string instanceName)
        {
            string combined = ((instanceId ?? string.Empty) + " " + (instanceName ?? string.Empty))
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);

            if (combined.IndexOf(nameof(Instances.InstancePrivacyType.FriendsOfFriends), StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("FriendsPlus", StringComparison.OrdinalIgnoreCase) >= 0)
                return nameof(Instances.InstancePrivacyType.FriendsOfFriends);

            if (combined.IndexOf(nameof(Instances.InstancePrivacyType.GroupPlus), StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("FriendsOfGroup", StringComparison.OrdinalIgnoreCase) >= 0)
                return nameof(Instances.InstancePrivacyType.GroupPlus);

            if (combined.IndexOf(nameof(Instances.InstancePrivacyType.Friends), StringComparison.OrdinalIgnoreCase) >= 0)
                return nameof(Instances.InstancePrivacyType.Friends);

            if (combined.IndexOf(nameof(Instances.InstancePrivacyType.Group), StringComparison.OrdinalIgnoreCase) >= 0
                || combined.IndexOf("GroupOnly", StringComparison.OrdinalIgnoreCase) >= 0)
                return nameof(Instances.InstancePrivacyType.Group);

            return string.Empty;
        }

        static bool SameGuid(string left, string right)
        {
            left = Settings.NormalizeId(left);
            right = Settings.NormalizeId(right);
            return !string.IsNullOrEmpty(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        static string ExtractWorldId(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                return string.Empty;

            Match match = GuidRegex.Match(instanceId);
            return match.Success ? Settings.NormalizeId(match.Value) : string.Empty;
        }

        static string FirstValue(Dictionary<string, string> data, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                string value;
                if (data.TryGetValue(keys[i], out value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            }

            foreach (KeyValuePair<string, string> pair in data)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (string.Equals(pair.Key, keys[i], StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value))
                        return pair.Value;
                }
            }

            return string.Empty;
        }

        static void TriggerWorldsPaged(ViewManager viewManager, List<World_t> worlds)
        {
            CohtmlControlledView cohtmlView = CohtmlViewField == null ? null : CohtmlViewField.GetValue(viewManager) as CohtmlControlledView;
            if (cohtmlView == null || cohtmlView.View == null)
                return;

            cohtmlView.View.TriggerEvent(
                "LoadWorldsPaged",
                worlds,
                CurrentWorldPageField == null ? (uint)0 : (uint)CurrentWorldPageField.GetValue(viewManager),
                CurrentWorldPagesField == null ? (uint)0 : (uint)CurrentWorldPagesField.GetValue(viewManager),
                WorldSortOrderField == null ? null : WorldSortOrderField.GetValue(viewManager),
                WorldAscendingField != null && (bool)WorldAscendingField.GetValue(viewManager));
        }

        sealed class FriendInstanceInfo
        {
            internal string InstanceId;
            internal string InstanceName;
            internal string WorldId;
            internal string WorldName;
            internal string WorldImageUrl;
            internal string WorldImageCoui;
            internal string InstanceRegion;
            internal string CurrentPlayerCount;
            internal string MaxPlayerCount;
            internal int FriendCount;

            internal void FillMissingFrom(FriendInstanceInfo other)
            {
                if (other == null)
                    return;

                if (string.IsNullOrWhiteSpace(InstanceName))
                    InstanceName = other.InstanceName;
                if (string.IsNullOrWhiteSpace(WorldName))
                    WorldName = other.WorldName;
                if (string.IsNullOrWhiteSpace(WorldImageUrl))
                    WorldImageUrl = other.WorldImageUrl;
                if (string.IsNullOrWhiteSpace(WorldImageCoui))
                    WorldImageCoui = other.WorldImageCoui;
                if (string.IsNullOrWhiteSpace(InstanceRegion))
                    InstanceRegion = other.InstanceRegion;
                if (string.IsNullOrWhiteSpace(CurrentPlayerCount))
                    CurrentPlayerCount = other.CurrentPlayerCount;
                if (string.IsNullOrWhiteSpace(MaxPlayerCount))
                    MaxPlayerCount = other.MaxPlayerCount;
            }

            internal InstanceData_t ToInstanceData()
            {
                return new InstanceData_t
                {
                    InstanceId = InstanceId,
                    InstanceName = InstanceName,
                    InstanceRegion = InstanceRegion,
                    CurrentPlayerCount = string.IsNullOrWhiteSpace(CurrentPlayerCount) ? FriendCount.ToString() : CurrentPlayerCount,
                    MaxPlayerCount = MaxPlayerCount
                };
            }

            internal World_t ToWorld()
            {
                return new World_t
                {
                    WorldId = WorldId,
                    WorldName = WorldName,
                    WorldImageUrl = WorldImageUrl,
                    WorldImageCoui = WorldImageCoui,
                    UsersInPublic = 0
                };
            }
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.GetFilteredWorldsTaskPaged))]
    static class GetFilteredWorldsTaskFriendWorldsPatch
    {
        static void Postfix(ViewManager __instance, string worldListCategory, int start, ref Task __result)
        {
            __result = AwaitAndMerge(__instance, worldListCategory, start, __result);
        }

        static async Task AwaitAndMerge(ViewManager viewManager, string worldListCategory, int start, Task original)
        {
            await original;
            try
            {
                FriendInstanceInjector.MergeFriendWorlds(viewManager, worldListCategory, start);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.GetFilteredWorldsPaged))]
    static class GetFilteredWorldsPagedFriendWorldsPatch
    {
        static void Postfix(ViewManager __instance, string category, int start)
        {
            MelonCoroutines.Start(MergeWhenWorldsLoaded(__instance, category, start));
        }

        static IEnumerator MergeWhenWorldsLoaded(ViewManager viewManager, string category, int start)
        {
            yield return null;

            for (int i = 0; i < 90 && !FriendInstanceInjector.HasWorldPage(viewManager); i++)
                yield return null;

            try
            {
                FriendInstanceInjector.MergeFriendWorlds(viewManager, category, start);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }
    }

    [HarmonyPatch(typeof(ViewManager), nameof(ViewManager.GetWorldDetailsTask))]
    static class GetWorldDetailsTaskFriendInstancesPatch
    {
        static void Postfix(ViewManager __instance, ref Task __result)
        {
            __result = AwaitAndMerge(__instance, __result);
        }

        static async Task AwaitAndMerge(ViewManager viewManager, Task original)
        {
            await original;
            try
            {
                FriendInstanceInjector.MergeFriendInstances(viewManager);
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }
    }
}
