using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRTraining.SpeechRecognition
{
    // 单个关键词配置条目
    // 注意：本类故意不挂任何 Odin 属性
    // 原因：FuzzyKeywordRecognizer 的 _keywords 字段使用 [DrawWithUnity]
    // 让 Unity 原生 ReorderableList 接管列表绘制，从而让内嵌的
    // UnityEvent<KeywordMatchResult> OnMatched 走标准 UnityEventDrawer
    // 否则 Odin 的 List 重排会破坏 SerializedProperty 路径，
    // 导致 PersistentCalls 的 Object/Method 下拉点不开
    // 美化由同目录 Editor/KeywordEntryDrawer.cs 完成
    [Serializable]
    public class KeywordEntry
    {
        [Tooltip("待识别的主关键词（中文或英文）")]
        public string Keyword;

        [Tooltip("是否启用本条关键词")]
        public bool Enabled = true;

        [Tooltip("等价表达，命中任一即触发")]
        public List<string> Aliases = new();

        [Tooltip("勾选后用本条目阈值覆盖全局阈值")]
        public bool UseCustomThreshold;

        [Range(0f, 1f)]
        [Tooltip("命中阈值 0.0~1.0，越高越严格")]
        public float CustomThreshold = 0.75f;

        [Tooltip("将关键词与识别文本均转为拼音再做模糊匹配")]
        public bool UsePinyinMatch = true;

        [Tooltip("匹配命中时触发，参数为命中详情")]
        public KeywordMatchedEvent OnMatched = new();

        // 运行时缓存：所有候选短语（主词+同义词），由 Recognizer 在启动时填充
        [NonSerialized]
        public string[] AllPhrases;

        // 运行时缓存：候选短语对应的拼音串
        [NonSerialized]
        public string[] AllPhrasesPinyin;
    }

    // KeywordMatchResult 命中事件
    // 必须用具名派生类才能让 Unity 序列化系统在嵌套 List 中正确路由 PersistentCalls
    [Serializable]
    public class KeywordMatchedEvent : UnityEvent<KeywordMatchResult> { }
}
