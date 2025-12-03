using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 扩展的GUIManager
    /// SegmentedHealthBar通过事件自动更新，无需通过此组件
    /// </summary>
    [AddComponentMenu("Corgi Engine/GUI/Extended GUI Manager")]
    public class ExtendedGUIManager : GUIManager
    {
        // SegmentedHealthBar 通过 HealthChangeEvent 自动更新
        // 此组件仅作为GUIManager的替代，保持兼容性
    }
}
