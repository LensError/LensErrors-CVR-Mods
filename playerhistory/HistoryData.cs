using MelonLoader;
using MelonLoader.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PlayerHistory
{
    class PlayerEntry
    {
        public string UserId;
        public string DisplayName;
        public DateTime LastSeen;
        public List<DateTime> Encounters; // newest first, max 10
    }

    static class HistoryData
    {
        static readonly string s_filePath = Path.Combine(MelonEnvironment.UserDataDirectory, "PlayerHistory.tsv");

        const int c_maxEntries = 50;
        const int c_maxEncounters = 10;

        internal static readonly List<PlayerEntry> Entries = new List<PlayerEntry>();

        internal static void Load()
        {
            Entries.Clear();
            if (!File.Exists(s_filePath)) return;
            try
            {
                foreach (var line in File.ReadAllLines(s_filePath, Encoding.UTF8))
                {
                    if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
                    var p = line.Split('\t');
                    if (p.Length < 3) continue;
                    if (!long.TryParse(p[2], out long ft)) continue;

                    var encounters = new List<DateTime>();
                    if (p.Length > 3)
                    {
                        foreach (var part in p[3].Split('|'))
                        {
                            if (long.TryParse(part, out long eft))
                                encounters.Add(DateTime.FromFileTimeUtc(eft).ToLocalTime());
                        }
                    }

                    Entries.Add(new PlayerEntry
                    {
                        UserId = p[0],
                        DisplayName = p[1],
                        LastSeen = DateTime.FromFileTimeUtc(ft).ToLocalTime(),
                        Encounters = encounters
                    });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        internal static void Save()
        {
            try
            {
                using var writer = new StreamWriter(s_filePath, false, Encoding.UTF8);
                writer.WriteLine("# PlayerHistory v3");
                foreach (var e in Entries)
                {
                    var encounterStr = string.Join("|", e.Encounters.ConvertAll(d => d.ToFileTimeUtc().ToString()));
                    writer.WriteLine(string.Join("\t",
                        e.UserId,
                        e.DisplayName.Replace("\t", " "),
                        e.LastSeen.ToFileTimeUtc(),
                        encounterStr
                    ));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        internal static PlayerEntry Find(string userId)
        {
            foreach (var e in Entries)
                if (e.UserId == userId) return e;
            return null;
        }

        internal static void AddOrUpdate(string userId, string displayName)
        {
            var now = DateTime.Now;
            var existing = Find(userId);
            if (existing != null)
            {
                existing.DisplayName = displayName;
                existing.LastSeen = now;
                existing.Encounters.Insert(0, now);
                if (existing.Encounters.Count > c_maxEncounters)
                    existing.Encounters.RemoveAt(existing.Encounters.Count - 1);
            }
            else
            {
                if (Entries.Count >= c_maxEntries)
                {
                    int oldestIdx = 0;
                    for (int i = 1; i < Entries.Count; i++)
                        if (Entries[i].LastSeen < Entries[oldestIdx].LastSeen)
                            oldestIdx = i;
                    Entries.RemoveAt(oldestIdx);
                }

                Entries.Add(new PlayerEntry
                {
                    UserId = userId,
                    DisplayName = displayName,
                    LastSeen = now,
                    Encounters = new List<DateTime> { now }
                });
            }
        }

        internal static bool Remove(string userId)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].UserId == userId)
                {
                    Entries.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        internal static void Clear() => Entries.Clear();
    }
}
