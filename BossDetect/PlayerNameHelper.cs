namespace BossDetect
{
    /// <summary>
    /// BOSS 显示名处理：俄文昵称转拉丁字母（复用原版 GStruct21.ConvertToLatinic，与原 TeamPanel 相同）。
    /// </summary>
    public static class PlayerNameHelper
    {
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
