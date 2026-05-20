# Fuzzy Keyword Recognizer — English Tutorial

> **Module Purpose**
> A Unity fuzzy keyword recognizer built on Recognissimo, designed for VR first-aid training scenarios.

---

## Table of Contents

1. [Overview](#1-overview)
2. [File Structure](#2-file-structure)
3. [Quick Start](#3-quick-start)
4. [Inspector Configuration](#4-inspector-configuration)
5. [Core Concepts](#5-core-concepts)
6. [Event System](#6-event-system)
7. [Code Examples](#7-code-examples)
8. [Tuning Guide](#8-tuning-guide)
9. [Pinyin Dictionary Extension](#9-pinyin-dictionary-extension)
10. [Performance](#10-performance)
11. [Debugging](#11-debugging)
12. [FAQ](#12-faq)

---

## 1. Overview

This module adds a **fuzzy keyword matching layer** on top of the Recognissimo ASR engine, solving:

- Slight deviations between ASR output and preset keywords (similar pronunciation, extra/missing characters)
- Homophone confusion in Chinese (e.g., "心肺复苏" recognized as "新肺复苏")
- Routing multiple synonyms to the same game-logic callback

---

## 2. File Structure

| File | Description |
|---|---|
| `FuzzyKeywordRecognizer.cs` | Main MonoBehaviour component |
| `KeywordEntry.cs` | Keyword config entry |
| `KeywordMatchResult.cs` | Match result struct |
| `FuzzyMatchWeights.cs` | Multi-algorithm weight config |
| `FuzzyMatchAlgorithm.cs` | Static algorithm library |
| `PinyinDictionary.cs` | Extensible pinyin dictionary ScriptableObject |
| `PinyinBuiltin.cs` | Built-in pinyin fallback set |
| `FuzzyKeywordSample.cs` | Usage sample |
| `Editor/KeywordEntryDrawer.cs` | Custom Inspector drawer |

---

## 3. Quick Start

**Prerequisites**: Recognissimo installed, Chinese language model placed in `StreamingAssets`.

**Step 1: Build the GameObject hierarchy**

```
[SpeechRoot GameObject]
├── MicrophoneSpeechSource               ← Recognissimo mic input
├── StreamingAssetsLanguageModelProvider ← Recognissimo model loader
├── SpeechRecognizer                     ← Recognissimo core recognizer
└── FuzzyKeywordRecognizer               ← This module's main component
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

## 4. Inspector Configuration

> With Odin Inspector installed, the Inspector is split into five tabs. Without it, all functionality remains — only the visual layout degrades to Unity's default.

### Basic Tab

| Field | Type | Description |
|---|---|---|
| 语音识别器 (Speech Recognizer) | SpeechRecognizer | **Required** Recognissimo recognizer reference |
| 自动启动 (Auto Start) | bool | Auto-call StartProcessing on OnEnable |
| 使用部分结果 (Match Partial Results) | bool | Match on PartialResult too — faster response but may false-trigger |
| 命中冷却 (Match Cooldown) | float 0~5 | Cooldown before the same keyword can fire again; 0 = no cooldown |
| 自动同步词汇表 (Auto Sync Vocabulary) | bool | Inject keywords into SpeechRecognizer.Vocabulary on start |

### Keywords Tab

Each `KeywordEntry` contains:

| Field | Description |
|---|---|
| Keyword | Primary keyword (Chinese or English) |
| Enabled | Whether this entry is active |
| Aliases | Synonym list — any match triggers the entry |
| UseCustomThreshold | Override global threshold for this entry |
| CustomThreshold | Per-entry threshold 0~1 |
| UsePinyinMatch | Enable pinyin matching channel |
| OnMatched | Per-entry match callback |

### Algorithm Tab

| Field | Default | Description |
|---|---|---|
| 默认命中阈值 (Default Threshold) | 0.72 | Combined similarity ≥ this value = hit |
| 歧义判定差值 (Ambiguity Delta) | 0.05 | Best vs. 2nd-best gap < this = ambiguous |
| ASR 置信度门槛 (Min ASR Confidence) | 0.0 | Ignore results below this ASR confidence; 0 = no limit |
| 权重配置 (Weights) | See below | Levenshtein / Jaro-Winkler / N-Gram / Containment weights |
| 拼音字典 (Pinyin Dictionary) | null | Optional ScriptableObject; falls back to built-in set |

### Events Tab

| Event | Parameter | When Fired |
|---|---|---|
| OnRecognitionStarted | none | Recognizer successfully started |
| OnRecognitionStopped | none | Recognizer stopped |
| OnKeywordMatched | KeywordMatchResult | Keyword hit from final result |
| OnPartialMatched | KeywordMatchResult | Keyword hit from partial result |
| OnAmbiguousMatch | KeywordMatchResult[] | Multiple candidates too close to distinguish |
| OnNoMatch | string | Text recognized but no keyword matched |
| OnError | string | Initialization or runtime error |

---

## 5. Core Concepts

### 5.1 Matching Pipeline

```
Microphone → Recognissimo ASR → Text
                                   ↓
                       FuzzyKeywordRecognizer
                                   ↓
                 ┌─────────────────────────────┐
                 │  For each KeywordEntry:      │
                 │  1. Literal channel          │
                 │     BestWindowSimilarity     │
                 │  2. Pinyin channel           │
                 │     text→pinyin → BestWindow │
                 │  Take the higher score       │
                 └─────────────────────────────┘
                                   ↓
                 ┌─────────────────────────────┐
                 │  Filter candidates < thresh  │
                 │  Cooldown check              │
                 │  Ambiguity check             │
                 └─────────────────────────────┘
                                   ↓
                 Hit / Ambiguous / No-match → Event callbacks
```

### 5.2 Four-Algorithm Fusion

`FuzzyMatchAlgorithm.Combined()` computes a weighted sum of four algorithms and normalizes:

| Algorithm | Best For | Default Weight |
|---|---|---|
| **Levenshtein** edit distance | Single-char substitution, insertions/deletions | 0.30 |
| **Jaro-Winkler** | Short words with common prefix | 0.30 |
| **N-Gram** (bi-gram) | Character reordering | 0.25 |
| **Containment** substring | Short keyword inside long sentence | 0.15 |

### 5.3 Sliding Window

`BestWindowSimilarity()` slides a window of size `phrase.length ± 1` over the recognized text and takes the highest similarity. This prevents long sentences from diluting the score of a short keyword via Levenshtein/Jaro-Winkler/N-Gram.

### 5.4 Pinyin Channel

When `KeywordEntry.UsePinyinMatch = true`, both the recognized text and the keyword are converted to tone-free pinyin strings before fuzzy matching. This allows homophones (e.g., "心" xin and "新" xin) to match correctly.

### 5.5 Cooldown

`_matchCooldown` prevents the same keyword from firing repeatedly in a short window (e.g., multiple PartialResults for the same utterance). Only final results reset the cooldown timer — partial results do not.

---

## 6. Event System

### KeywordMatchResult Fields

```csharp
public struct KeywordMatchResult
{
    public string Keyword;           // Primary keyword that was hit
    public string MatchedPhrase;     // Phrase that matched (keyword or alias)
    public string RecognizedText;    // Raw ASR recognized text
    public float  Similarity;        // Combined similarity 0~1
    public float  AsrConfidence;     // ASR engine confidence 0~1
    public bool   MatchedByPinyin;   // Whether matched via pinyin channel
    public MatchResultSource Source; // Complete (final result) or Partial
    public int    KeywordIndex;      // Index in the keywords list
    public float  Timestamp;         // Time.time when the hit occurred
}
```

### Event Priority

One recognition result triggers **exactly one** of these paths:

```
Recognized text
    ↓
ASR confidence < threshold? → silently dropped
    ↓
Single best hit? → OnKeywordMatched + KeywordEntry.OnMatched
    ↓
Multiple close candidates? → OnAmbiguousMatch
    ↓
No candidate above threshold? → OnNoMatch
```

---

## 7. Code Examples

### Example 1: Minimal Listener

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
        Debug.Log($"Hit: {r.Keyword}  Similarity: {r.Similarity:F3}");
    }
}
```

### Example 2: Runtime Keyword Addition

```csharp
using System.Collections.Generic;
using VRTraining.SpeechRecognition;

// Swap keyword set when the training step changes
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
// Restart recognition to sync the vocabulary
_recognizer.StopRecognition();
_recognizer.StartRecognition();
```

### Example 3: Handling Ambiguity

```csharp
_recognizer.OnAmbiguousMatch.AddListener(candidates =>
{
    // candidates are sorted by similarity descending
    Debug.LogWarning($"Ambiguous: best '{candidates[0].Keyword}' ({candidates[0].Similarity:F3})" +
                     $" vs '{candidates[1].Keyword}' ({candidates[1].Similarity:F3})");

    // You can force-pick the best candidate, or prompt the user to repeat
});
```

### Example 4: Manual Algorithm Call

```csharp
using VRTraining.SpeechRecognition;

// Call the algorithm library directly, without the component
var weights = FuzzyMatchWeights.Default;
float sim = FuzzyMatchAlgorithm.BestWindowSimilarity("我需要做心肺复苏", "心肺复苏", weights);
Debug.Log($"Similarity: {sim:F3}");  // Expected close to 1.0

// Test individual algorithms
float lev = FuzzyMatchAlgorithm.LevenshteinSimilarity("心肺复苏", "新肺复苏");
float jw  = FuzzyMatchAlgorithm.JaroWinkler("心肺复苏", "新肺复苏");
float ng  = FuzzyMatchAlgorithm.NGramSimilarity("心肺复苏", "新肺复苏", 2);
```

---

## 8. Tuning Guide

### Threshold Selection

| Scenario | Recommended Threshold | Notes |
|---|---|---|
| Strict command-style UI | 0.85 | Reduces false triggers; requires clear speech |
| General training (default) | 0.72 | Balanced accuracy and tolerance |
| Fault-tolerant training | 0.60 | Allows larger deviations; good for noisy environments |

### Weight Tuning Tips

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

### Ambiguity Delta

Default is 0.05. If keywords are semantically close (e.g., "开始" and "开始训练"), increase to 0.10–0.15 so the system prefers the best candidate over triggering the ambiguity event.

---

## 9. Pinyin Dictionary Extension

The built-in `PinyinBuiltin` only covers common first-aid Chinese characters. For project-specific terms (drug names, equipment names), create a custom dictionary:

1. Right-click in the Project window → `Create → VRTraining → Speech → Pinyin Dictionary`
2. Add hanzi-pinyin entries in the Inspector (no tones, all lowercase)
3. Drag the ScriptableObject into the "拼音字典 (Pinyin Dictionary)" slot on `FuzzyKeywordRecognizer`

**Lookup priority**: Custom dictionary → Built-in set → Keep original character

```csharp
// Example entries
// Hanzi  Pinyin
// '肾'   "shen"
// '脏'   "zang"
// '除颤' → two entries: '除' "chu", '颤' "chan"
```

---

## 10. Performance

### Complexity

Single match complexity: `O(keyword_count × (keyword + alias_count) × max(text_len, phrase_len))`

### Benchmark Reference

| Condition | Time |
|---|---|
| 50 keywords, avg sentence 20 chars, PC main thread | ~50µs |
| 20 keywords, avg sentence 10 chars | ~15µs |

### Low-GC Design

- Candidate buffer `_matchBuffer`: pre-allocated, reused each match
- String builder `_sb`: static reuse, avoids allocation during pinyin conversion
- Keyword phrase array `AllPhrases`: allocated once at startup
- Levenshtein / LCS: two-row rolling arrays, no 2D array allocation
- N-Gram intersection: sort + two-pointer, no HashSet boxing

---

## 11. Debugging

### Inspector Debug Panel (requires Odin Inspector)

In the "调试 (Debug)" tab:

- **Current State**: live display of SpeechRecognizer.State
- **Last Recognized Text**: most recent ASR output
- **Last Similarity**: combined similarity of the most recent best candidate
- **Test Input + Test Match button**: type text in the Editor and click to simulate matching without a microphone

### Debug Log Format

With `_debugLogEnabled` on, Console output looks like:

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

### Common Troubleshooting Steps

1. **No logs at all** → check that `SpeechRecognizer` is assigned and `AutoStart` is enabled
2. **Text recognized but no hit** → lower the threshold, or enable debug logging to see actual similarity scores
3. **Frequent false triggers** → raise the threshold, or increase the cooldown
4. **Homophones not matching** → confirm `UsePinyinMatch = true` and check that the pinyin dictionary covers the character
5. **Frequent ambiguity events** → increase `_ambiguityDelta`, or set different thresholds for similar keywords

---

## 12. FAQ

**Q: Can I use this without Odin Inspector?**
A: Yes. Without the `ODIN_INSPECTOR` scripting define, all runtime functionality works. Only the Inspector layout degrades to Unity's default, and the debug buttons are unavailable.

---

**Q: Will partial result matching cause duplicate triggers?**
A: The cooldown (`_matchCooldown`) prevents this. Note that only final results reset the cooldown timer. If duplicates persist, disable `_matchPartialResults`.

---

**Q: How do I swap the keyword set at runtime (e.g., on training step change)?**
A: Call `ClearKeywords()`, then `AddKeyword()` for each new entry, then `StopRecognition()` + `StartRecognition()` to restart and sync the vocabulary.

---

**Q: Where do I put the language model?**
A: Under `Assets/StreamingAssets/`, with the path configured on `StreamingAssetsLanguageModelProvider`. For Chinese, use a Vosk `vosk-model-small-cn-*` model.

---

**Q: Any extra setup for mobile / VR headsets?**
A: Enable the `Microphone` permission in `Player Settings → Android/iOS`. On Quest, also declare microphone permission in `OVRManager` or the Android Manifest.

---

*Doc Version: 1.0 — 2026-05-20*
