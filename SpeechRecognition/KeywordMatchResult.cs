using System;

namespace VRTraining.SpeechRecognition
{
    // 关键词匹配结果
    [Serializable]
    public struct KeywordMatchResult
    {
        // 命中的关键词主词
        public string Keyword;

        // 实际命中的候选短语（主词或某个同义词）
        public string MatchedPhrase;

        // 用户原始识别文本
        public string RecognizedText;

        // 综合相似度 0~1
        public float Similarity;

        // ASR 引擎置信度 0~1（取自 Recognissimo Word.conf 平均）
        public float AsrConfidence;

        // 是否走的拼音通道
        public bool MatchedByPinyin;

        // 该匹配的来源：完整结果或部分结果
        public MatchResultSource Source;

        // 关键词在原数组中的索引，便于外部回调路由
        public int KeywordIndex;

        // 命中发生的 Unity 时间戳（秒，Time.time）
        public float Timestamp;
    }

    // 匹配来源
    public enum MatchResultSource
    {
        // 完整识别结果（ResultReady）
        Complete = 0,

        // 部分识别结果（PartialResultReady）
        Partial = 1
    }
}
