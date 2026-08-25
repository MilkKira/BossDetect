using BepInEx.Configuration;
using EFT;
using EFTBallisticCalculator.Locale; // 如果没用到可以删掉，这里保留兼容
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static GClass1460;

namespace EFTBallisticCalculator.HUD
{
    public enum EBossStatus
    {
        Alive,
        Dead
    }

    public static class BossPanel
    {

        public class BossRecord
        {
            public string AccountId;
            public string Name;
            public string RolePrefix;
            public string RoleColor;
            public Player PlayerRef;
            public EBossStatus Status = EBossStatus.Alive;
            public int LastDistance = 0; // 缓存最后距离，死了也能知道在哪
        }
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> RectWidth;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;

        private static EFT.GameWorld _lastGameWorld = null;
        public static readonly Dictionary<string, BossRecord> _roster = new Dictionary<string, BossRecord>();
        private static float _lastScanTime = 0f;

        public static void InitCfg(ConfigFile config)
        {
            // 给 Boss 面板专属的配置绑定，Locale 键名我都帮你起好了，方便你直接去填词条
            OffsetX = config.Bind("Boss Panel / 敌对首领", "X轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_boss_x_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_boss_x_name"), IsAdvanced = true }));

            OffsetY = config.Bind("Boss Panel / 敌对首领", "Y轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_boss_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_boss_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Boss Panel / 敌对首领", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_boss_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_boss_scale_name"), IsAdvanced = true }));

            Active = config.Bind("Boss Panel / 敌对首领", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_boss_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_boss_active_name") }));

            // 默认颜色稍微带点红，以示敌意
            Color = config.Bind("Boss Panel / 敌对首领", "颜色设置", new UnityEngine.Color(0.8f, 0f, 0f, 0.85f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_boss_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_boss_color_name") }));

            RectWidth = config.Bind("Boss Panel / 敌对首领", "面板宽度", 340f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_boss_rect_desc"),
                new AcceptableValueRange<float>(0f, 800f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_boss_rect_name"), IsAdvanced = true }));
        }

        private static void UpdateRoster()
        {
            // 限制扫描频率，每1秒扫描一次足够了，省性能
            if (Time.time - _lastScanTime < 1f) return;
            _lastScanTime = Time.time;

            var gw = PluginsCore.CorrectGameWorld;
            var myPlayer = PluginsCore.CorrectPlayer;

            if (gw != null && gw != _lastGameWorld)
            {
                _roster.Clear();
                _lastGameWorld = gw;
            }

            if (gw == null || myPlayer == null) return;

            HashSet<string> aliveProfileIds = new HashSet<string>();

            // 1. 扫描当前存活列表，抓取Boss
            foreach (var player in gw.AllAlivePlayersList)
            {
                if (player == null || player == myPlayer) continue;

                var profile = player.Profile;
                if (profile == null || profile.Info == null) continue;

                var role = profile.Info.Settings?.Role.ToString().ToLower() ?? "assault";

                // 判定是否是我们关心的特殊单位 (套用你的ESP逻辑)
                if (TryGetBossPrefix(role, out string rolePrefix, out string roleColor))
                {
                    string profileId = profile.Id;
                    aliveProfileIds.Add(profileId);

                    if (!_roster.TryGetValue(profileId, out var record))
                    {
                        string safeName = TeamPanel.GetLatinName(profile.Info.Nickname);
                        record = new BossRecord
                        {
                            AccountId = profileId,
                            Name = safeName,
                            RolePrefix = rolePrefix,
                            RoleColor = roleColor,
                            Status = EBossStatus.Alive
                        };
                        _roster[profileId] = record;
                    }

                    record.PlayerRef = player;

                    // 更新距离 (只在活着的时候更新，死了就定格在最后位置)
                    if (record.Status == EBossStatus.Alive && player.Transform != null && myPlayer.Transform != null)
                    {
                        record.LastDistance = Mathf.RoundToInt(Vector3.Distance(myPlayer.Transform.position, player.Transform.position));
                    }

                    // 原版死亡判定：血条归零
                    if (player.HealthController != null && !player.HealthController.IsAlive)
                    {
                        record.Status = EBossStatus.Dead;
                    }
                }
            }

            // 2. 兜底消失判定 (被系统刷掉的尸体)
            foreach (var kvp in _roster)
            {
                var record = kvp.Value;
                if (record.Status == EBossStatus.Alive && !aliveProfileIds.Contains(record.AccountId))
                {
                    // 活着但从AliveList里消失了，判定为死亡
                    record.Status = EBossStatus.Dead;
                }
            }
        }

        public static float Draw(float startX, float startY, float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return startY;

            UpdateRoster();

            if (_roster.Count == 0) return startY;

            // 彻底使用自己的配置项进行计算
            float finalScale = globalScale * Scale.Value;

            // 加上独立的偏移量
            float finalX = startX + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = RectWidth.Value * finalScale;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize }.ApplyTarkovFont();
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize }.ApplyTarkovFont();

            // 颜色也换成了自己专属的 Color.Value
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            float currentY = finalY;

            string titleText = LocaleManager.Get("boss_title");
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), titleText, mainColor, titleStyle);
            currentY += lh;

            foreach (var record in _roster.Values)
            {
                DrawBossLine(record, finalX, ref currentY, rectWidth, lh, textStyle, mainColor);
            }

            return currentY;
        }

        private static void DrawBossLine(BossRecord record, float x, ref float y, float width, float lh, GUIStyle style, Color defaultColor)
        {
            if (record.Status == EBossStatus.Dead)
            {
                // 死亡状态显示：灰色名字，标红 [DEAD]，保留最后距离
                string deadLine = string.Format(LocaleManager.Get("team_teammate_dead"),
                   record.RoleColor,
                   record.RolePrefix,
                   record.Name,
                   LocaleManager.Get("team_teammate_dead_tag"));
                //string deadLine = $"<color=#808080>{record.RolePrefix} {record.Name}</color> - <color=#FFFF00>{record.LastDistance}m</color> <color=#BA0303>[DEAD]</color>";
                HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), deadLine, defaultColor, style);
                y += lh;
                return;
            }

            var player = record.PlayerRef;
            if (player == null || player.HealthController == null) return;

            var healthCtrl = player.HealthController;
            float totalHp = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Current;
            float maxHp = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Maximum;

            // 复用你写好的血量颜色计算
            //string hpColor = LocaleManager.Get($"health_bio_hp_color_{HealthPanel.HealthStatusLow(totalHp, maxHp)}");
            //if (hpColor.StartsWith("health_bio_hp_color_")) hpColor = "#55ff55"; // 兜底颜色

            // 存活状态显示：身份前缀 + 名字 + 距离 + 血量
            //string mainLine = $"{record.RolePrefix} {record.Name} - <color=#FFFF00>{record.LastDistance}m</color> <color={hpColor}>({totalHp:F0}/{maxHp:F0})</color>";
            string mainLine = string.Format(LocaleManager.Get("team_teammate_alive"),
                record.RoleColor,
                record.RolePrefix,
                record.Name,
                string.Format(LocaleManager.Get("boss_text_distance"), record.LastDistance),
                LocaleManager.Get($"health_bio_hp_color_{HealthPanel.HealthStatusLow(totalHp, maxHp)}"),
                totalHp,
                maxHp,
                LocaleManager.Get("team_teammate_alive_tag"));

            HUDManager.DrawShadowLabel(new Rect(x, y, width, lh), mainLine, defaultColor, style);
            y += lh;
            // Boss不需要额外换行渲染吃喝，所以非常紧凑
        }

        // =====================================
        // 工具方法区域
        // =====================================

        private static bool TryGetBossPrefix(string role, out string prefix, out string hexcolor)
        {
            prefix = "";
            var result = false;
            hexcolor = "#66CCFF";//保底

            // 核心Boss
            if (role.Contains("boss"))
            {
                prefix = LocaleManager.Get("boss_boss_tag");//"[BOSS]";
                hexcolor = LocaleManager.Get("boss_boss_color");//"#CE0000";
                result = true;
            }
            //邪教徒
            if (role.Contains("sectant"))
            {
                prefix = LocaleManager.Get("boss_cultist_tag");
                hexcolor = LocaleManager.Get("boss_cultist_color");
                result = true;
            }
            //圣诞老人
            if (role == "gifter")
            {
                prefix = LocaleManager.Get("boss_santa_tag");
                hexcolor = LocaleManager.Get("boss_santa_color");
                result = true;
            }
            //寻血猎犬
            if (role.Contains("arena"))
            {
                prefix = LocaleManager.Get("boss_bloodhound_tag");
                hexcolor = LocaleManager.Get("boss_bloodhound_color");
                result = true;
            }
            //wtt黑狐
            if (role.Contains("black"))
            {
                prefix = LocaleManager.Get("boss_blackfox_tag");
                hexcolor = LocaleManager.Get("boss_blackfox_color");
                result = true;
            }
            switch (role)
            {
                //特殊单位处理
                //不带Boss但属于Boss单位
                case "followerbirdeye":
                case "followerbigpipe":
                case "infectedtagilla":
                case "sectantoni":
                case "sectantpredvestnik":
                case "sectantprizark":
                    prefix = LocaleManager.Get("boss_boss_tag");
                    hexcolor = LocaleManager.Get("boss_boss_color");
                    result = true;
                    break;
            }
            return result;
        }
    }
}