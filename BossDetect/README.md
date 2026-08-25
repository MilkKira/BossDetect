# Boss Detect

独立的客户端 BepInEx 插件：把旧项目 `EFTBallisticCalculator` 里 `HUD/BossPanel.cs` 的客户端 BOSS 检测逻辑拆分出来，改为使用游戏内 Notify 通知，并按玩家藏身处**情报中心（Intelligence Center）**等级决定通知内容。

## 等级规则

| 情报中心等级 | 通知内容 |
| --- | --- |
| 0（未建造） | 不发送通知（仅日志） |
| 1 | `检测到BOSS {BOSS Name}，无法探测位置`（每个 BOSS 每局一次） |
| 2 | `检测到BOSS {BOSS Name}，距离您 {远近描述词}`，距离档位变化时重新通知 |
| 3 | `检测到BOSS {BOSS Name}，距离您 {X}m`，按配置间隔/最小变化量实时刷新 |

2 级距离档位（假设为"小于等于上限"）：`≤25m 很近`、`≤50m 近`、`≤75m 中近`、`≤100m 中`、`≤150m 中远`、`≤200m 远`、`>200m 很远`。

## 兼容目标

- EFT 客户端 0.16.9（已核对 `Assembly-CSharp.dll` 中 `NotificationManagerClass.DisplayMessageNotification`、`Profile.Hideout.Areas`、`EAreaType.IntelligenceCenter` 等接口）
- BepInEx 5（`BepInEx.dll` 5.4.23）
- SPT 4.0.13

插件只读取游戏公开状态，不修改任何原方法，因此不依赖 Harmony 补丁。

## 构建

1. 打开 `BossDetect.csproj`，把 `<GameRoot>` 改成你的 SPT 客户端根目录（默认 `D:\BaiduSyncdisk\EscapeFromTarkovFiles`）。
2. 构建 Release：

```powershell
dotnet build BossDetect.csproj -c Release
```

输出会直接写入 `<GameRoot>\BepInEx\plugins\BossDetect\BossDetect.dll`。若只想在仓库内验证构建，可临时覆盖输出目录：

```powershell
dotnet build BossDetect.csproj -c Release -p:OutDir=build\Release\
```

## 安装

把 `BossDetect.dll` 放到 `BepInEx\plugins\BossDetect\` 后启动游戏即可。首次运行后配置文件位于 `BepInEx\config\com.mochix2milk.bossdetect.cfg`。

## 配置项

| 配置 | 默认 | 说明 |
| --- | --- | --- |
| 启用 | true | 总开关 |
| 情报中心等级覆盖 | -1 | 手动指定 1/2/3 用于测试；-1 自动读取真实等级 |
| 3级实时刷新间隔(秒) | 5 | 3 级时实时距离通知的刷新间隔 |
| 3级最小距离变化(米) | 5 | 距离变化超过该值才刷新，避免刷屏 |
| 调试日志 | false | 输出详细日志 |

注意：如果同时安装了其他 BOSS 通知插件（如 `BossNotifier.dll`），两者会各自弹通知，建议只保留其一。
