using UnityEngine;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 蓄力近战攻击AI行为 - 三阶段攻击：Windup(抬手) → Charge(蓄力) → Attack(攻击)
    /// 支持攻击时向前位移（突进）
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Charged Melee Attack")]
    public class AIActionChargedMeleeAttack : AIAction
    {
        public enum AttackPhase
        {
            None,
            Windup,     // 抬手预备
            Charging,   // 蓄力
            Attacking,  // 攻击
            Recovery    // 恢复
        }

        [Header("=== 阶段设置 ===")]
        [Tooltip("是否启用Windup阶段（抬手动画）")]
        public bool UseWindupPhase = true;
        
        [Tooltip("是否启用Charge阶段（蓄力动画）")]
        public bool UseChargePhase = true;
        
        [Tooltip("Charge阶段固定时长（秒）")]
        public float ChargeDuration = 0.5f;

        [Header("=== 攻击设置 ===")]
        [Tooltip("攻击伤害")]
        public float Damage = 15f;
        
        [Tooltip("击退力度")]
        public Vector2 KnockbackForce = new Vector2(12f, 4f);
        
        [Tooltip("攻击范围半径")]
        public float AttackRadius = 1.5f;
        
        [Tooltip("攻击检测偏移")]
        public Vector2 AttackOffset = new Vector2(1f, 0.5f);
        
        [Tooltip("目标图层")]
        public LayerMask TargetLayerMask;

        [Header("=== 突进位移 ===")]
        [Tooltip("Charging阶段是否向前突进")]
        public bool LungeOnCharge = false;
        
        [Tooltip("Attacking阶段是否向前突进")]
        public bool LungeOnAttack = true;
        
        [Tooltip("突进水平速度")]
        public float LungeSpeed = 15f;
        
        [Tooltip("突进持续时间（秒）")]
        public float LungeDuration = 0.2f;
        
        [Tooltip("突进时的垂直力（可以轻微跳起）")]
        public float LungeVerticalForce = 0f;

        [Header("=== 动画参数名 ===")]
        [Tooltip("Windup动画Bool参数名")]
        public string WindupParameter = "Windup";
        
        [Tooltip("Charging Trigger参数名")]
        public string ChargingTrigger = "ChargingTrigger";
        
        [Tooltip("MeleeAttack Trigger参数名")]
        public string AttackTrigger = "MeleeAttackTrigger";

        [Header("=== 行为设置 ===")]
        [Tooltip("进入攻击时是否面向目标")]
        public bool FaceTargetOnEnter = true;
        
        [Tooltip("攻击后恢复时长")]
        public float RecoveryDuration = 0.2f;

        [Header("=== Feedbacks ===")]
        public MMFeedbacks WindupFeedback;
        public MMFeedbacks ChargeFeedback;
        public MMFeedbacks AttackFeedback;
        public MMFeedbacks HitFeedback;
        public MMFeedbacks LungeFeedback;

        [Header("=== 调试 ===")]
        public bool DebugMode = false;

        // 内部状态
        protected Character _character;
        protected CorgiController _controller;
        protected Animator _animator;
        protected AttackPhase _currentPhase = AttackPhase.None;
        protected float _phaseStartTime;
        protected bool _hasDoneDamage;
        
        // 突进状态
        protected bool _isLunging;
        protected float _lungeStartTime;
        protected float _lungeDirection;

        // 动画参数哈希
        protected int _windupHash;
        protected int _chargingTriggerHash;
        protected int _attackTriggerHash;

        /// <summary>
        /// 攻击是否完成（供AIDecision检测）
        /// </summary>
        public bool AttackComplete { get; protected set; }

        /// <summary>
        /// 当前攻击阶段
        /// </summary>
        public AttackPhase CurrentPhase => _currentPhase;

        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();

            _character = GetComponentInParent<Character>();
            _controller = GetComponentInParent<CorgiController>();
            _animator = _character?.CharacterAnimator;
            
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
            
            if (DebugMode)
            {
                Debug.Log($"[ChargedMelee] Initialization - Animator: {(_animator != null ? _animator.gameObject.name : "NULL")}, Controller: {(_controller != null ? "Found" : "NULL")}");
            }

            // 缓存动画参数哈希
            _windupHash = Animator.StringToHash(WindupParameter);
            _chargingTriggerHash = Animator.StringToHash(ChargingTrigger);
            _attackTriggerHash = Animator.StringToHash(AttackTrigger);

            // 默认目标层
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
            _isLunging = false;

            // 面向目标
            if (FaceTargetOnEnter && _brain.Target != null)
            {
                FaceTarget();
            }

            // 记录面向方向用于突进
            _lungeDirection = _character.IsFacingRight ? 1f : -1f;

            // 开始第一个阶段
            if (UseWindupPhase)
            {
                EnterPhase(AttackPhase.Windup);
            }
            else if (UseChargePhase)
            {
                EnterPhase(AttackPhase.Charging);
            }
            else
            {
                EnterPhase(AttackPhase.Attacking);
            }

            if (DebugMode) Debug.Log($"[ChargedMelee] ENTER → Phase: {_currentPhase}");
        }

        public override void PerformAction()
        {
            // 处理突进
            UpdateLunge();

            // Charge阶段使用固定时长
            if (_currentPhase == AttackPhase.Charging)
            {
                float elapsed = Time.time - _phaseStartTime;
                
                if (DebugMode && Time.frameCount % 30 == 0)
                {
                    Debug.Log($"[ChargedMelee] Charging: {elapsed:F2}s / {ChargeDuration:F2}s");
                }
                
                if (elapsed >= ChargeDuration)
                {
                    if (DebugMode) Debug.Log("[ChargedMelee] Charge complete!");
                    EnterPhase(AttackPhase.Attacking);
                }
            }

            // Recovery阶段检测完成
            if (_currentPhase == AttackPhase.Recovery)
            {
                if (Time.time - _phaseStartTime >= RecoveryDuration)
                {
                    AttackComplete = true;
                    if (DebugMode) Debug.Log("[ChargedMelee] COMPLETE");
                }
            }
        }

        #region Lunge (突进)

        /// <summary>
        /// 开始突进
        /// </summary>
        protected virtual void StartLunge()
        {
            if (_controller == null) return;
            
            _isLunging = true;
            _lungeStartTime = Time.time;
            
            LungeFeedback?.PlayFeedbacks(transform.position);
            
            if (DebugMode) Debug.Log($"[ChargedMelee] Lunge started! Direction: {_lungeDirection}");
        }

        /// <summary>
        /// 更新突进状态
        /// </summary>
        protected virtual void UpdateLunge()
        {
            if (!_isLunging || _controller == null) return;

            float elapsed = Time.time - _lungeStartTime;
            
            if (elapsed < LungeDuration)
            {
                // 应用突进力
                Vector2 lungeForce = new Vector2(LungeSpeed * _lungeDirection, LungeVerticalForce);
                _controller.SetForce(lungeForce);
            }
            else
            {
                // 突进结束
                StopLunge();
            }
        }

        /// <summary>
        /// 停止突进
        /// </summary>
        protected virtual void StopLunge()
        {
            if (!_isLunging) return;
            
            _isLunging = false;
            
            if (_controller != null)
            {
                _controller.SetForce(Vector2.zero);
            }
            
            if (DebugMode) Debug.Log("[ChargedMelee] Lunge ended");
        }

        #endregion

        #region Phase Management

        protected virtual void EnterPhase(AttackPhase newPhase)
        {
            if (_currentPhase == newPhase) return;
            
            ExitCurrentPhase();
            _currentPhase = newPhase;
            _phaseStartTime = Time.time;

            switch (newPhase)
            {
                case AttackPhase.Windup:
                    if (_animator != null) _animator.SetBool(_windupHash, true);
                    WindupFeedback?.PlayFeedbacks(transform.position);
                    if (DebugMode) Debug.Log("[ChargedMelee] → Windup Phase");
                    break;

                case AttackPhase.Charging:
                    if (_animator != null) _animator.SetTrigger(_chargingTriggerHash);
                    ChargeFeedback?.PlayFeedbacks(transform.position);
                    // 检查是否在Charging阶段突进
                    if (LungeOnCharge) StartLunge();
                    if (DebugMode) Debug.Log("[ChargedMelee] → Charging Phase");
                    break;

                case AttackPhase.Attacking:
                    _hasDoneDamage = false;
                    if (_animator != null) _animator.SetTrigger(_attackTriggerHash);
                    AttackFeedback?.PlayFeedbacks(transform.position);
                    // 检查是否在Attacking阶段突进
                    if (LungeOnAttack) StartLunge();
                    if (DebugMode) Debug.Log("[ChargedMelee] → Attacking Phase");
                    break;

                case AttackPhase.Recovery:
                    StopLunge(); // 确保停止突进
                    if (DebugMode) Debug.Log("[ChargedMelee] → Recovery Phase");
                    break;
            }
        }

        protected virtual void ExitCurrentPhase()
        {
            if (_currentPhase == AttackPhase.Windup && _animator != null)
            {
                _animator.SetBool(_windupHash, false);
            }
            
            // 阶段切换时停止突进
            StopLunge();
        }

        #endregion

        #region Animation Events

        /// <summary>
        /// Animation Event: Windup动画完成
        /// </summary>
        public virtual void OnWindupComplete()
        {
            if (_currentPhase != AttackPhase.Windup) return;

            if (UseChargePhase)
                EnterPhase(AttackPhase.Charging);
            else
                EnterPhase(AttackPhase.Attacking);

            if (DebugMode) Debug.Log("[ChargedMelee] Event: OnWindupComplete");
        }

        /// <summary>
        /// Animation Event: Charge动画完成（可选，通常用固定时长）
        /// </summary>
        public virtual void OnChargeComplete()
        {
            if (_currentPhase != AttackPhase.Charging) return;
            EnterPhase(AttackPhase.Attacking);
            if (DebugMode) Debug.Log("[ChargedMelee] Event: OnChargeComplete");
        }

        /// <summary>
        /// Animation Event: 攻击命中帧
        /// </summary>
        public virtual void OnMeleeHit()
        {
            if (_currentPhase != AttackPhase.Attacking) return;
            if (_hasDoneDamage) return;

            PerformDamage();
            _hasDoneDamage = true;

            if (DebugMode) Debug.Log("[ChargedMelee] Event: OnMeleeHit → DAMAGE!");
        }

        /// <summary>
        /// Animation Event: 攻击动画完成
        /// </summary>
        public virtual void OnAttackComplete()
        {
            if (_currentPhase != AttackPhase.Attacking) return;
            EnterPhase(AttackPhase.Recovery);
            if (DebugMode) Debug.Log("[ChargedMelee] Event: OnAttackComplete");
        }

        /// <summary>
        /// Animation Event: 开始突进（可选，用于精确控制突进时机）
        /// </summary>
        public virtual void OnLungeStart()
        {
            StartLunge();
            if (DebugMode) Debug.Log("[ChargedMelee] Event: OnLungeStart");
        }

        /// <summary>
        /// Animation Event: 停止突进（可选）
        /// </summary>
        public virtual void OnLungeEnd()
        {
            StopLunge();
            if (DebugMode) Debug.Log("[ChargedMelee] Event: OnLungeEnd");
        }

        #endregion

        #region Damage

        protected virtual void PerformDamage()
        {
            float direction = _character.IsFacingRight ? 1f : -1f;
            Vector2 attackCenter = (Vector2)transform.position + 
                new Vector2(AttackOffset.x * direction, AttackOffset.y);

            Vector2 knockbackDir = new Vector2(direction, 0f);
            
            int hitCount = KnockbackUtility.ApplyDirectionalKnockback(
                attackCenter,
                AttackRadius,
                KnockbackForce,
                knockbackDir,
                TargetLayerMask,
                Damage,
                gameObject
            );

            if (hitCount > 0)
            {
                HitFeedback?.PlayFeedbacks(attackCenter);
                if (DebugMode) Debug.Log($"[ChargedMelee] Hit {hitCount} target(s)");
            }
        }

        #endregion

        #region Helpers

        protected virtual void FaceTarget()
        {
            if (_brain.Target == null || _character == null) return;

            if (_brain.Target.position.x > transform.position.x)
                _character.Face(Character.FacingDirections.Right);
            else
                _character.Face(Character.FacingDirections.Left);
        }

        #endregion

        public override void OnExitState()
        {
            base.OnExitState();

            // 停止突进
            StopLunge();

            if (_animator != null)
            {
                _animator.SetBool(_windupHash, false);
            }

            _currentPhase = AttackPhase.None;

            if (DebugMode) Debug.Log("[ChargedMelee] EXIT");
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            float direction = Application.isPlaying && _character != null ? 
                (_character.IsFacingRight ? 1f : -1f) : 1f;
            
            Vector2 attackCenter = (Vector2)transform.position + 
                new Vector2(AttackOffset.x * direction, AttackOffset.y);

            // 攻击范围
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
            Gizmos.DrawWireSphere(attackCenter, AttackRadius);
            
            // 突进距离预览
            if (LungeOnAttack || LungeOnCharge)
            {
                float lungeDistance = LungeSpeed * LungeDuration;
                Vector3 lungeEnd = transform.position + new Vector3(lungeDistance * direction, 0f, 0f);
                
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
                Gizmos.DrawLine(transform.position, lungeEnd);
                Gizmos.DrawWireSphere(lungeEnd, 0.2f);
            }
        }
        #endif
    }
}
