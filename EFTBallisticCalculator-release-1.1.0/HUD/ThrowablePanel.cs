using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFTBallisticCalculator.Locale;
using System.Collections.Generic;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class ThrowablePanel
    {
        public enum TextAlign { Left, Center, Right }

        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> RectWidth;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<TextAlign> Alignment;
        public static ConfigEntry<Color> Color;

        private class ThrowableData
        {
            public string Name;
            public int Count;
        }

        private static readonly List<ThrowableData> _throwablesCache = new List<ThrowableData>();
        private static float _lastScanTime = 0f;

        public static void InitCfg(ConfigFile config)
        {
            OffsetY = config.Bind("Top Panel / 投掷物数据", "Y轴偏移", 12f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_throw_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_throw_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Top Panel / 投掷物数据", "缩放比例", 1.25f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_throw_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_throw_scale_name"), IsAdvanced = true }));

            Active = config.Bind("Top Panel / 投掷物数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_throw_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_throw_active_name") }));

            Alignment = config.Bind("Top Panel / 投掷物数据", "对齐方式", TextAlign.Center,
                new ConfigDescription(CfgLocaleManager.Get("cfg_throw_align_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_throw_align_name") }));

            Color = config.Bind("Top Panel / 投掷物数据", "颜色设置", new Color(0.9f, 0.7f, 0.2f, 0.85f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_throw_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_throw_color_name") }));

            RectWidth = config.Bind("Top Panel / 投掷物数据", "面板宽度", 180f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_throw_rect_desc"),
                new AcceptableValueRange<float>(0f, 800f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_throw_rect_name"), IsAdvanced = true }));
        }

        private static void UpdateThrowables(Player player)
        {
            if (Time.time - _lastScanTime < 1f) return;
            _lastScanTime = Time.time;

            _throwablesCache.Clear();
            var equipment = player.Inventory?.Equipment;
            if (equipment == null) return;

            Dictionary<string, int> grenadeCounts = new Dictionary<string, int>();

            void ScanContainerForGrenades(Item containerItem)
            {
                if (containerItem == null) return;
                foreach (var item in containerItem.GetAllItems())
                {
                    if (item is ThrowWeapItemClass grenade)
                    {
                        string gName = grenade.Name.Localized();
                        if (grenadeCounts.ContainsKey(gName)) grenadeCounts[gName]++;
                        else grenadeCounts[gName] = 1;
                    }
                }
            }

            ScanContainerForGrenades(equipment.GetSlot(EquipmentSlot.TacticalVest).ContainedItem);
            ScanContainerForGrenades(equipment.GetSlot(EquipmentSlot.Pockets).ContainedItem);

            foreach (var kvp in grenadeCounts)
            {
                _throwablesCache.Add(new ThrowableData { Name = kvp.Key, Count = kvp.Value });
            }
        }

        // 接收武器面板的左侧锚点 X，以及是否持枪的状态
        public static float Draw(float anchorRightX, float startY, float globalScale, bool hasWeapon)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return startY;

            UpdateThrowables(PluginsCore.CorrectPlayer);
            if (_throwablesCache.Count == 0) return startY;

            float finalScale = globalScale * Scale.Value;
            float currentY = startY + OffsetY.Value * finalScale;
            float lh = 18f * finalScale;

            TextAnchor anchor;
            Rect drawRect;

            // 【动态包围盒逻辑】：根据是否有武器，改变面板大小和渲染位置
            if (hasWeapon)
            {
                // 持枪时：面板躲到左边，贴紧武器板，默认向右对齐
                float panelWidth = RectWidth.Value * finalScale;
                float spacing = 25f * finalScale; // 距离武器板留点呼吸空间

                anchor = Alignment.Value == TextAlign.Left ? TextAnchor.MiddleLeft :
                         (Alignment.Value == TextAlign.Center ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight);

                // 起点在 锚点X - 宽度 - 间距
                drawRect = new Rect(anchorRightX - panelWidth - spacing, currentY, panelWidth, lh);
            }
            else
            {
                // 没枪时：嚣张地占满全屏居中显示
                anchor = Alignment.Value == TextAlign.Left ? TextAnchor.MiddleLeft :
                         (Alignment.Value == TextAlign.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter);

                float padding = 20f * finalScale;
                drawRect = new Rect(padding, currentY, Screen.width - (padding * 2), lh);
            }

            GUIStyle nadeStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = (int)(13 * finalScale), alignment = anchor }.ApplyTarkovFont();
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            HUDManager.DrawShadowLabel(drawRect, LocaleManager.Get("grenade_title"), mainColor, nadeStyle);
            currentY += lh;
            drawRect.y = currentY;

            foreach (var nade in _throwablesCache)
            {
                string line = string.Format(LocaleManager.Get("grenade_text"), nade.Name, nade.Count);//$"{nade.Name}  <color=#ffffff>x{nade.Count}</color>";
                HUDManager.DrawShadowLabel(drawRect, line, mainColor, nadeStyle);

                currentY += lh;
                drawRect.y = currentY;
            }

            return currentY + (5f * finalScale);
        }
    }
}