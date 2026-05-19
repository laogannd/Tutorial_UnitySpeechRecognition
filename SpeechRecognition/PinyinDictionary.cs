using System.Collections.Generic;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace VRTraining.SpeechRecognition
{
    // 拼音字典：以 ScriptableObject 提供可扩展映射，缺省字符回退至内置基础集
    [CreateAssetMenu(menuName = "VRTraining/Speech/Pinyin Dictionary", fileName = "pinyin_dictionary")]
    public class PinyinDictionary : ScriptableObject
    {
#if ODIN_INSPECTOR
        [InfoBox("用于将识别文本与关键词转换为无声调拼音串后做模糊匹配。\n" +
                 "若汉字不在本字典也不在内置基础集中，将原样保留。")]
        [BoxGroup("自定义映射")]
        [LabelText("汉字-拼音条目"), TableList(ShowIndexLabels = true, AlwaysExpanded = true)]
#endif
        public List<PinyinEntry> Entries = new();

        // 拼音是否带声调（当前实现统一无声调）
        [SerializeField, HideInInspector]
        private bool _withTone;

        private Dictionary<char, string> _runtimeMap;

        // 获取单字拼音；找不到返回 null（调用方决定回退策略）
        public string GetPinyin(char ch)
        {
            EnsureMap();
            return _runtimeMap.TryGetValue(ch, out var py) ? py : null;
        }

        // 整串汉字转拼音字符串（无空格，便于 N-Gram/Levenshtein）
        public string ToPinyin(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            EnsureMap();

            var sb = _stringBuilder;
            sb.Length = 0;

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];

                // ASCII 字母数字直接小写保留
                if (ch < 128)
                {
                    if (ch >= 'A' && ch <= 'Z')
                    {
                        sb.Append((char)(ch + 32));
                    }
                    else if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                    {
                        sb.Append(ch);
                    }
                    // 其它 ASCII 字符（标点空格）跳过
                    continue;
                }

                // 自定义字典优先
                if (_runtimeMap.TryGetValue(ch, out var py))
                {
                    sb.Append(py);
                    continue;
                }

                // 内置基础集回退
                var builtin = PinyinBuiltin.Get(ch);
                if (builtin != null)
                {
                    sb.Append(builtin);
                    continue;
                }

                // 未知字符原样保留
                sb.Append(ch);
            }

            return sb.ToString();
        }

        // 字符串构建器（避免 GC，仅主线程使用）
        private static readonly System.Text.StringBuilder _stringBuilder = new(64);

        private void EnsureMap()
        {
            if (_runtimeMap != null && _runtimeMap.Count == Entries.Count) return;
            _runtimeMap = new Dictionary<char, string>(Entries.Count);
            for (var i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (string.IsNullOrEmpty(e.Pinyin) || e.Hanzi == '\0') continue;
                _runtimeMap[e.Hanzi] = e.Pinyin.ToLowerInvariant();
            }
        }

        // 强制刷新缓存（编辑器 OnValidate 触发）
        private void OnValidate()
        {
            _runtimeMap = null;
        }
    }

    // 单字-拼音映射条目
    [System.Serializable]
    public struct PinyinEntry
    {
#if ODIN_INSPECTOR
        [TableColumnWidth(60, Resizable = false)]
#endif
        public char Hanzi;

        public string Pinyin;
    }
}
