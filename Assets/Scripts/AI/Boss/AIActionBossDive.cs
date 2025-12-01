using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss俯冲行为 - 从空中高速砸向目标位置
    /// 流程: 向目标位置俯冲 → 落地冲击 → 完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Dive")]
    public class AIActionBossDive : AIAction
    {
        [Header("俯冲设置")]
        [Tooltip("俯冲速度")]
        public float DiveSpeed = 30f;

        [Tooltip("俯冲前的短暂停顿")]
        public float DiveDelay = 0.2f;

        [Tooltip("最大俯冲时间（超时保护）")]
        public float MaxDiveDuration = 5f;

        [Header("落地伤害")]
        [Tooltip("落地冲击范围")]
        public float ImpactRadius = 4f;

        [Tooltip("落地伤害")]
        public float ImpactDamage = 30f;

        [Tooltip("落地击退力度")]
        public Vector2 ImpactKnockback = new Vector2(12f, 8f);

        [Tooltip("玩家层")]
        public LayerMask PlayerLayerMask;

        [Header("动画")]
        public string FallAnimationParameter = "Fall";
        public string LandAnimationParameter = "Land";

        [Tooltip("落地动画时长")]
        public float LandingDuration = 0.5f;

        [Header("特效")]
        [Tooltip("落地特效预制体")]
        public GameObject ImpactEffectPrefab;

        [Header("调试")]
        public bool DebugMode = true;  // 默认开启调试

        public bool DiveComplete { get; protected set; }

        protected Character _character;
        protected CorgiController _controller;
        protected Animator _animator;
        protected Health _health;
        protected AIActionBossAirTarget _airTargetAction;
        protected int _fallAnimationHash;
        protected int _landAnimationHash;
        protected float _actionStartTime;
        protected float _diveStartTime;
        protected Vector3 _targetPosition;
        protected Vector3 _startPosition;
        protected bool _isDiving;
        protected bool _hasLanded;
        protected bool _hasDealtDamage;
        protected float _landingStartTime;

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
            _airTargetAction = GetComponent<AIActionBossAirTarget>();

            if (!string.IsNullOrEmpty(FallAnimationParameter))
            {
                _fallAnimationHash = Animator.StringToHash(FallAnimationParameter);
            }
            if (!string.IsNullOrEmpty(LandAnimationParameter))
            {
                _landAnimationHash = Animator.StringToHash(LandAnimationParameter);
            }

            if (PlayerLayerMask == 0)
            {
                PlayerLayerMask = LayerMask.GetMask("Player");
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            DiveComplete = false;
            _isDiving = false;
            _hasLanded = false;
            _hasDealtDamage = false;
            _actionStartTime = Time.time;
            _diveStartTime = 0f;
            _startPosition = transform.position;

            // 获取目标位置（从AirTarget状态获取）
            if (_airTargetAction != null && _airTargetAction.LockedTargetPosition != Vector3.zero)
            {
                _targetPosition = _airTargetAction.LockedTargetPosition;
                if (DebugMode) Debug.Log($"[BossDive] Got target from AirTarget: {_targetPosition}");
            }
            else if (_brain.Target != null)
            {
                _targetPosition = GetGroundPosition(_brain.Target.position);
                if (DebugMode) Debug.Log($"[BossDive] Got target from Brain.Target: {_targetPosition}");
            }
            else
            {
                // 直接向下俯冲
                _targetPosition = GetGroundPosition(transform.position);
                if (DebugMode) Debug.Log($"[BossDive] Fallback target (below): {_targetPosition}");
            }

            // 确保目标位置有效
            if (float.IsNaN(_targetPosition.x) || float.IsNaN(_targetPosition.y))
            {
                _targetPosition = new Vector3(transform.position.x, transform.position.y - 15f, 0f);
                if (DebugMode) Debug.LogWarning("[BossDive] Target was NaN, using fallback");
            }

            // 确保重力关闭
            if (_controller != null)
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }

            // 播放Fall动画
            ResetAllAnimationParameters();
            if (_animator != null && _fallAnimationHash != 0)
            {
                _animator.SetBool(_fallAnimationHash, true);
            }

            if (DebugMode)
            {
                Debug.Log($"[BossDive] ENTER - Start: {_startPosition}, Target: {_targetPosition}, Distance: {Vector3.Distance(_startPosition, _targetPosition):F2}");
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

            // 延迟阶段
            if (elapsed < DiveDelay)
            {
                return;
            }

            // 开始俯冲
            if (!_isDiving && !_hasLanded)
            {
                _isDiving = true;
                _diveStartTime = Time.time;
                if (DebugMode) Debug.Log("[BossDive] Starting dive!");
            }

            // 俯冲中
            if (_isDiving && !_hasLanded)
            {
                PerformDive();
                
                // 超时保护
                float diveElapsed = Time.time - _diveStartTime;
                if (diveElapsed > MaxDiveDuration)
                {
                    if (DebugMode) Debug.LogWarning("[BossDive] TIMEOUT - Forcing landing!");
                    OnLanding();
                }
            }

            // 落地后等待动画
            if (_hasLanded)
            {
                float landingElapsed = Time.time - _landingStartTime;
                
                if (landingElapsed >= LandingDuration)
                {
                    DiveComplete = true;
                    if (DebugMode) Debug.Log("[BossDive] COMPLETE - DiveComplete = true");
                }
            }
        }

        protected virtual void PerformDive()
        {
            Vector3 currentPos = transform.position;
            Vector3 targetPos = _targetPosition;
            
            // 简化：直接向下俯冲，同时水平移动
            float horizontalDiff = targetPos.x - currentPos.x;
            float verticalSpeed = -DiveSpeed; // 向下
            float horizontalSpeed = Mathf.Clamp(horizontalDiff * 2f, -DiveSpeed * 0.5f, DiveSpeed * 0.5f);

            if (_controller != null)
            {
                _controller.SetForce(new Vector2(horizontalSpeed, verticalSpeed));
            }

            // 落地检测 - 多种方式
            bool shouldLand = false;
            
            // 方式1：高度检测
            if (currentPos.y <= targetPos.y + 1f)
            {
                shouldLand = true;
                if (DebugMode) Debug.Log($"[BossDive] Land trigger: Height ({currentPos.y:F2} <= {targetPos.y + 1f:F2})");
            }
            
            // 方式2：地面碰撞检测
            if (_controller != null && _controller.State.IsGrounded)
            {
                shouldLand = true;
                if (DebugMode) Debug.Log("[BossDive] Land trigger: IsGrounded");
            }

            // 方式3：向下射线检测
            RaycastHit2D groundHit = Physics2D.Raycast(
                currentPos,
                Vector2.down,
                1.5f,
                _controller != null ? _controller.PlatformMask : (1 << 8)
            );
            if (groundHit.collider != null)
            {
                shouldLand = true;
                if (DebugMode) Debug.Log($"[BossDive] Land trigger: Raycast hit {groundHit.collider.name}");
            }

            if (shouldLand)
            {
                OnLanding();
            }
        }

        protected virtual void OnLanding()
        {
            if (_hasLanded) return; // 防止重复调用
            
            _isDiving = false;
            _hasLanded = true;
            _landingStartTime = Time.time;

            // 完全停止移动
            if (_controller != null)
            {
                _controller.SetForce(Vector2.zero);
                _controller.GravityActive(true);
            }

            // 播放落地动画
            ResetAllAnimationParameters();
            if (_animator != null && _landAnimationHash != 0)
            {
                _animator.SetBool(_landAnimationHash, true);
            }

            // 造成落地伤害
            if (!_hasDealtDamage)
            {
                PerformImpactDamage();
                _hasDealtDamage = true;
            }

            // 清理瞄准视觉效果
            CleanupTargetingVisuals();

            // 生成落地特效
            if (ImpactEffectPrefab != null)
            {
                Instantiate(ImpactEffectPrefab, transform.position, Quaternion.identity);
            }

            if (DebugMode) Debug.Log($"[BossDive] LANDED at {transform.position}, waiting {LandingDuration}s for animation");
        }

        protected virtual void PerformImpactDamage()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ImpactRadius, PlayerLayerMask);

            foreach (var hit in hits)
            {
                Health targetHealth = hit.GetComponent<Health>();
                if (targetHealth != null)
                {
                    targetHealth.Damage(ImpactDamage, gameObject, 0f, 0.5f, Vector3.zero, null);
                    if (DebugMode) Debug.Log($"[BossDive] Impact hit {hit.name} for {ImpactDamage}");
                }

                CorgiController targetController = hit.GetComponent<CorgiController>();
                if (targetController != null)
                {
                    Vector2 knockbackDir = (hit.transform.position - transform.position).normalized;
                    if (float.IsNaN(knockbackDir.x) || knockbackDir.x == 0) knockbackDir.x = 1f;
                    
                    Vector2 force = new Vector2(
                        ImpactKnockback.x * Mathf.Sign(knockbackDir.x),
                        ImpactKnockback.y
                    );
                    
                    targetController.SetForce(force);
                }
            }
        }

        protected virtual void CleanupTargetingVisuals()
        {
            GameObject targetLine = GameObject.Find("BossTargetLine");
            if (targetLine != null) Destroy(targetLine);

            GameObject landingMarker = GameObject.Find("BossLandingMarker");
            if (landingMarker != null) Destroy(landingMarker);
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

            return new Vector3(position.x, position.y - 15f, 0f);
        }

        public override void OnExitState()
        {
            base.OnExitState();

            if (_controller != null)
            {
                _controller.GravityActive(true);
                _controller.SetForce(Vector2.zero);
            }

            if (DebugMode) Debug.Log("[BossDive] EXIT");
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, ImpactRadius);
            
            // 显示目标位置
            if (Application.isPlaying && _targetPosition != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, _targetPosition);
                Gizmos.DrawWireSphere(_targetPosition, 1f);
            }
        }
        #endif
    }
}
