using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EFTBallisticCalculator.Locale
{
    public static class CfgLocaleManager
    {
        public static ConfigEntry<string> CurrentLanguage;

        private static readonly Dictionary<string, Dictionary<string, string>> _loadedTranslations = new Dictionary<string, Dictionary<string, string>>();
        private const string FallbackLangName = "English";

        public static void Init(ConfigFile config)
        {
            string dirPath = Path.Combine(PluginsCore.pluginDir, "locales", "menu");
            //if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

            _loadedTranslations.Clear();
            List<string> availableLanguages = new List<string>();

            string[] jsonFiles = Directory.GetFiles(dirPath, "*.json");
            foreach (string file in jsonFiles)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    LocaleData data = JsonConvert.DeserializeObject<LocaleData>(json);

                    if (data != null && !string.IsNullOrEmpty(data.Language) && data.Translate != null)
                    {
                        _loadedTranslations[data.Language] = data.Translate;
                        availableLanguages.Add(data.Language);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[EFTBallisticCalculator] Menu Locale Load Error ({file}): {e.Message}");
                }
            }

            if (availableLanguages.Count == 0)
            {
                availableLanguages.Add(FallbackLangName);
                _loadedTranslations[FallbackLangName] = new Dictionary<string, string>();
            }

            // 绑定 Config
            CurrentLanguage = config.Bind(
                "Language / 语言",
                "Menu Language / 配置菜单语言",
                availableLanguages.Contains(FallbackLangName) ? FallbackLangName : availableLanguages[0],
                new ConfigDescription(
                    "Change Configuration menu's language (Requires game restart). / 更改 F12 配置菜单的显示语言（需要重启游戏生效）。",
                    new AcceptableValueList<string>(availableLanguages.ToArray())
                ));
        }

        public static string Get(string key)
        {
            if (_loadedTranslations.TryGetValue(CurrentLanguage.Value, out var currentDict))
            {
                if (currentDict.TryGetValue(key, out var text)) return text;
            }

            if (_loadedTranslations.TryGetValue(FallbackLangName, out var fallbackDict))
            {
                if (fallbackDict.TryGetValue(key, out var fallbackText)) return fallbackText;
            }

            return key;
        }
    }
}