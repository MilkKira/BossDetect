# Boss Detect

当前版本：**1.4.0**。

独立的客户端 BepInEx 插件：把旧项目 `EFTBallisticCalculator` 里 `HUD/BossPanel.cs` 的客户端 BOSS 检测逻辑拆分出来，改为使用游戏内 Notify 通知，并按玩家藏身处**情报中心（Intelligence Center）**等级决定通知内容。

## 通知时机

- **开局一次**：倒计时结束后（局内计时器 `GameTimer` 启动）自动执行一次情报播报，并开始对应情报中心等级的冷却。
- **手动扫描**：之后按快捷键 **O**（写死在源码中）执行扫描，冷却期间会提示
  `扫描仍在冷却中...请在{剩余秒数}秒后重试`。
- 开局播报和手动扫描共用冷却；没有目标或因情报错误未显示通知时也会进入冷却。冷却中按 O 只提示剩余秒数，不重置冷却。

## 通用规则

- **情报错误定义**：战局实际已刷新对应 BOSS，但情报有概率不推送刷新提示；多个 BOSS 同时刷新时，每个 BOSS 独立计算错误概率。
- **乱码距离**：距离远近（2 级）/ 距离数值（3 级）有概率以乱码形式呈现（通知无法真正滚动，按同长度静态乱码模拟干扰）。
- **无目标提示**：情报中心至少 1 级时，若本次扫描没有播报任何 BOSS 或死亡信息，则提示 `未发现任何BOSS`，包括没有可识别目标、全部存活目标因概率漏报而未显示，以及死亡信息已播报过的情况。开局播报和手动扫描均适用；等级限制与冷却仍然生效。

## 等级规则

| 情报中心等级 | 情报错误率 | 冷却 | 信息内容 | 检测限制 | 附加能力 |
| --- | --- | --- | --- | --- | --- |
| 0（未建造） | - | - | 不发送情报（仅日志） | - | - |
| 1 | 25% | 120 秒 | 仅提示该区域是否刷新 BOSS：`检测到BOSS {BOSS Name}` | 无法识别黑狐/伟哥/典狱长 | 不能检测 BOSS 死亡 |
| 2 | 10% | 90 秒 | `检测到BOSS {BOSS Name}，距离您 {远近描述词}`（10% 概率距离乱码） | 无法识别黑狐/伟哥/典狱长 | 可检测 BOSS 死亡（扫描时播报 `BOSS {name} 已死亡`） |
| 3 | 0% | 60 秒 | 普通 BOSS：`检测到BOSS {BOSS Name}，距离您 {X}m`（2% 概率乱码）；特殊目标黑狐/伟哥/典狱长：`检测到特殊目标 {BOSS Name}`，仅播报是否刷新、不显示位置 | 无 | 可检测 BOSS 死亡 |

2 级距离档位（假设为"小于等于上限"）：`≤25m 很近`、`≤50m 近`、`≤75m 中近`、`≤100m 中`、`≤150m 中远`、`≤200m 远`、`>200m 很远`。

特殊目标角色转为小写后按字符串包含关系匹配：`black`（黑狐）、`wedge`（伟哥/Wedge）、`odin`（典狱长/Odin）。

## 兼容目标

- EFT 客户端 0.16.9（已核对 `Assembly-CSharp.dll` 中 `NotificationManagerClass.DisplayMessageNotification`、`Profile.Hideout.Areas`、`EAreaType.IntelligenceCenter` 等接口）
- BepInEx 5（`BepInEx.dll` 5.4.23）
- SPT 4.0.13

插件只读取游戏公开状态，不修改任何原方法，因此不依赖 Harmony 补丁。

## 构建

1. 进入 `BossDetect` 项目目录，打开 `BossDetect.csproj`，把 `<GameRoot>` 改成你的 SPT 客户端根目录（当前为 `F:\TarKov\CH46284`）。
2. 构建 Release：

```powershell
dotnet build BossDetect.csproj -c Release
```

输出会直接写入 `<GameRoot>\BepInEx\plugins\BossDetect\BossDetect.dll`。若只想在仓库内验证构建，可临时覆盖输出目录：

```powershell
dotnet build BossDetect.csproj -c Release -p:OutDir=build\Release\
```

## 安装

把 `BossDetect.dll` 放到 `BepInEx\plugins\BossDetect\` 后启动游戏即可。玩法参数固定在代码中，Debug 日志开关使用 BepInEx Config。

## Debug 配置

首次启动插件后生成 `BepInEx\config\com.mochix2milk.bossdetect.cfg`，默认关闭调试日志：

```ini
[Debug]
DebugLogging = false
```

改为 `true` 并重启游戏，或通过配置管理器在运行中切换，即可输出战局状态、目标识别、冷却与情报判定日志。调试消息带有 `[BossDetect][Debug]` 前缀，使用 Info 级别写入 `BepInEx\LogOutput.log`，无需额外开启 BepInEx 的 Debug 级别过滤。

## 1.4.0 更新

- 开局播报与手动扫描共用冷却，冷却期间按 O 提示剩余秒数。
- 修正典狱长 `Odin` 角色标记的大小写匹配。
- 简化重复判断，补充关键函数中文注释，统一代码格式并清理文档。

## 写死参数（BossDetector.cs 顶部常量区）

| 参数 | 值 | 说明 |
| --- | --- | --- |
| 手动扫描快捷键 | `O` | 开局播报后手动扫描的按键 |
| 1级冷却 | 120 秒 | 情报中心 1 级时手动扫描的冷却 |
| 2级冷却 | 90 秒 | 情报中心 2 级时手动扫描的冷却 |
| 3级冷却 | 60 秒 | 情报中心 3 级时手动扫描的冷却 |
| 1级情报错误率 | 25% | 1 级不推送刷新提示的概率（每 BOSS 独立） |
| 2级情报错误率 | 10% | 2 级不推送刷新提示的概率（每 BOSS 独立） |
| 3级情报错误率 | 0% | 3 级不推送刷新提示的概率（每 BOSS 独立） |
| 2级距离乱码率 | 10% | 2 级距离远近描述乱码的概率 |
| 3级距离乱码率 | 2% | 3 级普通 BOSS 距离数值乱码的概率 |

注意：如果同时安装了其他 BOSS 通知插件（如 `BossNotifier.dll`），两者会各自弹通知，建议只保留其一。
