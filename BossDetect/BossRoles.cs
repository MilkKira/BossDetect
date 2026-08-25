using EFT;

namespace BossDetect
{
    /// <summary>
    /// BOSS 身份判定。逻辑与旧项目 BossPanel.TryGetBossPrefix 保持一致：
    /// 核心 Boss、邪教徒、圣诞老人、寻血猎犬、WTT 黑狐，以及少数不带 boss 前缀的特殊单位。
    /// </summary>
    public static class BossRoles
    {
        public static bool IsBoss(WildSpawnType? role)
        {
            return role.HasValue && IsBoss(role.Value.ToString().ToLowerInvariant());
        }

        public static bool IsBoss(string role)
        {
            if (string.IsNullOrEmpty(role)) return false;

            if (role.Contains("boss")) return true;
            if (role.Contains("sectant")) return true;
            if (role == "gifter") return true;
            if (role.Contains("arena")) return true;
            if (role.Contains("black")) return true;

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
    }
}
