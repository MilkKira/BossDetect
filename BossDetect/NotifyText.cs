namespace BossDetect
{
    /// <summary>
    /// 通知文案。占位符按用户规格固定：
    ///   1级：检测到BOSS {BOSS Name}，无法探测位置
    ///   2级：检测到BOSS {BOSS Name}，距离您 {远近描述词}
    ///   3级：实时显示距离（米）
    /// </summary>
    public static class NotifyText
    {
        public const string Level1 = "检测到BOSS {0}，无法探测位置";
        public const string Level2 = "检测到BOSS {0}，距离您 {1}";
        public const string Level3 = "检测到BOSS {0}，距离您 {1}m";
    }
}
