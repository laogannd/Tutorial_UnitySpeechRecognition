using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Recognissimo;
using Recognissimo.Components;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace VRTraining.SpeechRecognition
{
    // 关键词识别器：包装 Recognissimo SpeechRecognizer，提供模糊匹配 + 拼音匹配 + 健全事件回调
    // 用法：场景中放置 SpeechRecognizer + LanguageModelProvider + SpeechSource，
    //      再添加本组件并引用 SpeechRecognizer，即可在 Inspector 配置关键词与回调
    [AddComponentMenu("VRTraining/Speech/Fuzzy Keyword Recognizer")]
#if ODIN_INSPECTOR
    [HideMonoScript]
#endif
    public class FuzzyKeywordRecognizer : MonoBehaviour
    {
        // ========== Inspector 字段 ==========
        // Odin 装饰统一写在字段上方一行，避免上下文切换造成的视觉混乱
        // _keywords 强制走 Unity 原生绘制，保证嵌套 UnityEvent 的 PersistentCalls 路径正确

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "基础"), Required("必须引用 Recognissimo 的 SpeechRecognizer"), LabelText("语音识别器")]
#endif
        private SpeechRecognizer _speechRecognizer;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "基础"), LabelText("自动启动"), Tooltip("OnEnable 时自动 StartProcessing")]
#endif
        private bool _autoStart = true;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "基础"), LabelText("使用部分结果"), Tooltip("对 PartialResult 也尝试匹配，响应更快但可能误触发")]
#endif
        private bool _matchPartialResults = true;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "基础"), LabelText("命中冷却(秒)"), PropertyRange(0f, 5f),
         Tooltip("同一关键词在该时间内不会重复触发，0 表示无冷却")]
#endif
        private float _matchCooldown = 0.6f;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "基础"), LabelText("自动同步词汇表"),
         Tooltip("启动时把关键词及别名注入 SpeechRecognizer.Vocabulary，提升识别命中率")]
#endif
        private bool _autoSyncVocabulary = true;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "关键词"), LabelText("关键词列表"),
         DrawWithUnity,
         InfoBox("使用 Unity 原生 ReorderableList 绘制，确保元素内 UnityEvent 的 PersistentCalls 下拉框正常可点")]
#endif
        private List<KeywordEntry> _keywords = new();

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "算法"), LabelText("默认命中阈值"), PropertyRange(0f, 1f),
         Tooltip("综合相似度大于等于该值视为命中；条目可单独覆盖")]
#endif
        private float _defaultThreshold = 0.72f;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "算法"), LabelText("歧义判定差值"), PropertyRange(0f, 0.3f),
         Tooltip("最佳与次佳相似度差小于该值视为歧义，触发 OnAmbiguousMatch")]
#endif
        private float _ambiguityDelta = 0.05f;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "算法"), LabelText("ASR 置信度门槛"), PropertyRange(0f, 1f),
         Tooltip("低于此置信度的识别结果直接忽略，0 表示不限制")]
#endif
        private float _minAsrConfidence = 0.0f;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "算法"), LabelText("权重配置"), InlineProperty,
         Tooltip("多算法加权融合，总和无需为 1，会自动归一化")]
#endif
        private FuzzyMatchWeights _weights = FuzzyMatchWeights.Default;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "算法"), LabelText("拼音字典"), Tooltip("可选；为空时使用内置基础集")]
#endif
        private PinyinDictionary _pinyinDictionary;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "事件"), LabelText("识别已启动"), PropertySpace(SpaceBefore = 4)]
#endif
        private UnityEvent _onRecognitionStarted = new();

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "事件"), LabelText("识别已停止")]
#endif
        private UnityEvent _onRecognitionStopped = new();

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "事件"), LabelText("命中关键词")]
#endif
        private KeywordMatchedEvent _onKeywordMatched = new();

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "事件"), LabelText("命中(部分结果)")]
#endif
        private KeywordMatchedEvent _onPartialMatched = new();

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "事件"), LabelText("命中歧义"),
         Tooltip("多个候选相似度接近时触发，参数为按相似度降序的候选数组")]
#endif
        private AmbiguousMatchEvent _onAmbiguousMatch = new();

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "事件"), LabelText("未命中")]
#endif
        private UnityEvent<string> _onNoMatch = new();

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "事件"), LabelText("识别错误")]
#endif
        private UnityEvent<string> _onError = new();

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "调试"), LabelText("启用调试日志"), PropertySpace(SpaceBefore = 4),
         Tooltip("把识别文本、匹配相似度、命中/歧义/未命中等信息输出到 Console")]
#endif
        private bool _debugLogEnabled = false;

        [SerializeField]
#if ODIN_INSPECTOR
        [TabGroup("Tabs", "调试"), LabelText("打印部分结果"), EnableIf(nameof(_debugLogEnabled)),
         Tooltip("同时输出高频的部分识别文本，关闭可避免刷屏")]
#endif
        private bool _debugLogPartial = false;

#if ODIN_INSPECTOR
        [TabGroup("Tabs", "调试"), ShowInInspector, ReadOnly, LabelText("当前状态")]
        private string DebugState => _speechRecognizer == null ? "未引用" : _speechRecognizer.State.ToString();

        [TabGroup("Tabs", "调试"), ShowInInspector, ReadOnly, LabelText("最近识别文本")]
        private string DebugLastText => _lastText;

        [TabGroup("Tabs", "调试"), ShowInInspector, ReadOnly, LabelText("最近相似度"), PropertyRange(0f, 1f)]
        private float DebugLastSimilarity => _lastSimilarity;

        [TabGroup("Tabs", "调试"), LabelText("测试输入")]
        [SerializeField]
        private string _debugTestInput;

        [TabGroup("Tabs", "调试")]
        [Button("测试匹配", ButtonSizes.Medium), GUIColor(0.6f, 0.9f, 1f)]
        private void DebugTryMatch()
        {
            if (string.IsNullOrEmpty(_debugTestInput))
            {
                Debug.LogWarning("[FuzzyKeywordRecognizer] 测试输入为空");
                return;
            }
            EnsureKeywordsPrepared();
            var result = TryMatch(_debugTestInput, 1f, MatchResultSource.Complete, out var ambiguous);
            if (result.HasValue)
            {
                Debug.Log($"[测试匹配] 命中 '{result.Value.Keyword}' 相似度={result.Value.Similarity:F3} 来源={(result.Value.MatchedByPinyin ? "拼音" : "字面")}");
            }
            else if (ambiguous != null && ambiguous.Length > 1)
            {
                Debug.Log($"[测试匹配] 歧义候选数={ambiguous.Length}，最佳={ambiguous[0].Keyword}({ambiguous[0].Similarity:F3})");
            }
            else
            {
                Debug.Log("[测试匹配] 未命中任何关键词");
            }
        }

        [TabGroup("Tabs", "调试")]
        [Button("启动识别"), EnableIf(nameof(IsRecognizerInactive))]
        private void DebugStartProcessing() => StartRecognition();

        [TabGroup("Tabs", "调试")]
        [Button("停止识别"), DisableIf(nameof(IsRecognizerInactive))]
        private void DebugStopProcessing() => StopRecognition();

        private bool IsRecognizerInactive => _speechRecognizer == null
            || _speechRecognizer.State == SpeechProcessorState.Inactive;
#endif

        // ========== 公共接口 ==========

        // 关键词列表的只读视图
        public IReadOnlyList<KeywordEntry> Keywords => _keywords;

        // 识别启动事件
        public UnityEvent OnRecognitionStarted => _onRecognitionStarted;

        // 识别停止事件
        public UnityEvent OnRecognitionStopped => _onRecognitionStopped;

        // 命中事件（来自完整结果）
        public UnityEvent<KeywordMatchResult> OnKeywordMatched => _onKeywordMatched;

        // 命中事件（来自部分结果）
        public UnityEvent<KeywordMatchResult> OnPartialMatched => _onPartialMatched;

        // 歧义事件
        public AmbiguousMatchEvent OnAmbiguousMatch => _onAmbiguousMatch;

        // 未命中事件
        public UnityEvent<string> OnNoMatch => _onNoMatch;

        // 错误事件
        public UnityEvent<string> OnError => _onError;

        // ========== 运行时状态 ==========

        // 关键词上次命中时间（用于冷却）
        private float[] _keywordLastMatchTime;

        // 已准备完毕标记
        private bool _keywordsPrepared;

        // 调试用：最近识别文本
        private string _lastText = string.Empty;

        // 调试用：最近相似度
        private float _lastSimilarity;

        // 复用候选数组，避免每帧分配
        private readonly List<KeywordMatchResult> _matchBuffer = new(8);

        // 字符串构建器（避免 GC，仅主线程使用）
        private static readonly System.Text.StringBuilder _sb = new(64);

        // ========== Unity 生命周期 ==========

        private void OnEnable()
        {
            if (_speechRecognizer == null)
            {
                _onError.Invoke("SpeechRecognizer 未引用");
                return;
            }

            _speechRecognizer.PartialResultReady.AddListener(HandlePartialResult);
            _speechRecognizer.ResultReady.AddListener(HandleResult);
            _speechRecognizer.Started.AddListener(HandleStarted);
            _speechRecognizer.Finished.AddListener(HandleFinished);
            _speechRecognizer.InitializationFailed.AddListener(HandleInitFailed);
            _speechRecognizer.RuntimeFailed.AddListener(HandleRuntimeFailed);

            if (_autoStart)
            {
                StartRecognition();
            }
        }

        private void OnDisable()
        {
            if (_speechRecognizer == null) return;

            // 仅移除事件监听，识别器启停由用户显式控制 / Recognissimo 自身的生命周期托管
            // 避免与 SpeechProcessor.OnDisable 的 Stop(Hard) 冲突
            _speechRecognizer.PartialResultReady.RemoveListener(HandlePartialResult);
            _speechRecognizer.ResultReady.RemoveListener(HandleResult);
            _speechRecognizer.Started.RemoveListener(HandleStarted);
            _speechRecognizer.Finished.RemoveListener(HandleFinished);
            _speechRecognizer.InitializationFailed.RemoveListener(HandleInitFailed);
            _speechRecognizer.RuntimeFailed.RemoveListener(HandleRuntimeFailed);
        }

        // 启动识别
        public void StartRecognition()
        {
            if (_speechRecognizer == null)
            {
                _onError.Invoke("SpeechRecognizer 未引用");
                return;
            }

            EnsureKeywordsPrepared();

            if (_autoSyncVocabulary)
            {
                SyncVocabularyToRecognizer();
            }

            if (_speechRecognizer.State == SpeechProcessorState.Inactive)
            {
                _speechRecognizer.StartProcessing();
            }
        }

        // 停止识别
        public void StopRecognition()
        {
            if (_speechRecognizer == null) return;
            if (_speechRecognizer.State != SpeechProcessorState.Inactive)
            {
                _speechRecognizer.StopProcessing();
            }
        }

        // 运行时新增关键词
        public void AddKeyword(KeywordEntry entry)
        {
            _keywords.Add(entry);
            _keywordsPrepared = false;
        }

        // 运行时清空关键词
        public void ClearKeywords()
        {
            _keywords.Clear();
            _keywordsPrepared = false;
        }

        // ========== Recognissimo 事件桥 ==========

        private void HandleStarted() => _onRecognitionStarted.Invoke();

        private void HandleFinished() => _onRecognitionStopped.Invoke();

        private void HandleInitFailed(InitializationException e)
        {
            if (_debugLogEnabled) Debug.LogError($"[FuzzyKeyword] 初始化失败: {e.Message}", this);
            _onError.Invoke($"初始化失败: {e.Message}");
        }

        private void HandleRuntimeFailed(RuntimeException e)
        {
            if (_debugLogEnabled) Debug.LogError($"[FuzzyKeyword] 运行时错误: {e.Message}", this);
            _onError.Invoke($"运行时错误: {e.Message}");
        }

        private void HandlePartialResult(PartialResult partial)
        {
            if (!_matchPartialResults) return;
            if (string.IsNullOrEmpty(partial.partial)) return;

            EnsureKeywordsPrepared();
            _lastText = partial.partial;

            if (_debugLogEnabled && _debugLogPartial)
            {
                Debug.Log($"[FuzzyKeyword][Partial] 识别中: \"{partial.partial}\"", this);
            }

            var result = TryMatch(partial.partial, 1f, MatchResultSource.Partial, out _);
            if (result.HasValue)
            {
                if (_debugLogEnabled)
                {
                    var r = result.Value;
                    Debug.Log($"[FuzzyKeyword][Partial][命中] '{r.Keyword}' ← \"{partial.partial}\" 短语=\"{r.MatchedPhrase}\" 相似度={r.Similarity:F3} 通道={(r.MatchedByPinyin ? "拼音" : "字面")}", this);
                }
                _onPartialMatched.Invoke(result.Value);
            }
        }

        private void HandleResult(Result result)
        {
            if (string.IsNullOrEmpty(result.text)) return;

            EnsureKeywordsPrepared();
            _lastText = result.text;

            var asrConf = ComputeAverageConfidence(result.result);

            if (_debugLogEnabled)
            {
                Debug.Log($"[FuzzyKeyword][Final] 识别文本: \"{result.text}\" ASR置信度={asrConf:F3}", this);
            }

            if (_minAsrConfidence > 0f && asrConf > 0f && asrConf < _minAsrConfidence)
            {
                if (_debugLogEnabled)
                {
                    Debug.Log($"[FuzzyKeyword][Final] 低于 ASR 置信度门槛({_minAsrConfidence:F2})，已忽略", this);
                }
                return;
            }

            var match = TryMatch(result.text, asrConf, MatchResultSource.Complete, out var ambiguous);
            if (match.HasValue)
            {
                var entry = _keywords[match.Value.KeywordIndex];
                if (_debugLogEnabled)
                {
                    var r = match.Value;
                    Debug.Log($"[FuzzyKeyword][Final][命中] '{r.Keyword}' ← \"{result.text}\" 短语=\"{r.MatchedPhrase}\" 相似度={r.Similarity:F3} 通道={(r.MatchedByPinyin ? "拼音" : "字面")}", this);
                }
                _onKeywordMatched.Invoke(match.Value);
                entry.OnMatched?.Invoke(match.Value);
            }
            else if (ambiguous != null && ambiguous.Length > 1)
            {
                if (_debugLogEnabled)
                {
                    var sb = _sb;
                    sb.Length = 0;
                    sb.Append("[FuzzyKeyword][Final][歧义] \"").Append(result.text).Append("\" 候选: ");
                    for (var i = 0; i < ambiguous.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(ambiguous[i].Keyword).Append('(').Append(ambiguous[i].Similarity.ToString("F3")).Append(')');
                    }
                    Debug.LogWarning(sb.ToString(), this);
                }
                _onAmbiguousMatch.Invoke(ambiguous);
            }
            else
            {
                if (_debugLogEnabled)
                {
                    Debug.Log($"[FuzzyKeyword][Final][未命中] \"{result.text}\" 最近相似度={_lastSimilarity:F3}", this);
                }
                _onNoMatch.Invoke(result.text);
            }
        }

        // ========== 匹配核心 ==========

        // 返回最佳命中（如果阈值通过）；ambiguous 输出按相似度降序的候选数组（仅用于歧义场景）
        private KeywordMatchResult? TryMatch(string text, float asrConfidence,
            MatchResultSource source, out KeywordMatchResult[] ambiguous)
        {
            ambiguous = null;
            _matchBuffer.Clear();

            var lowered = text.ToLowerInvariant();
            var loweredPinyin = ConvertToPinyin(lowered);

            for (var i = 0; i < _keywords.Count; i++)
            {
                var entry = _keywords[i];
                if (!entry.Enabled) continue;
                if (entry.AllPhrases == null || entry.AllPhrases.Length == 0) continue;

                var threshold = entry.UseCustomThreshold ? entry.CustomThreshold : _defaultThreshold;

                // 字面通道：取所有候选短语的最高分
                var bestSim = 0f;
                var bestPhrase = entry.AllPhrases[0];
                var bestByPinyin = false;

                for (var k = 0; k < entry.AllPhrases.Length; k++)
                {
                    var sim = FuzzyMatchAlgorithm.BestWindowSimilarity(lowered, entry.AllPhrases[k], _weights);
                    if (sim > bestSim)
                    {
                        bestSim = sim;
                        bestPhrase = entry.AllPhrases[k];
                        bestByPinyin = false;
                    }
                }

                // 拼音通道
                if (entry.UsePinyinMatch && !string.IsNullOrEmpty(loweredPinyin)
                    && entry.AllPhrasesPinyin != null)
                {
                    for (var k = 0; k < entry.AllPhrasesPinyin.Length; k++)
                    {
                        var py = entry.AllPhrasesPinyin[k];
                        if (string.IsNullOrEmpty(py)) continue;
                        var sim = FuzzyMatchAlgorithm.BestWindowSimilarity(loweredPinyin, py, _weights);
                        if (sim > bestSim)
                        {
                            bestSim = sim;
                            bestPhrase = entry.AllPhrases[k];
                            bestByPinyin = true;
                        }
                    }
                }

                if (bestSim < threshold) continue;

                // 冷却检查
                if (_matchCooldown > 0f)
                {
                    var last = _keywordLastMatchTime[i];
                    if (last > 0f && Time.time - last < _matchCooldown) continue;
                }

                _matchBuffer.Add(new KeywordMatchResult
                {
                    Keyword = entry.Keyword,
                    MatchedPhrase = bestPhrase,
                    RecognizedText = text,
                    Similarity = bestSim,
                    AsrConfidence = asrConfidence,
                    MatchedByPinyin = bestByPinyin,
                    Source = source,
                    KeywordIndex = i,
                    Timestamp = Time.time
                });
            }

            if (_matchBuffer.Count == 0) return null;

            // 按相似度降序
            _matchBuffer.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));

            var best = _matchBuffer[0];
            _lastSimilarity = best.Similarity;

            // 歧义判定（仅完整结果走歧义事件，部分结果忽略歧义）
            if (source == MatchResultSource.Complete && _matchBuffer.Count >= 2)
            {
                var second = _matchBuffer[1];
                if (best.Similarity - second.Similarity < _ambiguityDelta)
                {
                    ambiguous = _matchBuffer.ToArray();
                    return null;
                }
            }

            // 仅完整结果记录冷却时间，避免部分结果反复重置
            if (source == MatchResultSource.Complete)
            {
                _keywordLastMatchTime[best.KeywordIndex] = Time.time;
            }

            return best;
        }

        // ========== 工具 ==========

        private void EnsureKeywordsPrepared()
        {
            if (_keywordsPrepared) return;

            _keywordLastMatchTime = new float[_keywords.Count];

            for (var i = 0; i < _keywords.Count; i++)
            {
                var e = _keywords[i];
                if (string.IsNullOrEmpty(e.Keyword))
                {
                    e.AllPhrases = System.Array.Empty<string>();
                    e.AllPhrasesPinyin = System.Array.Empty<string>();
                    continue;
                }

                var aliasCount = e.Aliases == null ? 0 : e.Aliases.Count;
                var phrases = new string[1 + aliasCount];
                phrases[0] = e.Keyword.ToLowerInvariant();
                for (var k = 0; k < aliasCount; k++)
                {
                    var a = e.Aliases[k];
                    phrases[1 + k] = string.IsNullOrEmpty(a) ? string.Empty : a.ToLowerInvariant();
                }
                e.AllPhrases = phrases;

                if (e.UsePinyinMatch)
                {
                    var pyArr = new string[phrases.Length];
                    for (var k = 0; k < phrases.Length; k++)
                    {
                        pyArr[k] = ConvertToPinyin(phrases[k]);
                    }
                    e.AllPhrasesPinyin = pyArr;
                }
            }

            _keywordsPrepared = true;
        }

        private string ConvertToPinyin(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // 有外部字典优先用外部
            if (_pinyinDictionary != null)
            {
                return _pinyinDictionary.ToPinyin(text);
            }

            // 无字典时使用内置基础集
            var sb = _sb;
            sb.Length = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch < 128)
                {
                    if (ch >= 'A' && ch <= 'Z') sb.Append((char)(ch + 32));
                    else if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
                    continue;
                }
                var py = PinyinBuiltin.Get(ch);
                if (py != null) sb.Append(py);
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private void SyncVocabularyToRecognizer()
        {
            if (_speechRecognizer == null) return;

            var vocab = new List<string>();
            for (var i = 0; i < _keywords.Count; i++)
            {
                var e = _keywords[i];
                if (!e.Enabled) continue;
                if (!string.IsNullOrEmpty(e.Keyword)) vocab.Add(e.Keyword);
                if (e.Aliases == null) continue;
                for (var k = 0; k < e.Aliases.Count; k++)
                {
                    var a = e.Aliases[k];
                    if (!string.IsNullOrEmpty(a)) vocab.Add(a);
                }
            }

            // 加入未知词标记，允许识别词典外词汇
            vocab.Add("[unk]");
            _speechRecognizer.Vocabulary = vocab;
        }

        // 计算 Word.conf 的算术平均值（无 conf 则返回 0）
        private static float ComputeAverageConfidence(List<Word> words)
        {
            if (words == null || words.Count == 0) return 0f;
            var sum = 0f;
            for (var i = 0; i < words.Count; i++)
            {
                sum += words[i].conf;
            }
            return sum / words.Count;
        }
    }

    // 多候选歧义事件
    [System.Serializable]
    public class AmbiguousMatchEvent : UnityEvent<KeywordMatchResult[]> { }
}
