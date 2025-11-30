using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using System;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 通用Boss控制器基类
    /// 管理Boss的阶段、无敌状态、被击中事件、难度循环等
    /// 可以被继承来创建特定Boss的控制器
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Boss/Boss Controller")]
    public class BossController : MonoBehaviour
    {
        #region 枚举定义

        /// <summary>
        /// Boss的主要行为阶段
        /// </summary>
        public enum BossPhase
        {
            Idle,              // 待机
            RangedAttack,      // 远程攻击
            Retreating,        // 后退拉距
            MeleeCounter,      // 近战反击
            AOE,               // AOE攻击
            TakeOff,           // 起飞（被击中后）
            Airborne,          // 空中瞄准
            DiveAttack,        // 俯冲攻击
            Landing,           // 落地
            Stunned,           // 硬直
            Dead               // 死亡
        }

        /// <summary>
        /// Boss的战斗循环阶段（用于难度递增）
        /// </summary>
        public enum CombatCycle
        {
            First,    // 第一轮循环
            Second,   // 第二轮（强度提升）
            Third     // 第三轮及之后（最高强度）
        }

        #endregion

        #region 公开属性

        [Header("=== Boss信息 ===")]
        [Tooltip("Boss的名称（用于调试和UI）")]
        public string BossName = "Boss";

        [Header("=== 状态（只读）===")]
        [Tooltip("当前Boss阶段")]
        [MMReadOnly]
        public BossPhase CurrentPhase = BossPhase.Idle;

        [Tooltip("当前战斗循环")]
        [MMReadOnly]
        public CombatCycle CurrentCycle = CombatCycle.First;

        [Tooltip("Boss是否无敌")]
        [MMReadOnly]
        public bool IsInvulnerable = false;

        [Tooltip("Boss是否被击中过（本循环内）")]
        [MMReadOnly]
        public bool WasHitThisCycle = false;

        [Header("=== 距离阈值 ===")]
        [Tooltip("近战触发距离 - 玩家太近时触发近战反击")]
        public float MeleeRange = 3f;

        [Tooltip("理想射程 - Boss想要保持的距离")]
        public float IdealRange = 7f;

        [Tooltip("最大射程 - 超过这个距离玩家就太远了")]
        public float MaxRange = 12f;

        [Header("=== 被击中反应 ===")]
        [Tooltip("被击中检测的时间窗口")]
        public float HitDetectionWindow = 0.5f;

        [Tooltip("被击中后多久进入飞天状态")]
        public float TakeOffDelay = 0.3f;

        [Tooltip("被击中时对玩家的击退力")]
        public Vector2 CounterKnockbackForce = new Vector2(15f, 8f);

        [Header("=== 难度递增 ===")]
        [Tooltip("第二循环的难度倍率")]
        public float SecondCycleMultiplier = 1.3f;

        [Tooltip("第三循环的难度倍率")]
        public float ThirdCycleMultiplier = 1.6f;

        [Header("=== 反馈 ===")]
        public MMFeedbacks TakeOffFeedback;
        public MMFeedbacks LandingFeedback;
        public MMFeedbacks PhaseChangeFeedback;
        public MMFeedbacks HitFeedback;

        #endregion

        #region 内部引用

        protected Health _health;
        protected Character _character;
        protected AIBrain _brain;
        protected CorgiController _controller;
        protected Animator _animator;

        // 事件 - 其他脚本可以订阅
        public event Action<BossPhase, BossPhase> OnPhaseChanged;
        public event Action<CombatCycle> OnCycleAdvanced;
        public event Action OnBossHit;
        public event Action OnBossDeath;

        // 内部状态
        protected float _lastHitTime = -999f;
        protected int _hitCountThisCycle;
        protected int _totalHitCount;
        protected bool _initialized;

        #endregion

        #region 公开访问器

        /// <summary>
        /// 获取Health组件
        /// </summary>
        public Health BossHealth => _health;

        /// <summary>
        /// 获取Character组件
        /// </summary>
        public Character BossCharacter => _character;

        /// <summary>
        /// 获取AIBrain组件
        /// </summary>
        public AIBrain BossBrain => _brain;

        /// <summary>
        /// 上次被击中的时间
        /// </summary>
        public float LastHitTime => _lastHitTime;

        /// <summary>
        /// 本循环被击中次数
        /// </summary>
        public int HitCountThisCycle => _hitCountThisCycle;

        /// <summary>
        /// 总被击中次数
        /// </summary>
        public int TotalHitCount => _totalHitCount;

        #endregion

        #region Unity生命周期

        protected virtual void Awake()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            if (_initialized) return;

            _health = GetComponent<Health>();
            _character = GetComponent<Character>();
            _brain = GetComponent<AIBrain>();
            _controller = GetComponent<CorgiController>();
            _animator = _character?.CharacterAnimator;

            // 订阅受伤事件
            if (_health != null)
            {
                _health.OnHit += HandleOnHit;
                _health.OnDeath += HandleOnDeath;
            }

            _initialized = true;
        }

        protected virtual void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnHit -= HandleOnHit;
                _health.OnDeath -= HandleOnDeath;
            }
        }

        #endregion

        #region 阶段管理

        /// <summary>
        /// 切换到新阶段
        /// </summary>
        public virtual void SetPhase(BossPhase newPhase)
        {
            if (CurrentPhase == newPhase) return;

            BossPhase oldPhase = CurrentPhase;
            CurrentPhase = newPhase;

            // 处理阶段特殊逻辑
            OnExitPhase(oldPhase);
            OnEnterPhase(newPhase);

            // 触发事件
            OnPhaseChanged?.Invoke(oldPhase, newPhase);

            Debug.Log($"[{BossName}] Phase: {oldPhase} -> {newPhase}");
        }

        /// <summary>
        /// 进入阶段时的处理（可被子类重写）
        /// </summary>
        protected virtual void OnEnterPhase(BossPhase phase)
        {
            switch (phase)
            {
                case BossPhase.TakeOff:
                    SetInvulnerable(true);
                    TakeOffFeedback?.PlayFeedbacks();
                    break;

                case BossPhase.Airborne:
                    SetInvulnerable(true);
                    break;

                case BossPhase.DiveAttack:
                    SetInvulnerable(true);
                    break;

                case BossPhase.AOE:
                    SetInvulnerable(true);
                    break;

                case BossPhase.Landing:
                    LandingFeedback?.PlayFeedbacks();
                    break;

                case BossPhase.RangedAttack:
                    SetInvulnerable(false);
                    break;

                case BossPhase.Idle:
                    SetInvulnerable(false);
                    break;
            }
        }

        /// <summary>
        /// 退出阶段时的处理（可被子类重写）
        /// </summary>
        protected virtual void OnExitPhase(BossPhase phase)
        {
            // 子类可以重写来添加特定逻辑
        }

        /// <summary>
        /// 推进到下一个战斗循环（难度提升）
        /// </summary>
        public virtual void AdvanceCycle()
        {
            if (CurrentCycle < CombatCycle.Third)
            {
                CurrentCycle++;
                _hitCountThisCycle = 0;
                WasHitThisCycle = false;
                OnCycleAdvanced?.Invoke(CurrentCycle);
                PhaseChangeFeedback?.PlayFeedbacks();

                Debug.Log($"[{BossName}] Cycle advanced to: {CurrentCycle}");
            }
        }

        /// <summary>
        /// 重置循环（用于测试或特殊机制）
        /// </summary>
        public virtual void ResetCycle()
        {
            CurrentCycle = CombatCycle.First;
            _hitCountThisCycle = 0;
            WasHitThisCycle = false;
        }

        #endregion

        #region 无敌状态

        public virtual void SetInvulnerable(bool invulnerable)
        {
            IsInvulnerable = invulnerable;

            if (_health != null)
            {
                _health.Invulnerable = invulnerable;
            }
        }

        #endregion

        #region 受伤处理

        protected virtual void HandleOnHit()
        {
            _lastHitTime = Time.time;
            _hitCountThisCycle++;
            _totalHitCount++;
            WasHitThisCycle = true;

            HitFeedback?.PlayFeedbacks();
            OnBossHit?.Invoke();

            Debug.Log($"[{BossName}] Hit! Count this cycle: {_hitCountThisCycle}, Total: {_totalHitCount}");
        }

        protected virtual void HandleOnDeath()
        {
            SetPhase(BossPhase.Dead);
            SetInvulnerable(false);
            OnBossDeath?.Invoke();

            Debug.Log($"[{BossName}] Defeated!");
        }

        /// <summary>
        /// 是否刚刚被击中（供AIDecision使用）
        /// </summary>
        public virtual bool WasRecentlyHit(float timeWindow = -1f)
        {
            if (timeWindow < 0) timeWindow = HitDetectionWindow;
            return Time.time - _lastHitTime < timeWindow;
        }

        /// <summary>
        /// 重置被击中状态（用于循环重置）
        /// </summary>
        public virtual void ResetHitState()
        {
            WasHitThisCycle = false;
            _lastHitTime = -999f;
        }

        #endregion

        #region 距离辅助方法

        /// <summary>
        /// 获取与玩家的距离
        /// </summary>
        public virtual float GetDistanceToPlayer()
        {
            if (_brain?.Target == null) return float.MaxValue;
            return Vector3.Distance(transform.position, _brain.Target.position);
        }

        /// <summary>
        /// 获取与玩家的水平距离
        /// </summary>
        public virtual float GetHorizontalDistanceToPlayer()
        {
            if (_brain?.Target == null) return float.MaxValue;
            return Mathf.Abs(transform.position.x - _brain.Target.position.x);
        }

        /// <summary>
        /// 玩家是否在近战范围内
        /// </summary>
        public virtual bool IsPlayerInMeleeRange()
        {
            return GetDistanceToPlayer() < MeleeRange;
        }

        /// <summary>
        /// 玩家是否在理想射程内
        /// </summary>
        public virtual bool IsPlayerInIdealRange()
        {
            float distance = GetDistanceToPlayer();
            return distance >= MeleeRange && distance <= IdealRange;
        }

        /// <summary>
        /// 玩家是否在最大射程内
        /// </summary>
        public virtual bool IsPlayerInMaxRange()
        {
            return GetDistanceToPlayer() <= MaxRange;
        }

        /// <summary>
        /// 玩家在Boss的哪个方向（1=右，-1=左）
        /// </summary>
        public virtual float GetPlayerDirection()
        {
            if (_brain?.Target == null) return 0f;
            return _brain.Target.position.x > transform.position.x ? 1f : -1f;
        }

        #endregion

        #region 难度倍率

        /// <summary>
        /// 获取当前循环的难度倍率
        /// </summary>
        public virtual float GetCycleDifficultyMultiplier()
        {
            switch (CurrentCycle)
            {
                case CombatCycle.First:
                    return 1.0f;
                case CombatCycle.Second:
                    return SecondCycleMultiplier;
                case CombatCycle.Third:
                    return ThirdCycleMultiplier;
                default:
                    return 1.0f;
            }
        }

        #endregion
    }
}
