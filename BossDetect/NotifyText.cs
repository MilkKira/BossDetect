namespace BossDetect
{
    /// <summary>
    /// 通知文案。占位符按用户规格固定：
    ///   1级：检测到BOSS {BOSS Name}（仅提示是否刷新，不含位置/距离）
    ///   2级：检测到BOSS {BOSS Name}，距离您 {远近描述词}
    ///   3级：检测到BOSS {BOSS Name}，距离您 {X}m（普通BOSS）
    ///   3级特殊目标（黑狐/伟哥/典狱长）：检测到特殊目标 {BOSS Name}，不显示位置
    ///   死亡播报（2/3级）：BOSS {BOSS Name} 已死亡
    ///   冷却提示：扫描仍在冷却中...请在{剩余秒数}秒后重试
    /// </summary>
    public static class NotifyText
    {
        public const string Level1 = "检测到BOSS {0}";
        public const string Level2 = "检测到BOSS {0}，距离您 {1}";
        public const string Level3 = "检测到BOSS {0}，距离您 {1}m";
        public const string Level3Special = "检测到特殊目标 {0}";
        public const string BossDead = "BOSS {0} 已死亡";
        public const string NoBossFound = "未发现任何BOSS";
        public const string Cooldown = "扫描仍在冷却中...请在{0}秒后重试";
    }
}
