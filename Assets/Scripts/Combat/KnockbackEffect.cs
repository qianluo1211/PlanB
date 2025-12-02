using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 击退效果组件 - 挂载到被击退的角色上，暂时禁用水平移动输入
    /// 由 KnockbackUtility 自动添加和管理
    /// </summary>
    public class KnockbackEffect : MonoBehaviour
    {
        private CharacterHorizontalMovement _horizontalMovement;
        private float _endTime;
        private bool _wasPermitted;

        public void Apply(float duration)
        {
            _endTime = Time.time + duration;
            
            // 获取水平移动能力
            Character character = GetComponent<Character>();
            if (character != null)
            {
                _horizontalMovement = character.FindAbility<CharacterHorizontalMovement>();
            }
            
            // 暂时禁用水平移动
            if (_horizontalMovement != null)
            {
                _wasPermitted = _horizontalMovement.AbilityPermitted;
                _horizontalMovement.AbilityPermitted = false;
            }
        }

        void Update()
        {
            if (Time.time >= _endTime)
            {
                // 恢复水平移动
                if (_horizontalMovement != null)
                {
                    _horizontalMovement.AbilityPermitted = _wasPermitted;
                }
                
                // 销毁自己
                Destroy(this);
            }
        }
    }
}
