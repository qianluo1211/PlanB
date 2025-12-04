using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 接触击退组件 - 独立于伤害系统的击退机制
    /// 当敌人接触玩家时，施加击退力（不依赖DamageOnTouch的击退）
    /// 
    /// 使用场景：
    /// - 敌人需要推开玩家但不造成伤害（或只造成微量伤害）
    /// - 需要更精细控制击退力度和方向
    /// - 支持摆荡状态下的物理击退
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Abilities/Touch Knockback")]
    public class TouchKnockback : MonoBehaviour
    {
        [Header("击退设置")]
        [Tooltip("水平击退力")]
        public float KnockbackForceX = 12f;
        
        [Tooltip("垂直击退力（向上）")]
        public float KnockbackForceY = 5f;
        
        [Tooltip("击退冷却时间（防止连续击退）")]
        public float KnockbackCooldown = 0.3f;
        
        [Header("目标设置")]
        [Tooltip("目标层级")]
        public LayerMask TargetLayerMask = 1 << 9; // Player层
        
        [Tooltip("是否在TriggerEnter时触发")]
        public bool TriggerOnEnter = true;
        
        [Tooltip("是否在TriggerStay时触发")]
        public bool TriggerOnStay = true;
        
        [Header("摆荡状态")]
        [Tooltip("摆荡状态击退倍率")]
        public float SwingKnockbackMultiplier = 1.5f;
        
        [Header("Feedbacks")]
        public MMF_Player KnockbackFeedback;
        
        [Header("调试")]
        public bool DebugMode = false;
        
        // 冷却追踪
        private float _lastKnockbackTime = -999f;
        
        protected virtual void OnTriggerEnter2D(Collider2D collision)
        {
            if (!TriggerOnEnter) return;
            TryApplyKnockback(collision);
        }
        
        protected virtual void OnTriggerStay2D(Collider2D collision)
        {
            if (!TriggerOnStay) return;
            TryApplyKnockback(collision);
        }
        
        /// <summary>
        /// 尝试对目标施加击退
        /// </summary>
        protected virtual void TryApplyKnockback(Collider2D target)
        {
            // 检查层级
            if (((1 << target.gameObject.layer) & TargetLayerMask) == 0)
            {
                return;
            }
            
            // 检查冷却
            if (Time.time - _lastKnockbackTime < KnockbackCooldown)
            {
                return;
            }
            
            // 检查Health组件
            Health health = target.GetComponent<Health>();
            if (health == null)
            {
                health = target.GetComponentInParent<Health>();
            }
            if (health == null) return;
            
            // 检查是否免疫击退
            if (health.ImmuneToKnockback) return;
            
            // 应用击退
            Vector2 knockbackForce = new Vector2(KnockbackForceX, KnockbackForceY);
            bool success = KnockbackUtility.ApplyKnockback(
                target,
                knockbackForce,
                transform.position,
                null,
                SwingKnockbackMultiplier
            );
            
            if (success)
            {
                _lastKnockbackTime = Time.time;
                KnockbackFeedback?.PlayFeedbacks(target.transform.position);
                
                if (DebugMode)
                {
                    Debug.Log($"[TouchKnockback] 击退成功 - 目标: {target.name}, 力度: {knockbackForce}");
                }
            }
        }
        
        /// <summary>
        /// 手动触发击退（供外部调用）
        /// </summary>
        public virtual bool ForceKnockback(Collider2D target)
        {
            _lastKnockbackTime = -999f; // 重置冷却
            TryApplyKnockback(target);
            return Time.time == _lastKnockbackTime;
        }
        
        /// <summary>
        /// 设置击退力度
        /// </summary>
        public virtual void SetKnockbackForce(float x, float y)
        {
            KnockbackForceX = x;
            KnockbackForceY = y;
        }
    }
}
