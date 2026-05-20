# Fuzzy Keyword Recognizer — 中英文教程 / Bilingual Tutorial

> **模块定位 / Module Purpose**
> 基于 Recognissimo 的 Unity 关键词模糊识别器，专为 VR 急救训练场景设计。
> A Unity fuzzy keyword recognizer built on Recognissimo, designed for VR first-aid training scenarios.

---

## 目录 / Table of Contents

1. [模块概览 / Overview](#overview)
2. [文件结构 / File Structure](#files)
3. [快速开始 / Quick Start](#quickstart)
4. [Inspector 配置 / Inspector Configuration](#inspector)
5. [核心概念 / Core Concepts](#concepts)
6. [事件系统 / Event System](#events)
7. [代码示例 / Code Examples](#code)
8. [调参指南 / Tuning Guide](#tuning)
9. [拼音字典扩展 / Pinyin Dictionary](#pinyin)
10. [性能说明 / Performance](#performance)
11. [调试技巧 / Debugging](#debug)
12. [常见问题 / FAQ](#faq)

---

## 1. 模块概览 / Overview {#overview}

### 中文

本模块在 Recognissimo ASR 引擎之上封装了一层**模糊关键词匹配层**，解决以下问题：

- ASR 输出文本与预设关键词存在轻微偏差（发音相近、多/漏字）
- 中文场景下同音字导致的识别偏差（如"心肺复苏"识别为"新肺复苏"）
- 需要对多个同义词统一路由到同一个游戏逻辑回调

### English

This module adds a **fuzzy keyword matching layer** on top of the Recognissimo ASR engine, solving:

- Slight deviations between ASR output and preset keywords (similar pronunciation, extra/missing characters)
- Homophone confusion in Chinese (e.g., "心肺复苏" recognized as "新肺复苏")
- Routing multiple synonyms to the same game-logic callback

---

## 2. 文件结构 / File Structure {#files}

| 文件 / File | 说明 / Description |
|---|---|
| `FuzzyKeywordRecognizer.cs` | 主组件 / Main MonoBehaviour component |
| `KeywordEntry.cs` | 关键词配置条目 / Keyword config entry |
| `KeywordMatchResult.cs` | 匹配结果数据结构 / Match result struct |
| `FuzzyMatchWeights.cs` | 多算法权重配置 / Multi-algorithm weight config |
| `FuzzyMatchAlgorithm.cs` | 静态算法库 / Static algorithm library |
| `PinyinDictionary.cs` | 可扩展拼音字典 ScriptableObject / Extensible pinyin dictionary |
| `PinyinBuiltin.cs` | 内置拼音兜底集 / Built-in pinyin fallback set |
| `FuzzyKeywordSample.cs` | 使用示例 / Usage sample |
| `Editor/KeywordEntryDrawer.cs` | 自定义 Inspector 绘制 / Custom Inspector drawer |

---

## 3. 快速开始 / Quick Start {#quickstart}

### 中文：场景搭建步骤

**前提条件**：已在项目中安装 Recognissimo，并在 `StreamingAssets` 中放置中文语言模型。

**第一步：搭建 GameObject 层级**

```
[SpeechRoot GameObject]
├── MicrophoneSpeechSource          ← Recognissimo 麦克风输入
├── StreamingAssetsLanguageModelProvider  ← Recognissimo 语言模型加载
├── SpeechRecognizer                ← Recognissimo 核心识别器
└── FuzzyKeywordRecognizer          ← 本模块主组件
```

**第二步：配置 SpeechRecognizer**

- `SpeechSource` → 拖入 `MicrophoneSpeechSource`
- `LanguageModelProvider` → 拖入 `StreamingAssetsLanguageModelProvider`
- `AutoStart` → **不要勾选**（由 `FuzzyKeywordRecognizer` 管控启动时机）

**第三步：配置 FuzzyKeywordRecognizer**

- `语音识别器` → 拖入同场景的 `SpeechRecognizer`
- 在"关键词"页签添加至少一个 `KeywordEntry`
- 在"事件"页签绑定 `OnKeywordMatched` 回调

**第四步：运行测试**

进入 Play Mode，对麦克风说出关键词，Console 中应出现命中日志（需开启调试日志）。

---

### English: Scene Setup Steps

**Prerequisites**: Recognissimo installed, Chinese language model placed in `StreamingAssets`.

**Step 1: Build the GameObject hierarchy**

```
[SpeechRoot GameObject]
├── MicrophoneSpeechSource          ← Recognissimo mic input
├── StreamingAssetsLanguageModelProvider  ← Recognissimo model loader
├── SpeechRecognizer                ← Recognissimo core recognizer
└── FuzzyKeywordRecognizer          ← This module's main component
```

**Step 2: Configure SpeechRecognizer**

- `SpeechSource` → drag in `MicrophoneSpeechSource`
- `LanguageModelProvider` → drag in `StreamingAssetsLanguageModelProvider`
- `AutoStart` → **leave unchecked** (startup is managed by `FuzzyKeywordRecognizer`)

**Step 3: Configure FuzzyKeywordRecognizer**

- `语音识别器 (Speech Recognizer)` → drag in the `SpeechRecognizer` from the same scene
- Add at least one `KeywordEntry` in the "关键词 (Keywords)" tab
- Wire up `OnKeywordMatched` callbacks in the "事件 (Events)" tab

**Step 4: Test in Play Mode**

Enter Play Mode, speak a keyword into the microphone. A hit log should appear in the Console (requires debug logging enabled).

---

## 4. Inspector 配置 / Inspector Configuration {#inspector}

> 安装 Odin Inspector 后，Inspector 会分为五个页签。未安装时功能不变，仅退化为 Unity 默认绘制。
> With Odin Inspector installed, the Inspector is split into five tabs. Without it, all functionality remains — only the visual layout degrades to Unity's default.

### 基础页签 / Basic Tab

| 字段 / Field | 类型 / Type | 说明 / Description |
|---|---|---|
| 语音识别器 | SpeechRecognizer | **必填** Recognissimo 识别器引用 / **Required** Recognissimo recognizer reference |
| 自动启动 | bool | OnEnable 时自动调用 StartProcessing / Auto-call StartProcessing on OnEnable |
| 使用部分结果 | bool | 对 PartialResult 也尝试匹配，响应更快但可能误触发 / Match on PartialResult too — faster response but may false-trigger |
| 命中冷却(秒) | float 0~5 | 同一关键词在该时间内不会重复触发，0 表示无冷却 / Cooldown before the same keyword can fire again; 0 = no cooldown |
| 自动同步词汇表 | bool | 启动时把关键词注入 SpeechRecognizer.Vocabulary，提升命中率 / Inject keywords into SpeechRecognizer.Vocabulary on start |

### 关键词页签 / Keywords Tab

每个 `KeywordEntry` 包含 / Each `KeywordEntry` contains:

| 字段 / Field | 说明 / Description |
|---|---|
| Keyword | 主关键词（中文或英文）/ Primary keyword (Chinese or English) |
| Enabled | 是否启用本条 / Whether this entry is active |
| Aliases | 等价表达列表，命中任一即触发 / Synonym list — any match triggers the entry |
| UseCustomThreshold | 勾选后用本条目阈值覆盖全局阈值 / Override global threshold for this entry |
| CustomThreshold | 本条目专属阈值 0~1 / Per-entry threshold 0~1 |
| UsePinyinMatch | 启用拼音通道 / Enable pinyin matching channel |
| OnMatched | 本条目专属命中回调 / Per-entry match callback |

### 算法页签 / Algorithm Tab

| 字段 / Field | 默认值 / Default | 说明 / Description |
|---|---|---|
| 默认命中阈值 | 0.72 | 综合相似度 ≥ 该值视为命中 / Combined similarity ≥ this value = hit |
| 歧义判定差值 | 0.05 | 最佳与次佳差 < 该值触发歧义事件 / Best vs. 2nd-best gap < this = ambiguous |
| ASR 置信度门槛 | 0.0 | 低于此值的识别结果直接忽略，0 表示不限制 / Ignore results below this ASR confidence; 0 = no limit |
| 权重配置 | 见下方 | Levenshtein / Jaro-Winkler / N-Gram / 子串 四路权重 / Four-channel weights |
| 拼音字典 | null | 可选 ScriptableObject，空时使用内置集 / Optional ScriptableObject; falls back to built-in set |

### 事件页签 / Events Tab

| 事件 / Event | 参数 / Parameter | 触发时机 / When Fired |
|---|---|---|
| OnRecognitionStarted | 无 / none | 识别器成功启动 / Recognizer successfully started |
| OnRecognitionStopped | 无 / none | 识别器停止 / Recognizer stopped |
| OnKeywordMatched | KeywordMatchResult | 完整结果命中关键词 / Keyword hit from final result |
| OnPartialMatched | KeywordMatchResult | 部分结果命中关键词 / Keyword hit from partial result |
| OnAmbiguousMatch | KeywordMatchResult[] | 多候选相似度接近，无法确定 / Multiple candidates too close to distinguish |
| OnNoMatch | string | 识别到文本但未命中任何关键词 / Text recognized but no keyword matched |
| OnError | string | 初始化或运行时错误 / Initialization or runtime error |

---

## 5. 核心概念 / Core Concepts {#concepts}

### 5.1 匹配流水线 / Matching Pipeline

```
麦克风 → Recognissimo ASR → 文本
                                ↓
                    FuzzyKeywordRecognizer
                                ↓
              ┌─────────────────────────────┐
              │  对每个 KeywordEntry:        │
              │  1. 字面通道 (Literal)       │
              │     BestWindowSimilarity     │
              │  2. 拼音通道 (Pinyin)        │
              │     文本→拼音 → BestWindow   │
              │  取两通道最高分              │
              └─────────────────────────────┘
                                ↓
              ┌─────────────────────────────┐
              │  过滤 < threshold 的候选     │
              │  冷却检查                    │
              │  歧义判定                    │
              └─────────────────────────────┘
                                ↓
              命中 / 歧义 / 未命中 → 事件回调
```

### 5.2 四路算法融合 / Four-Algorithm Fusion

`FuzzyMatchAlgorithm.Combined()` 将四种算法加权求和后归一化：

| 算法 / Algorithm | 擅长场景 / Best For | 默认权重 / Default Weight |
|---|---|---|
| **Levenshtein** 编辑距离 | 单字替换、多/漏字 / Single-char substitution, insertions/deletions | 0.30 |
| **Jaro-Winkler** | 相同前缀的短词 / Short words with common prefix | 0.30 |
| **N-Gram** (bi-gram) | 字符顺序颠倒、词序变化 / Character reordering | 0.25 |
| **Containment** 子串包含 | 长句中包含短关键词 / Short keyword inside long sentence | 0.15 |

### 5.3 滑动窗口 / Sliding Window

`BestWindowSimilarity()` 在识别文本中以关键词长度 ±1 的窗口滑动，取最高相似度。这解决了"长句包含短关键词时，整体 Levenshtein 被句长稀释"的问题。

`BestWindowSimilarity()` slides a window of size `phrase.length ± 1` over the recognized text and takes the highest similarity. This prevents long sentences from diluting the score of a short keyword.

### 5.4 拼音通道 / Pinyin Channel

当 `KeywordEntry.UsePinyinMatch = true` 时，识别文本和关键词均转为无声调拼音串后再做模糊匹配。这使得同音字（如"心"和"新"）也能命中。

When `UsePinyinMatch = true`, both the recognized text and the keyword are converted to tone-free pinyin strings before fuzzy matching. This allows homophones (e.g., "心" and "新") to match.

### 5.5 冷却机制 / Cooldown

`_matchCooldown` 防止同一关键词在短时间内被重复触发（例如 ASR 对同一句话产生多个 PartialResult）。冷却时间仅对完整结果记录，部分结果不重置冷却计时器。

`_matchCooldown` prevents the same keyword from firing repeatedly in a short window (e.g., multiple PartialResults for the same utterance). Only final results reset the cooldown timer — partial results do not.

---

## 6. 事件系统 / Event System {#events}

### KeywordMatchResult 结构体字段

```csharp
public struct KeywordMatchResult
{
    public string Keyword;          // 命中的主关键词 / Primary keyword that was hit
    public string MatchedPhrase;    // 实际命中的短语（主词或某个别名）/ Phrase that matched (keyword or alias)
    public string RecognizedText;   // ASR 原始识别文本 / Raw ASR recognized text
    public float  Similarity;       // 综合相似度 0~1 / Combined similarity 0~1
    public float  AsrConfidence;    // ASR 引擎置信度 0~1 / ASR engine confidence 0~1
    public bool   MatchedByPinyin;  // 是否走拼音通道命中 / Whether matched via pinyin channel
    public MatchResultSource Source;// Complete（完整结果）或 Partial（部分结果）
    public int    KeywordIndex;     // 关键词在列表中的索引 / Index in the keywords list
    public float  Timestamp;        // 命中时的 Time.time / Time.time when the hit occurred
}
```

### 事件优先级说明 / Event Priority

同一次识别结果只会触发以下**其中一个**路径：

One recognition result triggers **exactly one** of these paths:

```
识别文本
    ↓
ASR 置信度 < 门槛？ → 静默丢弃 / silently dropped
    ↓
有唯一最佳命中？ → OnKeywordMatched + KeywordEntry.OnMatched
    ↓
有多个相近候选？ → OnAmbiguousMatch
    ↓
无候选通过阈值？ → OnNoMatch
```

---

## 7. 代码示例 / Code Examples {#code}

### 示例 1：最简监听 / Minimal Listener

```csharp
using UnityEngine;
using VRTraining.SpeechRecognition;

public class SimpleListener : MonoBehaviour
{
    [SerializeField] private FuzzyKeywordRecognizer _recognizer;

    private void OnEnable()
    {
        _recognizer.OnKeywordMatched.AddListener(OnHit);
    }

    private void OnDisable()
    {
        _recognizer.OnKeywordMatched.RemoveListener(OnHit);
    }

    private void OnHit(KeywordMatchResult r)
    {
        Debug.Log($"命中: {r.Keyword}  相似度: {r.Similarity:F3}");
    }
}
```

### 示例 2：运行时动态添加关键词 / Runtime Keyword Addition

```csharp
using System.Collections.Generic;
using VRTraining.SpeechRecognition;

// 在训练步骤切换时动态更换关键词
_recognizer.ClearKeywords();
_recognizer.AddKeyword(new KeywordEntry
{
    Keyword = "心肺复苏",
    Enabled = true,
    Aliases = new List<string> { "CPR", "胸外按压" },
    UsePinyinMatch = true,
    UseCustomThreshold = true,
    CustomThreshold = 0.70f
});
// 重新启动识别以同步词汇表
_recognizer.StopRecognition();
_recognizer.StartRecognition();
```

### 示例 3：处理歧义 / Handling Ambiguity

```csharp
_recognizer.OnAmbiguousMatch.AddListener(candidates =>
{
    // candidates 按相似度降序排列
    // candidates are sorted by similarity descending
    Debug.LogWarning($"歧义：最佳候选 '{candidates[0].Keyword}' ({candidates[0].Similarity:F3})" +
                     $" vs '{candidates[1].Keyword}' ({candidates[1].Similarity:F3})");

    // 可以选择强制取最佳候选，或提示用户重说
    // You can force-pick the best candidate, or prompt the user to repeat
});
```

### 示例 4：手动调用算法 / Manual Algorithm Call

```csharp
using VRTraining.SpeechRecognition;

// 直接调用算法库，不依赖组件
var weights = FuzzyMatchWeights.Default;
float sim = FuzzyMatchAlgorithm.BestWindowSimilarity("我需要做心肺复苏", "心肺复苏", weights);
Debug.Log($"相似度: {sim:F3}");  // 预期接近 1.0

// 单独测试各算法
float lev = FuzzyMatchAlgorithm.LevenshteinSimilarity("心肺复苏", "新肺复苏");
float jw  = FuzzyMatchAlgorithm.JaroWinkler("心肺复苏", "新肺复苏");
float ng  = FuzzyMatchAlgorithm.NGramSimilarity("心肺复苏", "新肺复苏", 2);
```

---

## 8. 调参指南 / Tuning Guide {#tuning}

### 阈值选择 / Threshold Selection

| 场景 / Scenario | 推荐阈值 / Recommended Threshold | 说明 / Notes |
|---|---|---|
| 严格指令式 UI | 0.85 | 减少误触发，用户需清晰发音 / Reduces false triggers; requires clear speech |
| 一般训练场景（默认）| 0.72 | 平衡准确率与容错 / Balanced accuracy and tolerance |
| 容错训练场景 | 0.60 | 允许较大发音偏差，适合嘈杂环境 / Allows larger deviations; good for noisy environments |

### 权重调整建议 / Weight Tuning Tips

```
中文短词（2~4字）：
  → 加大 NGram（抗字序颠倒）
  → 推荐：Levenshtein 0.25 / JaroWinkler 0.25 / NGram 0.35 / Containment 0.15

长句关键词（5字以上）：
  → 加大 Containment（长句包含短词）
  → 推荐：Levenshtein 0.25 / JaroWinkler 0.20 / NGram 0.20 / Containment 0.35

英文关键词：
  → 加大 JaroWinkler（前缀匹配优势）
  → 推荐：Levenshtein 0.30 / JaroWinkler 0.40 / NGram 0.20 / Containment 0.10
```

```
Short Chinese words (2–4 chars):
  → Increase NGram (handles character reordering)
  → Suggested: Levenshtein 0.25 / JaroWinkler 0.25 / NGram 0.35 / Containment 0.15

Long keyword phrases (5+ chars):
  → Increase Containment (short keyword inside long sentence)
  → Suggested: Levenshtein 0.25 / JaroWinkler 0.20 / NGram 0.20 / Containment 0.35

English keywords:
  → Increase JaroWinkler (prefix-matching advantage)
  → Suggested: Levenshtein 0.30 / JaroWinkler 0.40 / NGram 0.20 / Containment 0.10
```

### 歧义差值 / Ambiguity Delta

`_ambiguityDelta` 默认 0.05。若关键词语义相近（如"开始"和"开始训练"），建议调大到 0.10~0.15，让系统更倾向于取最佳候选而非触发歧义。

Default is 0.05. If keywords are semantically close (e.g., "开始" and "开始训练"), increase to 0.10–0.15 so the system prefers the best candidate over triggering ambiguity.

---

## 9. 拼音字典扩展 / Pinyin Dictionary Extension {#pinyin}

### 中文

内置 `PinyinBuiltin` 仅覆盖急救场景常用汉字。对于项目专属术语（药品名、设备名），需要创建自定义字典：

1. 在 Project 窗口右键 → `Create → VRTraining → Speech → Pinyin Dictionary`
2. 在 Inspector 中添加汉字-拼音条目（无声调，全小写）
3. 将 ScriptableObject 拖到 `FuzzyKeywordRecognizer` 的"拼音字典"槽

**查找优先级**：自定义字典 → 内置基础集 → 原样保留

### English

The built-in `PinyinBuiltin` only covers common first-aid Chinese characters. For project-specific terms (drug names, equipment names), create a custom dictionary:

1. Right-click in the Project window → `Create → VRTraining → Speech → Pinyin Dictionary`
2. Add hanzi-pinyin entries in the Inspector (no tones, all lowercase)
3. Drag the ScriptableObject into the "拼音字典 (Pinyin Dictionary)" slot on `FuzzyKeywordRecognizer`

**Lookup priority**: Custom dictionary → Built-in set → Keep original character

```csharp
// 字典条目示例 / Example entries
// 汉字  拼音
// '肾'  "shen"
// '脏'  "zang"
// '除颤' → 两个条目: '除' "chu", '颤' "chan"
```

---

## 10. 性能说明 / Performance {#performance}

### 复杂度 / Complexity

单次匹配复杂度：`O(关键词数 × (主词+别名数) × max(文本长, 短语长))`

Single match complexity: `O(keyword_count × (keyword + alias_count) × max(text_len, phrase_len))`

### 实测参考 / Benchmark Reference

| 条件 / Condition | 耗时 / Time |
|---|---|
| 50 个关键词，平均句长 20 字，PC 主线程 | ~50µs |
| 20 个关键词，平均句长 10 字 | ~15µs |

### 低 GC 设计 / Low-GC Design

- 候选缓冲 `_matchBuffer`：预分配，每次匹配复用 / Pre-allocated, reused each match
- 字符串构建器 `_sb`：静态复用，避免拼音转换时分配 / Static reuse, avoids allocation during pinyin conversion
- 关键词短语数组 `AllPhrases`：启动时一次性分配 / Allocated once at startup
- Levenshtein / LCS：双行滚动数组，不分配二维数组 / Two-row rolling arrays, no 2D array allocation
- N-Gram 交集：排序+双指针，不使用 HashSet 装箱 / Sort + two-pointer, no HashSet boxing

---

## 11. 调试技巧 / Debugging {#debug}

### Inspector 调试面板（需 Odin Inspector）

在"调试"页签中：

- **当前状态**：实时显示 SpeechRecognizer 的 State
- **最近识别文本**：上一次 ASR 输出
- **最近相似度**：上一次最佳候选的综合相似度
- **测试输入 + 测试匹配按钮**：在编辑器中输入文本，点击按钮模拟匹配，无需麦克风

### Debug Log 字段说明

开启 `_debugLogEnabled` 后，Console 输出格式：

```
[FuzzyKeyword][Final] 识别文本: "我需要做心肺复苏" ASR置信度=0.923
[FuzzyKeyword][Final][命中] '心肺复苏' ← "我需要做心肺复苏" 短语="心肺复苏" 相似度=0.951 通道=字面
```

```
[FuzzyKeyword][Final][歧义] "开始" 候选: 开始(0.820), 开始训练(0.790)
```

```
[FuzzyKeyword][Final][未命中] "你好" 最近相似度=0.312
```

### 常用排查步骤 / Common Troubleshooting Steps

1. **无任何日志** → 检查 `SpeechRecognizer` 是否已引用，`AutoStart` 是否勾选
2. **识别到文本但未命中** → 降低阈值，或开启调试日志查看实际相似度
3. **频繁误触发** → 提高阈值，或增大冷却时间
4. **同音字无法命中** → 确认 `UsePinyinMatch = true`，并检查拼音字典是否覆盖该字
5. **歧义事件频繁** → 增大 `_ambiguityDelta`，或为相近关键词设置不同阈值

---

## 12. 常见问题 / FAQ {#faq}

**Q: 不安装 Odin Inspector 能用吗？**
A: 可以。移除 `ODIN_INSPECTOR` 宏定义后，所有功能正常，仅 Inspector 退化为 Unity 默认绘制，调试按钮不可用。

**Q: Can I use this without Odin Inspector?**
A: Yes. Without the `ODIN_INSPECTOR` scripting define, all runtime functionality works. Only the Inspector layout degrades to Unity's default, and the debug buttons are unavailable.

---

**Q: 部分结果匹配会不会导致重复触发？**
A: 冷却机制（`_matchCooldown`）会防止重复触发，但冷却时间仅由完整结果重置。如果仍有问题，可以关闭 `_matchPartialResults`。

**Q: Will partial result matching cause duplicate triggers?**
A: The cooldown (`_matchCooldown`) prevents this. Note that only final results reset the cooldown timer. If duplicates persist, disable `_matchPartialResults`.

---

**Q: 如何在运行时切换关键词集（例如训练步骤切换）？**
A: 调用 `ClearKeywords()` 清空，再逐个 `AddKeyword()` 添加，然后 `StopRecognition()` + `StartRecognition()` 重启以同步词汇表。

**Q: How do I swap the keyword set at runtime (e.g., on training step change)?**
A: Call `ClearKeywords()`, then `AddKeyword()` for each new entry, then `StopRecognition()` + `StartRecognition()` to restart and sync the vocabulary.

---

**Q: 语言模型放在哪里？**
A: 放在 `Assets/StreamingAssets/` 下，路径由 `StreamingAssetsLanguageModelProvider` 配置。中文模型通常是 Vosk 的 `vosk-model-small-cn-*` 系列。

**Q: Where do I put the language model?**
A: Under `Assets/StreamingAssets/`, with the path configured on `StreamingAssetsLanguageModelProvider`. For Chinese, use a Vosk `vosk-model-small-cn-*` model.

---

**Q: 移动端 / VR 头显需要额外配置吗？**
A: 需要在 `Player Settings → Android/iOS` 中开启麦克风权限（`Microphone`）。Quest 平台还需在 `OVRManager` 或 Manifest 中声明麦克风权限。

**Q: Any extra setup for mobile / VR headsets?**
A: Enable the `Microphone` permission in `Player Settings → Android/iOS`. On Quest, also declare microphone permission in `OVRManager` or the Android Manifest.

---

*文档版本 / Doc Version: 1.0 — 2026-05-20*
