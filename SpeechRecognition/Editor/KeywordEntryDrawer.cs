using UnityEditor;
using UnityEngine;

namespace VRTraining.SpeechRecognition.EditorTools
{
    // KeywordEntry 自定义绘制器
    // 设计目标：
    //   1. 折叠头一行展示主关键词与启用开关，方便在 List 中俯瞰
    //   2. OnMatched 直接交给 EditorGUI.PropertyField 走原生 UnityEventDrawer，
    //      不包 Rect、不包 BeginChangeCheck，确保 PersistentCalls 下拉框 hit-test 正确
    //   3. 不依赖 Odin，配合 Recognizer 上的 [DrawWithUnity] 属性使用
    [CustomPropertyDrawer(typeof(KeywordEntry))]
    public class KeywordEntryDrawer : PropertyDrawer
    {
        private const float Pad = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var keywordProp = property.FindPropertyRelative(nameof(KeywordEntry.Keyword));
            var enabledProp = property.FindPropertyRelative(nameof(KeywordEntry.Enabled));
            var aliasesProp = property.FindPropertyRelative(nameof(KeywordEntry.Aliases));
            var useCustomProp = property.FindPropertyRelative(nameof(KeywordEntry.UseCustomThreshold));
            var customThrProp = property.FindPropertyRelative(nameof(KeywordEntry.CustomThreshold));
            var usePinyinProp = property.FindPropertyRelative(nameof(KeywordEntry.UsePinyinMatch));
            var onMatchedProp = property.FindPropertyRelative(nameof(KeywordEntry.OnMatched));

            EditorGUI.BeginProperty(position, label, property);

            var lineH = EditorGUIUtility.singleLineHeight;
            var y = position.y;

            // 第一行：折叠头 + 启用开关 + 主关键词预览
            var headerRect = new Rect(position.x, y, position.width, lineH);
            DrawHeader(headerRect, property, keywordProp, enabledProp);
            y += lineH + Pad;

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;

            // 主关键词
            var keywordRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.PropertyField(keywordRect, keywordProp);
            y += lineH + Pad;

            // 别名列表
            var aliasesH = EditorGUI.GetPropertyHeight(aliasesProp, true);
            var aliasesRect = new Rect(position.x, y, position.width, aliasesH);
            EditorGUI.PropertyField(aliasesRect, aliasesProp, true);
            y += aliasesH + Pad;

            // 拼音匹配开关
            var pinyinRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.PropertyField(pinyinRect, usePinyinProp);
            y += lineH + Pad;

            // 自定义阈值开关
            var useCustomRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.PropertyField(useCustomRect, useCustomProp);
            y += lineH + Pad;

            // 阈值（仅当启用自定义时显示）
            if (useCustomProp.boolValue)
            {
                var thrRect = new Rect(position.x, y, position.width, lineH);
                EditorGUI.PropertyField(thrRect, customThrProp);
                y += lineH + Pad;
            }

            // OnMatched —— 完整把 Rect 让给原生 UnityEventDrawer
            // 不要 BeginChangeCheck，不要再嵌套 BeginProperty，否则会造成 hit region 偏移
            var eventH = EditorGUI.GetPropertyHeight(onMatchedProp, true);
            var eventRect = new Rect(position.x, y, position.width, eventH);
            EditorGUI.PropertyField(eventRect, onMatchedProp, true);

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var lineH = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return lineH + Pad;
            }

            var aliasesProp = property.FindPropertyRelative(nameof(KeywordEntry.Aliases));
            var useCustomProp = property.FindPropertyRelative(nameof(KeywordEntry.UseCustomThreshold));
            var onMatchedProp = property.FindPropertyRelative(nameof(KeywordEntry.OnMatched));

            // 头 + 主关键词 + 拼音 + 自定义阈值开关 = 4 行
            var rows = 4;
            var h = (lineH + Pad) * rows;
            h += EditorGUI.GetPropertyHeight(aliasesProp, true) + Pad;
            if (useCustomProp.boolValue)
            {
                h += lineH + Pad;
            }
            h += EditorGUI.GetPropertyHeight(onMatchedProp, true);
            return h;
        }

        private static void DrawHeader(Rect rect, SerializedProperty property,
            SerializedProperty keywordProp, SerializedProperty enabledProp)
        {
            // 折叠箭头占左侧固定宽度
            var foldoutRect = new Rect(rect.x, rect.y, 14f, rect.height);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);

            // 启用开关靠右
            var toggleW = 18f;
            var toggleRect = new Rect(rect.xMax - toggleW, rect.y, toggleW, rect.height);
            enabledProp.boolValue = EditorGUI.Toggle(toggleRect, enabledProp.boolValue);

            // 中间显示关键词预览 + 状态色
            var labelRect = new Rect(rect.x + 16f, rect.y, rect.width - 16f - toggleW - 4f, rect.height);
            var preview = string.IsNullOrEmpty(keywordProp.stringValue) ? "<未命名>" : keywordProp.stringValue;
            var prevColor = GUI.color;
            if (!enabledProp.boolValue) GUI.color = new Color(1f, 1f, 1f, 0.5f);
            EditorGUI.LabelField(labelRect, preview, EditorStyles.boldLabel);
            GUI.color = prevColor;
        }
    }
}
