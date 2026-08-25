using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using EFT.HealthSystem;
using Fika.Core.Main.Components;
using Fika.Core.Main.ObservedClasses;

namespace EFTBallisticCalculator.HUD
{
    /// <summary>
    /// Fika 专属的隔离工具类。
    /// 必须使用 NoInlining 标签，防止单机环境下 JIT 编译器提前加载 Fika 类型导致游戏报错。
    /// </summary>
    public static class FikaIntegration
    {
        //规避NetworksoftJson的自动扫描构建
        //你丫是不是手贱....
        private static object _cachedCoopHandlerObj = null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UpdateFikaTeammateStatus(Dictionary<string, TeamPanel.TeammateRecord> roster)
        {
            if (_cachedCoopHandlerObj == null)
            {
                _cachedCoopHandlerObj = UnityEngine.Object.FindObjectOfType<CoopHandler>();
                if (_cachedCoopHandlerObj == null) return;
            }

            var handler = (CoopHandler)_cachedCoopHandlerObj;
            var fikaPlayers = handler.Players;
            var extractedNetIds = handler.ExtractedPlayers;

            if (extractedNetIds == null) return;

            foreach (var kvp in roster)
            {
                var record = kvp.Value;

                // 1. 趁玩家还没撤离/还在字典里，赶紧把他的 NetId 记在小本本上！
                if (record.FikaNetId == -1 && fikaPlayers != null)
                {
                    foreach (var fikaKvp in fikaPlayers)
                    {
                        if (fikaKvp.Value.ProfileId == record.AccountId)
                        {
                            record.FikaNetId = fikaKvp.Key; // 缓存！
                            break;
                        }
                    }
                }

                // 2. 只要还没被彻底标记为撤离，就拿着记好的 NetId 去对账。
                // 注意：这里去掉了 Status == Alive 的限制！
                // 这样一来，哪怕他刚消失的一瞬间被 TeamPanel 误判成了 Dead，也能瞬间被救回来变成 Extracted！
                if (record.Status != ETeammateStatus.Extracted && record.FikaNetId != -1)
                {
                    if (extractedNetIds.Contains(record.FikaNetId))
                    {
                        record.Status = ETeammateStatus.Extracted;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TryGetFikaStats(IHealthController healthCtrl, out float hydr, out float hydrmax, out float energy, out float energymax)
        {
            hydr = 0f; hydrmax = 100f; energy = 0f; energymax = 100f;

            // 确认是远端玩家的组件
            if (healthCtrl is ObservedHealthController fikaHealth)
            {
                try
                {
                    hydr = fikaHealth.HealthValue_1.Current;
                    hydrmax = fikaHealth.HealthValue_1.Maximum;

                    energy = fikaHealth.HealthValue_0.Current;
                    energymax = fikaHealth.HealthValue_0.Maximum;
                }
                catch (Exception)
                {
                    // 仅保留一个兜底，防止万一数据结构有变动导致报错
                }
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ClearCache()
        {
            _cachedCoopHandlerObj = null;
        }
    }
}