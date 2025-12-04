using UnityEngine;
using UnityEditor;
using MoreMountains.CorgiEngine;

namespace MoreMountains.CorgiEngineEditor
{
    /// <summary>
    /// 自定义 Editor
    /// - 隐藏从 ButtonActivated 继承但不使用的字段
    /// - 根据是否设置 Prefab 智能显示/隐藏自动生成相关字段
    /// </summary>
    [CustomEditor(typeof(PixelInteractZone), true)]
    [CanEditMultipleObjects]
    public class PixelInteractZoneEditor : Editor
    {
        // 从 ButtonActivated 继承但不使用的字段
        private static readonly string[] _hiddenFields = new string[]
        {
            "UseVisualPrompt",
            "ButtonPromptPrefab",
            "ButtonPromptText",
            "ButtonPromptColor",
            "ButtonPromptTextColor",
            "AlwaysShowPrompt",
            "ShowPromptWhenColliding",
            "HidePromptAfterUse",
            "PromptRelativePosition"
        };
        
        // 仅在自动生成模式（无 Prefab）时显示的字段
        private static readonly string[] _autoGenerateOnlyFields = new string[]
        {
            "AutoSizeWidth",
            "PromptHeight",
            "PromptWidth",
            "Padding",
            "BackgroundColor",
            "BorderColor",
            "TextColor",
            "BorderWidth",
            "FontSize",
            "CustomFont"
        };
        
        // 仅在 AutoSizeWidth=false 时显示
        private static readonly string[] _fixedWidthOnlyFields = new string[]
        {
            "PromptWidth"
        };
        
        // 仅在 AutoSizeWidth=true 时显示
        private static readonly string[] _autoWidthOnlyFields = new string[]
        {
            "HorizontalPadding"
        };
        
        private SerializedProperty _promptPrefabProp;
        private SerializedProperty _autoSizeWidthProp;
        
        protected virtual void OnEnable()
        {
            _promptPrefabProp = serializedObject.FindProperty("PromptPrefab");
            _autoSizeWidthProp = serializedObject.FindProperty("AutoSizeWidth");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            bool hasPrefab = _promptPrefabProp != null && _promptPrefabProp.objectReferenceValue != null;
            bool autoSize = _autoSizeWidthProp != null && _autoSizeWidthProp.boolValue;
            
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                
                bool shouldHide = false;
                
                // 始终隐藏的字段
                foreach (string hiddenField in _hiddenFields)
                {
                    if (iterator.name == hiddenField)
                    {
                        shouldHide = true;
                        break;
                    }
                }
                
                // 有 Prefab 时隐藏自动生成相关字段
                if (!shouldHide && hasPrefab)
                {
                    foreach (string autoField in _autoGenerateOnlyFields)
                    {
                        if (iterator.name == autoField)
                        {
                            shouldHide = true;
                            break;
                        }
                    }
                }
                
                // 自动宽度开启时隐藏固定宽度字段
                if (!shouldHide && !hasPrefab && autoSize)
                {
                    foreach (string field in _fixedWidthOnlyFields)
                    {
                        if (iterator.name == field)
                        {
                            shouldHide = true;
                            break;
                        }
                    }
                }
                
                // 自动宽度关闭时隐藏 Padding 字段
                if (!shouldHide && !hasPrefab && !autoSize)
                {
                    foreach (string field in _autoWidthOnlyFields)
                    {
                        if (iterator.name == field)
                        {
                            shouldHide = true;
                            break;
                        }
                    }
                }
                
                if (!shouldHide)
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
            
            // 模式提示
            EditorGUILayout.Space();
            if (hasPrefab)
            {
                EditorGUILayout.HelpBox(
                    "Prefab 模式\n\nPrefab 要求：\n• 根物体需要 CanvasGroup\n• 子物体 \"KeyText\" 带 Text 组件\n• 可选：\"InnerBackground\" 或 \"Background\" 带 Image", 
                    MessageType.Info);
            }
            else
            {
                string modeText = autoSize 
                    ? "自动生成模式（自适应宽度）\n宽度根据文字内容自动调整"
                    : "自动生成模式（固定宽度）\n使用 PromptWidth 设置固定宽度";
                EditorGUILayout.HelpBox(modeText, MessageType.Info);
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
