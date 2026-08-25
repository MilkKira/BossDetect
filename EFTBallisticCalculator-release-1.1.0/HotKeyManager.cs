using BepInEx.Configuration;
using EFTBallisticCalculator.HUD;
using EFTBallisticCalculator.Locale;
using UnityEngine;
using static GClass2175;

namespace EFTBallisticCalculator.Core
{
    public static class HotKeyManager
    {
        public static ConfigEntry<KeyboardShortcut> KeyGlobalDraw;
        public static ConfigEntry<KeyboardShortcut> KeyFcsClear;
        public static ConfigEntry<KeyboardShortcut> KeyFcsTrack;

        public static ConfigEntry<KeyboardShortcut> KeyDistUp100;
        public static ConfigEntry<KeyboardShortcut> KeyDistDown100;
        public static ConfigEntry<KeyboardShortcut> KeyDistUp10;
        public static ConfigEntry<KeyboardShortcut> KeyDistDown10;
        public static ConfigEntry<KeyboardShortcut> KeyDistUp1;
        public static ConfigEntry<KeyboardShortcut> KeyDistDown1;

        public static void Init(ConfigFile config)
        {
            // --- 1. Controls ---
            KeyGlobalDraw = config.Bind("Controls / 控制", "绘制总开关", new KeyboardShortcut(KeyCode.KeypadMinus),
                new ConfigDescription(CfgLocaleManager.Get("cfg_hotkey_draw_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hotkey_draw_name") }));

            KeyFcsClear = config.Bind("Controls / 控制", "解除锁定", new KeyboardShortcut(KeyCode.Backspace),
                new ConfigDescription(CfgLocaleManager.Get("cfg_hotkey_clear_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hotkey_clear_name") }));
            KeyFcsTrack = config.Bind("Controls / 控制", "锁定目标", new KeyboardShortcut(KeyCode.T),
                new ConfigDescription(CfgLocaleManager.Get("cfg_hotkey_track_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_hotkey_track_name") }));

            // --- 2. Manual Dial ---
            KeyDistUp100 = config.Bind("Controls / 控制", "距离+100米", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftShift),
                new ConfigDescription(CfgLocaleManager.Get("cfg_dial_up_100_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_dial_up_100_name") }));

            KeyDistDown100 = config.Bind("Controls / 控制", "距离-100米", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftShift),
                new ConfigDescription(CfgLocaleManager.Get("cfg_dial_down_100_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_dial_down_100_name") }));

            KeyDistUp10 = config.Bind("Controls / 控制", "距离+10米", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftAlt),
                new ConfigDescription(CfgLocaleManager.Get("cfg_dial_up_10_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_dial_up_10_name") }));

            KeyDistDown10 = config.Bind("Controls / 控制", "距离-10米", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftAlt),
                new ConfigDescription(CfgLocaleManager.Get("cfg_dial_down_10_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_dial_down_10_name") }));

            KeyDistUp1 = config.Bind("Controls / 控制", "距离+1米", new KeyboardShortcut(KeyCode.UpArrow, KeyCode.LeftControl),
                new ConfigDescription(CfgLocaleManager.Get("cfg_dial_up_1_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_dial_up_1_name") }));

            KeyDistDown1 = config.Bind("Controls / 控制", "距离-1米", new KeyboardShortcut(KeyCode.DownArrow, KeyCode.LeftControl),
                new ConfigDescription(CfgLocaleManager.Get("cfg_dial_down_1_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_dial_down_1_name") }));
        }

        // 返回当前帧玩家手动按键输入的距离增量
        public static float GetManualDistanceDelta()
        {
            float deltaDist = 0f;
            if (KeyDistUp100.Value.IsDown()) deltaDist += 100f;
            if (KeyDistDown100.Value.IsDown()) deltaDist -= 100f;
            if (KeyDistUp10.Value.IsDown()) deltaDist += 10f;
            if (KeyDistDown10.Value.IsDown()) deltaDist -= 10f;
            if (KeyDistUp1.Value.IsDown()) deltaDist += 1f;
            if (KeyDistDown1.Value.IsDown()) deltaDist -= 1f;
            return deltaDist;
        }
        public static void ListenToHotKeyInput()
        {
            // 2. 
            //if (KeyEnvPannel.Value.IsDown()) EnvPanel.Active.Value = !EnvPanel.Active.Value;
            if (KeyGlobalDraw.Value.IsDown()) HUDManager.DrawGlobal.Value = !HUDManager.DrawGlobal.Value;

            if (KeyFcsTrack.Value.IsDown())
            {
                BallisticsCalculator.ExecuteFcsLogic();
            }

            if (PluginsCore._currentFC != null)
            {
                if (KeyFcsClear.Value.IsDown())
                {
                    PluginsCore._lockedHorizontalDist = 0f;
                }

                float deltaDist = GetManualDistanceDelta();

                if (deltaDist != 0f)
                {
                    PluginsCore._lockedHorizontalDist += deltaDist;
                    PluginsCore._lockedHorizontalDist = (int)PluginsCore._lockedHorizontalDist;

                    if (PluginsCore._lockedHorizontalDist <= 0f)
                    {
                        PluginsCore._lockedHorizontalDist = 0f;
                    }
                }
            }
        }
    }
}