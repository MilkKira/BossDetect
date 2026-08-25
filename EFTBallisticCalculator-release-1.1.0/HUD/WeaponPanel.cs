using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using EFTBallisticCalculator.Locale;
using System.Linq;
using UnityEngine;

namespace EFTBallisticCalculator.HUD
{
    public static class WeaponPanel
    {
        public static ConfigEntry<float> OffsetY;
        public static ConfigEntry<float> Scale;
        public static ConfigEntry<bool> Active;
        public static ConfigEntry<Color> Color;


        public static ItemAttributeClass? recoilAttr;// = weapon.Attributes.FirstOrDefault(a => a.Id == EItemAttributeId.RecoilUp);
        public static ItemAttributeClass? recoilbackAttr;// = weapon.Attributes.FirstOrDefault(a => a.Id == EItemAttributeId.Velocity);
        public static ItemAttributeClass? moaAttr;// = weapon.Attributes.FirstOrDefault(a => a.Id == EItemAttributeId.Velocity);
        public static Weapon? weaponCache;

        // 性能优化：静态复用 GUIContent，避免每帧 new 产生 GC Alloc
        private static readonly GUIContent _calcContent = new GUIContent();

        public static void InitCfg(ConfigFile config)
        {
            OffsetY = config.Bind("Top Panel / 武器数据", "Y轴偏移", 15f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_weap_y_desc"),
                new AcceptableValueRange<float>(-1080f, 1080f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_weap_y_name"), IsAdvanced = true }));

            Scale = config.Bind("Top Panel / 武器数据", "缩放比例", 1.0f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_weap_scale_desc"),
                new AcceptableValueRange<float>(0f, 5f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_weap_scale_name"), IsAdvanced = true }));

            Active = config.Bind("Top Panel / 武器数据", "显示面板", true,
                new ConfigDescription(CfgLocaleManager.Get("cfg_weap_active_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_weap_active_name") }));

            Color = config.Bind("Top Panel / 武器数据", "颜色设置", new Color(0.6f, 0.9f, 1f, 0.9f),
                new ConfigDescription(CfgLocaleManager.Get("cfg_weap_color_desc"), null,
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_weap_color_name") }));
        }

        // 返回武器面板占用的【最左侧绝对 X 坐标】
        public static float Draw(float startY, float globalScale)
        {
            if (!Active.Value || PluginsCore.CorrectPlayer == null) return Screen.width / 2f;

            var player = PluginsCore.CorrectPlayer;
            if (player.HandsController == null || !(player.HandsController.Item is Weapon weapon))
            {
                weaponCache = null;
                recoilAttr = null;
                recoilbackAttr = null;
                moaAttr = null;
                return Screen.width / 2f; // 没拿武器时，返回屏幕正中心作为锚点
            }

            if (weaponCache != null && weaponCache != weapon)
            {
                weaponCache = null;
                recoilAttr = null;
                recoilbackAttr = null;
                moaAttr = null;
            }
            if (weaponCache == null)
            {
                weaponCache = weapon;
            }
            if (recoilAttr == null)
            {
                recoilAttr = weapon.Attributes.FirstOrDefault(a => (EItemAttributeId)a.Id == EItemAttributeId.RecoilUp);
            }
            if (recoilbackAttr == null)
            {
                recoilbackAttr = weapon.Attributes.FirstOrDefault(a => (EItemAttributeId)a.Id == EItemAttributeId.RecoilBack);
            }
            if (moaAttr == null)
            {
                moaAttr = weapon.Attributes.FirstOrDefault(a => (EItemAttributeId)a.Id == EItemAttributeId.CenterOfImpact);
            }

            float finalScale = globalScale * Scale.Value;

            int nameSize = (int)(22 * finalScale);
            int statSize = (int)(15 * finalScale);
            int ammoSize = (int)(16 * finalScale);

            float currentY = startY + OffsetY.Value * finalScale;

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = nameSize, alignment = TextAnchor.MiddleCenter }.ApplyTarkovFont();
            GUIStyle statStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = statSize, alignment = TextAnchor.MiddleCenter }.ApplyTarkovFont();
            GUIStyle ammoStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = ammoSize, alignment = TextAnchor.MiddleCenter }.ApplyTarkovFont();
            UnityEngine.Color mainColor = HUDManager.RainbowUI.Value ? HUDManager.RainbowColor : HUDManager.UIColorOverride.Value ? HUDManager.OverrideColor.Value : Color.Value;

            // ==========================================
            // 核心数据提取
            // ==========================================
            string weaponName = weapon.Name.Localized();
            string fireMode = weapon.SelectedFireMode.ToString().Localized();

            float dur = weapon.Repairable?.Durability ?? 100f;
            float maxDur = weapon.Repairable?.MaxDurability ?? 100f;
            //string durColor = (dur / maxDur) < 0.5f ? "#ff4444" : "#ffffff";

            string moa = moaAttr.StringValue();
            float ergo = weapon.ErgonomicsTotal;
            float recoilUp = float.Parse(recoilAttr.StringValue());
            float recoilBack = float.Parse(recoilbackAttr.StringValue());

            string chamberAmmoName = LocaleManager.Get("weapon_no_ammo");
            string nextAmmoName = LocaleManager.Get("weapon_no_ammo");
            int currentMagCount = 0;
            int maxMagCount = 0;

            var chamber = weapon.Chambers?.FirstOrDefault();
            if (chamber?.ContainedItem is AmmoItemClass chamberBullet)
            {
                chamberAmmoName = chamberBullet.ShortName.Localized();
            }

            var mag = weapon.GetCurrentMagazine();
            if (mag != null)
            {
                currentMagCount = mag.Count;
                maxMagCount = mag.MaxCount;

                if (mag.Cartridges?.Items?.LastOrDefault() is AmmoItemClass topBullet)
                {
                    nextAmmoName = topBullet.ShortName.Localized();
                }
            }

            // ==========================================
            // 文本组装
            // ==========================================
            string nameLine = string.Format(LocaleManager.Get("weapon_name"), weaponName);//$"<b>[ {weaponName} ]</b>";
            string statLine = string.Format(
                LocaleManager.Get("weapon_state"),
                LocaleManager.Get($"weapon_dur_color_{GetWeaponDurStatus(dur, maxDur)}"),
                dur,
                maxDur,
                ergo,
                recoilUp,
                recoilBack,
                moa);//$"DUR: <color={durColor}>{dur:F1}</color>  |  ERGO: {ergo:F1}  |  REC: {recoilUp:F0}/{recoilBack:F0}  |  MOA: {moa}";

            //string chamberColor = chamberAmmoName == "EMPTY" ? "#ff4444" : "#55ff55";
            //string magColor = currentMagCount == 0 ? "#ff4444" : (currentMagCount <= maxMagCount * 0.3f ? "#ffaa00" : "#ffffff");
            string ammoCountStr = mag != null ?
                string.Format(LocaleManager.Get("weapon_mag"),
                LocaleManager.Get($"weapon_mag_color_{GetWeaponMagStatus(currentMagCount, maxMagCount)}"),
                currentMagCount,
                maxMagCount) :
                LocaleManager.Get("weapon_mag_empty");//$"MAG: <color={magColor}>{currentMagCount}/{maxMagCount}</color>" : "MAG: --/--";
            string ammoLine = string.Format(LocaleManager.Get("weapon_ammo"),
                fireMode,
                ammoCountStr,
                chamberAmmoName,
                nextAmmoName);//$"MODE: <color=#ffaa00>{fireMode}</color>   ||   {ammoCountStr}   |   CHBR: <color={chamberColor}>{chamberAmmoName}</color>  >>  NXT: {nextAmmoName}";

            // ==========================================
            // 【核心】：动态计算这段文字在屏幕上的实际宽度
            // ==========================================
            _calcContent.text = nameLine;
            float w1 = nameStyle.CalcSize(_calcContent).x;
            _calcContent.text = statLine;
            float w2 = statStyle.CalcSize(_calcContent).x;
            _calcContent.text = ammoLine;
            float w3 = ammoStyle.CalcSize(_calcContent).x;

            float maxTextWidth = Mathf.Max(w1, w2, w3);

            // 因为文字是屏幕居中的，最左侧边界 = 中心点 - (总宽 / 2)
            float leftBoundX = (Screen.width / 2f) - (maxTextWidth / 2f);

            // ==========================================
            // UI 渲染
            // ==========================================
            float screenW = Screen.width;

            float nameHeight = 30f * finalScale;
            HUDManager.DrawShadowLabel(new Rect(0, currentY, screenW, nameHeight), nameLine, mainColor, nameStyle);
            currentY += nameHeight;

            float statHeight = 24f * finalScale;
            HUDManager.DrawShadowLabel(new Rect(0, currentY, screenW, statHeight), statLine, mainColor, statStyle);
            currentY += statHeight;

            float ammoHeight = 26f * finalScale;
            HUDManager.DrawShadowLabel(new Rect(0, currentY, screenW, ammoHeight), ammoLine, mainColor, ammoStyle);

            return leftBoundX; // 吐出左边界给投掷物面板
        }
        public static int GetWeaponDurStatus(float current, float max)
        {
            if (max <= 0f) return 0; // 防呆：防止除以 0

            float ratio = current / max;

            if (ratio <= 0.25f) return 0;       // 极低
            if (ratio <= 0.60f) return 1;       // 较低
            if (ratio <= 0.80f) return 2;       // 较低
            if (ratio <= 0.90f) return 3;       // 较低
            return 4;                           // 健康
        }
        public static int GetWeaponMagStatus(float current, float max)
        {
            if (max <= 0f) return 0; // 防呆：防止除以 0

            float ratio = current / max;

            if (ratio <= 0.2f) return 0;
            if (ratio <= 0.5f) return 1;  
            return 2;                           
        }
    }
}