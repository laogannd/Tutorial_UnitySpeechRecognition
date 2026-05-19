using System;

namespace VRTraining.SpeechRecognition
{
    // 模糊匹配算法库（静态、纯函数、零状态）
    public static class FuzzyMatchAlgorithm
    {
        // 多算法加权融合：返回 0~1 综合相似度
        public static float Combined(string a, string b, FuzzyMatchWeights weights)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return 0f;
            }

            // 完全相等直接返回 1
            if (a.Length == b.Length && string.CompareOrdinal(a, b) == 0)
            {
                return 1f;
            }

            var sum = weights.Sum;
            if (sum <= 0f)
            {
                return 0f;
            }

            var lev = LevenshteinSimilarity(a, b) * weights.Levenshtein;
            var jw = JaroWinkler(a, b) * weights.JaroWinkler;
            var ng = NGramSimilarity(a, b, 2) * weights.NGram;
            var cn = ContainmentSimilarity(a, b) * weights.Containment;

            return (lev + jw + ng + cn) / sum;
        }

        // 滑动窗口最佳相似度：在 text 中按 phrase 长度附近的窗口滑动，逐窗 Combined 取最大
        // 用于解决"长句包含短关键词"时 Levenshtein/JaroWinkler/NGram 被句长稀释的问题
        // 同时保留整段 Combined 作为兜底，避免短文本时反而下降
        public static float BestWindowSimilarity(string text, string phrase, FuzzyMatchWeights weights)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(phrase))
            {
                return 0f;
            }

            // 文本不长于关键词，直接整体比较
            if (text.Length <= phrase.Length)
            {
                return Combined(text, phrase, weights);
            }

            // 整段相似度作为兜底（覆盖颠倒/省略等场景）
            var best = Combined(text, phrase, weights);
            if (best >= 1f) return 1f;

            // 三档窗口宽度：phrase.Length-1、phrase.Length、phrase.Length+1
            // 兼顾 ASR 多识别/漏识别一个字的情况
            var phraseLen = phrase.Length;
            var minWin = phraseLen > 1 ? phraseLen - 1 : phraseLen;
            var maxWin = phraseLen + 1;
            if (maxWin > text.Length) maxWin = text.Length;

            for (var win = minWin; win <= maxWin; win++)
            {
                var last = text.Length - win;
                for (var start = 0; start <= last; start++)
                {
                    var slice = text.Substring(start, win);
                    var sim = Combined(slice, phrase, weights);
                    if (sim > best)
                    {
                        best = sim;
                        if (best >= 1f) return 1f;
                    }
                }
            }

            return best;
        }

        // Levenshtein 编辑距离归一化为相似度
        public static float LevenshteinSimilarity(string a, string b)
        {
            var maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0)
            {
                return 1f;
            }
            var distance = LevenshteinDistance(a, b);
            return 1f - (float)distance / maxLen;
        }

        // 经典 Levenshtein 距离（双行滚动数组，避免分配二维数组）
        public static int LevenshteinDistance(string a, string b)
        {
            var lenA = a.Length;
            var lenB = b.Length;

            if (lenA == 0) return lenB;
            if (lenB == 0) return lenA;

            // 复用两行数组
            var prev = new int[lenB + 1];
            var curr = new int[lenB + 1];

            for (var j = 0; j <= lenB; j++)
            {
                prev[j] = j;
            }

            for (var i = 1; i <= lenA; i++)
            {
                curr[0] = i;
                var ca = a[i - 1];
                for (var j = 1; j <= lenB; j++)
                {
                    var cost = ca == b[j - 1] ? 0 : 1;
                    var del = prev[j] + 1;
                    var ins = curr[j - 1] + 1;
                    var sub = prev[j - 1] + cost;
                    var min = del < ins ? del : ins;
                    if (sub < min) min = sub;
                    curr[j] = min;
                }
                // 滚动
                (prev, curr) = (curr, prev);
            }

            return prev[lenB];
        }

        // Jaro-Winkler 相似度（带前缀加权 p=0.1，l<=4）
        public static float JaroWinkler(string a, string b)
        {
            var jaro = Jaro(a, b);
            if (jaro <= 0f)
            {
                return 0f;
            }

            // 公共前缀长度（最多 4）
            var prefix = 0;
            var maxPrefix = Math.Min(4, Math.Min(a.Length, b.Length));
            for (var i = 0; i < maxPrefix; i++)
            {
                if (a[i] == b[i]) prefix++;
                else break;
            }

            return jaro + prefix * 0.1f * (1f - jaro);
        }

        // Jaro 相似度
        public static float Jaro(string a, string b)
        {
            var lenA = a.Length;
            var lenB = b.Length;
            if (lenA == 0 && lenB == 0) return 1f;
            if (lenA == 0 || lenB == 0) return 0f;

            var matchDistance = Math.Max(lenA, lenB) / 2 - 1;
            if (matchDistance < 0) matchDistance = 0;

            var aMatches = new bool[lenA];
            var bMatches = new bool[lenB];
            var matches = 0;

            for (var i = 0; i < lenA; i++)
            {
                var start = Math.Max(0, i - matchDistance);
                var end = Math.Min(i + matchDistance + 1, lenB);
                for (var j = start; j < end; j++)
                {
                    if (bMatches[j]) continue;
                    if (a[i] != b[j]) continue;
                    aMatches[i] = true;
                    bMatches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0) return 0f;

            // 计算换位
            var transpositions = 0;
            var k = 0;
            for (var i = 0; i < lenA; i++)
            {
                if (!aMatches[i]) continue;
                while (!bMatches[k]) k++;
                if (a[i] != b[k]) transpositions++;
                k++;
            }
            transpositions /= 2;

            var m = (float)matches;
            return (m / lenA + m / lenB + (m - transpositions) / m) / 3f;
        }

        // N-Gram 相似度（默认 bi-gram，Dice 系数）
        public static float NGramSimilarity(string a, string b, int n)
        {
            if (n < 1) n = 1;
            // 短串退化处理：直接使用字符相等
            if (a.Length < n || b.Length < n)
            {
                return a == b ? 1f : 0f;
            }

            var ga = BuildNGrams(a, n);
            var gb = BuildNGrams(b, n);

            if (ga.Length == 0 || gb.Length == 0)
            {
                return 0f;
            }

            var inter = 0;
            // 用排序+双指针求交集大小，避免 HashSet 装箱
            Array.Sort(ga, StringComparer.Ordinal);
            Array.Sort(gb, StringComparer.Ordinal);
            int i = 0, j = 0;
            while (i < ga.Length && j < gb.Length)
            {
                var cmp = string.CompareOrdinal(ga[i], gb[j]);
                if (cmp == 0)
                {
                    inter++;
                    i++;
                    j++;
                }
                else if (cmp < 0) i++;
                else j++;
            }

            return 2f * inter / (ga.Length + gb.Length);
        }

        // 子串包含相似度：若 short 在 long 中出现，给高分；否则做最长公共子串占比
        public static float ContainmentSimilarity(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0) return 0f;

            var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
            if (longer.IndexOf(shorter, StringComparison.Ordinal) >= 0)
            {
                // 包含命中：基础分 0.85，再按长度比例加成
                var ratio = (float)shorter.Length / longer.Length;
                return 0.85f + 0.15f * ratio;
            }

            // 退化：以 shorter 长度为基的最长公共子串
            var lcs = LongestCommonSubstringLength(a, b);
            return (float)lcs / shorter.Length;
        }

        // 最长公共子串长度（DP）
        public static int LongestCommonSubstringLength(string a, string b)
        {
            var lenA = a.Length;
            var lenB = b.Length;
            if (lenA == 0 || lenB == 0) return 0;

            var prev = new int[lenB + 1];
            var curr = new int[lenB + 1];
            var max = 0;

            for (var i = 1; i <= lenA; i++)
            {
                var ca = a[i - 1];
                for (var j = 1; j <= lenB; j++)
                {
                    if (ca == b[j - 1])
                    {
                        curr[j] = prev[j - 1] + 1;
                        if (curr[j] > max) max = curr[j];
                    }
                    else
                    {
                        curr[j] = 0;
                    }
                }
                (prev, curr) = (curr, prev);
            }

            return max;
        }

        // 构造 n-gram 切片
        private static string[] BuildNGrams(string s, int n)
        {
            var count = s.Length - n + 1;
            if (count <= 0) return Array.Empty<string>();
            var grams = new string[count];
            for (var i = 0; i < count; i++)
            {
                grams[i] = s.Substring(i, n);
            }
            return grams;
        }
    }
}
