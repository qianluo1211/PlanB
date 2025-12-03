using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss起跳行为 - 修复：添加状态检查防止退出后继续执行
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Take Off")]
    public class AIActionBossTakeOff : AIAction
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

        [Header("调试")]
        public bool DebugMode = false;

        public bool TakeOffComplete { get; protected set; }

        protected Character _character;
        protected CorgiController _controller;
        protected Animator _animator;
        protected Health _health;
        protected SpriteRenderer _spriteRenderer;
        protected DamageOnTouch _damageOnTouch;
        protected BoxCollider2D _boxCollider;
        protected int _jumpAnimationHash;
        protected float _actionStartTime;
        protected bool _hasKnockedBack;
        protected bool _isStateActive; // 关键：状态是否激活

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
            _spriteRenderer = GetComponentInParent<SpriteRenderer>();
            _damageOnTouch = GetComponentInParent<DamageOnTouch>();
            _boxCollider = GetComponentInParent<BoxCollider2D>();

            if (_spriteRenderer == null && _character?.CharacterModel != null)
            {
                _spriteRenderer = _character.CharacterModel.GetComponent<SpriteRenderer>();
            }

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
            _isStateActive = true; // 标记状态激活
            _actionStartTime = Time.time;

            Debug.Log($"[BossTakeOff] ===== ENTER at {Time.time:F3} =====");

            // 设置无敌
            if (InvulnerableDuringTakeOff && _health != null)
            {
                _health.DamageDisabled();
            }

            // 关闭重力
            if (_controller != null)
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }

            // 播放跳跃动画
            ResetAllAnimationParameters();
            if (_animator != null && _jumpAnimationHash != 0)
            {
                _animator.SetBool(_jumpAnimationHash, true);
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
            // 关键检查：如果状态已退出，不执行任何操作
            if (!_isStateActive)
            {
                Debug.LogWarning($"[BossTakeOff] PerformAction called but state not active! BLOCKING.");
                return;
            }

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
                Debug.Log($"[BossTakeOff] ===== COMPLETE at {Time.time:F3} - NOW INVISIBLE =====");
            }
        }

protected virtual void PerformKnockback()
        {
            // 双重检查
            if (!_isStateActive)
            {
                if (DebugMode) Debug.LogWarning($"[BossTakeOff] PerformKnockback blocked - state not active");
                return;
            }

            if (DebugMode) Debug.Log($"[BossTakeOff] Knockback at {Time.time:F3}");

            int hitCount = KnockbackUtility.ApplyRadialKnockback(
                transform.position,
                KnockbackRadius,
                KnockbackForce,
                PlayerLayerMask,
                KnockbackDamage,
                gameObject
            );

            if (DebugMode) Debug.Log($"[BossTakeOff] Knockback hit {hitCount} targets");
        }

        protected virtual void SetBossVisible(bool visible)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = visible;
            }

            if (_damageOnTouch != null)
            {
                _damageOnTouch.enabled = visible;
            }

            if (_boxCollider != null)
            {
                _boxCollider.enabled = visible;
            }

            Debug.Log($"[BossTakeOff] SetBossVisible({visible})");
        }

        public override void OnExitState()
        {
            base.OnExitState();

            // 关键：立即标记状态为非激活，阻止任何后续操作
            _isStateActive = false;
            _hasKnockedBack = true; // 双重保险

            if (_controller != null)
            {
                _controller.SetForce(Vector2.zero);
            }

            Debug.Log($"[BossTakeOff] ===== EXIT at {Time.time:F3} - STATE DEACTIVATED =====");
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
