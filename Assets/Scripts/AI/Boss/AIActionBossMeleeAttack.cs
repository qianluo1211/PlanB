using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss近战攻击行为 - 使用Animation Event触发伤害
    /// 需要在动画中添加事件调用 OnMeleeAnimationEvent()
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Melee Attack")]
    public class AIActionBossMeleeAttack : AIAction
    {
        [Header("攻击设置")]
        public float Damage = 20f;
        public Vector2 KnockbackForce = new Vector2(15f, 5f);
        public float AttackRange = 2.5f;

        [Tooltip("攻击动画总时长")]
        public float AttackDuration = 0.8f;

        [Tooltip("是否使用Animation Event触发伤害（推荐）")]
        public bool UseAnimationEvents = true;

        [Tooltip("如果不用Animation Event，延迟多久后造成伤害")]
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
            _attackStartTime = Time.time;

            // 近战攻击时不转向，让玩家可以绕到背后进行攻击

            // 重置所有动画参数，然后只设置MeleeAttack
            ResetAllAnimationParameters();
            if (_animator != null && _meleeAnimationParameterHash != 0)
            {
                _animator.SetBool(_meleeAnimationParameterHash, true);
            }

            if (DebugMode)
            {
                Debug.Log("[BossMelee] ENTER - Melee attack animation ON");
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
                if (DebugMode) Debug.Log("[BossMelee] COMPLETE");
            }
        }

        /// <summary>
        /// 由Animation Event调用 - 在动画中添加事件调用此方法
        /// </summary>
        public void OnMeleeAnimationEvent()
        {
            if (_hasDoneDamage) return;

            PerformDamage();
            _hasDoneDamage = true;

            if (DebugMode)
            {
                Debug.Log("[BossMelee] Animation Event triggered - DAMAGE!");
            }
        }

protected virtual void PerformDamage()
        {
            float direction = _character.IsFacingRight ? 1f : -1f;
            Vector2 attackCenter = (Vector2)transform.position + new Vector2(AttackOffset.x * direction, AttackOffset.y);

            // 使用 KnockbackUtility 进行击退（支持水平方向）
            Vector2 knockbackDir = new Vector2(direction, 0f);
            int hitCount = KnockbackUtility.ApplyDirectionalKnockback(
                attackCenter,
                AttackSize.x / 2f, // 用宽度的一半作为范围
                KnockbackForce,
                knockbackDir,
                TargetLayerMask,
                Damage,
                gameObject
            );

            if (DebugMode && hitCount > 0)
            {
                Debug.Log($"[BossMelee] Hit {hitCount} target(s) for {Damage} damage with knockback {KnockbackForce}");
            }
        }



        public override void OnExitState()
        {
            base.OnExitState();
            // 不要在这里重置动画参数，让下一个状态来处理

            if (DebugMode) Debug.Log("[BossMelee] EXIT");
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
