using BepInEx;

namespace BossDetect
{
    /// <summary>
    /// 客户端 BOSS 检测通知插件入口。
    /// 只读取游戏公开状态（存活玩家列表 / Profile 藏身处数据），不修改任何原方法，因此不需要 Harmony 补丁。
    /// </summary>
    [BepInPlugin(PluginsInfo.GUID, PluginsInfo.NAME, PluginsInfo.VERSION)]
    public class BossDetectPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            var debugLogging = Config.Bind(
                "Debug",
                "DebugLogging",
                false,
                "启用详细调试日志，输出战局状态、目标识别、冷却及情报判定信息。");

            BossDetector.Init(Logger, debugLogging);
            Logger.LogInfo($"{PluginsInfo.NAME} {PluginsInfo.VERSION} loaded");
        }

        private void Update()
        {
            BossDetector.Update();
        }
    }
}
