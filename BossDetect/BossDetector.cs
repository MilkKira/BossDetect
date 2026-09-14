using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using UnityEngine;

namespace BossDetect
{
    /// <summary>
    /// 客户端 BOSS 情报核心。通用规则：
    /// - 通知时机：倒计时结束（局内计时器 GameTimer 启动）后自动播报一次；之后按快捷键 O 手动扫描。
    /// - 情报错误：战局实际刷新了 BOSS，但每次播报有概率不推送（多 BOSS 各自独立判定）。
    /// - 分级冷却：1级 120s / 2级 90s / 3级 60s。
    /// - 检测限制：1、2 级无法识别特殊目标（黑狐/伟哥/典狱长），3 级仅播报是否刷新、不显示位置。
    /// - 附加能力：1 级不能检测 BOSS 死亡；2、3 级可在扫描时播报死亡。
    /// - 乱码：2 级 10%、3 级 2% 概率距离以乱码形式描述（通知无法滚动，按静态乱码呈现）。
    /// - 所有参数写死在下方常量区，不使用 BepInEx Config。
    /// </summary>
    public static class BossDetector
    {
        // 固定参数
        private const KeyCode ManualScanKey = KeyCode.O;   // 手动扫描快捷键
        private static readonly bool DebugLogging = false;  // 调试日志开关（写死，改源码后重新编译）

        private const float CooldownSecondsLevel1 = 120f;  // 1 级情报中心冷却
        private const float CooldownSecondsLevel2 = 90f;   // 2 级情报中心冷却
        private const float CooldownSecondsLevel3 = 60f;   // 3 级情报中心冷却

        private const int ErrorRatePercentLevel1 = 25;     // 1 级情报错误率
        private const int ErrorRatePercentLevel2 = 10;     // 2 级情报错误率
        private const int ErrorRatePercentLevel3 = 0;      // 3 级情报错误率

        private const int GarbleRatePercentLevel2 = 10;    // 2 级距离乱码率
        private const int GarbleRatePercentLevel3 = 2;     // 3 级距离乱码率

        /// <summary>
        /// 保存本局已发现目标的最近状态，供手动扫描和死亡播报使用。
        /// </summary>
        private sealed class BossRecord
        {
            public string Name;
            public bool Special;
            public bool Alive = true;
            public bool DeathNotified;
            public float LastDistance;
        }

        private static readonly string[] TierWords =
        {
            "很近", "近", "中近", "中", "中远", "远", "很远"
        };

        // 静态“乱码”字符集，模拟情报被干扰的效果
        private const string GarbleChars = "▓▒░█¤§¶¥€#$%&?@*~¡¿";

        private static readonly Dictionary<string, BossRecord> Roster = new Dictionary<string, BossRecord>();

        private static ManualLogSource _log;
        private static GameWorld _currentGameWorld;
        private static int _intelLevel;
        private static float _lastScanTime;
        private static float _lastIntelNotifyTime = -1000f;
        private static bool _startNotified;
        private static bool _isDynamoRunning;

        public static void Init(ManualLogSource log)
        {
            _log = log;
        }

        /// <summary>
        /// 在 Unity 主线程维护战局状态，每秒更新目标记录，并处理开局播报和按键。
        /// </summary>
        public static void Update()
        {
            GameWorld gameWorld = Singleton<GameWorld>.Instantiated
                ? Singleton<GameWorld>.Instance
                : null;
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

            // 定期维护花名册（只更新数据，不自动发情报）
            if (Time.time - _lastScanTime >= 1f)
            {
                _lastScanTime = Time.time;
                ScanRoster(gameWorld, myPlayer);
            }

            // 开局播报：倒计时结束后自动执行一次情报
            if (!_startNotified && IsCountdownOver())
            {
                _startNotified = true;
                ScanRoster(gameWorld, myPlayer);
                LogDebug("倒计时结束，发送开局 BOSS 情报");
                NotifyCurrentBosses();
            }

            // 手动扫描
            if (Input.GetKeyDown(ManualScanKey))
            {
                TryManualScan();
            }
        }

        /// <summary>
        /// 进入新战局时重置播报和目标记录，并读取本局情报中心等级。
        /// </summary>
        private static void OnNewRaid(GameWorld gameWorld, Player myPlayer)
        {
            _currentGameWorld = gameWorld;
            _startNotified = false;
            _lastIntelNotifyTime = -1000f;
            Roster.Clear();
            _intelLevel = ResolveIntelCenterLevel(myPlayer);
            _isDynamoRunning = ResolveDynamoRunning(myPlayer);
            LogDebug($"进入地图 {gameWorld.LocationId}，情报中心等级 = {_intelLevel} 发电机状态: {_isDynamoRunning}");
        }

        /// <summary>
        /// 清除战局引用和缓存，避免下一局沿用旧目标或冷却时间。
        /// </summary>
        private static void OnRaidEnded()
        {
            LogDebug("离开对局，清空 BOSS 花名册");
            _currentGameWorld = null;
            _startNotified = false;
            _lastIntelNotifyTime = -1000f;
            Roster.Clear();
            _intelLevel = 0;
            _isDynamoRunning = false;
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

        /// <summary>
        /// 更新可识别目标的距离与存活状态；从存活列表消失的目标按死亡或撤离处理。
        /// 此处只维护记录，不发送通知，也不消耗情报冷却。
        /// </summary>
        private static void ScanRoster(GameWorld gameWorld, Player myPlayer)
        {
            HashSet<string> seenProfileIds = new HashSet<string>();

            foreach (var player in gameWorld.AllAlivePlayersList)
            {
                if (player == null || player == myPlayer) continue;

                var profile = player.Profile;
                if (profile == null || profile.Info == null || profile.Info.Settings == null) continue;

                string role = profile.Info.Settings.Role.ToString().ToLowerInvariant();
                bool isBoss = BossRoles.IsBoss(role);
                bool isSpecial = BossRoles.IsSpecialBoss(role);

                // 检测限制：1、2 级无法识别特殊目标（黑狐/伟哥/典狱长）
                if (!isBoss || (isSpecial && _intelLevel < 3)) continue;

                string profileId = profile.Id;
                seenProfileIds.Add(profileId);

                if (!Roster.TryGetValue(profileId, out var record))
                {
                    record = new BossRecord
                    {
                        Name = PlayerNameHelper.GetDisplayName(profile.Info.Nickname),
                        Special = isSpecial
                    };
                    Roster[profileId] = record;
                    LogDebug($"发现新 BOSS：{record.Name} (特殊目标={isSpecial}) ({profileId})");
                }

                if (player.Transform != null && myPlayer.Transform != null)
                {
                    record.LastDistance = Vector3.Distance(myPlayer.Transform.position, player.Transform.position);
                }

                bool deadNow = player.HealthController != null && !player.HealthController.IsAlive;
                if (deadNow)
                {
                    record.Alive = false;
                }
                else if (!record.Alive)
                {
                    // 重新出现（如复活/重刷）时恢复存活状态并重置死亡播报标记
                    record.Alive = true;
                    record.DeathNotified = false;
                    LogDebug($"BOSS 重新出现：{record.Name}");
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

        /// <summary>
        /// 处理手动扫描：冷却中仅提示剩余秒数，冷却结束后刷新记录并播报。
        /// </summary>
        private static void TryManualScan()
        {
            if (_currentGameWorld == null) return;

            float cooldown = GetCooldownSeconds();
            float elapsed = Time.time - _lastIntelNotifyTime;
            if (elapsed < cooldown)
            {
                int remaining = Mathf.CeilToInt(cooldown - elapsed);
                Notify(string.Format(NotifyText.Cooldown, remaining));
                LogDebug($"手动扫描冷却中，剩余 {remaining} 秒");
                return;
            }

            if (!Singleton<GameWorld>.Instantiated) return;
            GameWorld gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null || gameWorld.MainPlayer == null) return;

            ScanRoster(gameWorld, gameWorld.MainPlayer);
            NotifyCurrentBosses();
        }

        private static float GetCooldownSeconds() => _intelLevel switch
        {
            1 => CooldownSecondsLevel1,
            2 => CooldownSecondsLevel2,
            3 => CooldownSecondsLevel3,
            _ => 0f
        };

        private static int GetErrorRatePercent() => _intelLevel switch
        {
            1 => ErrorRatePercentLevel1,
            2 => ErrorRatePercentLevel2,
            3 => ErrorRatePercentLevel3,
            _ => 0
        };

        private static int GetGarbleRatePercent() => _intelLevel switch
        {
            2 => GarbleRatePercentLevel2,
            3 => GarbleRatePercentLevel3,
            _ => 0
        };

        /// <summary>
        /// 核心方法
        /// 按情报等级播报死亡和存活目标；有效情报扫描统一从此处开始冷却。
        /// 无目标或概率性漏报同样消耗冷却，未建造情报中心则不播报。
        /// </summary>
        private static void NotifyCurrentBosses()
        {
            if (_intelLevel < 1 || !_isDynamoRunning)
            {
                LogDebug("情报中心等级不足亦或发电机未启动，无法检测");
                return;
            }

            // 开局播报和手动扫描共用冷却；无目标或情报遗漏也消耗本次扫描。
            _lastIntelNotifyTime = Time.time;

            int errorPercent = GetErrorRatePercent();
            int garblePercent = GetGarbleRatePercent();

            // 附加能力：2、3 级可检测 BOSS 死亡（扫描时播报一次）
            if (_intelLevel >= 2)
            {
                foreach (var record in Roster.Values)
                {
                    if (!record.Alive && !record.DeathNotified)
                    {
                        record.DeathNotified = true;
                        LogDebug($"播报 BOSS 死亡：{record.Name}");
                        Notify(string.Format(NotifyText.BossDead, record.Name));
                    }
                }
            }

            // 存活 BOSS 情报：每个 BOSS 独立计算情报错误率
            foreach (var record in Roster.Values)
            {
                if (!record.Alive) continue;

                if (RollChance(errorPercent))
                {
                    LogDebug($"情报错误：{record.Name} 已刷新但未推送提示");
                    continue;
                }

                switch (_intelLevel)
                {
                    case 1:
                        // 仅提示该区域是否刷新 BOSS，不含位置/距离
                        Notify(string.Format(NotifyText.Level1, record.Name));
                        break;

                    case 2:
                        string tier = MaybeGarble(TierWords[DistanceTier(record.LastDistance)], garblePercent);
                        Notify(string.Format(NotifyText.Level2, record.Name, tier));
                        break;

                    case 3:
                        if (record.Special)
                        {
                            // 特殊目标：仅播报是否刷新，不显示位置
                            Notify(string.Format(NotifyText.Level3Special, record.Name));
                        }
                        else
                        {
                            string distance = MaybeGarble(Mathf.RoundToInt(record.LastDistance).ToString(), garblePercent);
                            Notify(string.Format(NotifyText.Level3, record.Name, distance));
                        }
                        break;
                }
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

        /// <summary>
        /// 概率判定：返回 true 表示本次命中了配置的概率（触发错误/乱码等效果）。
        /// </summary>
        private static bool RollChance(int percent)
        {
            return percent > 0 && UnityEngine.Random.Range(0, 100) < percent;
        }

        /// <summary>
        /// 按概率把文本替换成乱码；未命中时原样返回。
        /// </summary>
        private static string MaybeGarble(string text, int percent)
        {
            return RollChance(percent) ? Garble(text) : text;
        }

        /// <summary>
        /// 乱码文本（通知无法真正滚动，按与原文字数相同的静态乱码呈现，模拟干扰效果）。
        /// </summary>
        private static string Garble(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            char[] chars = new char[source.Length];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = GarbleChars[UnityEngine.Random.Range(0, GarbleChars.Length)];
            }
            return new string(chars);
        }

        /// <summary>
        /// 从玩家藏身处读取情报中心等级；数据缺失或读取失败时按未建造处理。
        /// </summary>
        private static int ResolveIntelCenterLevel(Player myPlayer)
        {
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

        /// <summary>
        /// 入局时检查发电机已建造、开关开启且燃料槽中有剩余燃料；读取失败按未运行处理。
        /// </summary>
        private static bool ResolveDynamoRunning(Player myPlayer)
        {
            try
            {
                var areas = myPlayer?.Profile?.Hideout?.Areas;
                if (areas == null)
                {
                    LogDebug("Profile 中没有藏身处数据，发电机按未运行处理");
                    return false;
                }

                foreach (var area in areas)
                {
                    if (area != null && area.AreaType == EAreaType.Generator)
                    {
                        if (area.Level <= 0 || !area.Active || area.Slots == null)
                            return false;

                        if (!Singleton<ItemFactoryClass>.Instantiated)
                            return false;

                        foreach (var slot in area.Slots)
                        {
                            if (slot?.Items == null || slot.Items.Length == 0) continue;

                            // 与游戏发电机初始化一致，通过物品工厂还原燃料及其剩余资源。
                            var items = Singleton<ItemFactoryClass>.Instance.FlatItemsToTree(slot.Items).Items;
                            foreach (var item in items.Values)
                            {
                                if (item is FuelItemClass fuel &&
                                    fuel.ResourceHolderComponent is ResourceComponent resource &&
                                    resource.Value > 0f)
                                    return true;
                            }
                        }

                        return false;
                    }
                }

                LogDebug("未找到发电机设施，按未运行处理");
                return false;
            }
            catch (Exception e)
            {
                LogDebug($"读取发电机运行状态失败：{e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 通过游戏原生通知显示文本，记录发送异常，避免中断后续帧更新。
        /// </summary>
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
            if (DebugLogging)
            {
                _log.LogInfo($"[BossDetect] {message}");
            }
        }
    }
}
