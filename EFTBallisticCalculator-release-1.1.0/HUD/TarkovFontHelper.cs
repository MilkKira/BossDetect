using TMPro;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System;

namespace EFTBallisticCalculator.HUD
{
    public static class TarkovFontHelper
    {
        private static Font _cachedTarkovFont = null;
        private static bool _initAttempted = false;

        public static Font GetTarkovFont()
        {
            //抓不到, 后续再改, 原地返回
            return _cachedTarkovFont;
            // 只在第一次调用时执行抓取逻辑，后续直接返回缓存
            if (_cachedTarkovFont!=null) return _cachedTarkovFont;

            try
            {
                // 1. 尝试使用 Discord 老哥提供的路径
                IEnumerable<TMP_FontAsset> tarkovFonts = LocaleManagerClass.LocaleManagerClass.Ienumerable_1;

                // 2. 万能兜底机制：如果上面的路径失效或为 null，直接去内存里强行搜刮所有 TMP 字体
                if (tarkovFonts == null || !tarkovFonts.Any())
                {
                    tarkovFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                }

                if (tarkovFonts != null && tarkovFonts.Any())
                {
                    // 塔科夫最具灵魂的标志性字体是 "Bender"
                    TMP_FontAsset benderTMP = tarkovFonts.FirstOrDefault(f => f.name.ToLower().Contains("bender"));

                    // 如果没找到 Bender，随便抓取内存里的第一个游戏字体
                    if (benderTMP == null)
                    {
                        benderTMP = tarkovFonts.FirstOrDefault();
                    }

                    // 3. 【核心魔法】：提取底层的原生 Font 给 OnGUI 使用！
                    if (benderTMP != null && benderTMP.sourceFontFile != null)
                    {
                        _cachedTarkovFont = benderTMP.sourceFontFile;
                        Console.WriteLine($"[ModernTacHUD] 成功提取塔科夫原生字体: {_cachedTarkovFont.name}");
                    }
                }
            }
            catch (System.Exception e)
            {
                // 如果出错了，保持 null，OnGUI 会自动回退到默认的 Arial 字体，绝对不崩
                Console.WriteLine($"[ModernTacHUD] 获取塔科夫字体失败，将使用系统默认字体: {e.Message}");
            }

            return _cachedTarkovFont;
        }
        public static GUIStyle ApplyTarkovFont(this GUIStyle style)
        {
            Font tFont = GetTarkovFont();
            if (tFont != null)
            {
                style.font = tFont;
            }
            return style;
        }
    }
}