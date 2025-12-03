using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss近战攻击行为
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Melee Attack")]
    public class AIActionBossMeleeAttack : AIActionBossBase
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

        protected override string ActionTag => "BossMelee";

        public bool AttackComplete { get; protected set; }

        protected bool _hasDoneDamage;

        protected override void CacheComponents()
        {
            base.CacheComponents();

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

            SetAnimationParameter(MeleeAnimationParameter);
            LogEnter();
        }

        public override void PerformAction()
        {
            if (!CheckStateActive()) return;

            float elapsed = Time.time - _actionStartTime;

            // 如果不用Animation Event，用定时器造成伤害
            if (!UseAnimationEvents && !_hasDoneDamage && elapsed >= DamageDelay)
            {
                PerformDamage();
                _hasDoneDamage = true;
            }

            if (elapsed >= AttackDuration)
            {
                AttackComplete = true;
                LogComplete();
            }
        }

        /// <summary>
        /// 由Animation Event调用
        /// </summary>
        public void OnMeleeAnimationEvent()
        {
            if (!CheckStateActive("AnimationEvent")) return;
            if (_hasDoneDamage) return;

            LogDebug("AnimationEvent triggered");
            PerformDamage();
            _hasDoneDamage = true;
        }

        protected virtual void PerformDamage()
        {
            if (!CheckStateActive("PerformDamage")) return;

            float direction = _character.IsFacingRight ? 1f : -1f;
            Vector2 attackCenter = (Vector2)transform.position + new Vector2(AttackOffset.x * direction, AttackOffset.y);

            LogDebug($"PerformDamage at center={attackCenter}");

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
                LogDebug($"HIT {hitCount} target(s)!");
            }
        }

        public override void OnExitState()
        {
            _hasDoneDamage = true; // 双重保险
            base.OnExitState();
            LogExit();
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
