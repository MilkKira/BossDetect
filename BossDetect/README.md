# Boss Detect

独立的客户端 BepInEx 插件：把旧项目 `EFTBallisticCalculator` 里 `HUD/BossPanel.cs` 的客户端 BOSS 检测逻辑拆分出来，改为使用游戏内 Notify 通知，并按玩家藏身处**情报中心（Intelligence Center）**等级决定通知内容。

## 通知时机

- **开局一次**：倒计时结束后（局内计时器 `GameTimer` 启动）自动执行一次情报播报，并开始对应情报中心等级的冷却。
- **手动扫描**：之后按快捷键 **O**（写死在源码中）执行扫描，冷却期间会提示
  `扫描仍在冷却中...请在{剩余秒数}秒后重试`。
- 开局播报和手动扫描共用冷却；没有目标或因情报错误未显示通知时也会进入冷却。冷却中按 O 只提示剩余秒数，不重置冷却。

## 通用规则

- **情报错误定义**：战局实际已刷新对应 BOSS，但情报有概率不推送刷新提示；多个 BOSS 同时刷新时，每个 BOSS 独立计算错误概率。
- **乱码距离**：距离远近（2 级）/ 距离数值（3 级）有概率以乱码形式呈现（通知无法真正滚动，按同长度静态乱码模拟干扰）。

## 等级规则

| 情报中心等级 | 情报错误率 | 冷却 | 信息内容 | 检测限制 | 附加能力 |
| --- | --- | --- | --- | --- | --- |
| 0（未建造） | - | - | 不发送情报（仅日志） | - | - |
| 1 | 25% | 120 秒 | 仅提示该区域是否刷新 BOSS：`检测到BOSS {BOSS Name}` | 无法识别黑狐/伟哥/典狱长 | 不能检测 BOSS 死亡 |
| 2 | 10% | 90 秒 | `检测到BOSS {BOSS Name}，距离您 {远近描述词}`（10% 概率距离乱码） | 无法识别黑狐/伟哥/典狱长 | 可检测 BOSS 死亡（扫描时播报 `BOSS {name} 已死亡`） |
| 3 | 0% | 60 秒 | 普通 BOSS：`检测到BOSS {BOSS Name}，距离您 {X}m`（2% 概率乱码）；特殊目标黑狐/伟哥/典狱长：`检测到特殊目标 {BOSS Name}`，仅播报是否刷新、不显示位置 | 无 | 可检测 BOSS 死亡 |

2 级距离档位（假设为"小于等于上限"）：`≤25m 很近`、`≤50m 近`、`≤75m 中近`、`≤100m 中`、`≤150m 中远`、`≤200m 远`、`>200m 很远`。

特殊目标角色按角色字符串匹配：`black`（黑狐）、`wedge`（伟哥/Wedge）、`mercenary`（典狱长/Mercenary）。

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

把 `BossDetect.dll` 放到 `BepInEx\plugins\BossDetect\` 后启动游戏即可。插件不使用 BepInEx Config，所有参数都写死在代码里。

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
| 调试日志 | 关 | 输出详细日志 |

注意：如果同时安装了其他 BOSS 通知插件（如 `BossNotifier.dll`），两者会各自弹通知，建议只保留其一。
