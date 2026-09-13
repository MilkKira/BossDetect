namespace BossDetect
{
    /// <summary>
    /// BOSS 显示名处理：俄文昵称转拉丁字母（复用原版 GStruct21.ConvertToLatinic，与原 TeamPanel 相同）。
    /// </summary>
    public static class PlayerNameHelper
    {
        /// <summary>
        /// 将昵称转换为通知显示名；空昵称使用占位名，转换失败时保留原名。
        /// </summary>
        public static string GetDisplayName(string nickname)
        {
            if (string.IsNullOrEmpty(nickname)) return "UNKNOWN";
            if (IsAllEnglish(nickname)) return nickname;

            try
            {
                return GStruct21.ConvertToLatinic(nickname);
            }
            catch
            {
                return nickname;
            }
        }

        /// <summary>
        /// 检查昵称是否仅含 ASCII 字母、数字及常用分隔符，无需转写。
        /// </summary>
        private static bool IsAllEnglish(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if ((c < 'A' || c > 'Z') &&
                    (c < 'a' || c > 'z') &&
                    (c < '0' || c > '9') &&
                    c != ' ' && c != '-' && c != '_')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
