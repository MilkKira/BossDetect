using BepInEx;
using EFT;
using EFT.InventoryLogic;
using EFTBallisticCalculator.Core;
using EFTBallisticCalculator.HUD;
using EFTBallisticCalculator.Locale;
using HarmonyLib;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace EFTBallisticCalculator
{
    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class PluginsCore : BaseUnityPlugin
    {
        public static string dllPath = Assembly.GetExecutingAssembly().Location;
        public static string pluginDir = Path.GetDirectoryName(dllPath);
        #region 全局状态缓存
        public static bool _hasAmmo = false;

        public static float _currentSpeed = 0f;
        public static float _currentMass = 0f;
        public static float _currentBC = 0f;
        public static float _currentDiam = 0f;

        public static float _lockedHorizontalDist;
        public static float _lockedTOF;
        #endregion

        #region 跨脚本对象与环境
        public static float _weatherSeedGlobal;
        public static System.Random _weatherRng;
        public class WeatherSeed { public float b; public float x; public float y; public float r; }
        public class WeatherSeedMap
        {
            public WeatherSeed windSpeed = new WeatherSeed();
            public WeatherSeed windDirection = new WeatherSeed();
            public WeatherSeed humidity = new WeatherSeed();
            public WeatherSeed temperature = new WeatherSeed();
        }
        public static WeatherSeedMap _weatherSeedMap = new WeatherSeedMap();
        public static string _cachedLocationName = null;
        public static int _layerMask = -1;
        public static Player CorrectPlayer { get; set; }
        public static GameWorld CorrectGameWorld { get; set; }
        public static string CorrectGroupId { get; set; }

        public static EFT.Player.FirearmController _currentFC;
        public static Camera _cachedOpticCamera;
        public static GameObject _impactMarker;
        #endregion

        public void Awake()
        {
            // 最先初始化语言管理器
            CfgLocaleManager.Init(Config);
            LocaleManager.Init(Config);
            // 1. 下发配置给两大管家
            HotKeyManager.Init(Config);
            HUDManager.InitCfg(Config);
            BallisticsCalculator.InitCfg(Config);

            var harmony = new Harmony(PluginsInfo.GUID);
            harmony.PatchAll();
        }

        public void Update()
        {
            if (CorrectPlayer == null || Camera.main == null) return;
            if (_layerMask == -1) _layerMask = LayerMask.GetMask("Terrain", "HighPolyCollider", "Default");

            BallisticsCalculator.UpdateFCSData();


            BallisticsCalculator.UpdateOpticCache();

            HotKeyManager.ListenToHotKeyInput();

            // 3. 渲染物理预测球
            BallisticsCalculator.UpdateImpactMarker();
            ActiveBuffManager.Update();
        }

        

        

        public void OnGUI()
        {
            HUDManager.DrawGUI();
        }

        
    }
}