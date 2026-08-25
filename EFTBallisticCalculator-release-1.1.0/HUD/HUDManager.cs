using BepInEx.Configuration;
using EFTBallisticCalculator.Locale;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class HUDManager
    {
        // --- 左侧 HUD 全局设置 ---
        public static ConfigEntry<float> GlobalOffsetX;
        public static ConfigEntry<float> GlobalStartYOffset;
        public static ConfigEntry<float> GlobalScale;
        public static ConfigEntry<float> PanelSpacing;

        // --- 右侧 HUD 全局设置 ---
        public static ConfigEntry<float> RightGlobalOffsetX;
        public static ConfigEntry<float> RightGlobalStartYOffset;
        public static ConfigEntry<float> RightGlobalScale;
        public static ConfigEntry<float> RightPanelSpacing;

        // --- 顶部 HUD 全局设置 ---
        public static ConfigEntry<float> TopGlobalOffsetY;
        public static ConfigEntry<float> TopGlobalScale;
        public static ConfigEntry<float> TopPanelSpacing;

        // --- 其他全局特效 ---
        public static ConfigEntry<float> RainbowUISpeed;
        public static ConfigEntry<bool> RainbowUI;
        public static Color RainbowColor = new Color(1f, 1f, 1f, 0.85f);
        public static ConfigEntry<bool> UIColorOverride;
        public static ConfigEntry<Color> OverrideColor;
        public static ConfigEntry<Color> ShadowColor;
        public static ConfigEntry<float> ShadowOffsetX;
        public static ConfigEntry<float> ShadowOffsetY;
        public static ConfigEntry<bool> PureMode;
        public static ConfigEntry<bool> DrawGlobal;

        private static readonly Regex _colorRegex = new Regex(@"<color[^>]*>|</color>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void InitCfg(ConfigFile config)
        {
            // ==================== 左侧 HUD ====================
            GlobalOffsetX = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "全局X轴偏移", 30f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_left_x_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_left_x_name"), IsAdvanced = true }));

            GlobalStartYOffset = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "全局Y轴偏移", -180f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_left_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_left_y_name"), IsAdvanced = true }));

            GlobalScale = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "全局缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_left_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_left_scale_name"), IsAdvanced = true }));

            PanelSpacing = config.Bind("Left HUD Pannel Global / 左侧HUD全局设置", "面板间距", 15f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_left_space_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_left_space_name"), IsAdvanced = true }));

            // ==================== 右侧 HUD ====================
            RightGlobalOffsetX = config.Bind("Right HUD Pannel Global / 右侧HUD全局设置", "全局X轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_right_x_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_right_x_name"), IsAdvanced = true }));

            RightGlobalStartYOffset = config.Bind("Right HUD Pannel Global / 右侧HUD全局设置", "全局Y轴偏移", -180f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_right_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_right_y_name"), IsAdvanced = true }));

            RightGlobalScale = config.Bind("Right HUD Pannel Global / 右侧HUD全局设置", "全局缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_right_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_right_scale_name"), IsAdvanced = true }));

            RightPanelSpacing = config.Bind("Right HUD Pannel Global / 右侧HUD全局设置", "面板间距", 15f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_right_space_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_right_space_name"), IsAdvanced = true }));

            // ==================== 顶部 HUD ====================
            TopGlobalOffsetY = config.Bind("Top HUD Pannel Global / 顶部HUD全局设置", "全局Y轴偏移", 85f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_top_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_top_y_name"), IsAdvanced = true }));

            TopGlobalScale = config.Bind("Top HUD Pannel Global / 顶部HUD全局设置", "全局缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_top_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_top_scale_name"), IsAdvanced = true }));

            TopPanelSpacing = config.Bind("Top HUD Pannel Global / 顶部HUD全局设置", "面板间距", 5f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_top_space_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_top_space_name"), IsAdvanced = true }));

            // ==================== 视觉特效 ====================
            RainbowUI = config.Bind("HUD Visuals / 视觉效果", "彩虹UI", false,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_rb_ui_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_rb_ui_name"), IsAdvanced = true }));

            RainbowUISpeed = config.Bind("HUD Visuals / 视觉效果", "彩虹UI滚动速度", 0.25f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_rb_spd_desc"),
                new AcceptableValueRange<float>(0f, 1f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_rb_spd_name"), IsAdvanced = true }));

            UIColorOverride = config.Bind("HUD Visuals / 视觉效果", "全局UI色彩覆盖", false,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_oc_ui_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_oc_ui_name") }));

            OverrideColor = config.Bind("HUD Visuals / 视觉效果", "UI覆盖颜色", new Color(1f, 1f, 1f, 0.9f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_oc_clr_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_oc_clr_name") }));

            ShadowColor = config.Bind("HUD Visuals / 视觉效果", "文字阴影颜色", new Color(0.2f, 0.2f, 0.2f, 0.8f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_shdw_clr_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_shdw_clr_name"), IsAdvanced = true }));

            ShadowOffsetX = config.Bind("HUD Visuals / 视觉效果", "文字阴影X偏移量", 1.5f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_shdw_ofst_x_desc"),
                new AcceptableValueRange<float>(-100f, 100f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_shdw_ofst_x_name"), IsAdvanced = true }));

            ShadowOffsetY = config.Bind("HUD Visuals / 视觉效果", "文字阴影Y偏移量", 1.5f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_shdw_ofst_y_desc"),
                new AcceptableValueRange<float>(-100f, 100f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_shdw_ofst_y_name"), IsAdvanced = true }));

            PureMode = config.Bind("HUD Visuals / 视觉效果", "纯净模式", false,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_pure_ui_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_pure_ui_name"), IsAdvanced = true }));
            DrawGlobal = config.Bind("HUD Visuals / 视觉效果", "信息面板总开关", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_hud_draw_ui_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hud_draw_ui_name") }));

            // ==================== 初始化子面板 ====================
            FCSPanel.InitCfg(config);
            EnvPanel.InitCfg(config);
            HealthPanel.InitCfg(config);
            ActiveBuffPanel.InitCfg(config);
            TeamPanel.InitCfg(config);
            BossPanel.InitCfg(config);
            WeaponPanel.InitCfg(config);
            ThrowablePanel.InitCfg(config); // 别忘了初始化我们新加的投掷物面板
        }

        public static void UpdateRainbowColor()
        {
            if (RainbowUI.Value)
            {
                float hue = Mathf.Repeat(Time.time * RainbowUISpeed.Value, 1f);
                Color hsvColor = UnityEngine.Color.HSVToRGB(hue, 0.8f, 1f);
                RainbowColor = new Color(hsvColor.r, hsvColor.g, hsvColor.b, 0.85f);
            }
        }

        public static void DrawGUI()
        {
            if (Camera.main == null || PluginsCore.CorrectPlayer == null || !DrawGlobal.Value) return;

            bool hasWeapon = PluginsCore.CorrectPlayer.HandsController as EFT.Player.FirearmController != null;
            UpdateRainbowColor();

            // 1. 渲染左侧面板流 (严格左侧优先的矩阵填充布局)
            // ==========================================
            float leftStartX = GlobalOffsetX.Value;
            float leftStartY = (Screen.height / 2f) + GlobalStartYOffset.Value;
            float leftScale = GlobalScale.Value;

            float columnSpacing = 350f * leftScale;
            float rowSpacing = PanelSpacing.Value * leftScale;
            float rightStartX = leftStartX + columnSpacing;

            // 记录已经成功画出了几个面板
            int drawnPanelsCount = 0;
            float currentLeftY = leftStartY;
            float currentRightY = leftStartY;

            // 声明一个局部函数：负责自动找坑位
            void TryDraw(System.Func<float, float, float> drawFunc)
            {
                if (drawnPanelsCount < 2)
                {
                    // 第 1、2 个面板，强制去左列！
                    float newY = drawFunc(leftStartX, currentLeftY);
                    if (newY > currentLeftY)
                    {
                        currentLeftY = newY + rowSpacing;
                        drawnPanelsCount++;
                    }
                }
                else
                {
                    // 第 3、4 个面板，去右列！
                    float newY = drawFunc(rightStartX, currentRightY);
                    if (newY > currentRightY)
                    {
                        currentRightY = newY + rowSpacing;
                        drawnPanelsCount++;
                    }
                }
            }

            // 严格按照你期望的优先级（排队顺序）执行：火控 -> 环境 -> 队伍 -> Boss
            TryDraw((x, y) => FCSPanel.Draw(x, y, leftScale, hasWeapon));
            TryDraw((x, y) => EnvPanel.Draw(x, y, leftScale));
            TryDraw((x, y) => TeamPanel.Draw(x, y, leftScale));
            TryDraw((x, y) => BossPanel.Draw(x, y, leftScale));

            // ==========================================
            // 2. 渲染右侧面板流 (Health -> Buff)
            // ==========================================
            float rightAnchorX = Screen.width - RightGlobalOffsetX.Value;
            float rightCurrentY = (Screen.height / 2f) + RightGlobalStartYOffset.Value;
            float rightScale = RightGlobalScale.Value;

            // 因为队伍面板挪走了，现在右侧只剩健康和 Buff
            rightAnchorX = HealthPanel.Draw(rightAnchorX, rightCurrentY, rightScale);
            rightAnchorX = ActiveBuffPanel.Draw(rightAnchorX, rightCurrentY, rightScale);

            // ==========================================
            // 3. 渲染顶部面板流 (Weapon -> Throwable)
            // ==========================================
            float topCurrentY = TopGlobalOffsetY.Value;
            float topScale = TopGlobalScale.Value;

            float weaponLeftX = WeaponPanel.Draw(topCurrentY, topScale);
            ThrowablePanel.Draw(weaponLeftX, topCurrentY, topScale, hasWeapon);

            // ==========================================
            // 4. 中心锁定标记
            // ==========================================
            if (hasWeapon && PluginsCore._lockedHorizontalDist > 0f)
            {
                //这个准星还是不要了
                //DrawCenterMarker();
            }
        }

        private static void DrawCenterMarker()
        {
            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;
            float size = 50f;
            float thick = 2f;
            float length = 15f;

            float alphaPulse = 0.5f + Mathf.PingPong(Time.time * 2f, 0.5f);
            GUI.color = new Color(0.2f, 1f, 0.4f, alphaPulse);

            GUI.DrawTexture(new Rect(cx - size, cy - size, length, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - size, cy - size, thick, length), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + size - length, cy - size, length, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + size, cy - size, thick, length), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - size, cy + size, length, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - size, cy + size - length, thick, length), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + size - length, cy + size, length, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + size, cy + size - length, thick, length), Texture2D.whiteTexture);
        }

        public static void DrawShadowLabel(Rect rect, string text, Color textColor, GUIStyle style)
        {
            string pureText = text;

            if (!string.IsNullOrEmpty(text) &&
               (text.IndexOf("<color", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("</color", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                pureText = _colorRegex.Replace(text, string.Empty);
            }

            GUI.color = ShadowColor.Value;
            Rect shadowRect = new Rect(rect.x + ShadowOffsetX.Value, rect.y + ShadowOffsetY.Value, rect.width, rect.height);
            GUI.Label(shadowRect, pureText, style);

            GUI.color = textColor;
            GUI.Label(rect, PureMode.Value? pureText : text, style);
        }

        public static string GetCompassDir(float az)
        {
            string[] dirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N" };
            string[] dirsch = { "北", "北偏东", "东北", "东偏北", "东", "东偏南", "东南", "南偏东", "南", "南偏西", "西南", "西偏南", "西", "西偏北", "西北", "北偏西", "北" };
            if (LocaleManager.CurrentLanguage.Value == "简体中文")
            {
                return dirsch[(int)Mathf.Round(((az % 360) / 22.5f))];
            }
            return dirs[(int)Mathf.Round(((az % 360) / 22.5f))];
        }
    }
}