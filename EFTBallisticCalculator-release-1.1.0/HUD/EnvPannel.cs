using System;
using BepInEx.Configuration;
using EFTBallisticCalculator.Locale;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class EnvPanel
    {
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> RectWidth;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;
        // 新增：用于计算移速的静态缓存
        private static Vector3 _lastPos;
        private static float _lastTime;
        private static float _smoothedSpeed;

        public static void InitCfg(ConfigFile config)
        {
            OffsetX = config.Bind("Environment Panel / 环境数据", "X轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_env_x_desc"), 
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_env_x_name"), IsAdvanced = true }));

            OffsetY = config.Bind("Environment Panel / 环境数据", "Y轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_env_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_env_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Environment Panel / 环境数据", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_env_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_env_scale_name"), IsAdvanced = true }));
                
            Active = config.Bind("Environment Panel / 环境数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_env_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_env_active_name") }));

            Color = config.Bind("Environment Panel / 环境数据", "颜色设置", new Color(0.3f, 0.8f, 0.9f, 0.85f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_env_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_env_color_name") }));

            RectWidth = config.Bind("Environment Panel / 环境数据", "面板宽度", 300f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_env_rect_desc"),
                new AcceptableValueRange<float>(0f, 800f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_env_rect_name"), IsAdvanced = true }));
        }

        public static float Draw(float startX, float startY, float globalScale)
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
            Color atmosColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            string realTimeStr = DateTime.Now.ToString("HH:mm:ss");
            string tarkovTimeStr = "UNKNOWN";
            if (PluginsCore.CorrectGameWorld != null && PluginsCore.CorrectGameWorld.GameDateTime != null)
            {
                tarkovTimeStr = PluginsCore.CorrectGameWorld.GameDateTime.Calculate().ToString("HH:mm:ss");
            }

            Vector3 playerTransform = PluginsCore.CorrectPlayer.Transform.position;
            float altitude = playerTransform.y;
            // ==========================================
            // 【新增】：平滑地速计算 (仅在 Repaint 阶段且间隔大于 0.1 秒时计算，防 OnGUI 陷阱)
            // ==========================================
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Time.time - _lastTime;
                if (dt >= 0.1f) // 10Hz 的采样率，足够平滑且节省性能
                {
                    // 只取水平 X, Z 轴，忽略掉落或跳跃的 Y 轴干扰
                    Vector2 currentXZ = new Vector2(playerTransform.x, playerTransform.z);
                    Vector2 lastXZ = new Vector2(_lastPos.x, _lastPos.z);

                    float dist = Vector2.Distance(currentXZ, lastXZ);
                    float currentSpeed = dist / dt;

                    // 使用 Lerp 平滑过渡，防止数值瞎跳
                    _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, currentSpeed, 0.3f);

                    _lastPos = playerTransform;
                    _lastTime = Time.time;
                }
            }
            // 极低速时强制归零，防止浮点数残留导致站着不动显示 0.1 m/s
            if (_smoothedSpeed < 0.05f) _smoothedSpeed = 0f;
            float GetSwing(float x, float y, float muti) { return (Mathf.PerlinNoise(x, y) * 2f - 1f) * muti; }

            float windSpeed = PluginsCore._weatherSeedMap.windSpeed.b + PluginsCore._weatherSeedMap.windSpeed.r * 5f + GetSwing(Time.time * 0.003f, PluginsCore._weatherSeedGlobal + 2f, 2f);
            float windDir = Mathf.Repeat(PluginsCore._weatherSeedMap.windDirection.b + GetSwing(Time.time * 0.00025f, PluginsCore._weatherSeedGlobal + 7f, 30f), 360f);
            float humidity = PluginsCore._weatherSeedMap.humidity.b + PluginsCore._weatherSeedMap.humidity.r * 35f + GetSwing(Time.time * 0.0015f, PluginsCore._weatherSeedGlobal + 41f, 10f);
            float tempC = PluginsCore._weatherSeedMap.temperature.b + PluginsCore._weatherSeedMap.temperature.r * 6f + GetSwing(Time.time * 0.0005f, PluginsCore._weatherSeedGlobal + 67f, 6f);
            float tempF = tempC * 1.8f + 32;
            float hPa = 1013.25f - (altitude * 0.012f) + (Mathf.PerlinNoise(Time.time * 0.0035f, PluginsCore._weatherSeedGlobal + 101f) * 2.25f - 1.05f);

            float relativeWindAngle = windDir - BallisticsCalculator.GetAzimuth().eulerAngles.y;
            float crossWind = Mathf.Sin(relativeWindAngle * Mathf.Deg2Rad) * windSpeed;
            float headWind = Mathf.Cos(relativeWindAngle * Mathf.Deg2Rad) * windSpeed;
            // 预处理变量
            string crossDir = crossWind > 0 ? LocaleManager.Get("env_dir_left") : LocaleManager.Get("env_dir_right");
            string headDir = headWind > 0 ? LocaleManager.Get("env_dir_head") : LocaleManager.Get("env_dir_tail");

            double baseLat = 60.051200 + (playerTransform.z * 0.000009);
            double baseLon = 29.351400 + (playerTransform.x * 0.000018);
            string latVal = string.Format(LocaleManager.Get("env_val_lat"), baseLat);
            string lonVal = string.Format(LocaleManager.Get("env_val_lon"), baseLon);

            string windDirVal = string.Format(LocaleManager.Get("env_val_wind_dir"), windDir, HUDManager.GetCompassDir(windDir), windSpeed);
            string crossVal = string.Format(LocaleManager.Get("env_val_wind_spd"), Mathf.Abs(crossWind), crossDir);
            string vectVal = string.Format(LocaleManager.Get("env_val_wind_spd"), Mathf.Abs(headWind), headDir);
            string altVal = string.Format(LocaleManager.Get("env_val_alt"), altitude);
            string pressVal = string.Format(LocaleManager.Get("env_val_press"), hPa);
            string humVal = string.Format(LocaleManager.Get("env_val_hum"), humidity);
            string tempVal = string.Format(LocaleManager.Get("env_val_temp"), tempC, tempF);
            // 新增：格式化移速 (同时显示 M/S 和 KM/H)
            string moveSpeedVal = string.Format(LocaleManager.Get("env_val_move_spd"), _smoothedSpeed, _smoothedSpeed * 3.6f);

            // 绘制
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY, 400, 25), LocaleManager.Get("env_title"), atmosColor, titleStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 1, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_loc"), PluginsCore._cachedLocationName), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 2, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_gps"), latVal, lonVal), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 3, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_time"), tarkovTimeStr, realTimeStr), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 4, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_move_spd"), moveSpeedVal), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 5, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_wind_dir"), windDirVal), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 6, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_cross"), crossVal), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 7, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_vect"), vectVal), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 8, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_alt"), altVal), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 9, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_press"), pressVal), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 10, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_hum"), humVal), atmosColor, textStyle);
            HUDManager.DrawShadowLabel(new Rect(finalX, finalY + lh * 11, rectWidth, lh), string.Format(LocaleManager.Get("env_lbl_temp"), tempVal), atmosColor, textStyle);

            return finalY + (lh * 12f);
        }
    }
}