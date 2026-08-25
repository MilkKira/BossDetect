using BepInEx.Configuration;
using EFT;
using EFTBallisticCalculator.Locale;
using System;
using System.Linq;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class HealthPanel
    {
        public static ConfigEntry<float> OffsetX;
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<float> RectWidth;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;

        public static void InitCfg(ConfigFile config)
        {
            OffsetX = config.Bind("Health Panel / 健康数据", "X轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_x_desc"),
                new AcceptableValueRange<float>(-1920f, 1920f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_x_name"), IsAdvanced = true }));

            OffsetY = config.Bind("Health Panel / 健康数据", "Y轴偏移", 0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Health Panel / 健康数据", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_scale_name"), IsAdvanced = true }));

            Active = config.Bind("Health Panel / 健康数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_active_name") }));

            Color = config.Bind("Health Panel / 健康数据", "颜色设置", new Color(1f, 0.7f, 0.8f, 0.85f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_color_name") }));

            RectWidth = config.Bind("Health Panel / 健康数据", "面板宽度", 300f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_health_rect_desc"),
                new AcceptableValueRange<float>(0f, 800f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_health_rect_name"), IsAdvanced = true }));
        }

        // 返回占用后的最左侧 X 坐标 (现在它就是自身的 finalX - width)
        public static float Draw(float anchorRightX, float startY, float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return anchorRightX;

            var healthCtrl = PluginsCore.CorrectPlayer.ActiveHealthController;
            var physCtrl = PluginsCore.CorrectPlayer.Physical;
            if (healthCtrl == null || physCtrl == null) return anchorRightX;

            float finalScale = globalScale * Scale.Value;

            float lh = 20f * finalScale;
            int titleSize = (int)(15 * finalScale);
            int textSize = (int)(13 * finalScale);
            float rectWidth = RectWidth.Value * finalScale;

            // 动态向左推演：从右侧锚点减去自身宽度
            float spacing = 0f * finalScale; // 面板间隙
            float finalX = anchorRightX - rectWidth - spacing + OffsetX.Value;
            float finalY = startY + OffsetY.Value;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = titleSize }.ApplyTarkovFont();
            GUIStyle textStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = textSize }.ApplyTarkovFont();
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            float currentY = finalY;

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), LocaleManager.Get("health_bio_title"), mainColor, titleStyle);
            currentY += lh;

            // --- 区块 1：基本生存指标 ---
            float totalHealth = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Current;
            float totalMaxHealth = healthCtrl.GetBodyPartHealth(EBodyPart.Common).Maximum;

            string hydStr = string.Format(
                LocaleManager.Get("health_bio_hydr"),
                    LocaleManager.Get($"health_bio_hydr_color_{HealthStatusLow(healthCtrl.Hydration.Current, healthCtrl.Hydration.Maximum)}"),
                    healthCtrl.Hydration.Current, 
                    healthCtrl.Hydration.Maximum, 
                    LocaleManager.Get($"health_bio_hydr_status_{GetHealthStatus(healthCtrl.Hydration.Current, healthCtrl.Hydration.Maximum)}"), 
                    healthCtrl.HydrationRate);
            string engStr = string.Format(
                LocaleManager.Get("health_bio_energy"),
                    LocaleManager.Get($"health_bio_energy_color_{HealthStatusLow(healthCtrl.Energy.Current, healthCtrl.Energy.Maximum)}"),
                    healthCtrl.Energy.Current,
                    healthCtrl.Energy.Maximum,
                    LocaleManager.Get($"health_bio_energy_status_{GetHealthStatus(healthCtrl.Energy.Current, healthCtrl.Energy.Maximum)}"),
                    healthCtrl.EnergyRate);
            string tempStr = string.Format(LocaleManager.Get("health_bio_temp"), healthCtrl.Temperature.Current);

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), 
                string.Format(LocaleManager.Get("health_bio_hp"),
                    LocaleManager.Get($"health_bio_hp_color_{HealthStatusLow(totalHealth, totalMaxHealth)}"),
                    totalHealth,
                    totalMaxHealth,
                    healthCtrl.HealthRate),
                mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), hydStr, mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), engStr, mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), tempStr, mainColor, textStyle); currentY += lh;

            currentY += 5f * finalScale;

            // --- 区块 2：肢体诊断 ---
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), LocaleManager.Get("health_limb_title"), mainColor, titleStyle); currentY += lh;

            EBodyPart[] partsToDraw = { EBodyPart.Head, EBodyPart.Chest, EBodyPart.Stomach, EBodyPart.LeftArm, EBodyPart.RightArm, EBodyPart.LeftLeg, EBodyPart.RightLeg };
            foreach (var part in partsToDraw)
            {
                var hp = healthCtrl.GetBodyPartHealth(part);
                string partName = part.ToString().Localized();
                string statusText = healthCtrl.IsBodyPartDestroyed(part)  ? LocaleManager.Get("health_limb_part_destroy") : LocaleManager.Get("health_limb_part_healthy");

                var activeEffects = healthCtrl.GetAllActiveEffects(part);
                if (activeEffects != null)
                {
                    foreach (var effect in activeEffects)
                    {
                        //var type = effect.Type.FullName.ToString().ToLower();
                        var variation = effect.DisplayableVariations?.FirstOrDefault();
                        //string effectName = (!type.Contains("encumber") && !type.Contains("over") && !type.Contains("weight") && !type.Contains("exhaustion") && variation != null && variation.BuffType != GClass3056.EBuffType.Stimulant) ? variation.Buffs?.FirstOrDefault()?.Text ?? "" : "";
                        //怪了, 怎么改都滤不掉超重
                        //暂且搁置吧
                        string effectName = (variation != null && variation.BuffType != GClass3056.EBuffType.Stimulant) ? variation.Buffs?.FirstOrDefault()?.Text?.Localized() ?? "" : "";
                        var notInBlackList = (effectName != "SevereMusclePain" && effectName != "MildMusclePain" && effectName != "Exhaustion");
                        if (!string.IsNullOrEmpty(effectName) && notInBlackList && part != EBodyPart.Head && part != EBodyPart.Chest)
                        {
                            statusText += string.Format(LocaleManager.Get("health_limb_part_buff"),effectName);
                        }
                    }
                }


                string line = string.Format(LocaleManager.Get("health_limb_part_hp"),
                    partName,//.PadLeft(10),
                    LocaleManager.Get($"health_limb_part_hp_color_{HealthStatusLow(hp.Current, hp.Maximum)}"), 
                    hp.Current, 
                    hp.Maximum,
                    statusText);
                HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh), line, mainColor, textStyle); currentY += lh;
            }

            currentY += 5f * finalScale;

            // --- 区块 3：战斗与耐力 ---
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, 25), LocaleManager.Get("health_endur_title"), mainColor, titleStyle); currentY += lh;

            float weight = physCtrl.IobserverToPlayerBridge_0.TotalWeight;
            float overWeight = physCtrl.BaseOverweightLimits.x;
            float maxWeight = physCtrl.BaseOverweightLimits.y;
            float weightLimit = weight >= overWeight ? maxWeight : overWeight;
            string weightColor = weight < overWeight ? LocaleManager.Get("health_endur_normal_weight_color") : weight >= overWeight ? LocaleManager.Get("health_endur_over_weight_color") : LocaleManager.Get("health_endur_critical_weight_color");

            string weightStatus = string.Format(LocaleManager.Get("health_endur_normal_weight"), LocaleManager.Get("health_endur_normal_weight_color"));
            if (weight >= overWeight) weightStatus = string.Format(LocaleManager.Get("health_endur_over_weight"), LocaleManager.Get("health_endur_over_weight_color"));
            if (weight >= maxWeight) weightStatus = string.Format(LocaleManager.Get("health_endur_critical_weight"), LocaleManager.Get("health_endur_critical_weight_color"));

            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh),
                string.Format(LocaleManager.Get("health_endur_weight"),
                    weightColor,
                    weight,
                    weightLimit,
                    weightStatus), 
                mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh),
                string.Format(LocaleManager.Get("health_endur_oxygen"),
                    LocaleManager.Get("health_endur_oxygen_color"),
                    physCtrl.Oxygen.Current,
                    physCtrl.Oxygen.TotalCapacity.Value,
                    physCtrl.Oxygen.Current / physCtrl.Oxygen.TotalCapacity.Value * 100), 
                mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh),
                string.Format(LocaleManager.Get("health_endur_upper_stam"),
                    LocaleManager.Get("health_endur_upper_stam_color"),
                    physCtrl.HandsStamina.Current,
                    physCtrl.HandsStamina.TotalCapacity.Value,
                    physCtrl.HandsStamina.Current / physCtrl.HandsStamina.TotalCapacity.Value * 100),
                mainColor, textStyle); currentY += lh;
            HUDManager.DrawShadowLabel(new Rect(finalX, currentY, rectWidth, lh),
                string.Format(LocaleManager.Get("health_endur_lower_stam"),
                    LocaleManager.Get("health_endur_lower_stam_color"),
                    physCtrl.Stamina.Current,
                    physCtrl.Stamina.TotalCapacity.Value,
                    physCtrl.Stamina.Current / physCtrl.Stamina.TotalCapacity.Value * 100),
                mainColor, textStyle); currentY += lh;

            return finalX; // 返回占用后的最左侧坐标
        }
        public static int GetHealthStatus(float current, float max)
        {
            if (max <= 0f) return 0; // 防呆：防止除以 0

            float ratio = current / max;

            if (ratio <= 0f) return 0;          // 脱水/力竭
            if (ratio <= 0.20f) return 1;       // 极低
            if (ratio <= 0.50f) return 2;       // 较低
            return 3;                           // 健康
        }
        public static int HealthStatusLow(float current, float max)
        {
            if (max <= 0f) return 0; // 防呆：防止除以 0

            float ratio = current / max;

            if (ratio <= 0.20f) return 0;       // 极低
            return 1;                           // 健康
        }
    }
}