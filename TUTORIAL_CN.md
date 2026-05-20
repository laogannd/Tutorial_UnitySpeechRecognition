# Fuzzy Keyword Recognizer — 中文教程

> **模块定位**
> 基于 Recognissimo 的 Unity 关键词模糊识别器，专为 VR 急救训练场景设计。

---

## 目录

1. [模块概览](#1-模块概览)
2. [文件结构](#2-文件结构)
3. [快速开始](#3-快速开始)
4. [Inspector 配置](#4-inspector-配置)
5. [核心概念](#5-核心概念)
6. [事件系统](#6-事件系统)
7. [代码示例](#7-代码示例)
8. [调参指南](#8-调参指南)
9. [拼音字典扩展](#9-拼音字典扩展)
10. [性能说明](#10-性能说明)
11. [调试技巧](#11-调试技巧)
12. [常见问题](#12-常见问题)

---

## 1. 模块概览

本模块在 Recognissimo ASR 引擎之上封装了一层**模糊关键词匹配层**，解决以下问题：

- ASR 输出文本与预设关键词存在轻微偏差（发音相近、多/漏字）
- 中文场景下同音字导致的识别偏差（如"心肺复苏"识别为"新肺复苏"）
- 需要对多个同义词统一路由到同一个游戏逻辑回调

---

## 2. 文件结构

| 文件 | 说明 |
|---|---|
| `FuzzyKeywordRecognizer.cs` | 主组件 |
| `KeywordEntry.cs` | 关键词配置条目 |
| `KeywordMatchResult.cs` | 匹配结果数据结构 |
| `FuzzyMatchWeights.cs` | 多算法权重配置 |
| `FuzzyMatchAlgorithm.cs` | 静态算法库 |
| `PinyinDictionary.cs` | 可扩展拼音字典 ScriptableObject |
| `PinyinBuiltin.cs` | 内置拼音兜底集 |
| `FuzzyKeywordSample.cs` | 使用示例 |
| `Editor/KeywordEntryDrawer.cs` | 自定义 Inspector 绘制 |

---

## 3. 快速开始

**前提条件**：已在项目中安装 Recognissimo，并在 `StreamingAssets` 中放置中文语言模型。

**第一步：搭建 GameObject 层级**

```
[SpeechRoot GameObject]
├── MicrophoneSpeechSource               ← Recognissimo 麦克风输入
├── StreamingAssetsLanguageModelProvider ← Recognissimo 语言模型加载
├── SpeechRecognizer                     ← Recognissimo 核心识别器
└── FuzzyKeywordRecognizer               ← 本模块主组件
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

## 4. Inspector 配置

> 安装 Odin Inspector 后，Inspector 会分为五个页签。未安装时功能不变，仅退化为 Unity 默认绘制。

### 基础页签

| 字段 | 类型 | 说明 |
|---|---|---|
| 语音识别器 | SpeechRecognizer | **必填** Recognissimo 识别器引用 |
| 自动启动 | bool | OnEnable 时自动调用 StartProcessing |
| 使用部分结果 | bool | 对 PartialResult 也尝试匹配，响应更快但可能误触发 |
| 命中冷却(秒) | float 0~5 | 同一关键词在该时间内不会重复触发，0 表示无冷却 |
| 自动同步词汇表 | bool | 启动时把关键词注入 SpeechRecognizer.Vocabulary，提升命中率 |

### 关键词页签

每个 `KeywordEntry` 包含：

| 字段 | 说明 |
|---|---|
| Keyword | 主关键词（中文或英文） |
| Enabled | 是否启用本条 |
| Aliases | 等价表达列表，命中任一即触发 |
| UseCustomThreshold | 勾选后用本条目阈值覆盖全局阈值 |
| CustomThreshold | 本条目专属阈值 0~1 |
| UsePinyinMatch | 启用拼音通道 |
| OnMatched | 本条目专属命中回调 |

### 算法页签

| 字段 | 默认值 | 说明 |
|---|---|---|
| 默认命中阈值 | 0.72 | 综合相似度 ≥ 该值视为命中 |
| 歧义判定差值 | 0.05 | 最佳与次佳差 < 该值触发歧义事件 |
| ASR 置信度门槛 | 0.0 | 低于此值的识别结果直接忽略，0 表示不限制 |
| 权重配置 | 见下方 | Levenshtein / Jaro-Winkler / N-Gram / 子串 四路权重 |
| 拼音字典 | null | 可选 ScriptableObject，空时使用内置集 |

### 事件页签

| 事件 | 参数 | 触发时机 |
|---|---|---|
| OnRecognitionStarted | 无 | 识别器成功启动 |
| OnRecognitionStopped | 无 | 识别器停止 |
| OnKeywordMatched | KeywordMatchResult | 完整结果命中关键词 |
| OnPartialMatched | KeywordMatchResult | 部分结果命中关键词 |
| OnAmbiguousMatch | KeywordMatchResult[] | 多候选相似度接近，无法确定 |
| OnNoMatch | string | 识别到文本但未命中任何关键词 |
| OnError | string | 初始化或运行时错误 |

---

## 5. 核心概念

### 5.1 匹配流水线

```
麦克风 → Recognissimo ASR → 文本
                                ↓
                    FuzzyKeywordRecognizer
                                ↓
              ┌─────────────────────────────┐
              │  对每个 KeywordEntry:        │
              │  1. 字面通道                 │
              │     BestWindowSimilarity     │
              │  2. 拼音通道                 │
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

### 5.2 四路算法融合

`FuzzyMatchAlgorithm.Combined()` 将四种算法加权求和后归一化：

| 算法 | 擅长场景 | 默认权重 |
|---|---|---|
| **Levenshtein** 编辑距离 | 单字替换、多/漏字 | 0.30 |
| **Jaro-Winkler** | 相同前缀的短词 | 0.30 |
| **N-Gram** (bi-gram) | 字符顺序颠倒、词序变化 | 0.25 |
| **Containment** 子串包含 | 长句中包含短关键词 | 0.15 |

### 5.3 滑动窗口

`BestWindowSimilarity()` 在识别文本中以关键词长度 ±1 的窗口滑动，取最高相似度。这解决了"长句包含短关键词时，整体 Levenshtein 被句长稀释"的问题。

### 5.4 拼音通道

当 `KeywordEntry.UsePinyinMatch = true` 时，识别文本和关键词均转为无声调拼音串后再做模糊匹配。这使得同音字（如"心"和"新"）也能命中。

### 5.5 冷却机制

`_matchCooldown` 防止同一关键词在短时间内被重复触发（例如 ASR 对同一句话产生多个 PartialResult）。冷却时间仅对完整结果记录，部分结果不重置冷却计时器。

---

## 6. 事件系统

### KeywordMatchResult 结构体字段

```csharp
public struct KeywordMatchResult
{
    public string Keyword;           // 命中的主关键词
    public string MatchedPhrase;     // 实际命中的短语（主词或某个别名）
    public string RecognizedText;    // ASR 原始识别文本
    public float  Similarity;        // 综合相似度 0~1
    public float  AsrConfidence;     // ASR 引擎置信度 0~1
    public bool   MatchedByPinyin;   // 是否走拼音通道命中
    public MatchResultSource Source; // Complete（完整结果）或 Partial（部分结果）
    public int    KeywordIndex;      // 关键词在列表中的索引
    public float  Timestamp;         // 命中时的 Time.time
}
```

### 事件优先级

同一次识别结果只会触发以下**其中一个**路径：

```
识别文本
    ↓
ASR 置信度 < 门槛？ → 静默丢弃
    ↓
有唯一最佳命中？ → OnKeywordMatched + KeywordEntry.OnMatched
    ↓
有多个相近候选？ → OnAmbiguousMatch
    ↓
无候选通过阈值？ → OnNoMatch
```

---

## 7. 代码示例

### 示例 1：最简监听

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

### 示例 2：运行时动态添加关键词

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

### 示例 3：处理歧义

```csharp
_recognizer.OnAmbiguousMatch.AddListener(candidates =>
{
    // candidates 按相似度降序排列
    Debug.LogWarning($"歧义：最佳候选 '{candidates[0].Keyword}' ({candidates[0].Similarity:F3})" +
                     $" vs '{candidates[1].Keyword}' ({candidates[1].Similarity:F3})");

    // 可以选择强制取最佳候选，或提示用户重说
});
```

### 示例 4：手动调用算法

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

## 8. 调参指南

### 阈值选择

| 场景 | 推荐阈值 | 说明 |
|---|---|---|
| 严格指令式 UI | 0.85 | 减少误触发，用户需清晰发音 |
| 一般训练场景（默认）| 0.72 | 平衡准确率与容错 |
| 容错训练场景 | 0.60 | 允许较大发音偏差，适合嘈杂环境 |

### 权重调整建议

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

### 歧义差值

`_ambiguityDelta` 默认 0.05。若关键词语义相近（如"开始"和"开始训练"），建议调大到 0.10~0.15，让系统更倾向于取最佳候选而非触发歧义。

---

## 9. 拼音字典扩展

内置 `PinyinBuiltin` 仅覆盖急救场景常用汉字。对于项目专属术语（药品名、设备名），需要创建自定义字典：

1. 在 Project 窗口右键 → `Create → VRTraining → Speech → Pinyin Dictionary`
2. 在 Inspector 中添加汉字-拼音条目（无声调，全小写）
3. 将 ScriptableObject 拖到 `FuzzyKeywordRecognizer` 的"拼音字典"槽

**查找优先级**：自定义字典 → 内置基础集 → 原样保留

```csharp
// 字典条目示例
// 汉字  拼音
// '肾'  "shen"
// '脏'  "zang"
// '除颤' → 两个条目: '除' "chu", '颤' "chan"
```

---

## 10. 性能说明

### 复杂度

单次匹配复杂度：`O(关键词数 × (主词+别名数) × max(文本长, 短语长))`

### 实测参考

| 条件 | 耗时 |
|---|---|
| 50 个关键词，平均句长 20 字，PC 主线程 | ~50µs |
| 20 个关键词，平均句长 10 字 | ~15µs |

### 低 GC 设计

- 候选缓冲 `_matchBuffer`：预分配，每次匹配复用
- 字符串构建器 `_sb`：静态复用，避免拼音转换时分配
- 关键词短语数组 `AllPhrases`：启动时一次性分配
- Levenshtein / LCS：双行滚动数组，不分配二维数组
- N-Gram 交集：排序+双指针，不使用 HashSet 装箱

---

## 11. 调试技巧

### Inspector 调试面板（需 Odin Inspector）

在"调试"页签中：

- **当前状态**：实时显示 SpeechRecognizer 的 State
- **最近识别文本**：上一次 ASR 输出
- **最近相似度**：上一次最佳候选的综合相似度
- **测试输入 + 测试匹配按钮**：在编辑器中输入文本，点击按钮模拟匹配，无需麦克风

### Debug Log 格式说明

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

### 常用排查步骤

1. **无任何日志** → 检查 `SpeechRecognizer` 是否已引用，`AutoStart` 是否勾选
2. **识别到文本但未命中** → 降低阈值，或开启调试日志查看实际相似度
3. **频繁误触发** → 提高阈值，或增大冷却时间
4. **同音字无法命中** → 确认 `UsePinyinMatch = true`，并检查拼音字典是否覆盖该字
5. **歧义事件频繁** → 增大 `_ambiguityDelta`，或为相近关键词设置不同阈值

---

## 12. 常见问题

**Q: 不安装 Odin Inspector 能用吗？**
A: 可以。移除 `ODIN_INSPECTOR` 宏定义后，所有功能正常，仅 Inspector 退化为 Unity 默认绘制，调试按钮不可用。

---

**Q: 部分结果匹配会不会导致重复触发？**
A: 冷却机制（`_matchCooldown`）会防止重复触发，但冷却时间仅由完整结果重置。如果仍有问题，可以关闭 `_matchPartialResults`。

---

**Q: 如何在运行时切换关键词集（例如训练步骤切换）？**
A: 调用 `ClearKeywords()` 清空，再逐个 `AddKeyword()` 添加，然后 `StopRecognition()` + `StartRecognition()` 重启以同步词汇表。

---

**Q: 语言模型放在哪里？**
A: 放在 `Assets/StreamingAssets/` 下，路径由 `StreamingAssetsLanguageModelProvider` 配置。中文模型通常是 Vosk 的 `vosk-model-small-cn-*` 系列。

---

**Q: 移动端 / VR 头显需要额外配置吗？**
A: 需要在 `Player Settings → Android/iOS` 中开启麦克风权限（`Microphone`）。Quest 平台还需在 `OVRManager` 或 Manifest 中声明麦克风权限。

---

*文档版本: 1.0 — 2026-05-20*
