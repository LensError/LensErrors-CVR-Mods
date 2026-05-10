using ABI_RC.Core.Player;
using ABI_RC.Systems.GameEventSystem;
using MelonLoader;
using System;

namespace PlayerHistory
{
    static class GameEvents
    {
        internal static void Init()
        {
            CVRGameEventSystem.Player.OnJoinEntity.AddListener(OnPlayerJoin);
        }

        static void OnPlayerJoin(CVRPlayerEntity entity)
        {
            try
            {
                HistoryData.AddOrUpdate(entity.Uuid, entity.Username);
                HistoryData.Save();
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }
    }
}
