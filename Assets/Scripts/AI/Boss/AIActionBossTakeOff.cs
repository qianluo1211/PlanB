using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss起跳行为 - 被打中后跳起、震飞玩家、然后隐身
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Take Off")]
    public class AIActionBossTakeOff : AIActionBossBase
    {
        [Header("起跳设置")]
        public float JumpAnimationDuration = 0.5f;
        public float KnockbackDelay = 0.2f;

        [Header("震飞玩家")]
        public float KnockbackRadius = 4f;
        public Vector2 KnockbackForce = new Vector2(15f, 8f);
        public float KnockbackDamage = 10f;
        public LayerMask PlayerLayerMask;

        [Header("动画")]
        public string JumpAnimationParameter = "Jump";

        [Header("无敌设置")]
        public bool InvulnerableDuringTakeOff = true;

        protected override string ActionTag => "BossTakeOff";

        public bool TakeOffComplete { get; protected set; }

        protected bool _hasKnockedBack;

        protected override void CacheComponents()
        {
            base.CacheComponents();

            if (PlayerLayerMask == 0)
            {
                PlayerLayerMask = LayerMask.GetMask("Player");
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            TakeOffComplete = false;
            _hasKnockedBack = false;

            LogEnter();

            // 设置无敌
            if (InvulnerableDuringTakeOff)
            {
                SetInvulnerable(true);
            }

            // 关闭重力
            if (_controller != null)
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }

            // 播放跳跃动画
            SetAnimationParameter(JumpAnimationParameter);
        }

        public override void PerformAction()
        {
            if (!CheckStateActive("PerformAction")) return;

            float elapsed = Time.time - _actionStartTime;

            // 震飞玩家（只执行一次）
            if (!_hasKnockedBack && elapsed >= KnockbackDelay)
            {
                PerformKnockback();
                _hasKnockedBack = true;
            }

            // 动画结束后隐身
            if (elapsed >= JumpAnimationDuration && !TakeOffComplete)
            {
                SetBossVisible(false);
                TakeOffComplete = true;
                LogDebug("NOW INVISIBLE");
                LogComplete();
            }
        }

        protected virtual void PerformKnockback()
        {
            if (!CheckStateActive("PerformKnockback")) return;

            LogDebug($"Knockback at {Time.time:F3}");

            int hitCount = KnockbackUtility.ApplyRadialKnockback(
                transform.position,
                KnockbackRadius,
                KnockbackForce,
                PlayerLayerMask,
                KnockbackDamage,
                gameObject
            );

            LogDebug($"Knockback hit {hitCount} targets");
        }

        public override void OnExitState()
        {
            _hasKnockedBack = true; // 双重保险

            if (_controller != null)
            {
                _controller.SetForce(Vector2.zero);
            }

            base.OnExitState();
            LogExit();
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, KnockbackRadius);
        }
        #endif
    }
}
