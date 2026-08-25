using BepInEx.Configuration;
using EFTBallisticCalculator.Locale;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class FCSPanel
    {
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> RectWidth;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;

        public static void InitCfg(ConfigFile config)
        {
            OffsetX = config.Bind("FCS Panel / 火控数据", "X轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_fcs_x_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_fcs_x_name"), IsAdvanced = true }));

            OffsetY = config.Bind("FCS Panel / 火控数据", "Y轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_fcs_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_fcs_y_name"), IsAdvanced = true }));

            Scale = config.Bind("FCS Panel / 火控数据", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_fcs_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_fcs_scale_name"), IsAdvanced = true }));

            Active = config.Bind("FCS Panel / 火控数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_fcs_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_fcs_active_name") }));

            Color = config.Bind("FCS Panel / 火控数据", "颜色设置", new Color(0.2f, 1f, 0.4f, 0.9f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_fcs_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_fcs_color_name") }));

            RectWidth = config.Bind("FCS Panel / 火控数据", "面板宽度", 300f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_fcs_rect_desc"),
                new AcceptableValueRange<float>(0f, 800f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_fcs_rect_name"), IsAdvanced = true }));
        }

        public static float Draw(float startX, float startY, float globalScale, bool hasWeapon)
        {
            if (!Active.Value) return startY;
            float finalScale = globalScale * Scale.Value;
            float finalX = startX + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = RectWidth.Value * finalScale;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize }.ApplyTarkovFont();
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize }.ApplyTarkovFont();
            Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            var fc = PluginsCore.CorrectPlayer.HandsController as EFT.Player.FirearmController;
            Vector3 currentPos = hasWeapon ? fc.CurrentFireport.position : Camera.main.transform.position;

            bool hasLockedDistance = PluginsCore._lockedHorizontalDist > 0f;
            bool isFcsLocked = hasWeapon && PluginsCore._hasAmmo && hasLockedDistance;

            float dist3D = 0f;
            if (isFcsLocked)
            {
                dist3D = Vector3.Distance(currentPos, PluginsCore._impactMarker != null ? PluginsCore._impactMarker.transform.position : currentPos + fc.WeaponDirection * PluginsCore._lockedHorizontalDist);
            }

            string compassHeading = HUDManager.GetCompassDir(BallisticsCalculator.GetAzimuth().eulerAngles.y);
            float rollAngle = BallisticsCalculator.GetAzimuth().eulerAngles.z;
            if (rollAngle > 180f) rollAngle -= 360f;
            float vertAngle = BallisticsCalculator.GetAzimuth().eulerAngles.x;
            if (vertAngle > 180f) vertAngle -= 360f;

            string nodata = LocaleManager.Get("no_data");

            // --- 1. 预处理变量值 (Value) ---
            string headingVal = string.Format(LocaleManager.Get("fcs_val_heading"), BallisticsCalculator.GetAzimuth().eulerAngles.y, compassHeading);

            string rangeStr = nodata;
            if (hasWeapon)
            {
                rangeStr = hasLockedDistance ? string.Format(LocaleManager.Get("fcs_val_range"), PluginsCore._lockedHorizontalDist) : LocaleManager.Get("fcs_no_lock");
            }

            string tofStr = isFcsLocked ? string.Format(LocaleManager.Get("fcs_val_tof"), PluginsCore._lockedTOF) : nodata;
            string inclineStr = hasWeapon ? string.Format(LocaleManager.Get("fcs_val_angle"), vertAngle) : nodata;
            string cantStr = hasWeapon ? string.Format(LocaleManager.Get("fcs_val_angle"), rollAngle) : nodata;
            string speedStr = (hasWeapon && PluginsCore._hasAmmo) ? string.Format(LocaleManager.Get("fcs_val_speed"), PluginsCore._currentSpeed) : nodata;

            string massVal = (hasWeapon && PluginsCore._hasAmmo) ? string.Format(LocaleManager.Get("fcs_val_mass"), PluginsCore._currentMass) : nodata;
            string bcVal = (hasWeapon && PluginsCore._hasAmmo) ? string.Format(LocaleManager.Get("fcs_val_bc"), PluginsCore._currentBC) : "";
            string massStr = string.IsNullOrEmpty(bcVal) ? massVal : $"{massVal} {bcVal}";

            // 顶部状态栏
            string fcsStatusText;
            if (!hasWeapon) fcsStatusText = LocaleManager.Get("fcs_title_no_weapon");
            else if (!PluginsCore._hasAmmo) fcsStatusText = LocaleManager.Get("fcs_title_no_ammo");
            else if (hasLockedDistance) fcsStatusText = LocaleManager.Get("fcs_title_locked");
            else fcsStatusText = LocaleManager.Get("fcs_title_standby");

            // --- 2. 注入标签模板并绘制 (Label) ---
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY, 400, 25), $"<b>{fcsStatusText}</b>", mainColor, titleStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 1, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_heading"), headingVal), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 2, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_range"), rangeStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 3, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_incline"), inclineStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 4, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_cant"), cantStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 5, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_tof"), tofStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 6, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_vel"), speedStr), mainColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 7, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_mass"), massStr), mainColor, textStyle);

            // 系统状态
            string aimStatus = LocaleManager.Get("fcs_status_offline");
            string hexCode = "0x0000";
            if (hasWeapon)
            {
                hexCode = "0x" + UnityEngine.Random.Range(0x1000, 0xFFFF).ToString("X4");
                var pwa = PluginsCore.CorrectPlayer.ProceduralWeaponAnimation;
                bool isAiming = (pwa != null && pwa.IsAiming);

                if (!PluginsCore._hasAmmo) aimStatus = LocaleManager.Get("fcs_status_no_ammo");
                else if (!isAiming) aimStatus = LocaleManager.Get("fcs_status_standby");
                else aimStatus = hasLockedDistance ? LocaleManager.Get("fcs_status_tracked") : LocaleManager.Get("fcs_status_optic_sync");
            }

            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 8f, rectWidth, lh), string.Format(LocaleManager.Get("fcs_lbl_sys"), aimStatus, hexCode), mainColor, textStyle);

            // 返回不包含子面板Y偏移的真实底部边界，方便排版叠加
            return startY + (lh * 10f);
        }
    }
}