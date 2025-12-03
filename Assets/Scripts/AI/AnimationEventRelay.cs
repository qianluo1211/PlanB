using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Animation Event转发器 - 放在Animator所在的子物体上
    /// 将Animation Events转发给父物体上的AIActionChargedMeleeAttack组件
    /// 
    /// 使用方法：
    /// 1. 把这个脚本挂到有Animator的子物体上
    /// 2. 在动画中添加Animation Events，调用这个脚本的方法
    /// 3. 事件会自动转发到父物体的AIActionChargedMeleeAttack
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Helpers/Animation Event Relay")]
    public class AnimationEventRelay : MonoBehaviour
    {
        [Header("设置")]
        [Tooltip("如果为空，会自动在父物体中查找")]
        public AIActionChargedMeleeAttack TargetAction;

        [Header("调试")]
        public bool DebugMode = false;

        protected virtual void Awake()
        {
            EnsureTargetAction();
        }

        protected virtual void EnsureTargetAction()
        {
            if (TargetAction == null)
            {
                TargetAction = GetComponentInParent<AIActionChargedMeleeAttack>();
                
                if (TargetAction == null && DebugMode)
                {
                    Debug.LogWarning($"[AnimationEventRelay] 未找到AIActionChargedMeleeAttack组件在 {gameObject.name} 的父物体上");
                }
            }
        }

        #region Animation Event Methods

        /// <summary>
        /// Windup动画完成 - 在windup动画最后一帧添加此事件
        /// </summary>
        public virtual void OnWindupComplete()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnWindupComplete");
            TargetAction?.OnWindupComplete();
        }

        /// <summary>
        /// Charge动画完成 - 在charge动画最后一帧添加此事件（可选）
        /// </summary>
        public virtual void OnChargeComplete()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnChargeComplete");
            TargetAction?.OnChargeComplete();
        }

        /// <summary>
        /// 攻击命中 - 在攻击动画的命中帧添加此事件
        /// </summary>
        public virtual void OnMeleeHit()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnMeleeHit");
            TargetAction?.OnMeleeHit();
        }

        /// <summary>
        /// 攻击动画完成 - 在攻击动画最后一帧添加此事件
        /// </summary>
        public virtual void OnAttackComplete()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnAttackComplete");
            TargetAction?.OnAttackComplete();
        }

        #endregion
    }
}
