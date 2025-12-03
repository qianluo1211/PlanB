using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Animation Event转发器 - 放在Animator所在的子物体上
    /// 将Animation Events转发给父物体上的AIActionChargedMeleeAttack组件
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
                    Debug.LogWarning($"[AnimationEventRelay] 未找到AIActionChargedMeleeAttack组件");
                }
            }
        }

        #region Animation Event Methods

        /// <summary>
        /// Windup动画完成
        /// </summary>
        public virtual void OnWindupComplete()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnWindupComplete");
            TargetAction?.OnWindupComplete();
        }

        /// <summary>
        /// Charge动画完成
        /// </summary>
        public virtual void OnChargeComplete()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnChargeComplete");
            TargetAction?.OnChargeComplete();
        }

        /// <summary>
        /// 攻击命中
        /// </summary>
        public virtual void OnMeleeHit()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnMeleeHit");
            TargetAction?.OnMeleeHit();
        }

        /// <summary>
        /// 攻击动画完成
        /// </summary>
        public virtual void OnAttackComplete()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnAttackComplete");
            TargetAction?.OnAttackComplete();
        }

        /// <summary>
        /// 开始突进 - 用于精确控制突进时机
        /// </summary>
        public virtual void OnLungeStart()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnLungeStart");
            TargetAction?.OnLungeStart();
        }

        /// <summary>
        /// 停止突进
        /// </summary>
        public virtual void OnLungeEnd()
        {
            EnsureTargetAction();
            if (DebugMode) Debug.Log("[AnimationEventRelay] → OnLungeEnd");
            TargetAction?.OnLungeEnd();
        }

        #endregion
    }
}
