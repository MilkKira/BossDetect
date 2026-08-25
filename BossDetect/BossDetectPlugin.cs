using BepInEx;
using BepInEx.Logging;

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
            BossDetector.Init(Config, Logger);
            Logger.LogInfo($"{PluginsInfo.NAME} {PluginsInfo.VERSION} loaded");
        }

        private void Update()
        {
            BossDetector.Update();
        }
    }
}
