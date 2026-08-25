using BepInEx.Bootstrap;
using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using EFTBallisticCalculator.Locale;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public enum ETeammateStatus
    {
        Alive,
        Dead,
        Extracted
    }

    public static class TeamPanel
    {
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> RectWidth;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;

        // 【修改为 public】：让 FikaIntegration 工具类可以修改状态
        public class TeammateRecord
        {
            public string AccountId;
            public string Name;
            public Player PlayerRef;
            public ETeammateStatus Status = ETeammateStatus.Alive; 
            public int FikaNetId = -1;
        }

        private static EFT.GameWorld _lastGameWorld = null;
        private static bool _isDebugMode = false;
        private static int _debugTargetCount = 4;
        private static HashSet<string> _debugFakeTeammates = new HashSet<string>();

        // 【修改为 public】：让 FikaIntegration 工具类可以遍历
        public static readonly Dictionary<string, TeammateRecord> _roster = new Dictionary<string, TeammateRecord>();
        private static float _lastScanTime = 0f;

        // Fika 软依赖检测
        private static bool _isFikaLoaded = false;
        private const string FIKA_GUID = "com.fika.core";

        public static void InitCfg(ConfigFile config)
        {
            // ... (原有的 Bind 代码保持完全不变) ...
            OffsetX = config.Bind("Team Panel / 队伍数据", "X轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_x_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_x_name"), IsAdvanced = true }));

            OffsetY = config.Bind("Team Panel / 队伍数据", "Y轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Team Panel / 队伍数据", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_scale_name"), IsAdvanced = true }));

            Active = config.Bind("Team Panel / 队伍数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_active_name") }));

            Color = config.Bind("Team Panel / 队伍数据", "颜色设置", new Color(0.8f, 0.9f, 1f, 0.85f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_color_name") }));

            RectWidth = config.Bind("Team Panel / 队伍数据", "面板宽度", 340f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_team_rect_desc"),
                new AcceptableValueRange<float>(0f, 800f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_team_rect_name"), IsAdvanced = true }));

            // 检测 Fika 是否安装
            _isFikaLoaded = Chainloader.PluginInfos.ContainsKey(FIKA_GUID);
        }

        private static void UpdateRoster()
        {
            bool forceUpdate = false;
            foreach (var r in _roster.Values)
            {
                if (r.Status == ETeammateStatus.Alive && (r.PlayerRef == null || r.PlayerRef.HealthController == null))
                {
                    forceUpdate = true;
                    break;
                }
            }

            if (!forceUpdate && Time.time - _lastScanTime < 2f) return;
            _lastScanTime = Time.time;

            var gw = PluginsCore.CorrectGameWorld;

            if (gw != null && gw != _lastGameWorld)
            {
                _roster.Clear();
                _debugFakeTeammates.Clear();
                _lastGameWorld = gw;
                if (_isFikaLoaded)
                {
                    SafeClearFikaCache();
                }
            }
            string myGroupId = PluginsCore.CorrectGroupId;

            if (gw == null || string.IsNullOrEmpty(myGroupId)) return;

            HashSet<string> aliveProfileIds = new HashSet<string>();

            // 1. 扫描当前存活列表，更新或添加队友
            foreach (var player in gw.AllAlivePlayersList)
            {
                if (player == null || player == PluginsCore.CorrectPlayer) continue;

                bool isTeammate = false;

                if (_isDebugMode)
                {
                    string profileId = player.Profile?.Id;
                    if (!string.IsNullOrEmpty(profileId))
                    {
                        if (_debugFakeTeammates.Contains(profileId)) isTeammate = true;
                        else if (_debugFakeTeammates.Count < _debugTargetCount)
                        {
                            _debugFakeTeammates.Add(profileId);
                            isTeammate = true;
                        }
                    }
                }
                else
                {
                    string targetGroupId = player.Profile?.Info?.GroupId ?? "";
                    isTeammate = !string.IsNullOrEmpty(myGroupId) && targetGroupId == myGroupId;
                }

                if (isTeammate && player.Profile != null)
                {
                    string profileId = player.Profile.Id;
                    aliveProfileIds.Add(profileId);

                    if (!_roster.TryGetValue(profileId, out var record))
                    {
                        string safeName = GetLatinName(player.Profile.Info.Nickname);
                        record = new TeammateRecord
                        {
                            AccountId = profileId,
                            Name = _isDebugMode ? $"[TEST] {safeName}" : safeName,
                            Status = ETeammateStatus.Alive
                        };
                        _roster[profileId] = record;
                    }

                    record.PlayerRef = player;

                    // 原版死亡判定：血条归零
                    if (player.HealthController != null && !player.HealthController.IsAlive)
                    {
                        record.Status = ETeammateStatus.Dead;
                    }
                }
            }

            // 2. 将控制权交给 FikaIntegration 去精准判定撤离状态
            if (_isFikaLoaded)
            {
                SafeCallFikaUpdate();
            }

            // 3. 兜底消失判定 (防断线/异常丢失)
            foreach (var kvp in _roster)
            {
                var record = kvp.Value;
                if (record.Status == ETeammateStatus.Alive && !aliveProfileIds.Contains(record.AccountId))
                {
                    // 既没有血条归零，又没在 Fika 的撤离列表里，人却没了，按失踪(死亡)处理
                    record.Status = ETeammateStatus.Dead;
                }
            }
        }

        public static float Draw(float startX, float startY, float globalScale)
        {
            // 【核心拦截】：没开面板，或者没加载 Fika，直接跳过绘制，变为纯联机功能！
            if (!Active.Value || !_isFikaLoaded || PluginsCore.CorrectPlayer == null) return startY;

            UpdateRoster();

            if (string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && _roster.Count == 0) return startY;

            float finalScale = globalScale * Scale.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = RectWidth.Value * finalScale;

            float finalX = startX + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize }.ApplyTarkovFont();
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize }.ApplyTarkovFont();
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            float currentY = finalY;

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), LocaleManager.Get("team_title"), mainColor, titleStyle);
            currentY += lh;

            // 1. 绘制自己 (You)
            DrawPlayerLine(PluginsCore.CorrectPlayer.Profile?.Info?.Nickname ?? "YOU", PluginsCore.CorrectPlayer, true,
                PluginsCore.CorrectPlayer?.ActiveHealthController?.IsAlive == false ? ETeammateStatus.Dead : ETeammateStatus.Alive,
                finalX, ref currentY, rectWidth, lh, textStyle, mainColor);

            // 2. 绘制花名册里的所有队友 (Teammates)
            foreach (var record in _roster.Values)
            {
                DrawPlayerLine(record.Name, record.PlayerRef, false, record.Status, finalX, ref currentY, rectWidth, lh, textStyle, mainColor);
            }

            return currentY;
        }

        private static void DrawPlayerLine(string name, Player player, bool isSelf, ETeammateStatus status, float x, ref float y, float width, float lh, GUIStyle style, Color defaultColor)
        {
            string prefix = isSelf ? LocaleManager.Get("team_you") : LocaleManager.Get("team_ally");

            // --- 状态判定分支 ---
            if (status == ETeammateStatus.Dead)
            {
                string deadLine = string.Format(LocaleManager.Get("team_teammate_dead"),
                   LocaleManager.Get("team_teammate_color_dead"),
                   prefix,
                   name,
                   LocaleManager.Get("team_teammate_dead_tag"));
                HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), deadLine, defaultColor, style);
                y += lh;
                return;
            }
            else if (status == ETeammateStatus.Extracted)
            {
                // 撤离状态，用绿色高亮显示 (可自行去 Locale 字典里加词条，这里临时用硬编码富文本展示)
                string extLine = string.Format(LocaleManager.Get("team_teammate_dead"),
                   LocaleManager.Get("team_teammate_color_dead"),
                   prefix,
                   name,
                   LocaleManager.Get("team_teammate_extracted_tag"));
                HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), extLine, defaultColor, style);
                y += lh;
                return;
            }

            // Fika 联机同步中 (移除了 player.Physical == null 的判断)
            if (player == null || player.HealthController == null)
            {
                string syncLine = string.Format(LocaleManager.Get("team_teammate_dead"),
                    LocaleManager.Get("team_teammate_color_loading"),
                    prefix,
                    name,
                    LocaleManager.Get("team_teammate_loading_tag"));
                HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), syncLine, defaultColor, style);
                y += lh;
                return;
            }

            var healthCtrl = player.HealthController;

            // 存活状态：计算总血量
            float totalHp = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Current;
            float maxHp = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Maximum;
            var level = player?.Profile?.Info?.Level ?? 0;

            // 吃喝数据抓取 (原版 try，Fika 兜底)
            // --- 吃喝数据终极抓取逻辑 ---
            float hydr = 0f, hydrmax = 100f, energy = 0f, energymax = 100f;

            //Fika数据
            if (_isFikaLoaded)
            {
                SafeCallFikaStats(healthCtrl, out hydr, out hydrmax, out energy, out energymax);
            }

            //fallback到原版
            if (!_isFikaLoaded || hydrmax <= 0f || energymax <= 0f)
            {
                try
                {
                    hydr = healthCtrl.Hydration.Current;
                    hydrmax = healthCtrl.Hydration.Maximum;
                    energy = healthCtrl.Energy.Current;
                    energymax = healthCtrl.Energy.Maximum;
                }
                catch { }
            }
            //保底
            if (hydrmax <= 0f) { hydr = 0f; hydrmax = 100f; }
            if (energymax <= 0f) { energy = 0f; energymax = 100f; }

            var side = GetPlayerSide(player);

            string mainLine = string.Format(LocaleManager.Get("team_teammate_alive"),
                LocaleManager.Get($"team_teammate_color_{side}"),
                prefix,
                name,
                side !=0 ? string.Format(LocaleManager.Get("team_teammate_level"), level) : "",
                LocaleManager.Get($"health_bio_hp_color_{HealthPanel.HealthStatusLow(totalHp, maxHp)}"),
                totalHp,
                maxHp,
                LocaleManager.Get("team_teammate_alive_tag"));//$"<b>{prefix} {name}</b> - <color={hpColor}>({totalHp:F0}/{maxHp:F0})</color> <color=#55ff55>[ALIVE]</color>";

            HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), mainLine, defaultColor, style);
            y += lh;

            // 过滤自己，渲染下层吃喝状态 (彻底遗弃了负重的渲染)
            if (!player.IsYourPlayer)
            {
                // 你可以根据需求更新 locale 词条，把负重占位符去了，这里直接写死了吃喝的排版
                string subLine = string.Format(LocaleManager.Get("team_teammate_status"),
                    LocaleManager.Get($"health_bio_hydr_color_{HealthPanel.HealthStatusLow(hydr, hydrmax)}"),
                    hydr,
                    LocaleManager.Get($"health_bio_energy_color_{HealthPanel.HealthStatusLow(energy, energymax)}"),
                    energy);//$"  └ W: {weight}kg | H: {hydr} | E: {energy}";
                HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), subLine, defaultColor, style);
                y += lh;
            }

            y += 4f;
        }

        private static int GetPlayerSide(Player player)
        {
            if (player == null) return 0;
            switch (player?.Profile?.Info?.Side.ToString() ?? "")
            {
                case "Savage": return 0;
                case "Bear": return 1;
                case "Usec": return 2;
            }
            return 0;
        }

        public static string GetLatinName(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) return "UNKNOWN";

            // 如果全是英文字符/数字，直接返回，省性能
            if (IsAllEnglish(nickname)) return nickname;

            try
            {
                // 调用原版底层方法翻译俄语名字 (常用于狗牌)
                return GStruct21.ConvertToLatinic(nickname);
            }
            catch
            {
                // 兜底：如果某些特定版本找不到 GStruct21，直接返回原名防止报错
                return nickname;
            }
        }


        public static bool IsAllEnglish(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                // 允许大写 A-Z，小写 a-z，数字 0-9，以及空格、连字符、下划线
                if ((c < 'A' || c > 'Z') &&
                    (c < 'a' || c > 'z') &&
                    (c < '0' || c > '9') &&
                    c != ' ' && c != '-' && c != '_')
                {
                    return false;
                }
            }
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SafeCallFikaUpdate()
        {
            // JIT 只有在 _isFikaLoaded 为 true 时，才会进入这里并解析 FikaIntegration
            FikaIntegration.UpdateFikaTeammateStatus(_roster);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SafeCallFikaStats(IHealthController healthCtrl, out float hydr, out float hydrmax, out float energy, out float energymax)
        {
            FikaIntegration.TryGetFikaStats(healthCtrl, out hydr, out hydrmax, out energy, out energymax);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SafeClearFikaCache()
        {
            FikaIntegration.ClearCache();
        }
    }
}