using MelonLoader;
using System;
using System.Collections.Generic;
using System.Text;

namespace CVRTrainer
{
    static class Settings
    {
        static MelonPreferences_Entry<string> _vehiclesEntry;
        static MelonPreferences_Entry<string> _propsEntry;
        static MelonPreferences_Entry<string> _favoritesEntry;
        static MelonPreferences_Entry<string> _recentEntry;

        internal static List<SavedContentEntry> Vehicles { get; private set; } = new List<SavedContentEntry>();
        internal static List<SavedContentEntry> Props { get; private set; } = new List<SavedContentEntry>();
        internal static List<SavedContentEntry> Favorites { get; private set; } = new List<SavedContentEntry>();
        internal static List<SavedContentEntry> Recent { get; private set; } = new List<SavedContentEntry>();

        internal static void Init()
        {
            var cat = MelonPreferences.CreateCategory("CVRTrainer", "CVR Trainer");
            _vehiclesEntry = cat.CreateEntry("Vehicles", string.Empty, "Saved Vehicles");
            _propsEntry = cat.CreateEntry("Props", string.Empty, "Saved Props");
            _favoritesEntry = cat.CreateEntry("Favorites", string.Empty, "Favorite Spawnables");
            _recentEntry = cat.CreateEntry("Recent", string.Empty, "Recent Spawnables");

            Vehicles = DecodeList(_vehiclesEntry.Value);
            Props = DecodeList(_propsEntry.Value);
            Favorites = DecodeList(_favoritesEntry.Value);
            Recent = DecodeList(_recentEntry.Value);
        }

        internal static bool AddVehicle(string guid)
        {
            bool added = AddEntry(Vehicles, guid);
            if (added)
                SaveVehicles();
            return added;
        }

        internal static bool AddProp(string guid)
        {
            bool added = AddEntry(Props, guid);
            if (added)
                SaveProps();
            return added;
        }

        internal static bool AddFavorite(SavedContentEntry source)
        {
            bool added = AddEntry(Favorites, source);
            if (added)
                SaveFavorites();
            return added;
        }

        internal static void AddRecent(SavedContentEntry source)
        {
            if (source == null)
                return;

            for (int i = Recent.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Recent[i].Guid, source.Guid, StringComparison.OrdinalIgnoreCase))
                    Recent.RemoveAt(i);
            }

            Recent.Insert(0, source.Clone());
            while (Recent.Count > 20)
                Recent.RemoveAt(Recent.Count - 1);

            SaveRecent();
        }

        internal static void RemoveVehicle(int index)
        {
            if (index < 0 || index >= Vehicles.Count)
                return;

            Vehicles.RemoveAt(index);
            SaveVehicles();
        }

        internal static void RemoveProp(int index)
        {
            if (index < 0 || index >= Props.Count)
                return;

            Props.RemoveAt(index);
            SaveProps();
        }

        internal static void RemoveFavorite(int index)
        {
            if (index < 0 || index >= Favorites.Count)
                return;

            Favorites.RemoveAt(index);
            SaveFavorites();
        }

        internal static void RemoveRecent(int index)
        {
            if (index < 0 || index >= Recent.Count)
                return;

            Recent.RemoveAt(index);
            SaveRecent();
        }

        internal static void MoveVehicle(int index, int direction) => MoveEntry(Vehicles, index, direction, SaveVehicles);
        internal static void MoveProp(int index, int direction) => MoveEntry(Props, index, direction, SaveProps);
        internal static void MoveFavorite(int index, int direction) => MoveEntry(Favorites, index, direction, SaveFavorites);

        internal static void UpdateVehicle(SavedContentEntry entry)
        {
            SaveVehicles();
        }

        internal static void UpdateProp(SavedContentEntry entry)
        {
            SaveProps();
        }

        internal static void UpdateFavorite(SavedContentEntry entry)
        {
            SaveFavorites();
        }

        internal static void UpdateRecent(SavedContentEntry entry)
        {
            SaveRecent();
        }

        static bool AddEntry(List<SavedContentEntry> list, string rawGuid)
        {
            string guid = NormalizeGuid(rawGuid);
            if (string.IsNullOrEmpty(guid))
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Guid, guid, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            list.Add(new SavedContentEntry
            {
                Guid = guid,
                Name = "Resolving...",
                LocalLabel = string.Empty,
                Author = string.Empty,
                Status = "Queued"
            });
            return true;
        }

        static bool AddEntry(List<SavedContentEntry> list, SavedContentEntry source)
        {
            if (source == null || string.IsNullOrEmpty(NormalizeGuid(source.Guid)))
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Guid, source.Guid, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            list.Add(source.Clone());
            return true;
        }

        internal static string NormalizeGuid(string rawGuid)
        {
            if (string.IsNullOrWhiteSpace(rawGuid))
                return string.Empty;

            Guid guid;
            if (!Guid.TryParse(rawGuid.Trim(), out guid))
                return string.Empty;

            return guid.ToString("D");
        }

        static void SaveVehicles()
        {
            _vehiclesEntry.Value = EncodeList(Vehicles);
            MelonPreferences.Save();
        }

        static void SaveProps()
        {
            _propsEntry.Value = EncodeList(Props);
            MelonPreferences.Save();
        }

        static void SaveFavorites()
        {
            _favoritesEntry.Value = EncodeList(Favorites);
            MelonPreferences.Save();
        }

        static void SaveRecent()
        {
            _recentEntry.Value = EncodeList(Recent);
            MelonPreferences.Save();
        }

        static void MoveEntry(List<SavedContentEntry> list, int index, int direction, Action save)
        {
            int target = index + direction;
            if (index < 0 || index >= list.Count || target < 0 || target >= list.Count)
                return;

            SavedContentEntry entry = list[index];
            list.RemoveAt(index);
            list.Insert(target, entry);
            save();
        }

        static string EncodeList(List<SavedContentEntry> list)
        {
            var lines = new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                SavedContentEntry entry = list[i];
                lines.Add(string.Join("|", new[]
                {
                    entry.Guid,
                    Encode(entry.Name),
                    Encode(entry.Author),
                    Encode(entry.Status),
                    entry.IsPermitted ? "1" : "0",
                    entry.IsPublic ? "1" : "0",
                    Encode(entry.LocalLabel)
                }));
            }

            return string.Join("\n", lines.ToArray());
        }

        static List<SavedContentEntry> DecodeList(string raw)
        {
            var list = new List<SavedContentEntry>();
            if (string.IsNullOrEmpty(raw))
                return list;

            string[] lines = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('|');
                if (parts.Length == 0)
                    continue;

                string guid = NormalizeGuid(parts[0]);
                if (string.IsNullOrEmpty(guid))
                    continue;

                list.Add(new SavedContentEntry
                {
                    Guid = guid,
                    Name = parts.Length > 1 ? Decode(parts[1]) : ShortGuid(guid),
                    Author = parts.Length > 2 ? Decode(parts[2]) : string.Empty,
                    Status = parts.Length > 3 ? Decode(parts[3]) : "Saved",
                    IsPermitted = parts.Length <= 4 || parts[4] == "1",
                    IsPublic = parts.Length > 5 && parts[5] == "1",
                    LocalLabel = parts.Length > 6 ? Decode(parts[6]) : string.Empty
                });
            }

            return list;
        }

        static string Encode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
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

        internal static string ShortGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid) || guid.Length < 8)
                return guid;

            return guid.Substring(0, 8);
        }
    }

    class SavedContentEntry
    {
        public string Guid;
        public string Name;
        public string LocalLabel;
        public string Author;
        public string Status;
        public bool IsPermitted = true;
        public bool IsPublic;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(LocalLabel))
                    return LocalLabel;

                return string.IsNullOrEmpty(Name) ? Settings.ShortGuid(Guid) : Name;
            }
        }

        public SavedContentEntry Clone()
        {
            return new SavedContentEntry
            {
                Guid = Guid,
                Name = Name,
                LocalLabel = LocalLabel,
                Author = Author,
                Status = Status,
                IsPermitted = IsPermitted,
                IsPublic = IsPublic
            };
        }
    }
}
