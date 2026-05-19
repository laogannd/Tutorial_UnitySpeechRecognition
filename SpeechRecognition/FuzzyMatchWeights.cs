using System;

namespace VRTraining.SpeechRecognition
{
    // 模糊匹配权重配置（多算法加权融合）
    [Serializable]
    public struct FuzzyMatchWeights
    {
        // Levenshtein 归一化相似度权重
        public float Levenshtein;

        // Jaro-Winkler 权重（对相同前缀有利）
        public float JaroWinkler;

        // N-Gram 权重（抗词序）
        public float NGram;

        // 子串包含权重（长句含关键词）
        public float Containment;

        // 默认权重，总和约 1.0
        public static FuzzyMatchWeights Default => new()
        {
            Levenshtein = 0.30f,
            JaroWinkler = 0.30f,
            NGram = 0.25f,
            Containment = 0.15f
        };

        // 总权重，用于归一化
        public float Sum => Levenshtein + JaroWinkler + NGram + Containment;
    }
}
