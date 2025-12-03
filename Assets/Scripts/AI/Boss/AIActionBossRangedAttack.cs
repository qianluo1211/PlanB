using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss远程攻击行为
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Ranged Attack")]
    public class AIActionBossRangedAttack : AIActionBossBase
    {
        [Header("武器设置")]
        public CharacterHandleWeapon TargetHandleWeaponAbility;

        [Header("攻击模式")]
        public float AttackDuration = 1.5f;
        public bool UseAnimationEvents = true;
        public float ShootDelay = 0.5f;

        [Header("动画")]
        public string AttackAnimationParameter = "RangeAttack";

        [Header("行为")]
        public bool FaceTargetWhileAttacking = true;

        protected override string ActionTag => "BossRanged";

        public bool AttackComplete { get; protected set; }
        public bool IsAttacking { get; protected set; }

        protected bool _hasShot;

        protected override void CacheComponents()
        {
            base.CacheComponents();

            if (TargetHandleWeaponAbility == null)
            {
                TargetHandleWeaponAbility = _character?.FindAbility<CharacterHandleWeapon>();
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            AttackComplete = false;
            IsAttacking = true;
            _hasShot = false;

            SetAnimationParameter(AttackAnimationParameter);
            LogEnter();
        }

        public override void PerformAction()
        {
            float elapsed = Time.time - _actionStartTime;

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
                LogComplete();
            }
        }

        /// <summary>
        /// 由Animation Event调用
        /// </summary>
        public void OnShootAnimationEvent()
        {
            if (!IsAttacking) return;

            Shoot();
            LogDebug("Animation Event triggered - SHOOT!");
        }

        protected virtual void Shoot()
        {
            if (TargetHandleWeaponAbility != null && TargetHandleWeaponAbility.CurrentWeapon != null)
            {
                TargetHandleWeaponAbility.ShootStart();
                LogDebug("Shooting!");
            }
            else
            {
                LogWarning("No weapon to shoot!");
            }
        }

        public override void OnExitState()
        {
            if (TargetHandleWeaponAbility != null)
            {
                TargetHandleWeaponAbility.ShootStop();
            }

            IsAttacking = false;
            base.OnExitState();
            LogExit();
        }
    }
}
