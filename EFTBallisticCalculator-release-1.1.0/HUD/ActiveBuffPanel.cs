using BepInEx.Configuration;
using EFTBallisticCalculator.Locale;
using System.Collections.Generic;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class ActiveBuffPanel
    {
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> RectWidth;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;

        public static void InitCfg(ConfigFile config)
        {
            OffsetX = config.Bind("Buff Panel / 状态数据", "X轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_buff_x_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_buff_x_name"), IsAdvanced = true }));

            OffsetY = config.Bind("Buff Panel / 状态数据", "Y轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_buff_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_buff_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Buff Panel / 状态数据", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_buff_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_buff_scale_name"), IsAdvanced = true }));

            Active = config.Bind("Buff Panel / 状态数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_buff_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_buff_active_name") }));

            Color = config.Bind("Buff Panel / 状态数据", "颜色设置", new Color(1f, 0.4f, 0.9f, 0.85f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_buff_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_buff_color_name") }));

            RectWidth = config.Bind("Buff Panel / 状态数据", "面板宽度", 160f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_buff_rect_desc"),
                new AcceptableValueRange<float>(0f, 800f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_buff_rect_name"), IsAdvanced = true }));
        }

        // 接收右侧锚点，返回占用后的最左侧坐标
        public static float Draw(float anchorRightX, float startY, float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return anchorRightX;

            var buffs = ActiveBuffManager.AllEffects;
            if (buffs.Count == 0) return anchorRightX; // 没有状态时不渲染，不占用空间

            float finalScale = globalScale * Scale.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = RectWidth.Value * finalScale; // 状态面板不需要太宽

            float spacing = 15f * finalScale;
            float finalX = anchorRightX - rectWidth - spacing + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize }.ApplyTarkovFont();
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize }.ApplyTarkovFont();
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            float currentY = finalY;

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), LocaleManager.Get("health_buff_title"), mainColor, titleStyle);
            currentY += lh;

            float elapsedSinceScan = Time.time - ActiveBuffManager.LastUpdateTime;

            foreach (var buff in buffs)
            {
                // 视觉时间平滑插值
                float displayTime = buff.TimeLeft;
                if (displayTime > 0)
                {
                    displayTime = Mathf.Max(0f, displayTime - elapsedSinceScan);
                }

                string timeStr = displayTime > 0 ? string.Format(LocaleManager.Get("health_buff_time"), displayTime) : "";
                string valueStr = buff.Strength != 0 ? $" {(buff.Strength > 0 ? "+" : "")}{buff.Strength:G3}" : "";
                var name = buff.Name;
                if(buff.Name.ToLower() == "painkiller")
                {
                    name = LocaleManager.Get("buff_painkiller_name");
                }
                string display = $"{name}{valueStr} {timeStr}";

                HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), display, mainColor, textStyle);
                currentY += lh;
            }

            return finalX; // 返回最左侧边界给下一个面板
        }
    }
}