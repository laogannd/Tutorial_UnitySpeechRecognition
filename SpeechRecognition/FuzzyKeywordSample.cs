using UnityEngine;

namespace VRTraining.SpeechRecognition.Samples
{
    // 示例：监听急救关键词并打印命中信息
    // 使用方式：
    //   1. 场景中放置 MicrophoneSpeechSource、StreamingAssetsLanguageModelProvider、SpeechRecognizer
    //   2. 添加 FuzzyKeywordRecognizer，引用 SpeechRecognizer 并配置关键词
    //   3. 添加本组件，引用上方 FuzzyKeywordRecognizer
    [AddComponentMenu("VRTraining/Speech/Sample/Fuzzy Keyword Sample")]
    public class FuzzyKeywordSample : MonoBehaviour
    {
        [SerializeField]
        private FuzzyKeywordRecognizer _recognizer;

        private void OnEnable()
        {
            if (_recognizer == null) return;
            _recognizer.OnRecognitionStarted.AddListener(OnStarted);
            _recognizer.OnRecognitionStopped.AddListener(OnStopped);
            _recognizer.OnKeywordMatched.AddListener(OnMatched);
            _recognizer.OnPartialMatched.AddListener(OnPartialMatched);
            _recognizer.OnAmbiguousMatch.AddListener(OnAmbiguous);
            _recognizer.OnNoMatch.AddListener(OnNoMatch);
            _recognizer.OnError.AddListener(OnError);
        }

        private void OnDisable()
        {
            if (_recognizer == null) return;
            _recognizer.OnRecognitionStarted.RemoveListener(OnStarted);
            _recognizer.OnRecognitionStopped.RemoveListener(OnStopped);
            _recognizer.OnKeywordMatched.RemoveListener(OnMatched);
            _recognizer.OnPartialMatched.RemoveListener(OnPartialMatched);
            _recognizer.OnAmbiguousMatch.RemoveListener(OnAmbiguous);
            _recognizer.OnNoMatch.RemoveListener(OnNoMatch);
            _recognizer.OnError.RemoveListener(OnError);
        }

        private void OnStarted() => Debug.Log("[Sample] 识别已启动");

        private void OnStopped() => Debug.Log("[Sample] 识别已停止");

        private void OnMatched(KeywordMatchResult r)
            => Debug.Log($"[Sample] 命中 '{r.Keyword}' 相似度={r.Similarity:F3} 拼音={r.MatchedByPinyin} 文本='{r.RecognizedText}'");

        private void OnPartialMatched(KeywordMatchResult r)
            => Debug.Log($"[Sample] 部分命中 '{r.Keyword}' ({r.Similarity:F3})");

        private void OnAmbiguous(KeywordMatchResult[] candidates)
        {
            var sb = new System.Text.StringBuilder(64);
            sb.Append("[Sample] 歧义候选: ");
            for (var i = 0; i < candidates.Length; i++)
            {
                sb.Append(candidates[i].Keyword);
                sb.Append('(');
                sb.Append(candidates[i].Similarity.ToString("F3"));
                sb.Append(") ");
            }
            Debug.LogWarning(sb.ToString());
        }

        private void OnNoMatch(string text) => Debug.Log($"[Sample] 未命中: {text}");

        private void OnError(string msg) => Debug.LogError($"[Sample] 错误: {msg}");
    }
}
