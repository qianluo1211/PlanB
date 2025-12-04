using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 击退效果组件 - 挂载到被击退的角色上，暂时禁用水平移动输入
    /// 由 KnockbackUtility 自动添加和管理
    /// 
    /// 修复：正确处理多次连续击退，只在首次应用时保存原始状态
    /// </summary>
    public class KnockbackEffect : MonoBehaviour
    {
        private CharacterHorizontalMovement _horizontalMovement;
        private float _endTime;
        private bool _wasPermitted = true; // 默认为true
        private bool _initialized = false;
        private bool _restored = false;

        public void Apply(float duration)
        {
            float newEndTime = Time.time + duration;
            
            // 如果已经在运行，只延长时间，不重新保存状态
            if (_initialized)
            {
                if (newEndTime > _endTime)
                {
                    _endTime = newEndTime;
                }
                return;
            }
            
            // 首次初始化
            _initialized = true;
            _restored = false;
            _endTime = newEndTime;
            
            // 获取水平移动能力
            Character character = GetComponent<Character>();
            if (character != null)
            {
                _horizontalMovement = character.FindAbility<CharacterHorizontalMovement>();
            }
            
            // 保存原始状态并禁用水平移动
            if (_horizontalMovement != null)
            {
                _wasPermitted = _horizontalMovement.AbilityPermitted;
                _horizontalMovement.AbilityPermitted = false;
            }
        }

        void Update()
        {
            if (!_restored && Time.time >= _endTime)
            {
                Restore();
                Destroy(this);
            }
        }
        
        /// <summary>
        /// 恢复原始状态
        /// </summary>
        private void Restore()
        {
            if (_restored) return;
            _restored = true;
            
            if (_horizontalMovement != null)
            {
                _horizontalMovement.AbilityPermitted = _wasPermitted;
            }
        }
        
        /// <summary>
        /// 强制立即恢复（供外部调用）
        /// </summary>
        public void ForceRestore()
        {
            Restore();
            Destroy(this);
        }
        
        /// <summary>
        /// 组件被销毁时确保恢复状态（防止意外销毁导致状态卡住）
        /// </summary>
        void OnDestroy()
        {
            if (_initialized && !_restored)
            {
                Restore();
            }
        }
    }
}
