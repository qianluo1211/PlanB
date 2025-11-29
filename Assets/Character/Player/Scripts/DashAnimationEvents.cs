using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 放在Model子物体上，用于接收Animation Event并转发给父物体的CharacterDashAim
    /// 
    /// 使用方法：
    /// 1. 把这个脚本挂到有Animator的Model子物体上
    /// 2. 在动画中添加Event，Function选择 PlayDashSound 或 PlayDashSoundByIndex
    /// </summary>
    public class DashAnimationEvents : MonoBehaviour
    {
        // 缓存父物体的CharacterDashAim
        private CharacterDashAim _dashAim;

        void Awake()
        {
            // 在父物体中查找CharacterDashAim
            _dashAim = GetComponentInParent<CharacterDashAim>();
            
            if (_dashAim == null)
            {
                Debug.LogWarning("[DashAnimationEvents] 找不到CharacterDashAim组件！请确保父物体上有这个组件。");
            }
        }

        /// <summary>
        /// 播放随机Dash音效 - Animation Event调用
        /// Function: PlayDashSound
        /// </summary>
        public void PlayDashSound()
        {
            if (_dashAim != null)
            {
                _dashAim.PlayDashAttackSound();
            }
        }

        /// <summary>
        /// 播放指定索引的音效 - Animation Event调用
        /// Function: PlayDashSoundByIndex
        /// Int参数: 音效索引（从0开始）
        /// </summary>
        public void PlayDashSoundByIndex(int index)
        {
            if (_dashAim != null)
            {
                _dashAim.PlayDashSoundByIndex(index);
            }
        }
    }
}
