using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断Boss是否刚刚被击中
    /// 用于触发"被击中 → 飞天 → 砸地"的反击循环
    /// 通用组件，适用于所有使用BossController的Boss
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Boss Was Hit")]
    public class AIDecisionBossWasHit : AIDecision
    {
        [Header("设置")]
        [Tooltip("被击中后的检测时间窗口（秒），-1表示使用BossController的默认值")]
        public float DetectionWindow = -1f;

        [Tooltip("是否只在特定阶段触发")]
        public bool OnlyInSpecificPhases = true;

        [Tooltip("可触发的阶段列表")]
        public BossController.BossPhase[] TriggerPhases = new BossController.BossPhase[]
        {
            BossController.BossPhase.Idle,
            BossController.BossPhase.RangedAttack,
            BossController.BossPhase.Retreating
        };

        protected BossController _bossController;
        protected Health _health;
        protected float _lastHitTime = -999f;
        protected bool _wasHitThisCheck = false;

        protected override void Awake()
        {
            base.Awake();

            _bossController = GetComponent<BossController>();
            _health = GetComponent<Health>();

            if (_health != null)
            {
                _health.OnHit += OnHealthHit;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnHit -= OnHealthHit;
            }
        }

        protected virtual void OnHealthHit()
        {
            _lastHitTime = Time.time;
            _wasHitThisCheck = true;
        }

        public override bool Decide()
        {
            // 确定检测窗口
            float window = DetectionWindow > 0 ? DetectionWindow : 
                (_bossController != null ? _bossController.HitDetectionWindow : 0.5f);

            // 检查是否在时间窗口内被击中
            bool recentlyHit = (Time.time - _lastHitTime) < window;

            if (!recentlyHit)
            {
                return false;
            }

            // 如果需要检查特定阶段
            if (OnlyInSpecificPhases && _bossController != null)
            {
                bool inValidPhase = false;
                foreach (var phase in TriggerPhases)
                {
                    if (_bossController.CurrentPhase == phase)
                    {
                        inValidPhase = true;
                        break;
                    }
                }

                if (!inValidPhase)
                {
                    return false;
                }
            }

            // 返回true后重置，防止重复触发
            if (_wasHitThisCheck)
            {
                _wasHitThisCheck = false;
                return true;
            }

            return false;
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            // 进入新状态时重置
            _wasHitThisCheck = false;
        }
    }
}
