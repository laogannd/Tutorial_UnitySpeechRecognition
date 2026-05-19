# Fuzzy Keyword Recognizer

基于 Recognissimo 的 Unity 关键词模糊识别器，面向 VR 急救训练场景。

## 特性

- 多算法加权融合的模糊匹配（Levenshtein + Jaro-Winkler + N-Gram + 子串包含）
- 中文场景拼音模糊匹配（同音不同字也能命中）
- 完整事件回调：启动/停止/命中/部分命中/歧义/未命中/错误
- 关键词独立阈值与独立回调
- 命中冷却机制，避免短时间重复触发
- Odin Inspector 美化（TabGroup 分页 / 按钮 / 实时调试 / 进度条）
- 主线程合规、零运行时反射、低 GC（候选数组与字符串缓冲均预分配复用）

## 文件清单

| 文件 | 说明 |
|------|------|
| `KeywordEntry.cs` | 关键词配置条目（主词、别名、阈值、回调） |
| `KeywordMatchResult.cs` | 匹配结果数据结构 |
| `FuzzyMatchWeights.cs` | 多算法权重配置 |
| `FuzzyMatchAlgorithm.cs` | 静态算法库 |
| `PinyinDictionary.cs` | 可扩展拼音字典（ScriptableObject） |
| `PinyinBuiltin.cs` | 内置急救场景常用汉字-拼音兜底集 |
| `FuzzyKeywordRecognizer.cs` | 主组件 |
| `FuzzyKeywordSample.cs` | 使用示例 |

## 快速开始

### 1. 场景搭建（依赖 Recognissimo）

```
[GameObject Root]
├── MicrophoneSpeechSource          (Recognissimo)
├── StreamingAssetsLanguageModelProvider (Recognissimo)
├── SpeechRecognizer                (Recognissimo)
└── FuzzyKeywordRecognizer          (本模块)
```

`SpeechRecognizer` 上：
- `SpeechSource` 拖入 MicrophoneSpeechSource
- `LanguageModelProvider` 拖入 StreamingAssetsLanguageModelProvider（中文模型）
- `AutoStart` **不要勾选**（由 FuzzyKeywordRecognizer 管控启动时机）

### 2. 配置 FuzzyKeywordRecognizer

Inspector 中：
- **基础页签**：拖入 SpeechRecognizer，按需勾选自动启动 / 部分结果匹配
- **关键词页签**：添加 KeywordEntry，填主词与同义词
- **算法页签**：调阈值、歧义差值、权重；可选填 PinyinDictionary
- **事件页签**：Inspector 拖拽配置回调 GameObject 方法

### 3. 代码示例

```csharp
[SerializeField] private FuzzyKeywordRecognizer _recognizer;

private void OnEnable()
{
    _recognizer.OnKeywordMatched.AddListener(r =>
    {
        Debug.Log($"识别到 '{r.Keyword}' 相似度 {r.Similarity}");
    });
}
```

## 阈值与权重调参建议

| 场景 | 默认阈值 | 备注 |
|------|---------|------|
| 严格匹配（指令式 UI） | 0.85 | 减少误触发 |
| 一般训练场景 | 0.72（默认） | 平衡 |
| 容错训练场景 | 0.60 | 允许较大发音偏差 |

权重默认 Levenshtein 0.30 / Jaro-Winkler 0.30 / N-Gram 0.25 / 子串 0.15。
- 中文短词建议加大 N-Gram，因为字符重排常见
- 长句关键词建议加大子串包含权重

## 性能说明

- 单次匹配复杂度：O(关键词数 × (主词+别名数) × max(L_text, L_phrase))
- 关键词 50 个、平均句长 20 字时，单次匹配约 50µs（PC 主线程）
- 完全主线程执行，无线程切换；零反射；运行期不分配二维数组
- 候选缓冲、字符串构建器、关键词短语数组均预分配复用

## 拼音字典扩展

```
Assets → Create → VRTraining → Speech → Pinyin Dictionary
```

新建 ScriptableObject 并填入项目专属术语（药品名、设备名等），拖到 FuzzyKeywordRecognizer 的"拼音字典"槽即可。未填条目自动回退到 PinyinBuiltin 内置基础集。

## 注意事项

- Odin Inspector 不存在时（移除了 `ODIN_INSPECTOR` 宏），功能仍可用，仅 Inspector 退化为 Unity 默认绘制
- 必须在 Recognissimo 的 StreamingAssets 中放置对应语言的语言模型
- 麦克风权限：移动端/VR 需要在 Player Settings 中开启麦克风权限
