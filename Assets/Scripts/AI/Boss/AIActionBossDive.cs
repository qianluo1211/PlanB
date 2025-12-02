using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss俯冲行为 - 从光柱顶端垂直下落
    /// 流程: 移动到光柱顶端 → 显身 → Fall动画 → 垂直下落 → 落地伤害 → Land动画 → 完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Dive")]
    public class AIActionBossDive : AIAction
    {
        [Header("俯冲设置")]
        [Tooltip("俯冲高度（从落点上方多高开始下落）")]
        public float DiveHeight = 15f;

        [Tooltip("俯冲速度")]
        public float DiveSpeed = 25f;

        [Tooltip("显身后开始下落前的延迟")]
        public float DiveDelay = 0.3f;

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
        public bool DebugMode = false;

        public bool DiveComplete { get; protected set; }

        protected Character _character;
        protected CorgiController _controller;
        protected Animator _animator;
        protected Health _health;
        protected SpriteRenderer _spriteRenderer;
        protected DamageOnTouch _damageOnTouch;
        protected AIActionBossAirTarget _airTargetAction;
        protected int _fallAnimationHash;
        protected int _landAnimationHash;
        protected float _actionStartTime;
        protected Vector3 _targetPosition;
        protected Vector3 _startPosition;
        protected bool _hasRepositioned;
        protected bool _isDiving;
        protected bool _hasLanded;
        protected bool _hasDealtDamage;
        protected float _diveStartTime;
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
            _spriteRenderer = GetComponentInParent<SpriteRenderer>();
            _damageOnTouch = GetComponentInParent<DamageOnTouch>();

            // 如果有 CharacterModel，从那里获取 SpriteRenderer
            if (_spriteRenderer == null && _character?.CharacterModel != null)
            {
                _spriteRenderer = _character.CharacterModel.GetComponent<SpriteRenderer>();
            }

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
            _hasRepositioned = false;
            _isDiving = false;
            _hasLanded = false;
            _hasDealtDamage = false;
            _actionStartTime = Time.time;

            // 获取锁定的目标位置（从AirTarget状态获取）
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

            // 计算起始位置（目标位置正上方）
            _startPosition = _targetPosition + Vector3.up * DiveHeight;

            // 确保重力关闭
            if (_controller != null)
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }

            if (DebugMode)
            {
                Debug.Log($"[BossDive] ENTER - Target: {_targetPosition}, Start: {_startPosition}");
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
                if (DebugMode) Debug.Log("[BossDive] Starting dive!");
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
                    if (DebugMode) Debug.Log("[BossDive] COMPLETE");
                }
            }
        }

        /// <summary>
        /// 瞬移到光柱顶端并显身
        /// </summary>
        protected virtual void RepositionAndShow()
        {
            // 瞬移到起始位置（光柱顶端）
            transform.position = _startPosition;

            // 显身并启用伤害
            SetBossVisible(true);

            // 播放Fall动画
            ResetAllAnimationParameters();
            if (_animator != null && _fallAnimationHash != 0)
            {
                _animator.SetBool(_fallAnimationHash, true);
            }

            if (DebugMode)
            {
                Debug.Log($"[BossDive] Repositioned to {_startPosition}, now visible");
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
                Debug.Log($"[BossDive] Boss visibility: {visible}, DamageOnTouch: {visible}");
            }
        }

        /// <summary>
        /// 执行垂直俯冲
        /// </summary>
        protected virtual void PerformDive()
        {
            // 垂直向下移动
            if (_controller != null)
            {
                _controller.SetForce(new Vector2(0f, -DiveSpeed));
            }

            // 检测落地
            bool shouldLand = false;

            // 方式1：高度检测
            if (transform.position.y <= _targetPosition.y + 0.5f)
            {
                shouldLand = true;
                if (DebugMode) Debug.Log("[BossDive] Land trigger: Height");
            }

            // 方式2：地面碰撞检测
            if (_controller != null && _controller.State.IsGrounded)
            {
                shouldLand = true;
                if (DebugMode) Debug.Log("[BossDive] Land trigger: IsGrounded");
            }

            // 方式3：向下射线检测
            RaycastHit2D groundHit = Physics2D.Raycast(
                transform.position,
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

        /// <summary>
        /// 落地处理
        /// </summary>
        protected virtual void OnLanding()
        {
            if (_hasLanded) return;

            _isDiving = false;
            _hasLanded = true;
            _landingStartTime = Time.time;

            // 停止移动
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

            if (DebugMode)
            {
                Debug.Log($"[BossDive] LANDED at {transform.position}");
            }
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

            if (DebugMode && hitCount > 0)
            {
                Debug.Log($"[BossDive] Impact knocked back {hitCount} target(s)");
            }
        }

        protected virtual void CleanupTargetingVisuals()
        {
            // 清理光柱
            GameObject beam = GameObject.Find("BossTargetBeam");
            if (beam != null) Destroy(beam);

            // 清理落点标记
            GameObject marker = GameObject.Find("BossLandingMarker");
            if (marker != null) Destroy(marker);

            // 也尝试通过AirTarget组件清理
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
            base.OnExitState();

            // 确保Boss可见并启用伤害
            SetBossVisible(true);

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
            // 落地冲击范围
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, ImpactRadius);

            // 显示俯冲路径
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
