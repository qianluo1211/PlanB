using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss远程攻击行为 - 使用Animation Event触发射击
    /// 需要在动画中添加事件调用 OnShootAnimationEvent()
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Ranged Attack")]
    public class AIActionBossRangedAttack : AIAction
    {
        [Header("武器设置")]
        public CharacterHandleWeapon TargetHandleWeaponAbility;

        [Header("攻击模式")]
        [Tooltip("攻击动画总时长（等动画播完再判定完成）")]
        public float AttackDuration = 1.5f;

        [Tooltip("是否使用Animation Event触发射击（推荐）")]
        public bool UseAnimationEvents = true;

        [Tooltip("如果不用Animation Event，延迟多久后射击")]
        public float ShootDelay = 0.5f;

        [Header("动画")]
        public string AttackAnimationParameter = "RangeAttack";

        [Header("行为")]
        public bool FaceTargetWhileAttacking = true;

        [Header("调试")]
        public bool DebugMode = false;

        public bool AttackComplete { get; protected set; }
        public bool IsAttacking { get; protected set; }

        protected Character _character;
        protected Animator _animator;
        protected int _attackAnimationHash;
        protected float _attackStartTime;
        protected bool _hasShot;

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
            _animator = _character?.CharacterAnimator;

            if (TargetHandleWeaponAbility == null)
            {
                TargetHandleWeaponAbility = _character?.FindAbility<CharacterHandleWeapon>();
            }

            if (!string.IsNullOrEmpty(AttackAnimationParameter))
            {
                _attackAnimationHash = Animator.StringToHash(AttackAnimationParameter);
            }
        }

public override void OnEnterState()
        {
            base.OnEnterState();

            AttackComplete = false;
            IsAttacking = true;
            _hasShot = false;
            _attackStartTime = Time.time;

            // 不主动转向，由 CharacterDelayedTurn 统一管理

            // 设置动画
            ResetAllAnimationParameters();
            if (_animator != null && _attackAnimationHash != 0)
            {
                _animator.SetBool(_attackAnimationHash, true);
            }

            if (DebugMode)
            {
                Debug.Log("[BossRangedAttack] ENTER - Starting attack animation");
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

            // 如果不用Animation Event，用定时器射击
            if (!UseAnimationEvents && !_hasShot && elapsed >= ShootDelay)
            {
                Shoot();
                _hasShot = true;
            }

            // 检查是否完成
            if (elapsed >= AttackDuration)
            {
                AttackComplete = true;
                IsAttacking = false;

                if (DebugMode)
                {
                    Debug.Log("[BossRangedAttack] COMPLETE");
                }
            }
        }

        /// <summary>
        /// 由Animation Event调用 - 在动画中添加事件调用此方法
        /// </summary>
        public void OnShootAnimationEvent()
        {
            if (!IsAttacking) return;

            Shoot();

            if (DebugMode)
            {
                Debug.Log("[BossRangedAttack] Animation Event triggered - SHOOT!");
            }
        }

        protected virtual void Shoot()
        {
            if (TargetHandleWeaponAbility != null && TargetHandleWeaponAbility.CurrentWeapon != null)
            {
                TargetHandleWeaponAbility.ShootStart();

                if (DebugMode)
                {
                    Debug.Log("[BossRangedAttack] Shooting!");
                }
            }
            else if (DebugMode)
            {
                Debug.LogWarning("[BossRangedAttack] No weapon to shoot!");
            }
        }



        public override void OnExitState()
        {
            base.OnExitState();

            if (TargetHandleWeaponAbility != null)
            {
                TargetHandleWeaponAbility.ShootStop();
            }

            // 不要在这里重置动画参数，让下一个状态来处理
            IsAttacking = false;

            if (DebugMode)
            {
                Debug.Log("[BossRangedAttack] EXIT");
            }
        }
    }
}
