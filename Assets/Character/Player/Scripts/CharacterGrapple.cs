using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 钩爪摆荡能力 - 纯手动物理实现
    /// 按住右键发射钩爪并摆荡，松开飞出
    /// Animator parameters: Swinging (bool), GrappleFiring (bool)
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Abilities/Character Grapple")]
    public class CharacterGrapple : CharacterAbility
    {
        public override string HelpBoxText() { return "钩爪摆荡能力。按住右键发射钩爪，命中后开始摆荡，Shift+方向键加速（有CD），松开飞出。"; }

        [Header("=== 钩爪设置 ===")]
        
        [Tooltip("可以钩住的层")]
        public LayerMask GrappleLayerMask = 1 << 8;
        
        [Tooltip("最大钩爪距离")]
        public float MaxGrappleDistance = 12f;
        
        [Tooltip("钩爪飞行速度")]
        public float HookTravelSpeed = 40f;
        
        [Tooltip("搜索角度范围（度）")]
        public float GrappleSearchAngle = 60f;

        [Header("=== 摆荡物理 ===")]
        
        [Tooltip("摆荡重力强度")]
        public float SwingGravity = 25f;
        
        [Tooltip("加速冲击力")]
        public float BoostImpulse = 5f;
        
        [Tooltip("加速冷却时间（秒）")]
        public float BoostCooldown = 0.4f;
        
        [Tooltip("最大角速度（弧度/秒）")]
        public float MaxAngularVelocity = 12f;
        
        [Tooltip("摆荡阻尼")]
        public float SwingDamping = 0.3f;

        [Header("=== 飞出设置 ===")]
        
        [Tooltip("飞出速度倍率")]
        public float ExitVelocityMultiplier = 1.1f;
        
        [Tooltip("最大飞出速度")]
        public float MaxExitSpeed = 28f;
        
        [Tooltip("最小向上速度")]
        public float MinUpwardBoost = 3f;

        [Header("=== 视觉效果 ===")]
        
        [Tooltip("绳索LineRenderer（可选，会自动创建）")]
        public LineRenderer RopeRenderer;
        
        [Tooltip("绳索宽度")]
        public float RopeWidth = 0.08f;
        
        [Tooltip("绳索颜色")]
        public Color RopeColor = new Color(0.8f, 0.6f, 0.4f);
        
        [Tooltip("钩爪预制体（可选）")]
        public GameObject HookPrefab;
        
        [Tooltip("钩爪发射偏移")]
        public Vector2 HookOffset = new Vector2(0.3f, 0.3f);

        [Header("=== 反馈 ===")]
        public MMFeedbacks FireFeedback;
        public MMFeedbacks HitFeedback;
        public MMFeedbacks ReleaseFeedback;
        public MMFeedbacks BoostFeedback;

        // === 状态 ===
        public bool IsSwinging => _isSwinging;
        public bool IsFiring => _isFiring;
        
        // 内部状态
        protected bool _isSwinging;
        protected bool _isFiring;
        protected Vector2 _grapplePoint;
        protected float _ropeLength;
        protected float _currentAngle;
        protected float _angularVelocity;
        
        // 钩爪飞行
        protected Vector2 _hookPosition;
        protected Vector2 _hookTarget;
        protected GameObject _hookInstance;
        
        // 加速CD
        protected float _lastBoostTime = -999f;
        
        // 缓存
        protected CharacterRun _runAbility;
        protected Vector2 _lastPosition;
        
        // 动画参数
        protected const string _swingingParam = "Swinging";
        protected const string _firingParam = "GrappleFiring";
        protected int _swingingHash;
        protected int _firingHash;

        #region 初始化

        protected override void Initialization()
        {
            base.Initialization();
            _runAbility = _character?.FindAbility<CharacterRun>();
            SetupRope();
        }

        protected virtual void SetupRope()
        {
            if (RopeRenderer == null)
            {
                var ropeObj = new GameObject("GrappleRope");
                ropeObj.transform.SetParent(transform);
                RopeRenderer = ropeObj.AddComponent<LineRenderer>();
            }
            
            RopeRenderer.startWidth = RopeWidth;
            RopeRenderer.endWidth = RopeWidth;
            RopeRenderer.positionCount = 2;
            RopeRenderer.material = new Material(Shader.Find("Sprites/Default"));
            RopeRenderer.startColor = RopeColor;
            RopeRenderer.endColor = RopeColor;
            RopeRenderer.sortingOrder = 100;
            RopeRenderer.enabled = false;
        }

        #endregion

        #region 输入处理

        protected override void HandleInput()
        {
            if (_inputManager == null) return;
            
            if (_inputManager.SecondaryShootButton.State.CurrentState == MMInput.ButtonStates.ButtonDown)
            {
                TryFireGrapple();
            }
            
            if (_inputManager.SecondaryShootButton.State.CurrentState == MMInput.ButtonStates.ButtonUp)
            {
                if (_isSwinging || _isFiring)
                {
                    Release();
                }
            }
        }

        #endregion

        #region 主循环

        public override void ProcessAbility()
        {
            base.ProcessAbility();
            
            if (_isFiring)
            {
                ProcessHookFlight();
            }
            
            if (_isSwinging)
            {
                ProcessSwing();
            }
            
            UpdateRopeVisual();
        }

        #endregion

        #region 发射钩爪

        protected virtual void TryFireGrapple()
        {
            if (!AbilityAuthorized || _isSwinging || _isFiring) return;
            
            if (_movement.CurrentState == CharacterStates.MovementStates.Gripping ||
                _movement.CurrentState == CharacterStates.MovementStates.LedgeHanging ||
                _movement.CurrentState == CharacterStates.MovementStates.Dashing)
                return;
            
            Vector2 aimDir = GetAimDirection();
            Vector2? target = FindGrappleTarget(aimDir);
            
            if (target.HasValue)
            {
                _hookTarget = target.Value;
                StartFiring();
            }
        }

        protected virtual Vector2 GetAimDirection()
        {
            Vector3 mouseScreenPos = Input.mousePosition;
            Camera cam = Camera.main;
            
            if (cam != null)
            {
                mouseScreenPos.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
                Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);
                
                Vector2 dirToMouse = (Vector2)mouseWorldPos - (Vector2)transform.position;
                
                if (dirToMouse.magnitude > 0.1f)
                {
                    return dirToMouse.normalized;
                }
            }
            
            float facing = _character.IsFacingRight ? 1f : -1f;
            return new Vector2(facing * 0.6f, 0.8f).normalized;
        }

        protected virtual Vector2? FindGrappleTarget(Vector2 aimDir)
        {
            Vector2 origin = (Vector2)transform.position + HookOffset;
            
            int rayCount = 7;
            float halfAngle = GrappleSearchAngle * 0.5f;
            
            RaycastHit2D bestHit = default;
            float bestScore = float.MaxValue;
            
            for (int i = 0; i < rayCount; i++)
            {
                float t = rayCount > 1 ? (float)i / (rayCount - 1) : 0.5f;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                Vector2 rayDir = RotateVector(aimDir, angle);
                
                RaycastHit2D hit = Physics2D.Raycast(origin, rayDir, MaxGrappleDistance, GrappleLayerMask);
                
                Color debugColor = hit.collider != null ? Color.green : Color.red;
                Debug.DrawRay(origin, rayDir * MaxGrappleDistance, debugColor, 0.3f);
                
                if (hit.collider != null)
                {
                    float angleScore = Mathf.Abs(angle) / halfAngle;
                    float distScore = hit.distance / MaxGrappleDistance;
                    float score = angleScore * 0.4f + distScore * 0.6f;
                    
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestHit = hit;
                    }
                }
            }
            
            return bestHit.collider != null ? (Vector2?)bestHit.point : null;
        }

        protected Vector2 RotateVector(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        protected virtual void StartFiring()
        {
            _isFiring = true;
            _hookPosition = (Vector2)transform.position + HookOffset;
            
            if (HookPrefab != null)
            {
                _hookInstance = Instantiate(HookPrefab, _hookPosition, Quaternion.identity);
                Vector2 dir = (_hookTarget - _hookPosition).normalized;
                _hookInstance.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }
            
            RopeRenderer.enabled = true;
            FireFeedback?.PlayFeedbacks(transform.position);
            PlayAbilityStartFeedbacks();
        }

        protected virtual void ProcessHookFlight()
        {
            Vector2 dir = (_hookTarget - _hookPosition).normalized;
            float dist = Vector2.Distance(_hookPosition, _hookTarget);
            float moveDist = HookTravelSpeed * Time.deltaTime;
            
            if (moveDist >= dist)
            {
                _hookPosition = _hookTarget;
                OnHookHit();
            }
            else
            {
                _hookPosition += dir * moveDist;
                
                if (_hookInstance != null)
                {
                    _hookInstance.transform.position = _hookPosition;
                }
            }
        }

        protected virtual void OnHookHit()
        {
            _isFiring = false;
            _grapplePoint = _hookTarget;
            
            if (_hookInstance != null)
            {
                _hookInstance.transform.position = _grapplePoint;
            }
            
            HitFeedback?.PlayFeedbacks(_grapplePoint);
            StartSwinging();
        }

        #endregion

        #region 摆荡

        protected virtual void StartSwinging()
        {
            _isSwinging = true;
            
            Vector2 toPlayer = (Vector2)transform.position - _grapplePoint;
            _ropeLength = toPlayer.magnitude;
            _currentAngle = Mathf.Atan2(toPlayer.x, -toPlayer.y);
            
            Vector2 currentVel = _controller.Speed;
            Vector2 tangent = new Vector2(-toPlayer.y, toPlayer.x).normalized;
            float tangentSpeed = Vector2.Dot(currentVel, tangent);
            _angularVelocity = tangentSpeed / _ropeLength;
            
            _movement.ChangeState(CharacterStates.MovementStates.Swinging);
            _controller.GravityActive(false);
            
            _lastPosition = transform.position;
            _lastBoostTime = -999f;
        }

        protected virtual void ProcessSwing()
        {
            // === 1. 重力 ===
            float gravityAccel = -(SwingGravity / _ropeLength) * Mathf.Sin(_currentAngle);
            
            // === 2. 加速输入（Shift+方向键，按一下给冲击，有CD） ===
            bool shiftDown = _inputManager.RunButton.State.CurrentState == MMInput.ButtonStates.ButtonDown;
            bool canBoost = (Time.time - _lastBoostTime) >= BoostCooldown;
            
            if (shiftDown && canBoost && Mathf.Abs(_horizontalInput) > 0.1f)
            {
                float boostDirection = Mathf.Sign(_horizontalInput);
                float impulse = BoostImpulse / _ropeLength;
                
                // 直接设置角速度到目标方向
                _angularVelocity = boostDirection * Mathf.Max(Mathf.Abs(_angularVelocity), impulse);
                
                // 如果方向相反，覆盖
                if (Mathf.Sign(_angularVelocity) != boostDirection)
                {
                    _angularVelocity = boostDirection * impulse;
                }
                
                _lastBoostTime = Time.time;
                BoostFeedback?.PlayFeedbacks(transform.position);
            }
            
            // === 3. 更新角速度 ===
            _angularVelocity += gravityAccel * Time.deltaTime;
            _angularVelocity *= (1f - SwingDamping * Time.deltaTime);
            _angularVelocity = Mathf.Clamp(_angularVelocity, -MaxAngularVelocity, MaxAngularVelocity);
            
            // === 4. 计算新位置 ===
            float newAngle = _currentAngle + _angularVelocity * Time.deltaTime;
            Vector2 newPos;
            newPos.x = _grapplePoint.x + Mathf.Sin(newAngle) * _ropeLength;
            newPos.y = _grapplePoint.y - Mathf.Cos(newAngle) * _ropeLength;
            
            // === 5. 碰撞检测 ===
            Vector2 currentPos = transform.position;
            Vector2 moveDir = (newPos - currentPos);
            float moveDist = moveDir.magnitude;
            
            if (moveDist > 0.001f)
            {
                moveDir = moveDir.normalized;
                
                BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
                Vector2 boxSize = boxCollider != null ? boxCollider.size * 0.9f : new Vector2(0.5f, 0.8f);
                
                RaycastHit2D hit = Physics2D.BoxCast(
                    currentPos, boxSize, 0f, moveDir,
                    moveDist + 0.05f, _controller.PlatformMask
                );
                
                if (hit.collider != null && hit.distance < moveDist)
                {
                    float safeDistance = Mathf.Max(0, hit.distance - 0.1f);
                    Vector2 safePos = currentPos + moveDir * safeDistance;
                    
                    Vector2 slideDir = Vector2.Perpendicular(hit.normal);
                    float slideAmount = Vector2.Dot(moveDir * (moveDist - safeDistance), slideDir);
                    
                    if (Mathf.Abs(slideAmount) > 0.01f)
                    {
                        RaycastHit2D slideHit = Physics2D.BoxCast(
                            safePos, boxSize, 0f,
                            slideDir * Mathf.Sign(slideAmount),
                            Mathf.Abs(slideAmount), _controller.PlatformMask
                        );
                        
                        if (slideHit.collider == null)
                        {
                            safePos += slideDir * slideAmount * 0.8f;
                        }
                    }
                    
                    newPos = safePos;
                    
                    Vector2 toPlayer = newPos - _grapplePoint;
                    newAngle = Mathf.Atan2(toPlayer.x, -toPlayer.y);
                    
                    _angularVelocity *= 0.3f;
                    
                    float dotProduct = Vector2.Dot(moveDir, hit.normal);
                    if (dotProduct < -0.5f)
                    {
                        _angularVelocity = -_angularVelocity * 0.5f;
                    }
                }
            }
            
            // === 6. 应用位置 ===
            _currentAngle = newAngle;
            _controller.SetTransformPosition(newPos);
            _controller.SetForce(Vector2.zero);
            
            _lastPosition = newPos;
        }

        #endregion

        #region 释放

        protected virtual void Release()
        {
            if (_isFiring)
            {
                CancelFiring();
                return;
            }
            
            if (!_isSwinging) return;
            
            Vector2 exitVelocity = CalculateExitVelocity();
            
            _isSwinging = false;
            _angularVelocity = 0f;
            
            _controller.GravityActive(true);
            _controller.SetForce(exitVelocity);
            
            _movement.ChangeState(CharacterStates.MovementStates.Falling);
            
            CleanupVisuals();
            
            ReleaseFeedback?.PlayFeedbacks(transform.position);
            StopStartFeedbacks();
            PlayAbilityStopFeedbacks();
        }

        protected virtual Vector2 CalculateExitVelocity()
        {
            float linearSpeed = _angularVelocity * _ropeLength;
            
            Vector2 ropeDir = ((Vector2)transform.position - _grapplePoint).normalized;
            Vector2 tangent = new Vector2(-ropeDir.y, ropeDir.x);
            
            Vector2 velocity = tangent * linearSpeed * ExitVelocityMultiplier;
            
            if (velocity.y > -1f)
            {
                velocity.y = Mathf.Max(velocity.y, MinUpwardBoost);
            }
            
            if (velocity.magnitude > MaxExitSpeed)
            {
                velocity = velocity.normalized * MaxExitSpeed;
            }
            
            return velocity;
        }

        protected virtual void CancelFiring()
        {
            _isFiring = false;
            CleanupVisuals();
            StopStartFeedbacks();
        }

        #endregion

        #region 视觉

        protected virtual void UpdateRopeVisual()
        {
            if (RopeRenderer == null) return;
            
            if (_isFiring || _isSwinging)
            {
                RopeRenderer.enabled = true;
                Vector2 start = (Vector2)transform.position + HookOffset;
                Vector2 end = _isFiring ? _hookPosition : _grapplePoint;
                
                RopeRenderer.SetPosition(0, start);
                RopeRenderer.SetPosition(1, end);
            }
            else
            {
                RopeRenderer.enabled = false;
            }
        }

        protected virtual void CleanupVisuals()
        {
            if (_hookInstance != null)
            {
                Destroy(_hookInstance);
                _hookInstance = null;
            }
            
            if (RopeRenderer != null)
            {
                RopeRenderer.enabled = false;
            }
        }

        #endregion

        #region 安全措施

        public virtual void ForceStop()
        {
            if (_isFiring) CancelFiring();
            
            if (_isSwinging)
            {
                _isSwinging = false;
                _controller.GravityActive(true);
                _movement.ChangeState(CharacterStates.MovementStates.Falling);
            }
            
            CleanupVisuals();
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            ForceStop();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ForceStop();
        }

        public override void ResetAbility()
        {
            base.ResetAbility();
            ForceStop();
        }

        #endregion

        #region 动画

        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(_swingingParam, AnimatorControllerParameterType.Bool, out _swingingHash);
            RegisterAnimatorParameter(_firingParam, AnimatorControllerParameterType.Bool, out _firingHash);
        }

        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _swingingHash, _isSwinging, 
                _character._animatorParameters, _character.PerformAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _firingHash, _isFiring, 
                _character._animatorParameters, _character.PerformAnimatorSanityChecks);
        }

        #endregion
    }
}
