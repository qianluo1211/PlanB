using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss起跳行为 - 被打中后跳起并震飞玩家
    /// 流程: 播放Jump动画 → 震飞周围玩家 → 向上移动 → 完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Take Off")]
    public class AIActionBossTakeOff : AIAction
    {
        [Header("起跳设置")]
        [Tooltip("起跳高度")]
        public float TakeOffHeight = 8f;

        [Tooltip("起跳速度")]
        public float TakeOffSpeed = 15f;

        [Tooltip("起跳前的延迟（播放准备动画）")]
        public float TakeOffDelay = 0.3f;

        [Header("震飞玩家")]
        [Tooltip("震飞范围")]
        public float KnockbackRadius = 4f;

        [Tooltip("震飞力度")]
        public Vector2 KnockbackForce = new Vector2(15f, 8f);

        [Tooltip("震飞伤害")]
        public float KnockbackDamage = 10f;

        [Tooltip("玩家层")]
        public LayerMask PlayerLayerMask;

        [Header("动画")]
        public string JumpAnimationParameter = "Jump";

        [Header("无敌设置")]
        [Tooltip("起跳期间是否无敌")]
        public bool InvulnerableDuringTakeOff = true;

        [Header("调试")]
        public bool DebugMode = false;

        public bool TakeOffComplete { get; protected set; }
        public Vector3 TargetAirPosition { get; protected set; }

        protected Character _character;
        protected CorgiController _controller;
        protected Animator _animator;
        protected Health _health;
        protected int _jumpAnimationHash;
        protected float _actionStartTime;
        protected Vector3 _startPosition;
        protected bool _hasKnockedBack;
        protected bool _isRising;

        protected string[] _allAnimationParameters = new string[] 
        { 
            "Idle", "Walking", "RangeAttack", "MeleeAttack", 
            "AOE", "Jump", "Fall", "Land", "Dead" 
        };

        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();

            _character = GetComponentInParent<Character>();
            _controller = GetComponentInParent<CorgiController>();
            _animator = _character?.CharacterAnimator;
            _health = GetComponentInParent<Health>();

            if (!string.IsNullOrEmpty(JumpAnimationParameter))
            {
                _jumpAnimationHash = Animator.StringToHash(JumpAnimationParameter);
            }

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
            _isRising = false;
            _actionStartTime = Time.time;
            _startPosition = transform.position;

            // 计算目标空中位置
            TargetAirPosition = _startPosition + Vector3.up * TakeOffHeight;

            // 关闭重力
            if (_controller != null)
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }

            // 设置无敌（起跳期间不能被打断）
            if (InvulnerableDuringTakeOff && _health != null)
            {
                _health.DamageDisabled();
            }

            // 播放跳跃动画
            ResetAllAnimationParameters();
            if (_animator != null && _jumpAnimationHash != 0)
            {
                _animator.SetBool(_jumpAnimationHash, true);
            }

            if (DebugMode)
            {
                Debug.Log($"[BossTakeOff] ENTER - Starting take off from {_startPosition} to {TargetAirPosition}");
            }
        }

        protected virtual void ResetAllAnimationParameters()
        {
            if (_animator == null) return;

            foreach (string param in _allAnimationParameters)
            {
                int hash = Animator.StringToHash(param);
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Bool)
                    {
                        _animator.SetBool(hash, false);
                        break;
                    }
                }
            }
        }

        public override void PerformAction()
        {
            float elapsed = Time.time - _actionStartTime;

            // 延迟阶段 - 等待准备动画
            if (elapsed < TakeOffDelay)
            {
                return;
            }

            // 震飞玩家（只执行一次）
            if (!_hasKnockedBack)
            {
                PerformKnockback();
                _hasKnockedBack = true;
                _isRising = true;

                if (DebugMode) Debug.Log("[BossTakeOff] Knockback performed, starting rise");
            }

            // 上升阶段
            if (_isRising)
            {
                PerformRise();
            }
        }

        protected virtual void PerformRise()
        {
            Vector3 currentPos = transform.position;
            
            // 检查是否到达目标高度
            float distance = Vector3.Distance(currentPos, TargetAirPosition);
            if (distance < 0.5f)
            {
                TakeOffComplete = true;
                _isRising = false;

                // 停止移动
                if (_controller != null)
                {
                    _controller.SetForce(Vector2.zero);
                }

                if (DebugMode) Debug.Log("[BossTakeOff] COMPLETE - Reached target height");
                return;
            }

            // 计算上升速度 - 简单直接向上
            if (_controller != null)
            {
                _controller.SetForce(new Vector2(0f, TakeOffSpeed));
            }
        }

        protected virtual void PerformKnockback()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, KnockbackRadius, PlayerLayerMask);

            foreach (var hit in hits)
            {
                // 造成伤害
                Health targetHealth = hit.GetComponent<Health>();
                if (targetHealth != null && KnockbackDamage > 0)
                {
                    targetHealth.Damage(KnockbackDamage, gameObject, 0f, 0.5f, Vector3.zero, null);
                    if (DebugMode) Debug.Log($"[BossTakeOff] Damaged {hit.name} for {KnockbackDamage}");
                }

                // 击退
                CorgiController targetController = hit.GetComponent<CorgiController>();
                if (targetController != null)
                {
                    // 计算击退方向（从Boss指向玩家）
                    Vector2 knockbackDir = (hit.transform.position - transform.position).normalized;
                    if (float.IsNaN(knockbackDir.x) || knockbackDir.x == 0) knockbackDir.x = 1f;
                    
                    Vector2 force = new Vector2(
                        KnockbackForce.x * Mathf.Sign(knockbackDir.x),
                        KnockbackForce.y
                    );
                    
                    targetController.SetForce(force);
                    if (DebugMode) Debug.Log($"[BossTakeOff] Knocked back {hit.name} with force {force}");
                }
            }
        }

        public override void OnExitState()
        {
            base.OnExitState();

            // 停止移动
            if (_controller != null)
            {
                _controller.SetForce(Vector2.zero);
                // 注意：保持重力关闭，因为下一个状态 (AirTarget) 需要在空中
            }

            // 注意：不要在这里恢复可受伤状态，因为整个反击循环中Boss都应该无敌
            // 由 AOE 状态结束时统一恢复

            if (DebugMode) Debug.Log("[BossTakeOff] EXIT");
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            // 震飞范围
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, KnockbackRadius);

            // 目标高度
            Gizmos.color = Color.cyan;
            Vector3 targetPos = transform.position + Vector3.up * TakeOffHeight;
            Gizmos.DrawLine(transform.position, targetPos);
            Gizmos.DrawWireSphere(targetPos, 0.5f);
        }
        #endif
    }
}
