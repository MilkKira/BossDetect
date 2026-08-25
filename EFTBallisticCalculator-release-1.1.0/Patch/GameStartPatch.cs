using EFT;
using HarmonyLib;
using UnityEngine;

namespace EFTBallisticCalculator.Patch
{
    /// <summary>
    /// GameWorld 启动补丁，在此阶段拦截实例并初始化相关的环境种子数据
    /// </summary>
    [HarmonyPatch(typeof(GameWorld), "OnGameStarted")]
    public class GameStartPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GameWorld __instance)
        {
            PluginsCore.CorrectGameWorld = __instance;
            PluginsCore.CorrectPlayer = __instance.MainPlayer;
            PluginsCore.CorrectGroupId = __instance.MainPlayer.Profile?.Info?.GroupId ?? "";
            PluginsCore._weatherSeedGlobal = Random.Range(0f, 100000f);
            PluginsCore._weatherRng = new System.Random((int)PluginsCore._weatherSeedGlobal);

            PluginsCore._weatherSeedMap.windSpeed.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windSpeed.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windSpeed.b = PluginsCore._weatherRng.Next(20, 50) / 10f;
            PluginsCore._weatherSeedMap.windSpeed.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.windSpeed.x, PluginsCore._weatherSeedMap.windSpeed.y);

            PluginsCore._weatherSeedMap.windDirection.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windDirection.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.windDirection.b = PluginsCore._weatherRng.Next(0, 360);
            PluginsCore._weatherSeedMap.windDirection.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.windDirection.x, PluginsCore._weatherSeedMap.windDirection.y);

            PluginsCore._weatherSeedMap.humidity.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.humidity.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.humidity.b = PluginsCore._weatherRng.Next(1500, 3500) / 100f;
            PluginsCore._weatherSeedMap.humidity.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.humidity.x, PluginsCore._weatherSeedMap.humidity.y);

            PluginsCore._weatherSeedMap.temperature.x = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.temperature.y = PluginsCore._weatherRng.Next(0, 100000);
            PluginsCore._weatherSeedMap.temperature.b = PluginsCore._weatherRng.Next(60, 120) / 10f;
            PluginsCore._weatherSeedMap.temperature.r = Mathf.PerlinNoise(PluginsCore._weatherSeedMap.temperature.x, PluginsCore._weatherSeedMap.temperature.y);

            var locationId = __instance.LocationId;
            var location = locationId.Localized();
            PluginsCore._cachedLocationName = string.IsNullOrEmpty(location) ? locationId : location;
        }
    }
}