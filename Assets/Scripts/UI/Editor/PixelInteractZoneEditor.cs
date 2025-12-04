using UnityEngine;
using UnityEditor;
using MoreMountains.CorgiEngine;

namespace MoreMountains.CorgiEngineEditor
{
    /// <summary>
    /// 自定义 Editor，隐藏从 ButtonActivated 继承但不使用的字段
    /// </summary>
    [CustomEditor(typeof(PixelInteractZone), true)]
    [CanEditMultipleObjects]
    public class PixelInteractZoneEditor : Editor
    {
        // 要隐藏的字段名（从 ButtonActivated 继承但不使用）
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
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // 获取所有属性
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                
                // 跳过隐藏的字段
                bool shouldHide = false;
                foreach (string hiddenField in _hiddenFields)
                {
                    if (iterator.name == hiddenField)
                    {
                        shouldHide = true;
                        break;
                    }
                }
                
                if (!shouldHide)
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
