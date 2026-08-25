using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.HealthSystem;
using UnityEngine;
using System.Text.RegularExpressions;

namespace EFTBallisticCalculator.HUD
{
    public static class ActiveBuffManager
    {
        public class DisplayEffect
        {
            public string Name;
            public float TimeLeft;
            public float Strength;
            public string EffectId;
        }

        public static IReadOnlyList<DisplayEffect> AllEffects => _allEffects;

        private static Player _localPlayer;
        private static IHealthController _healthController;
        private static Type _ipbType;
        private static Type _ieffectType; // 【新增】IEffect 接口类型缓存
        private static bool _ipbSearched;
        private static bool _eventsSubscribed;
        private static bool _deepScanDone;

        private static readonly Dictionary<string, object> _capturedBuffs = new Dictionary<string, object>();
        private static readonly Dictionary<int, object> _buffToContainer = new Dictionary<int, object>();
        private static readonly HashSet<int> _containerIds = new HashSet<int>();
        private static readonly List<object> _containers = new List<object>();
        private static readonly Dictionary<int, float> _buffWholeTimeOffset = new Dictionary<int, float>();

        private static readonly List<DisplayEffect> _allEffects = new List<DisplayEffect>();

        // ==========================================
        // 【优化 1：对象池与免分配静态容器】
        // ==========================================
        private static readonly Stack<DisplayEffect> _effectPool = new Stack<DisplayEffect>();
        private static readonly List<string> _deadKeys = new List<string>();
        private static readonly HashSet<int> _scanVisited = new HashSet<int>();
        private static readonly List<string> _staleKeys = new List<string>();

        // 使用 struct 代替 String 拼接，实现 0 GC 哈希查重
        private struct EffectIdentity : IEquatable<EffectIdentity>
        {
            public string Name;
            public float Strength;
            public bool Equals(EffectIdentity other) => Name == other.Name && Math.Abs(Strength - other.Strength) < 0.001f;
            public override int GetHashCode() => (Name?.GetHashCode() ?? 0) ^ Strength.GetHashCode();
        }
        private static readonly HashSet<EffectIdentity> _seenEffects = new HashSet<EffectIdentity>();

        // ==========================================
        // 【优化 2：反射极速缓存】
        // ==========================================
        private static readonly Dictionary<(Type, string), PropertyInfo> _propCache = new Dictionary<(Type, string), PropertyInfo>();
        private static readonly Dictionary<(Type, string), FieldInfo> _fieldCache = new Dictionary<(Type, string), FieldInfo>();

        // 编译好的正则，拒绝每帧创建状态机
        private static readonly Regex _colorRegex = new Regex(@"<color=[^>]*>", RegexOptions.Compiled);

        private static float _lastUpdateTime;
        public static float LastUpdateTime => _lastUpdateTime;
        private const float UPDATE_INTERVAL = 0.5f;
        private static int _tick;

        public static void Update()
        {
            if (Time.time - _lastUpdateTime < UPDATE_INTERVAL) return;
            _lastUpdateTime = Time.time;

            RefreshPlayerRef();
            if (_healthController != null)
                RefreshEffects();
        }

        private static void RefreshPlayerRef()
        {
            var player = PluginsCore.CorrectPlayer;
            if (player == null)
            {
                Reset();
                return;
            }

            if (_localPlayer == player && _healthController == player.HealthController)
                return;

            _localPlayer = player;
            _healthController = player.HealthController;
            _eventsSubscribed = false;
            _deepScanDone = false;
            _capturedBuffs.Clear();
            _buffToContainer.Clear();
            _containerIds.Clear();
            _containers.Clear();
            _buffWholeTimeOffset.Clear();
        }

        private static void Reset()
        {
            _localPlayer = null;
            _healthController = null;
            _eventsSubscribed = false;
            _deepScanDone = false;
            _capturedBuffs.Clear();
            _buffToContainer.Clear();
            _containerIds.Clear();
            _containers.Clear();
            _buffWholeTimeOffset.Clear();

            // 归还所有特效到对象池
            foreach (var effect in _allEffects) _effectPool.Push(effect);
            _allEffects.Clear();
        }

        private static void RefreshEffects()
        {
            // 1. 将现有的特效全部打回对象池，避免 new 操作
            foreach (var effect in _allEffects) _effectPool.Push(effect);
            _allEffects.Clear();

            if (_healthController == null) return;

            _tick++;

            if (!_eventsSubscribed) SubscribeEvents();
            if (!_deepScanDone) DeepScanHealthController();
            if (_tick % 3 == 0) QuickRescan();

            // 2. 清空并复用静态容器
            _deadKeys.Clear();
            _seenEffects.Clear();

            foreach (var kv in _capturedBuffs)
            {
                try
                {
                    var buff = kv.Value;
                    if (buff == null) { _deadKeys.Add(kv.Key); continue; }
                    if (!GetBoolProp(buff, "Active", true)) { _deadKeys.Add(kv.Key); continue; }

                    string buffName = GetStringProp(buff, "BuffName");
                    if (string.IsNullOrEmpty(buffName))
                    {
                        // 经过 GetBuffKey 的过滤，能走到这里的无名氏绝对只有止痛药了
                        buffName = "PainKiller";
                    }
                    if (string.IsNullOrEmpty(buffName)) continue;

                    float value = GetFloatProp(buff, "Value");
                    float timeLeft = SanitizeTimer(GetBuffTimeLeft(buff));

                    string displayName = StripValueFromName(StripColorTags(buffName));

                    // 使用 Struct 键值对，彻底消灭 string.Format 产生的 GC Alloc
                    var identity = new EffectIdentity { Name = displayName, Strength = value };

                    AddDedup(identity, displayName, timeLeft, value, GetBuffEffectId(buff));
                }
                catch { }
            }

            // 3. 批量移除死掉的 Key
            for (int i = 0; i < _deadKeys.Count; i++) _capturedBuffs.Remove(_deadKeys[i]);

            if (_allEffects.Count > 1)
                _allEffects.Sort((a, b) => a.TimeLeft.CompareTo(b.TimeLeft));
        }

        private static void AddDedup(EffectIdentity identity, string displayName, float timeLeft, float strength, string effectId)
        {
            var existing = _allEffects.Find(e => e.Name == displayName);
            if (existing != null)
            {
                bool sameStrength = Math.Abs(existing.Strength - strength) < 0.001f || (existing.Strength == 0f && strength == 0f);
                if (!sameStrength && strength != 0f && existing.Strength != 0f)
                {
                    if (!_seenEffects.Contains(identity))
                    {
                        _seenEffects.Add(identity);
                        _allEffects.Add(GetEffectFromPool(displayName, timeLeft, strength, effectId));
                    }
                    return;
                }
                if (timeLeft > 0 && (existing.TimeLeft <= 0 || timeLeft > existing.TimeLeft))
                    existing.TimeLeft = timeLeft;
                if (strength != 0f)
                    existing.Strength = strength;

                _seenEffects.Add(identity);
                return;
            }

            if (!_seenEffects.Contains(identity))
            {
                _seenEffects.Add(identity);
                _allEffects.Add(GetEffectFromPool(displayName, timeLeft, strength, effectId));
            }
        }

        private static DisplayEffect GetEffectFromPool(string name, float timeLeft, float strength, string effectId)
        {
            var effect = _effectPool.Count > 0 ? _effectPool.Pop() : new DisplayEffect();
            effect.Name = name;
            effect.TimeLeft = timeLeft;
            effect.Strength = strength;
            effect.EffectId = effectId;
            return effect;
        }

        // ---------- 时间获取 ----------
        private static float GetBuffTimeLeft(object buff)
        {
            float best = -1f;
            float duration = GetSettingsDuration(buff);
            float elapsed = GetFloatProp(buff, "WholeTime");
            int bid = buff.GetHashCode();

            if (duration > 0 && elapsed >= 0)
            {
                float offset = _buffWholeTimeOffset.TryGetValue(bid, out float off) ? off : 0f;
                float remaining = duration - (elapsed - offset);
                if (remaining > 0 && remaining < 100000f) best = remaining;
            }

            if (_buffToContainer.TryGetValue(bid, out object container))
            {
                float containerTL = GetFloatProp(container, "TimeLeft");
                if (containerTL > 0 && containerTL < 100000f && containerTL > best) best = containerTL;
            }

            float selfTL = GetFloatProp(buff, "TimeLeft");
            if (selfTL > 0 && selfTL < 100000f && selfTL > best) best = selfTL;

            // 【新增】：针对 IEffect 专属的 RemainingTime
            float remainingTL = GetFloatProp(buff, "RemainingTime");
            if (remainingTL > 0 && remainingTL < 100000f && remainingTL > best) best = remainingTL;

            return best;
        }

        private static float GetSettingsDuration(object buff)
        {
            try
            {
                var settingsProp = GetPropertyCached(buff.GetType(), "Settings");
                if (settingsProp == null) return -1f;
                var settings = settingsProp.GetValue(buff);
                if (settings == null) return -1f;
                return GetFloatProp(settings, "Duration");
            }
            catch { return -1f; }
        }

        // ---------- 事件订阅 ----------
        private static void SubscribeEvents()
        {
            if (_eventsSubscribed) return;
            _eventsSubscribed = true;
            ResolveTypes();

            try
            {
                object hc = _healthController;
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                var t = hc.GetType();

                while (t != null && t != typeof(object))
                {
                    foreach (var f in t.GetFields(flags))
                    {
                        bool isIPB = _ipbType != null && f.FieldType == typeof(Action<>).MakeGenericType(_ipbType);
                        bool isEffect = _ieffectType != null && f.FieldType == typeof(Action<>).MakeGenericType(_ieffectType);

                        if (!isIPB && !isEffect) continue;

                        bool isRemove = f.Name.Contains("1") || f.Name.ToLower().Contains("remove");
                        Type targetType = isIPB ? _ipbType : _ieffectType;

                        var handler = BuildActionDelegate(isRemove, targetType);
                        if (handler == null) continue;

                        var existing = (Delegate)f.GetValue(hc);
                        var combined = Delegate.Combine(existing, handler);
                        f.SetValue(hc, combined);
                    }
                    t = t.BaseType;
                }
            }
            catch { }
        }

        private static Delegate BuildActionDelegate(bool isRemove, Type targetType)
        {
            try
            {
                var actionType = typeof(Action<>).MakeGenericType(targetType);
                var wrapper = new BuffEventWrapper(isRemove);
                var mi = typeof(BuffEventWrapper).GetMethod("Handle");
                return Delegate.CreateDelegate(actionType, wrapper, mi);
            }
            catch { return null; }
        }

        private class BuffEventWrapper
        {
            private readonly bool _isRemove;
            public BuffEventWrapper(bool isRemove) => _isRemove = isRemove;
            public void Handle(object buff)
            {
                if (_isRemove) OnBuffRemoved(buff);
                else OnBuffAdded(buff);
            }
        }

        private static void OnBuffAdded(object buff)
        {
            try
            {
                string key = GetBuffKey(buff);
                if (string.IsNullOrEmpty(key)) return;

                string buffName = GetStringProp(buff, "BuffName");
                string bodyPart = GetStringProp(buff, "BodyPart");

                // 消灭 LINQ Alloc
                _staleKeys.Clear();
                string prefix = $"{buffName}|{bodyPart}|";
                foreach (var k in _capturedBuffs.Keys)
                {
                    if (k.StartsWith(prefix)) _staleKeys.Add(k);
                }
                for (int i = 0; i < _staleKeys.Count; i++) _capturedBuffs.Remove(_staleKeys[i]);

                _capturedBuffs[key] = buff;
                int bid = buff.GetHashCode();
                _buffWholeTimeOffset[bid] = GetFloatProp(buff, "WholeTime");
            }
            catch { }
        }

        private static void OnBuffRemoved(object buff)
        {
            try
            {
                string key = GetBuffKey(buff);
                if (!string.IsNullOrEmpty(key) && _capturedBuffs.ContainsKey(key))
                {
                    if (!GetBoolProp(buff, "Active", false))
                        _capturedBuffs.Remove(key);
                }
            }
            catch { }
        }

        private static string GetBuffKey(object buff)
        {
            try
            {
                // 先尝试拿它正儿八经的名字 (IPlayerBuff 都有)
                string name = GetStringProp(buff, "BuffName");

                // 【白名单核心】：如果没有名字，说明它是 IEffect 底层机制
                if (string.IsNullOrEmpty(name))
                {
                    string typeName = buff.GetType().Name;
                    // 只有类名包含 PainKiller 的，才允许它伪装成 "PainKiller" 混进来
                    if (typeName.IndexOf("PainKiller", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        name = "PainKiller";
                    }
                    else
                    {
                        // 其它所有的 Existence, Stimulator, Med(1s) 全部在这里被无情抛弃！
                        return null;
                    }
                }

                string bodyPart = GetStringProp(buff, "BodyPart") ?? "Global";
                float value = GetFloatProp(buff, "Value");
                int hash = buff.GetHashCode();
                return $"{name}|{bodyPart}|{(value >= 0 ? "+" : "-")}|{hash}";
            }
            catch { return null; }
        }

        // ---------- 深度扫描 ----------
        private static void DeepScanHealthController()
        {
            if (_deepScanDone) return;
            _deepScanDone = true;
            ResolveTypes();
            _scanVisited.Clear();
            DeepScan(_healthController, 0, 5, _scanVisited, "HC");
            ScanEffectsForBuffs(_scanVisited);
        }

        private static void QuickRescan()
        {
            try
            {
                _scanVisited.Clear();
                var hcType = _healthController.GetType();
                var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

                var t = hcType;
                while (t != null && t != typeof(object))
                {
                    var list0 = t.GetField("List_0", flags);
                    if (list0?.GetValue(_healthController) is IList l0)
                        foreach (var c in l0) if (c != null) DeepScan(c, 0, 3, _scanVisited, "QS.L0");

                    var dict1 = t.GetField("Dictionary_1", flags);
                    if (dict1?.GetValue(_healthController) is IDictionary d1)
                        foreach (DictionaryEntry de in d1) if (de.Value != null) DeepScan(de.Value, 0, 3, _scanVisited, "QS.D1");

                    t = t.BaseType;
                }
                RefreshContainerMappings();
            }
            catch { }
        }

        private static void RefreshContainerMappings()
        {
            foreach (var container in _containers)
            {
                if (container == null) continue;
                var buffsField = GetFieldCached(container.GetType(), "Buffs");
                if (buffsField?.GetValue(container) is IList buffsList)
                {
                    foreach (var buff in buffsList)
                        if (buff != null)
                            _buffToContainer[buff.GetHashCode()] = container;
                }
            }
        }

        private static int DeepScan(object obj, int depth, int maxDepth, HashSet<int> visited, string path)
        {
            if (obj == null || depth > maxDepth) return 0;
            int id = obj.GetHashCode();
            if (visited.Contains(id)) return 0;
            visited.Add(id);

            var type = obj.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return 0;
            if (typeof(Delegate).IsAssignableFrom(type)) return 0;
            if (type.Namespace?.StartsWith("UnityEngine") == true) return 0;

            int found = 0;
            if (IsBuffOrEffect(obj))
            {
                found += CaptureBuffFromScan(obj, path);
                return found;
            }

            found += TryReadBuffContainer(obj, path);

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (var f in type.GetFields(flags))
            {
                if (f.IsStatic) continue;
                if (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)) continue;
                var val = f.GetValue(obj);
                if (val == null) continue;

                if (val is IList list)
                {
                    for (int i = 0; i < Math.Min(list.Count, 300); i++)
                        found += DeepScan(list[i], depth + 1, maxDepth, visited, $"{path}.{f.Name}[{i}]");
                }
                else if (val is IDictionary dict)
                {
                    foreach (DictionaryEntry de in dict)
                        found += DeepScan(de.Value, depth + 1, maxDepth, visited, $"{path}.{f.Name}[D]");
                }
                else
                {
                    found += DeepScan(val, depth + 1, maxDepth, visited, $"{path}.{f.Name}");
                }
            }
            return found;
        }

        private static int TryReadBuffContainer(object obj, string path)
        {
            var buffsField = GetFieldCached(obj.GetType(), "Buffs");
            if (buffsField?.GetValue(obj) is IList buffsList && buffsList.Count > 0)
            {
                int cid = obj.GetHashCode();
                if (_containerIds.Add(cid)) _containers.Add(obj);

                foreach (var buff in buffsList)
                    if (buff != null)
                        _buffToContainer[buff.GetHashCode()] = obj;

                return CaptureBuffFromScan(buffsList[0], path);
            }
            return 0;
        }

        private static int CaptureBuffFromScan(object buff, string path)
        {
            if (!IsBuffOrEffect(buff)) return 0;
            string key = GetBuffKey(buff);
            if (string.IsNullOrEmpty(key) || _capturedBuffs.ContainsKey(key)) return 0;
            _capturedBuffs[key] = buff;
            return 1;
        }

        private static void ScanEffectsForBuffs(HashSet<int> visited)
        {
            var method = _healthController.GetType().GetMethod("GetAllEffects", Type.EmptyTypes);
            if (method?.Invoke(_healthController, null) is IEnumerable effects)
            {
                foreach (var fx in effects)
                    if (fx != null)
                        DeepScan(fx, 0, 3, visited, "S4");
            }
        }

        private static void ResolveTypes()
        {
            if (_ipbSearched) return;
            _ipbSearched = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.FullName.Contains("Assembly-CSharp")) continue;
                foreach (var t in asm.GetTypes())
                {
                    if (t.IsInterface)
                    {
                        if (t.Name == "IPlayerBuff") _ipbType = t;
                        if (t.Name == "IEffect") _ieffectType = t;
                    }
                    if (_ipbType != null && _ieffectType != null) return;
                }
            }
        }

        private static bool IsBuffOrEffect(object obj)
        {
            if (obj == null) return false;
            if (_ipbType != null && _ipbType.IsInstanceOfType(obj)) return true;
            if (_ieffectType != null && _ieffectType.IsInstanceOfType(obj)) return true;

            var type = obj.GetType();
            return GetPropertyCached(type, "BuffName") != null ||
                   GetPropertyCached(type, "RemainingTime") != null; // IEffect 的特征
        }

        // ==========================================
        // 【反射极速读取模块】
        // ==========================================
        private static PropertyInfo GetPropertyCached(Type type, string name)
        {
            var key = (type, name);
            if (_propCache.TryGetValue(key, out var prop)) return prop;

            prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _propCache[key] = prop;
            return prop;
        }

        private static FieldInfo GetFieldCached(Type type, string name)
        {
            var key = (type, name);
            if (_fieldCache.TryGetValue(key, out var field)) return field;

            field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _fieldCache[key] = field;
            return field;
        }

        private static float GetFloatProp(object obj, string name)
        {
            try
            {
                var type = obj.GetType();
                var prop = GetPropertyCached(type, name);
                if (prop != null && prop.GetValue(obj) is float f) return f;
                var field = GetFieldCached(type, name);
                if (field != null && field.GetValue(obj) is float f2) return f2;
            }
            catch { }
            return 0f;
        }

        private static bool GetBoolProp(object obj, string name, bool def = false)
        {
            try
            {
                var type = obj.GetType();
                var prop = GetPropertyCached(type, name);
                if (prop != null && prop.GetValue(obj) is bool b) return b;
                var field = GetFieldCached(type, name);
                if (field != null && field.GetValue(obj) is bool b2) return b2;
            }
            catch { }
            return def;
        }

        private static string GetStringProp(object obj, string name)
        {
            try
            {
                var type = obj.GetType();
                var prop = GetPropertyCached(type, name);
                if (prop != null) return prop.GetValue(obj)?.ToString();
                var field = GetFieldCached(type, name);
                if (field != null) return field.GetValue(obj)?.ToString();
            }
            catch { }
            return null;
        }

        private static string GetBuffEffectId(object buff)
        {
            string name = GetStringProp(buff, "BuffName");
            return StripColorTags(name)?.Replace(" ", "");
        }

        // ---------- 字符串处理 ----------
        private static string StripColorTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = _colorRegex.Replace(s, "");
            s = s.Replace("</color>", "");
            return s.Trim();
        }

        private static string StripValueFromName(string name)
        {
            int idx = name.LastIndexOf(" (");
            if (idx > 0 && name.EndsWith(")"))
            {
                string inner = name.Substring(idx + 2, name.Length - idx - 3);
                if (float.TryParse(inner, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
                    return name.Substring(0, idx);
            }
            return name;
        }

        private static float SanitizeTimer(float val) =>
            float.IsNaN(val) || float.IsInfinity(val) || val < -1f || val > 100000f ? -1f : val;
    }
}