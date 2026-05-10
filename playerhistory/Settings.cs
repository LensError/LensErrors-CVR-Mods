using ABI_RC.Core.Networking.IO.Social;
using BTKUILib;
using BTKUILib.UIObjects;
using BTKUILib.UIObjects.Components;
using MelonLoader;
using System;
using System.Linq;

namespace PlayerHistory
{
    static class Settings
    {
        static bool ms_friendsOnly = false;

        static Category ms_playerListCat;
        static TextBlock ms_playerContextText;

        static Page ms_detailPage;
        static Category ms_encounterCat;
        static string ms_detailUserId;

        internal static void Init()
        {
            BuildUI();
            QuickMenuAPI.OnPlayerSelected += OnPlayerSelected;
        }

        static void BuildUI()
        {
            var page = new Page("PlayerHistory", "Main", isRootPage: true);
            page.MenuTitle = "Player History";
            page.MenuSubtitle = "Players you've met";

            // Controls row
            var controlsCat = page.AddCategory("Controls", false);

            controlsCat.AddToggle("Friends Only", "Only show players on your friends list", ms_friendsOnly)
                .OnValueUpdated += b =>
                {
                    ms_friendsOnly = b;
                    RefreshPlayerList();
                };

            controlsCat.AddButton("Clear History", "", "Clear all recorded players", ButtonStyle.TextOnly)
                .OnPress += OnClearAllHeld;

            // Player list
            ms_playerListCat = page.AddCategory("History", false);
            page.OnPageOpen += RefreshPlayerList;

            // Detail page — hidden nav button, opened programmatically
            var detailNavCat = page.AddCategory("DetailNav", false);
            ms_detailPage = detailNavCat.AddPage("Detail", "", "", "PlayerHistory");
            ms_detailPage.SubpageButton.Hidden = true;
            ms_detailPage.MenuSubtitle = "Encounters";

            var detailActionCat = ms_detailPage.AddCategory("Actions", false);
            detailActionCat.AddButton("Open Player Details", "", "Open this player's CVR profile page")
                .OnPress += () =>
                {
                    if (ms_detailUserId != null)
                        QuickMenuAPI.OpenPlayerListByUserID(ms_detailUserId);
                };

            ms_encounterCat = ms_detailPage.AddCategory("Encounters", false);

            var playerCat = QuickMenuAPI.PlayerSelectPage.AddCategory("Player History", "PlayerHistory");
            ms_playerContextText = playerCat.AddTextBlock("Select a player to see their history");
        }

        internal static void RefreshPlayerList()
        {
            try
            {
                ms_playerListCat.ClearChildren();

                if (HistoryData.Entries.Count == 0)
                {
                    ms_playerListCat.AddTextBlock("No players recorded yet.");
                    return;
                }

                var sorted = HistoryData.Entries.OrderByDescending(e => e.LastSeen).ToList();

                if (ms_friendsOnly)
                    sorted = sorted.Where(e => IsFriend(e.UserId)).ToList();

                if (sorted.Count == 0)
                {
                    ms_playerListCat.AddTextBlock("No friends in history.");
                    return;
                }

                foreach (var entry in sorted)
                {
                    var e = entry;
                    ms_playerListCat.AddButton(
                        $"{e.DisplayName}  ·  {FormatRelative(e.LastSeen)}",
                        "",
                        "View encounter history",
                        ButtonStyle.TextOnly
                    ).OnPress += () => OpenPlayerEntry(e);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static void OpenPlayerEntry(PlayerEntry entry)
        {
            try
            {
                ms_detailUserId = entry.UserId;
                ms_detailPage.MenuTitle = entry.DisplayName;
                ms_encounterCat.ClearChildren();
                foreach (var dt in entry.Encounters)
                    ms_encounterCat.AddTextBlock(dt.ToString("yyyy-MM-dd   HH:mm"));
                ms_detailPage.OpenPage();
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static void OnClearAllHeld()
        {
            QuickMenuAPI.ShowConfirm(
                "Clear History",
                $"Delete all {HistoryData.Entries.Count} recorded players?",
                () =>
                {
                    HistoryData.Clear();
                    HistoryData.Save();
                    RefreshPlayerList();
                    QuickMenuAPI.ShowAlertToast("Player history cleared", 3);
                }
            );
        }

        static void OnPlayerSelected(string username, string userId)
        {
            try
            {
                var entry = HistoryData.Find(userId);
                ms_playerContextText.Text = entry != null
                    ? $"Last seen: {FormatRelative(entry.LastSeen)}"
                    : "Not in history yet";
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        static bool IsFriend(string userId)
        {
            try { return Friends.FriendsWith(userId); }
            catch { return false; }
        }

        static string FormatRelative(DateTime dt)
        {
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}min ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 2) return "yesterday";
            return $"{(int)diff.TotalDays}d ago";
        }
    }
}
