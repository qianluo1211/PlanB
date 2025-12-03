using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss近战攻击行为 - 修复：添加状态检查防止退出后继续造成伤害
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Melee Attack")]
    public class AIActionBossMeleeAttack : AIAction
    {
        [Header("攻击设置")]
        public float Damage = 20f;
        public Vector2 KnockbackForce = new Vector2(15f, 5f);
        public float AttackRange = 2.5f;
        public float AttackDuration = 0.8f;
        public bool UseAnimationEvents = true;
        public float DamageDelay = 0.3f;

        [Header("动画")]
        public string MeleeAnimationParameter = "MeleeAttack";

        [Header("检测设置")]
        public LayerMask TargetLayerMask;
        public Vector2 AttackOffset = new Vector2(1f, 0f);
        public Vector2 AttackSize = new Vector2(2f, 2f);

        [Header("调试")]
        public bool DebugMode = false;

        protected Character _character;
        protected Animator _animator;
        protected int _meleeAnimationParameterHash;
        protected float _attackStartTime;
        protected bool _hasDoneDamage;
        protected bool _isStateActive; // 关键：状态是否激活

        protected string[] _allAnimationParameters = new string[] 
        { 
            "Idle", "Walking", "RangeAttack", "MeleeAttack", 
            "AOE", "Jump", "Fall", "Land", "Dead" 
        };

        public bool AttackComplete { get; protected set; }

        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();

            _character = GetComponentInParent<Character>();
            _animator = _character?.CharacterAnimator;

            if (!string.IsNullOrEmpty(MeleeAnimationParameter))
            {
                _meleeAnimationParameterHash = Animator.StringToHash(MeleeAnimationParameter);
            }

            if (TargetLayerMask == 0)
            {
                TargetLayerMask = LayerMask.GetMask("Player");
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            AttackComplete = false;
            _hasDoneDamage = false;
            _isStateActive = true; // 标记状态激活
            _attackStartTime = Time.time;

            ResetAllAnimationParameters();
            if (_animator != null && _meleeAnimationParameterHash != 0)
            {
                _animator.SetBool(_meleeAnimationParameterHash, true);
            }

            Debug.Log($"[BossMelee] ===== ENTER at {Time.time:F3} =====");
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
            // 关键检查
            if (!_isStateActive)
            {
                return;
            }

            float elapsed = Time.time - _attackStartTime;

            // 如果不用Animation Event，用定时器造成伤害
            if (!UseAnimationEvents && !_hasDoneDamage && elapsed >= DamageDelay)
            {
                PerformDamage();
                _hasDoneDamage = true;
            }

            if (elapsed >= AttackDuration)
            {
                AttackComplete = true;
                Debug.Log($"[BossMelee] COMPLETE at {Time.time:F3}");
            }
        }

        /// <summary>
        /// 由Animation Event调用
        /// </summary>
        public void OnMeleeAnimationEvent()
        {
            // 关键检查：如果状态已退出，不执行
            if (!_isStateActive)
            {
                Debug.LogWarning($"[BossMelee] AnimationEvent BLOCKED - state not active!");
                return;
            }

            if (_hasDoneDamage) return;

            Debug.Log($"[BossMelee] AnimationEvent triggered at {Time.time:F3}");
            PerformDamage();
            _hasDoneDamage = true;
        }

        protected virtual void PerformDamage()
        {
            // 双重检查
            if (!_isStateActive)
            {
                Debug.LogError($"[BossMelee] PerformDamage BLOCKED - state not active!");
                return;
            }

            float direction = _character.IsFacingRight ? 1f : -1f;
            Vector2 attackCenter = (Vector2)transform.position + new Vector2(AttackOffset.x * direction, AttackOffset.y);

            Debug.Log($"[BossMelee] PerformDamage at {Time.time:F3}, center={attackCenter}");

            Vector2 knockbackDir = new Vector2(direction, 0f);
            int hitCount = KnockbackUtility.ApplyDirectionalKnockback(
                attackCenter,
                AttackSize.x / 2f,
                KnockbackForce,
                knockbackDir,
                TargetLayerMask,
                Damage,
                gameObject
            );

            if (hitCount > 0)
            {
                Debug.Log($"[BossMelee] HIT {hitCount} target(s)!");
            }
        }

        public override void OnExitState()
        {
            base.OnExitState();

            // 关键：立即标记状态为非激活
            _isStateActive = false;
            _hasDoneDamage = true; // 双重保险

            Debug.Log($"[BossMelee] ===== EXIT at {Time.time:F3} - STATE DEACTIVATED =====");
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            float direction = Application.isPlaying && _character != null ? 
                (_character.IsFacingRight ? 1f : -1f) : 1f;
            
            Vector2 attackCenter = (Vector2)transform.position + new Vector2(AttackOffset.x * direction, AttackOffset.y);

            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireCube(attackCenter, AttackSize);
        }
        #endif
    }
}
