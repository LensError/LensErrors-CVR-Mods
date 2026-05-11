using MelonLoader;
using MelonLoader.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PlayerHistory
{
    class Encounter
    {
        public DateTime Time;
        public string World;
    }

    class PlayerEntry
    {
        public string UserId;
        public string DisplayName;
        public DateTime LastSeen;
        public List<Encounter> Encounters;
    }

    static class HistoryData
    {
        static readonly string s_filePath = Path.Combine(MelonEnvironment.UserDataDirectory, "PlayerHistory.tsv");

        const int c_maxEntries    = 1000;
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

                    var encounters = new List<Encounter>();
                    if (p.Length > 3)
                    {
                        foreach (var part in p[3].Split('|'))
                        {
                            var colonIdx = part.IndexOf(':');
                            if (colonIdx > 0 && long.TryParse(part.Substring(0, colonIdx), out long eft))
                                encounters.Add(new Encounter { Time = DateTime.FromFileTimeUtc(eft).ToLocalTime(), World = part.Substring(colonIdx + 1) });
                            else if (long.TryParse(part, out long eft2))
                                encounters.Add(new Encounter { Time = DateTime.FromFileTimeUtc(eft2).ToLocalTime(), World = "" });
                        }
                    }

                    Entries.Add(new PlayerEntry
                    {
                        UserId      = p[0],
                        DisplayName = p[1],
                        LastSeen    = DateTime.FromFileTimeUtc(ft).ToLocalTime(),
                        Encounters  = encounters
                    });
                }
            }
            catch (Exception ex) { MelonLogger.Error(ex); }
        }

        internal static void Save()
        {
            try
            {
                using var writer = new StreamWriter(s_filePath, false, Encoding.UTF8);
                writer.WriteLine("# PlayerHistory v4");
                foreach (var e in Entries)
                {
                    var encounterStr = string.Join("|", e.Encounters.ConvertAll(enc =>
                        $"{enc.Time.ToFileTimeUtc()}:{enc.World.Replace("|", " ").Replace("\t", " ")}"));
                    writer.WriteLine(string.Join("\t",
                        e.UserId,
                        e.DisplayName.Replace("\t", " "),
                        e.LastSeen.ToFileTimeUtc(),
                        encounterStr));
                }
            }
            catch (Exception ex) { MelonLogger.Error(ex); }
        }

        internal static PlayerEntry Find(string userId) =>
            Entries.FirstOrDefault(e => e.UserId == userId);

        internal static void AddOrUpdate(string userId, string displayName, string worldName)
        {
            var now      = DateTime.Now;
            var enc      = new Encounter { Time = now, World = worldName ?? "" };
            var existing = Find(userId);

            if (existing != null)
            {
                existing.DisplayName = displayName;
                existing.LastSeen    = now;
                existing.Encounters.Insert(0, enc);
                if (existing.Encounters.Count > c_maxEncounters)
                    existing.Encounters.RemoveAt(existing.Encounters.Count - 1);
            }
            else
            {
                if (Entries.Count >= c_maxEntries)
                    EvictOldest();

                Entries.Add(new PlayerEntry
                {
                    UserId      = userId,
                    DisplayName = displayName,
                    LastSeen    = now,
                    Encounters  = new List<Encounter> { enc }
                });
            }
        }

        internal static void TrimToLimit()
        {
            while (Entries.Count > c_maxEntries)
                EvictOldest();
        }

        internal static void Clear() => Entries.Clear();

        static void EvictOldest() => Entries.Remove(Entries.OrderBy(e => e.LastSeen).First());
    }
}
