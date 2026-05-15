using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ContentMenuTweaks
{
    static class Settings
    {
        const string CategoryId = "ContentMenuTweaks";
        const int MaxEntries = 48;
        const float SaveDelaySeconds = 2.0f;

        static MelonPreferences_Entry<bool> _recentAvatarsEnabledEntry;
        static MelonPreferences_Entry<bool> _recentPropsEnabledEntry;
        static MelonPreferences_Entry<bool> _recentWorldsEnabledEntry;
        static MelonPreferences_Entry<bool> _friendInstancesEnabledEntry;
        static MelonPreferences_Entry<bool> _recentSeenAvatarsEnabledEntry;
        static MelonPreferences_Entry<bool> _recentSeenPropsEnabledEntry;
        static MelonPreferences_Entry<bool> _seenAvatarsHidePrivateEntry;
        static MelonPreferences_Entry<bool> _seenPropsHidePrivateEntry;
        static MelonPreferences_Entry<string> _avatarsEntry;
        static MelonPreferences_Entry<string> _propsEntry;
        static MelonPreferences_Entry<string> _worldsEntry;
        static MelonPreferences_Entry<string> _seenAvatarsEntry;
        static MelonPreferences_Entry<string> _seenPropsEntry;
        static bool _savePending;
        static bool _saveCoroutineRunning;

        internal static bool RecentAvatarsEnabled { get; private set; } = true;
        internal static bool RecentPropsEnabled { get; private set; } = true;
        internal static bool RecentWorldsEnabled { get; private set; } = true;
        internal static bool FriendInstancesEnabled { get; private set; } = true;
        internal static bool RecentSeenAvatarsEnabled { get; private set; } = true;
        internal static bool RecentSeenPropsEnabled { get; private set; } = true;
        internal static bool SeenAvatarsHidePrivate { get; private set; } = false;
        internal static bool SeenPropsHidePrivate { get; private set; } = false;

        internal static List<RecentEntry> Avatars { get; private set; } = new List<RecentEntry>();
        internal static List<RecentEntry> Props { get; private set; } = new List<RecentEntry>();
        internal static List<RecentEntry> Worlds { get; private set; } = new List<RecentEntry>();
        internal static List<RecentEntry> SeenAvatars { get; private set; } = new List<RecentEntry>();
        internal static List<RecentEntry> SeenProps { get; private set; } = new List<RecentEntry>();

        internal static void Init()
        {
            var cat = MelonPreferences.CreateCategory(CategoryId, "Content Menu Tweaks");
            _recentAvatarsEnabledEntry = cat.CreateEntry("RecentAvatarsEnabled", true, "Recent Avatar Category", "Show a Recently Used category in the avatar menu.", false, false, null);
            _recentPropsEnabledEntry = cat.CreateEntry("RecentPropsEnabled", true, "Recent Prop Category", "Show a Recently Spawned category in the prop menu.", false, false, null);
            _recentWorldsEnabledEntry = cat.CreateEntry("RecentWorldsEnabled", true, "Recent World Category", "Show a Recently Visited category in the world menu.", false, false, null);
            _friendInstancesEnabledEntry = cat.CreateEntry("FriendInstancesEnabled", true, "Friend Active Instances", "Add your own and friends' joinable Friends, FriendsOfFriends, GroupPlus, Group-only, and missing Public instances to world active instances.", false, false, null);
            _recentSeenAvatarsEnabledEntry = cat.CreateEntry("RecentSeenAvatarsEnabled", true, "Recently Seen Avatar Category", "Show a Recently Seen category in the avatar menu for avatars worn by other players.", false, false, null);
            _recentSeenPropsEnabledEntry = cat.CreateEntry("RecentSeenPropsEnabled", true, "Recently Seen Prop Category", "Show a Recently Seen category in the prop menu for props spawned by other players.", false, false, null);
            _seenAvatarsHidePrivateEntry = cat.CreateEntry("SeenAvatarsHidePrivate", false, "Hide Private Seen Avatars", "Filter out private avatars from the Recently Seen avatar category.", false, false, null);
            _seenPropsHidePrivateEntry = cat.CreateEntry("SeenPropsHidePrivate", false, "Hide Private Seen Props", "Filter out private props from the Recently Seen prop category.", false, false, null);

            _avatarsEntry = cat.CreateEntry("Avatars", string.Empty, "Recently Used Avatars", "Stored avatar history for Content Menu Tweaks.", true, false, null);
            _propsEntry = cat.CreateEntry("Props", string.Empty, "Recently Spawned Props", "Stored prop history for Content Menu Tweaks.", true, false, null);
            _worldsEntry = cat.CreateEntry("Worlds", string.Empty, "Recently Visited Worlds", "Stored world history for Content Menu Tweaks.", true, false, null);
            _seenAvatarsEntry = cat.CreateEntry("SeenAvatars", string.Empty, "Recently Seen Avatars", "Stored seen avatar history for Content Menu Tweaks.", true, false, null);
            _seenPropsEntry = cat.CreateEntry("SeenProps", string.Empty, "Recently Seen Props", "Stored seen prop history for Content Menu Tweaks.", true, false, null);

            RecentAvatarsEnabled = _recentAvatarsEnabledEntry.Value;
            RecentPropsEnabled = _recentPropsEnabledEntry.Value;
            RecentWorldsEnabled = _recentWorldsEnabledEntry.Value;
            FriendInstancesEnabled = _friendInstancesEnabledEntry.Value;
            RecentSeenAvatarsEnabled = _recentSeenAvatarsEnabledEntry.Value;
            RecentSeenPropsEnabled = _recentSeenPropsEnabledEntry.Value;
            SeenAvatarsHidePrivate = _seenAvatarsHidePrivateEntry.Value;
            SeenPropsHidePrivate = _seenPropsHidePrivateEntry.Value;
            Avatars = DecodeList(_avatarsEntry.Value);
            Props = DecodeList(_propsEntry.Value);
            Worlds = DecodeList(_worldsEntry.Value);
            SeenAvatars = DecodeList(_seenAvatarsEntry.Value);
            SeenProps = DecodeList(_seenPropsEntry.Value);

            _recentAvatarsEnabledEntry.OnEntryValueChanged.Subscribe((_, value) => SetRecentAvatarsEnabled(value));
            _recentPropsEnabledEntry.OnEntryValueChanged.Subscribe((_, value) => SetRecentPropsEnabled(value));
            _recentWorldsEnabledEntry.OnEntryValueChanged.Subscribe((_, value) => SetRecentWorldsEnabled(value));
            _friendInstancesEnabledEntry.OnEntryValueChanged.Subscribe((_, value) => FriendInstancesEnabled = value);
            _recentSeenAvatarsEnabledEntry.OnEntryValueChanged.Subscribe((_, value) => SetRecentSeenAvatarsEnabled(value));
            _recentSeenPropsEnabledEntry.OnEntryValueChanged.Subscribe((_, value) => SetRecentSeenPropsEnabled(value));
            _seenAvatarsHidePrivateEntry.OnEntryValueChanged.Subscribe((_, value) => SetSeenAvatarsHidePrivate(value));
            _seenPropsHidePrivateEntry.OnEntryValueChanged.Subscribe((_, value) => SetSeenPropsHidePrivate(value));
        }

        static void SetRecentAvatarsEnabled(bool value)
        {
            RecentAvatarsEnabled = value;
            RecentCategoryInjector.RefreshCategories();
        }

        static void SetRecentPropsEnabled(bool value)
        {
            RecentPropsEnabled = value;
            RecentCategoryInjector.RefreshCategories();
        }

        static void SetRecentWorldsEnabled(bool value)
        {
            RecentWorldsEnabled = value;
            RecentCategoryInjector.RefreshCategories();
        }

        static void SetRecentSeenAvatarsEnabled(bool value)
        {
            RecentSeenAvatarsEnabled = value;
            RecentCategoryInjector.RefreshCategories();
        }

        static void SetRecentSeenPropsEnabled(bool value)
        {
            RecentSeenPropsEnabled = value;
            RecentCategoryInjector.RefreshCategories();
        }

        static void SetSeenAvatarsHidePrivate(bool value)
        {
            SeenAvatarsHidePrivate = value;
            var viewManager = ABI_RC.Core.InteractionSystem.ViewManager.Instance;
            if (viewManager != null && RecentCategoryInjector.IsViewingRecentSeenAvatars(viewManager))
                RecentCategoryInjector.LoadRecentSeenAvatars(viewManager);
        }

        static void SetSeenPropsHidePrivate(bool value)
        {
            SeenPropsHidePrivate = value;
            var viewManager = ABI_RC.Core.InteractionSystem.ViewManager.Instance;
            if (viewManager != null && RecentCategoryInjector.IsViewingRecentSeenProps(viewManager))
                RecentCategoryInjector.LoadRecentSeenProps(viewManager);
        }

        internal static void AddAvatar(string id, string name, string imageUrl)
        {
            if (AddOrMoveToTop(Avatars, id, name, imageUrl))
                SaveAvatars();
        }

        internal static void AddProp(string id, string name, string imageUrl)
        {
            if (AddOrMoveToTop(Props, id, name, imageUrl))
                SaveProps();
        }

        internal static void AddWorld(string id, string name, string imageUrl)
        {
            if (AddOrMoveToTop(Worlds, id, name, imageUrl))
                SaveWorlds();
        }

        internal static bool UpdateAvatar(string id, string name, string imageUrl)
        {
            return UpdateEntry(Avatars, id, name, imageUrl, SaveAvatars);
        }

        internal static bool UpdateProp(string id, string name, string imageUrl)
        {
            return UpdateEntry(Props, id, name, imageUrl, SaveProps);
        }

        internal static bool UpdateWorld(string id, string name, string imageUrl)
        {
            return UpdateEntry(Worlds, id, name, imageUrl, SaveWorlds);
        }

        internal static void AddSeenAvatar(string id, string name, string imageUrl)
        {
            if (AddOrMoveToTop(SeenAvatars, id, name, imageUrl))
                SaveSeenAvatars();
        }

        internal static void AddSeenProp(string id, string name, string imageUrl)
        {
            if (AddOrMoveToTop(SeenProps, id, name, imageUrl))
                SaveSeenProps();
        }

        internal static bool UpdateSeenAvatar(string id, string name, string imageUrl)
        {
            return UpdateEntry(SeenAvatars, id, name, imageUrl, SaveSeenAvatars);
        }

        internal static bool UpdateSeenProp(string id, string name, string imageUrl)
        {
            return UpdateEntry(SeenProps, id, name, imageUrl, SaveSeenProps);
        }

        internal static bool UpdateSeenAvatarPublic(string id, bool isPublic)
        {
            return UpdateEntryPublic(SeenAvatars, id, isPublic, SaveSeenAvatars);
        }

        internal static bool UpdateSeenPropPublic(string id, bool isPublic)
        {
            return UpdateEntryPublic(SeenProps, id, isPublic, SaveSeenProps);
        }

        static bool UpdateEntryPublic(List<RecentEntry> list, string id, bool isPublic, Action save)
        {
            id = NormalizeId(id);
            if (string.IsNullOrEmpty(id))
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                RecentEntry entry = list[i];
                if (!string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.IsPublic == isPublic)
                    return false;

                entry.IsPublic = isPublic;
                save();
                return true;
            }

            return false;
        }

        static bool AddOrMoveToTop(List<RecentEntry> list, string id, string name, string imageUrl)
        {
            id = NormalizeId(id);
            if (string.IsNullOrEmpty(id))
                return false;

            name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            imageUrl = imageUrl ?? string.Empty;

            RecentEntry existing = null;
            int existingIndex = -1;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (string.Equals(list[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    existing = list[i];
                    existingIndex = i;
                    list.RemoveAt(i);
                }
            }

            if (existing == null)
            {
                existing = new RecentEntry
                {
                    Id = id,
                    Name = string.IsNullOrWhiteSpace(name) || IsPlaceholderName(id, name) ? ShortId(id) : name,
                    ImageUrl = imageUrl
                };
            }

            if (string.IsNullOrWhiteSpace(name) || (!IsPlaceholderName(existing) && IsPlaceholderName(id, name)))
                name = existing.Name;
            if (string.IsNullOrWhiteSpace(imageUrl))
                imageUrl = existing.ImageUrl;

            bool changed = existingIndex != 0
                || !string.Equals(existing.Name, name, StringComparison.Ordinal)
                || !string.Equals(existing.ImageUrl, imageUrl, StringComparison.Ordinal);

            existing.Name = name;
            existing.ImageUrl = imageUrl;
            existing.LastSeen = DateTime.UtcNow;
            list.Insert(0, existing);

            while (list.Count > MaxEntries)
            {
                list.RemoveAt(list.Count - 1);
                changed = true;
            }

            return changed;
        }

        static bool UpdateEntry(List<RecentEntry> list, string id, string name, string imageUrl, Action save)
        {
            id = NormalizeId(id);
            if (string.IsNullOrEmpty(id))
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                RecentEntry entry = list[i];
                if (!string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool changed = false;
                if (!string.IsNullOrWhiteSpace(name) && (IsPlaceholderName(entry) || !string.Equals(entry.Name, name.Trim(), StringComparison.Ordinal)))
                {
                    entry.Name = name.Trim();
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(imageUrl) && !string.Equals(entry.ImageUrl, imageUrl, StringComparison.Ordinal))
                {
                    entry.ImageUrl = imageUrl;
                    changed = true;
                }

                if (changed)
                    save();

                return changed;
            }

            return false;
        }

        internal static bool IsPlaceholderName(RecentEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                return true;

            return IsPlaceholderName(entry.Id, entry.Name)
                || string.Equals(entry.Name, "Resolving...", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsPlaceholderName(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return true;

            id = NormalizeId(id);
            if (string.IsNullOrEmpty(id))
                return false;

            string trimmed = name.Trim();
            string compact = trimmed.Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
            string compactId = id.Replace("-", string.Empty);

            return string.Equals(trimmed, id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, ShortId(id), StringComparison.OrdinalIgnoreCase)
                || compact.IndexOf(compactId, StringComparison.OrdinalIgnoreCase) >= 0
                || compact.IndexOf(compactId.Substring(0, 8), StringComparison.OrdinalIgnoreCase) >= 0 && LooksLikeGeneratedContentName(trimmed);
        }

        static bool LooksLikeGeneratedContentName(string name)
        {
            return name.StartsWith("cvravatar", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("cvrspawnable", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("cvrprop", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("cvrworld", StringComparison.OrdinalIgnoreCase);
        }

        static void SaveAvatars()
        {
            _avatarsEntry.Value = EncodeList(Avatars);
            ScheduleSave();
        }

        static void SaveProps()
        {
            _propsEntry.Value = EncodeList(Props);
            ScheduleSave();
        }

        static void SaveWorlds()
        {
            _worldsEntry.Value = EncodeList(Worlds);
            ScheduleSave();
        }

        static void SaveSeenAvatars()
        {
            _seenAvatarsEntry.Value = EncodeList(SeenAvatars);
            ScheduleSave();
        }

        static void SaveSeenProps()
        {
            _seenPropsEntry.Value = EncodeList(SeenProps);
            ScheduleSave();
        }

        static void ScheduleSave()
        {
            _savePending = true;
            if (_saveCoroutineRunning)
                return;

            _saveCoroutineRunning = true;
            MelonCoroutines.Start(SaveWhenQuiet());
        }

        internal static void FlushSave()
        {
            if (!_savePending && !_saveCoroutineRunning)
                return;

            _savePending = false;
            _saveCoroutineRunning = false;
            MelonPreferences.Save();
        }

        static IEnumerator SaveWhenQuiet()
        {
            while (true)
            {
                _savePending = false;
                DateTime saveAfter = DateTime.UtcNow.AddSeconds(SaveDelaySeconds);
                while (DateTime.UtcNow < saveAfter)
                    yield return null;

                if (!_saveCoroutineRunning)
                    yield break;

                if (_savePending)
                    continue;

                MelonPreferences.Save();
                _saveCoroutineRunning = false;
                yield break;
            }
        }

        internal static void ClearAvatars()
        {
            Avatars.Clear();
            SaveAvatars();
            RecentCategoryInjector.MarkDirty();
        }

        internal static void ClearProps()
        {
            Props.Clear();
            SaveProps();
            RecentCategoryInjector.MarkDirty();
        }

        internal static void ClearWorlds()
        {
            Worlds.Clear();
            SaveWorlds();
            RecentCategoryInjector.MarkDirty();
        }

        internal static void ClearSeenAvatars()
        {
            SeenAvatars.Clear();
            SaveSeenAvatars();
            RecentCategoryInjector.MarkDirty();
        }

        internal static void ClearSeenProps()
        {
            SeenProps.Clear();
            SaveSeenProps();
            RecentCategoryInjector.MarkDirty();
        }

        static string EncodeList(List<RecentEntry> list)
        {
            var lines = new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                RecentEntry entry = list[i];
                lines.Add(string.Join("|", new[]
                {
                    entry.Id,
                    Encode(entry.Name),
                    Encode(entry.ImageUrl),
                    entry.LastSeen.ToFileTimeUtc().ToString(),
                    entry.IsPublic.HasValue ? (entry.IsPublic.Value ? "1" : "0") : string.Empty
                }));
            }

            return string.Join("\n", lines.ToArray());
        }

        static List<RecentEntry> DecodeList(string raw)
        {
            var list = new List<RecentEntry>();
            if (string.IsNullOrEmpty(raw))
                return list;

            string[] lines = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('|');
                if (parts.Length == 0)
                    continue;

                string id = NormalizeId(parts[0]);
                if (string.IsNullOrEmpty(id))
                    continue;

                if (list.Count >= MaxEntries)
                    continue;

                long fileTime;
                DateTime lastSeen = DateTime.UtcNow;
                if (parts.Length > 3 && long.TryParse(parts[3], out fileTime))
                    lastSeen = DateTime.FromFileTimeUtc(fileTime);

                bool? isPublic = null;
                if (parts.Length > 4 && parts[4] == "1") isPublic = true;
                else if (parts.Length > 4 && parts[4] == "0") isPublic = false;

                list.Add(new RecentEntry
                {
                    Id = id,
                    Name = parts.Length > 1 ? Decode(parts[1]) : ShortId(id),
                    ImageUrl = parts.Length > 2 ? Decode(parts[2]) : string.Empty,
                    LastSeen = lastSeen,
                    IsPublic = isPublic
                });
            }

            return list;
        }

        internal static string NormalizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            Guid guid;
            if (!Guid.TryParse(raw.Trim(), out guid))
                return string.Empty;

            return guid.ToString("D");
        }

        internal static string ShortId(string id)
        {
            return string.IsNullOrEmpty(id) || id.Length < 8 ? id : id.Substring(0, 8);
        }

        static string Encode(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    class RecentEntry
    {
        public string Id;
        public string Name;
        public string ImageUrl;
        public DateTime LastSeen;
        public bool? IsPublic;
    }

}
