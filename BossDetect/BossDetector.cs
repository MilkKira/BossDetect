using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Communications;
using UnityEngine;

namespace BossDetect
{
    /// <summary>
    /// 客户端 BOSS 检测核心：
    /// 1. 每 1 秒扫描 GameWorld.AllAlivePlayersList，识别 BOSS（复用旧 BossPanel 的判定规则）。
    /// 2. 读取玩家 Profile.Hideout 中情报中心(IntelligenceCenter)等级。
    /// 3. 按等级用游戏内 Notify 通知：
    ///    1级：仅提示 BOSS 存在，无法探测位置。
    ///    2级：提示远近描述词（25/50/75/100/150/200m 分档），档次变化时重新通知。
    ///    3级：实时显示精确距离（米），按配置间隔与最小变化量刷新。
    /// </summary>
    public static class BossDetector
    {
        private class BossRecord
        {
            public string ProfileId;
            public string Name;
            public Player PlayerRef;
            public bool Alive = true;
            public float LastDistance;

            // 1级：每个 BOSS 每局只通知一次
            public bool Level1Notified;

            // 2级：远近档位缓存，档位变化才重新通知
            public int LastTier = -1;

            // 3级：实时距离刷新状态
            public float LastLiveNotifyTime;
            public float LastLiveNotifyDistance = -1f;
        }

        private static readonly string[] TierWords =
        {
            "很近", "近", "中近", "中", "中远", "远", "很远"
        };

        private static readonly Dictionary<string, BossRecord> Roster = new Dictionary<string, BossRecord>();

        private static ManualLogSource _log;
        private static GameWorld _currentGameWorld;
        private static int _intelLevel;
        private static float _lastScanTime;

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<int> IntelLevelOverride;
        public static ConfigEntry<float> LiveRefreshInterval;
        public static ConfigEntry<float> LiveMinDelta;
        public static ConfigEntry<bool> DebugLog;

        public static void Init(ConfigFile config, ManualLogSource log)
        {
            _log = log;

            Enabled = config.Bind("Boss Detect / 敌对首领检测", "启用", true,
                "是否启用 BOSS 检测通知。");

            IntelLevelOverride = config.Bind("Boss Detect / 敌对首领检测", "情报中心等级覆盖", -1,
                new ConfigDescription(
                    "手动指定情报中心等级(1/2/3)用于测试，-1 为自动读取玩家藏身处真实等级。",
                    new AcceptableValueRange<int>(-1, 3)));

            LiveRefreshInterval = config.Bind("Boss Detect / 敌对首领检测", "3级实时刷新间隔(秒)", 5f,
                new ConfigDescription(
                    "情报中心 3 级时，实时距离通知的刷新间隔。",
                    new AcceptableValueRange<float>(1f, 60f)));

            LiveMinDelta = config.Bind("Boss Detect / 敌对首领检测", "3级最小距离变化(米)", 5f,
                new ConfigDescription(
                    "距离相对上次通知变化超过该值才刷新实时通知，避免刷屏。",
                    new AcceptableValueRange<float>(1f, 500f)));

            DebugLog = config.Bind("Boss Detect / 敌对首领检测", "调试日志", false,
                "输出详细调试日志。");
        }

        public static void Update()
        {
            if (!Enabled.Value) return;

            if (!Singleton<GameWorld>.Instantiated)
            {
                if (_currentGameWorld != null) OnRaidEnded();
                return;
            }

            GameWorld gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null)
            {
                if (_currentGameWorld != null) OnRaidEnded();
                return;
            }

            Player myPlayer = gameWorld.MainPlayer;
            if (myPlayer == null) return;

            if (gameWorld != _currentGameWorld)
            {
                OnNewRaid(gameWorld, myPlayer);
            }

            if (Time.time - _lastScanTime < 1f) return;
            _lastScanTime = Time.time;

            ScanAndNotify(gameWorld, myPlayer);
        }

        private static void OnNewRaid(GameWorld gameWorld, Player myPlayer)
        {
            _currentGameWorld = gameWorld;
            Roster.Clear();
            _intelLevel = ResolveIntelCenterLevel(myPlayer);
            LogDebug($"进入地图 {gameWorld.LocationId}，情报中心等级 = {_intelLevel}");
        }

        private static void OnRaidEnded()
        {
            LogDebug("离开对局，清空 BOSS 花名册");
            _currentGameWorld = null;
            Roster.Clear();
            _intelLevel = 0;
        }

        private static void ScanAndNotify(GameWorld gameWorld, Player myPlayer)
        {
            HashSet<string> seenProfileIds = new HashSet<string>();

            foreach (var player in gameWorld.AllAlivePlayersList)
            {
                if (player == null || player == myPlayer) continue;

                var profile = player.Profile;
                if (profile == null || profile.Info == null || profile.Info.Settings == null) continue;

                if (!BossRoles.IsBoss(profile.Info.Settings.Role)) continue;

                string profileId = profile.Id;
                seenProfileIds.Add(profileId);

                if (!Roster.TryGetValue(profileId, out var record))
                {
                    record = new BossRecord
                    {
                        ProfileId = profileId,
                        Name = PlayerNameHelper.GetDisplayName(profile.Info.Nickname)
                    };
                    Roster[profileId] = record;
                    LogDebug($"发现新 BOSS：{record.Name} ({profileId})");
                }

                // 重新出现（如复活/重刷）时重置通知状态
                if (!record.Alive)
                {
                    record.Alive = true;
                    record.Level1Notified = false;
                    record.LastTier = -1;
                    record.LastLiveNotifyDistance = -1f;
                    LogDebug($"BOSS 重新出现：{record.Name}");
                }

                record.PlayerRef = player;

                if (player.Transform != null && myPlayer.Transform != null)
                {
                    record.LastDistance = Vector3.Distance(myPlayer.Transform.position, player.Transform.position);
                }

                if (player.HealthController != null && !player.HealthController.IsAlive)
                {
                    record.Alive = false;
                }

                if (record.Alive)
                {
                    NotifyForRecord(record);
                }
            }

            // 兜底：活着但从存活列表消失，判定为死亡/撤离
            foreach (var kvp in Roster)
            {
                var record = kvp.Value;
                if (record.Alive && !seenProfileIds.Contains(kvp.Key))
                {
                    record.Alive = false;
                    LogDebug($"BOSS 消失/死亡：{record.Name}");
                }
            }
        }

        private static void NotifyForRecord(BossRecord record)
        {
            switch (_intelLevel)
            {
                case 1:
                    if (!record.Level1Notified)
                    {
                        record.Level1Notified = true;
                        Notify(string.Format(NotifyText.Level1, record.Name));
                    }
                    break;

                case 2:
                    int tier = DistanceTier(record.LastDistance);
                    if (tier != record.LastTier)
                    {
                        record.LastTier = tier;
                        Notify(string.Format(NotifyText.Level2, record.Name, TierWords[tier]));
                    }
                    break;

                case 3:
                    int meters = Mathf.RoundToInt(record.LastDistance);
                    bool first = record.LastLiveNotifyDistance < 0f;
                    bool intervalElapsed = Time.time - record.LastLiveNotifyTime >= LiveRefreshInterval.Value;
                    bool movedEnough = Mathf.Abs(meters - record.LastLiveNotifyDistance) >= LiveMinDelta.Value;

                    if (first || (intervalElapsed && movedEnough))
                    {
                        record.LastLiveNotifyTime = Time.time;
                        record.LastLiveNotifyDistance = meters;
                        Notify(string.Format(NotifyText.Level3, record.Name, meters));
                    }
                    break;
            }
        }

        /// <summary>
        /// 距离远近档位（假设各档位为"小于等于"上限，200m 以上为很远）：
        /// ≤25 很近 | ≤50 近 | ≤75 中近 | ≤100 中 | ≤150 中远 | ≤200 远 | &gt;200 很远
        /// </summary>
        private static int DistanceTier(float meters)
        {
            if (meters <= 25f) return 0;
            if (meters <= 50f) return 1;
            if (meters <= 75f) return 2;
            if (meters <= 100f) return 3;
            if (meters <= 150f) return 4;
            if (meters <= 200f) return 5;
            return 6;
        }

        private static int ResolveIntelCenterLevel(Player myPlayer)
        {
            // 手动覆盖优先（用于测试三个档位）
            if (IntelLevelOverride.Value >= 1 && IntelLevelOverride.Value <= 3)
            {
                LogDebug($"使用配置覆盖的情报中心等级：{IntelLevelOverride.Value}");
                return IntelLevelOverride.Value;
            }

            try
            {
                var areas = myPlayer?.Profile?.Hideout?.Areas;
                if (areas == null)
                {
                    LogDebug("Profile 中没有藏身处数据，情报中心等级按 0 处理");
                    return 0;
                }

                foreach (var area in areas)
                {
                    if (area != null && area.AreaType == EAreaType.IntelligenceCenter)
                    {
                        return area.Level;
                    }
                }

                LogDebug("未找到情报中心设施，等级按 0 处理");
                return 0;
            }
            catch (Exception e)
            {
                LogDebug($"读取情报中心等级失败：{e.Message}");
                return 0;
            }
        }

        private static void Notify(string message)
        {
            try
            {
                NotificationManagerClass.DisplayMessageNotification(
                    message,
                    ENotificationDurationType.Default,
                    ENotificationIconType.Default,
                    null);
            }
            catch (Exception e)
            {
                _log.LogError($"BossDetect：发送通知失败：{e.Message}");
            }
        }

        private static void LogDebug(string message)
        {
            if (DebugLog.Value)
            {
                _log.LogInfo($"[BossDetect] {message}");
            }
        }
    }
}
