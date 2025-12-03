using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss俯冲行为 - 从光柱顶端垂直下落
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Dive")]
    public class AIActionBossDive : AIActionBossBase
    {
        [Header("俯冲设置")]
        public float DiveHeight = 15f;
        public float DiveSpeed = 25f;
        public float DiveDelay = 0.3f;

        [Header("落地伤害")]
        public float ImpactRadius = 4f;
        public float ImpactDamage = 30f;
        public Vector2 ImpactKnockback = new Vector2(12f, 8f);
        public LayerMask PlayerLayerMask;

        [Header("动画")]
        public string FallAnimationParameter = "Fall";
        public string LandAnimationParameter = "Land";
        public float LandingDuration = 0.5f;

        [Header("特效")]
        public GameObject ImpactEffectPrefab;

        protected override string ActionTag => "BossDive";

        public bool DiveComplete { get; protected set; }

        protected AIActionBossAirTarget _airTargetAction;
        protected Vector3 _targetPosition;
        protected Vector3 _startPosition;
        protected bool _hasRepositioned;
        protected bool _isDiving;
        protected bool _hasLanded;
        protected bool _hasDealtDamage;
        protected float _diveStartTime;
        protected float _landingStartTime;

        protected override void CacheComponents()
        {
            base.CacheComponents();
            _airTargetAction = GetComponent<AIActionBossAirTarget>();

            if (PlayerLayerMask == 0)
            {
                PlayerLayerMask = LayerMask.GetMask("Player");
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            DiveComplete = false;
            _hasRepositioned = false;
            _isDiving = false;
            _hasLanded = false;
            _hasDealtDamage = false;

            // 获取锁定的目标位置
            if (_airTargetAction != null && _airTargetAction.LockedTargetPosition != Vector3.zero)
            {
                _targetPosition = _airTargetAction.LockedTargetPosition;
            }
            else if (_brain.Target != null)
            {
                _targetPosition = GetGroundPosition(_brain.Target.position);
            }
            else
            {
                _targetPosition = GetGroundPosition(transform.position);
            }

            _startPosition = _targetPosition + Vector3.up * DiveHeight;

            // 确保重力关闭
            if (_controller != null)
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }

            LogDebug($"Target: {_targetPosition}, Start: {_startPosition}");
            LogEnter();
        }

        public override void PerformAction()
        {
            // 阶段1：瞬移到光柱顶端并显身
            if (!_hasRepositioned)
            {
                RepositionAndShow();
                _hasRepositioned = true;
                _diveStartTime = Time.time;
                return;
            }

            // 阶段2：等待延迟后开始俯冲
            float elapsed = Time.time - _diveStartTime;
            if (!_isDiving && elapsed >= DiveDelay)
            {
                _isDiving = true;
                LogDebug("Starting dive!");
            }

            // 阶段3：俯冲中
            if (_isDiving && !_hasLanded)
            {
                PerformDive();
            }

            // 阶段4：落地后等待动画
            if (_hasLanded)
            {
                float landingElapsed = Time.time - _landingStartTime;
                if (landingElapsed >= LandingDuration)
                {
                    DiveComplete = true;
                    LogComplete();
                }
            }
        }

        protected virtual void RepositionAndShow()
        {
            transform.position = _startPosition;
            SetBossVisible(true);
            SetAnimationParameter(FallAnimationParameter);

            LogDebug($"Repositioned to {_startPosition}, now visible");
        }

        protected virtual void PerformDive()
        {
            if (_controller != null)
            {
                _controller.SetForce(new Vector2(0f, -DiveSpeed));
            }

            // 检测落地
            bool shouldLand = false;

            if (transform.position.y <= _targetPosition.y + 0.5f)
            {
                shouldLand = true;
            }

            if (_controller != null && _controller.State.IsGrounded)
            {
                shouldLand = true;
            }

            RaycastHit2D groundHit = Physics2D.Raycast(
                transform.position,
                Vector2.down,
                1.5f,
                _controller != null ? _controller.PlatformMask : (1 << 8)
            );
            if (groundHit.collider != null)
            {
                shouldLand = true;
            }

            if (shouldLand)
            {
                OnLanding();
            }
        }

        protected virtual void OnLanding()
        {
            if (_hasLanded) return;

            _isDiving = false;
            _hasLanded = true;
            _landingStartTime = Time.time;

            if (_controller != null)
            {
                _controller.SetForce(Vector2.zero);
                _controller.GravityActive(true);
            }

            SetAnimationParameter(LandAnimationParameter);

            if (!_hasDealtDamage)
            {
                PerformImpactDamage();
                _hasDealtDamage = true;
            }

            CleanupTargetingVisuals();

            if (ImpactEffectPrefab != null)
            {
                Instantiate(ImpactEffectPrefab, transform.position, Quaternion.identity);
            }

            LogDebug($"LANDED at {transform.position}");
        }

        protected virtual void PerformImpactDamage()
        {
            int hitCount = KnockbackUtility.ApplyRadialKnockback(
                transform.position,
                ImpactRadius,
                ImpactKnockback,
                PlayerLayerMask,
                ImpactDamage,
                gameObject
            );

            if (hitCount > 0)
            {
                LogDebug($"Impact knocked back {hitCount} target(s)");
            }
        }

        protected virtual void CleanupTargetingVisuals()
        {
            GameObject beam = GameObject.Find("BossTargetBeam");
            if (beam != null) Destroy(beam);

            GameObject marker = GameObject.Find("BossLandingMarker");
            if (marker != null) Destroy(marker);

            if (_airTargetAction != null)
            {
                _airTargetAction.CleanupVisuals();
            }
        }

        protected virtual Vector3 GetGroundPosition(Vector3 position)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                new Vector2(position.x, position.y + 1f),
                Vector2.down,
                100f,
                _controller != null ? _controller.PlatformMask : (1 << 8)
            );

            if (hit.collider != null)
            {
                return new Vector3(position.x, hit.point.y, 0f);
            }

            return new Vector3(position.x, position.y, 0f);
        }

        public override void OnExitState()
        {
            SetBossVisible(true);

            if (_controller != null)
            {
                _controller.GravityActive(true);
                _controller.SetForce(Vector2.zero);
            }

            base.OnExitState();
            LogExit();
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, ImpactRadius);

            if (Application.isPlaying && _targetPosition != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Vector3 startPos = _targetPosition + Vector3.up * DiveHeight;
                Gizmos.DrawLine(startPos, _targetPosition);
                Gizmos.DrawWireSphere(startPos, 0.5f);
                Gizmos.DrawWireSphere(_targetPosition, 0.5f);
            }
        }
        #endif
    }
}
