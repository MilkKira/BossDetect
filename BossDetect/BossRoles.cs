namespace BossDetect
{
    /// <summary>
    /// BOSS 身份判定。逻辑与旧项目 BossPanel.TryGetBossPrefix 保持一致：
    /// 核心 Boss、邪教徒、圣诞老人、寻血猎犬、WTT 黑狐，以及少数不带 boss 前缀的特殊单位。
    /// 另区分普通 BOSS 与“特殊目标”（仅 3 级情报中心可识别）：
    /// 黑狐(Black Fox)、伟哥(Wedge)、典狱长(Mercenary)。
    /// </summary>
    public static class BossRoles
    {
        // WTT 等自定义 BOSS 的角色名标记（按角色字符串包含关系匹配）
        private const string BlackFoxMarker = "black";
        private const string WedgeMarker = "wedge";
        private const string MercenaryMarker = "Odin";

        public static bool IsBoss(string role)
        {
            if (string.IsNullOrEmpty(role)) return false;

            if (role.Contains("boss")) return true;
            if (role.Contains("sectant")) return true;
            if (role == "gifter") return true;
            if (role.Contains("arena")) return true;
            if (role.Contains(BlackFoxMarker)) return true;
            if (role.Contains(WedgeMarker)) return true;
            if (role.Contains(MercenaryMarker)) return true;

            switch (role)
            {
                // 不带 Boss 前缀但属于 Boss 单位的特殊角色
                case "followerbirdeye":
                case "followerbigpipe":
                case "infectedtagilla":
                case "sectantoni":
                case "sectantpredvestnik":
                case "sectantprizark":
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 是否为特殊目标：黑狐 / 伟哥(Wedge) / 典狱长(Mercenary)。
        /// 1、2 级情报中心无法识别，3 级仅播报是否刷新、不显示位置。
        /// </summary>
        public static bool IsSpecialBoss(string role)
        {
            if (string.IsNullOrEmpty(role)) return false;
            return role.Contains(BlackFoxMarker)
                || role.Contains(WedgeMarker)
                || role.Contains(MercenaryMarker);
        }
    }
}
