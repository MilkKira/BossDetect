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
    /// 3. 通知时机：
    ///    - 游戏开局（倒计时结束后，即局内计时器 GameTimer 启动）自动提示一次；
    ///    - 之后所有通知均需按下快捷键 O 手动触发，并按情报中心等级执行冷却：
    ///      1级 10 秒 / 2级 30 秒 / 3级 10 秒。
    /// 4. 通知内容按等级区分：
    ///    1级：无法探测位置；2级：远近描述词；3级：精确距离（米）。
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
        private static float _lastManualNotifyTime = -1000f;
        private static bool _startNotified;

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<KeyboardShortcut> Hotkey;
        public static ConfigEntry<int> IntelLevelOverride;
        public static ConfigEntry<float> CooldownLevel1;
        public static ConfigEntry<float> CooldownLevel2;
        public static ConfigEntry<float> CooldownLevel3;
        public static ConfigEntry<bool> DebugLog;

        public static void Init(ConfigFile config, ManualLogSource log)
        {
            _log = log;

            Enabled = config.Bind("Boss Detect / 敌对首领检测", "启用", true,
                "是否启用 BOSS 检测通知。");

            Hotkey = config.Bind("Boss Detect / 敌对首领检测", "手动通知快捷键", new KeyboardShortcut(KeyCode.O, new KeyCode[0]),
                "开局提示后，按此键手动获取 BOSS 情报。");

            IntelLevelOverride = config.Bind("Boss Detect / 敌对首领检测", "情报中心等级覆盖", -1,
                new ConfigDescription(
                    "手动指定情报中心等级(1/2/3)用于测试，-1 为自动读取玩家藏身处真实等级。",
                    new AcceptableValueRange<int>(-1, 3)));

            CooldownLevel1 = config.Bind("Boss Detect / 敌对首领检测", "1级冷却(秒)", 10f,
                new ConfigDescription(
                    "情报中心 1 级时，手动通知的冷却时间。",
                    new AcceptableValueRange<float>(0f, 300f)));

            CooldownLevel2 = config.Bind("Boss Detect / 敌对首领检测", "2级冷却(秒)", 30f,
                new ConfigDescription(
                    "情报中心 2 级时，手动通知的冷却时间。",
                    new AcceptableValueRange<float>(0f, 300f)));

            CooldownLevel3 = config.Bind("Boss Detect / 敌对首领检测", "3级冷却(秒)", 10f,
                new ConfigDescription(
                    "情报中心 3 级时，手动通知的冷却时间。",
                    new AcceptableValueRange<float>(0f, 300f)));

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

            // 定期扫描花名册（只更新数据，不自动发通知）
            if (Time.time - _lastScanTime >= 1f)
            {
                _lastScanTime = Time.time;
                ScanRoster(gameWorld, myPlayer);
            }

            // 开局通知：倒计时结束（局内计时器启动）后提示一次
            if (!_startNotified && IsCountdownOver())
            {
                _startNotified = true;
                ScanRoster(gameWorld, myPlayer);
                LogDebug("倒计时结束，发送开局 BOSS 情报");
                NotifyCurrentBosses();
            }

            // 手动通知：按快捷键 O
            if (Hotkey.Value.IsDown())
            {
                TryManualNotify();
            }
        }

        private static void OnNewRaid(GameWorld gameWorld, Player myPlayer)
        {
            _currentGameWorld = gameWorld;
            _startNotified = false;
            _lastManualNotifyTime = -1000f;
            Roster.Clear();
            _intelLevel = ResolveIntelCenterLevel(myPlayer);
            LogDebug($"进入地图 {gameWorld.LocationId}，情报中心等级 = {_intelLevel}");
        }

        private static void OnRaidEnded()
        {
            LogDebug("离开对局，清空 BOSS 花名册");
            _currentGameWorld = null;
            _startNotified = false;
            _lastManualNotifyTime = -1000f;
            Roster.Clear();
            _intelLevel = 0;
        }

        /// <summary>
        /// 倒计时结束信号：本地/联机对局都是在部署倒计时（TimeBeforeDeployLocal）结束后才启动
        /// 局内计时器（GameTimerClass.Start → Status = Started），因此以 GameTimer 状态为准。
        /// </summary>
        private static bool IsCountdownOver()
        {
            try
            {
                if (!Singleton<AbstractGame>.Instantiated) return false;
                GameTimerClass timer = Singleton<AbstractGame>.Instance.GameTimer;
                return timer != null && timer.Status == GameTimerClass.EGameTimerStatus.Started;
            }
            catch
            {
                return false;
            }
        }

        private static void ScanRoster(GameWorld gameWorld, Player myPlayer)
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

                // 重新出现（如复活/重刷）时恢复存活状态
                record.Alive = true;
                record.PlayerRef = player;

                if (player.Transform != null && myPlayer.Transform != null)
                {
                    record.LastDistance = Vector3.Distance(myPlayer.Transform.position, player.Transform.position);
                }

                if (player.HealthController != null && !player.HealthController.IsAlive)
                {
                    record.Alive = false;
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

        private static void TryManualNotify()
        {
            if (_currentGameWorld == null) return;

            float cooldown = GetCooldownSeconds();
            float elapsed = Time.time - _lastManualNotifyTime;
            if (elapsed < cooldown)
            {
                LogDebug($"手动通知冷却中，剩余 {Mathf.CeilToInt(cooldown - elapsed)} 秒");
                return;
            }

            _lastManualNotifyTime = Time.time;

            if (!Singleton<GameWorld>.Instantiated) return;
            GameWorld gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null || gameWorld.MainPlayer == null) return;

            ScanRoster(gameWorld, gameWorld.MainPlayer);
            NotifyCurrentBosses();
        }

        private static float GetCooldownSeconds()
        {
            switch (_intelLevel)
            {
                case 1: return CooldownLevel1.Value;
                case 2: return CooldownLevel2.Value;
                case 3: return CooldownLevel3.Value;
                default: return 0f;
            }
        }

        private static void NotifyCurrentBosses()
        {
            if (_intelLevel < 1)
            {
                LogDebug("情报中心等级不足(0)，不发送通知");
                return;
            }

            bool any = false;
            foreach (var record in Roster.Values)
            {
                if (!record.Alive) continue;
                any = true;

                switch (_intelLevel)
                {
                    case 1:
                        Notify(string.Format(NotifyText.Level1, record.Name));
                        break;

                    case 2:
                        Notify(string.Format(NotifyText.Level2, record.Name, TierWords[DistanceTier(record.LastDistance)]));
                        break;

                    case 3:
                        Notify(string.Format(NotifyText.Level3, record.Name, Mathf.RoundToInt(record.LastDistance)));
                        break;
                }
            }

            if (!any)
            {
                LogDebug("当前未检测到 BOSS");
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
