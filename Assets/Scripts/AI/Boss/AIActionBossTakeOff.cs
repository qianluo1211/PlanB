using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss起跳行为 - 被打中后跳起、震飞玩家、然后隐身
    /// 流程: 播放Jump动画 → 震飞周围玩家 → 动画结束后隐身 → 完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Take Off")]
    public class AIActionBossTakeOff : AIAction
    {
        [Header("起跳设置")]
        [Tooltip("Jump动画时长（动画结束后隐身）")]
        public float JumpAnimationDuration = 0.5f;

        [Tooltip("震飞玩家的延迟（从动画开始计算）")]
        public float KnockbackDelay = 0.2f;

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

        protected Character _character;
        protected CorgiController _controller;
        protected Animator _animator;
        protected Health _health;
        protected SpriteRenderer _spriteRenderer;
        protected DamageOnTouch _damageOnTouch;
        protected int _jumpAnimationHash;
        protected float _actionStartTime;
        protected bool _hasKnockedBack;

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

            // 如果有 CharacterModel，从那里获取 SpriteRenderer
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
            _actionStartTime = Time.time;

            // 设置无敌（起跳期间不能被打断）
            if (InvulnerableDuringTakeOff && _health != null)
            {
                _health.DamageDisabled();
            }

            // 关闭重力，停止移动
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

            if (DebugMode)
            {
                Debug.Log("[BossTakeOff] ENTER - Playing Jump animation");
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

            // 震飞玩家（在指定延迟后执行一次）
            if (!_hasKnockedBack && elapsed >= KnockbackDelay)
            {
                PerformKnockback();
                _hasKnockedBack = true;
                if (DebugMode) Debug.Log("[BossTakeOff] Knockback performed");
            }

            // Jump动画结束后，隐身并完成
            if (elapsed >= JumpAnimationDuration)
            {
                // 隐身并禁用伤害
                SetBossVisible(false);
                
                TakeOffComplete = true;
                if (DebugMode) Debug.Log("[BossTakeOff] COMPLETE - Boss is now invisible");
            }
        }

        protected virtual void PerformKnockback()
        {
            int hitCount = KnockbackUtility.ApplyRadialKnockback(
                transform.position,
                KnockbackRadius,
                KnockbackForce,
                PlayerLayerMask,
                KnockbackDamage,
                gameObject
            );

            if (DebugMode && hitCount > 0)
            {
                Debug.Log($"[BossTakeOff] Knocked back {hitCount} target(s)");
            }
        }

        /// <summary>
        /// 设置Boss可见性，同时控制伤害能力
        /// </summary>
        protected virtual void SetBossVisible(bool visible)
        {
            // 控制渲染
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = visible;
            }

            // 控制接触伤害
            if (_damageOnTouch != null)
            {
                _damageOnTouch.enabled = visible;
            }

            if (DebugMode)
            {
                Debug.Log($"[BossTakeOff] Boss visibility: {visible}, DamageOnTouch: {visible}");
            }
        }

        public override void OnExitState()
        {
            base.OnExitState();

            // 停止移动，保持重力关闭（下一个状态需要）
            if (_controller != null)
            {
                _controller.SetForce(Vector2.zero);
            }

            if (DebugMode) Debug.Log("[BossTakeOff] EXIT");
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            // 震飞范围
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, KnockbackRadius);
        }
        #endif
    }
}
