using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 钩爪摆荡能力 - 纯手动物理实现
    /// 按住右键发射钩爪并摆荡，松开飞出
    /// Animator parameters: Swinging (bool), GrappleFiring (bool), GrappleExiting (bool)
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
        
        [Tooltip("钩爪收回速度")]
        public float HookRetractSpeed = 50f;
        
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
        
        [Tooltip("地面检测距离")]
        public float GroundCheckDistance = 0.3f;

        [Header("=== 飞出设置 ===")]
        
        [Tooltip("飞出速度倍率")]
        public float ExitVelocityMultiplier = 1.1f;
        
        [Tooltip("最大飞出速度")]
        public float MaxExitSpeed = 28f;
        
        [Tooltip("最小向上速度")]
        public float MinUpwardBoost = 3f;
        
        [Tooltip("退出动画持续时间（秒），之后恢复正常状态")]
        public float ExitAnimationDuration = 0.5f;
        
        [Tooltip("飞出时的空中微调力度（0=无控制，1=完全控制）")]
        [Range(0f, 1f)]
        public float ExitAirControlStrength = 0.3f;
        
        [Tooltip("空中微调加速度")]
        public float ExitAirControlAcceleration = 15f;


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

        [Header("=== 残影效果 ===")]
        
        [Tooltip("加速时启用残影效果")]
        public bool EnableAfterimageOnBoost = true;
        
        [Tooltip("残影持续时间（秒）")]
        public float AfterimageEffectDuration = 0.3f;
        
        [Tooltip("残影组件引用（留空自动查找/创建）")]
        public AfterimageEffect AfterimageEffect;

        [Header("=== 反馈 ===")]
        public MMFeedbacks FireFeedback;
        public MMFeedbacks HitFeedback;
        public MMFeedbacks MissFeedback;  // 新增：钩爪没命中的反馈
        public MMFeedbacks ReleaseFeedback;
        public MMFeedbacks BoostFeedback;

        // === 状态 ===
        public bool IsSwinging => _isSwinging;
        public bool IsFiring => _isFiring;
        public bool IsExiting => _isExiting;
        public bool IsRetracting => _isRetracting;
        
        // 内部状态
        protected bool _isSwinging;
        protected bool _isFiring;
        protected bool _isRetracting;  // 新增：钩爪正在收回
        protected bool _isExiting;
        protected bool _hasValidTarget;  // 新增：是否有有效目标
        protected Vector2 _grapplePoint;
        protected float _ropeLength;
        protected float _currentAngle;
        protected float _angularVelocity;
        
        // 钩爪飞行
        protected Vector2 _hookPosition;
        protected Vector2 _hookTarget;
        protected Vector2 _hookDirection;  // 新增：钩爪飞行方向
        protected Vector2 _fireOrigin;     // 新增：发射原点
        protected GameObject _hookInstance;
        
        // 加速CD
        protected float _lastBoostTime = -999f;
        
        // 残影效果
        protected float _afterimageEndTime = 0f;
        
        // 退出动画计时
        protected float _exitStartTime = 0f;
        protected Vector2 _exitVelocity;  // 存储飞出速度
        protected bool _applyingExitMomentum;  // 是否正在应用飞出惯性
        
        // 缓存
        protected CharacterRun _runAbility;
        protected Vector2 _lastPosition;
        
        // 动画参数
        protected const string _swingingParam = "Swinging";
        protected const string _firingParam = "GrappleFiring";
        protected const string _exitingParam = "GrappleExiting";
        protected int _swingingHash;
        protected int _firingHash;
        protected int _exitingHash;

        #region 初始化

        protected override void Initialization()
        {
            base.Initialization();
            _runAbility = _character?.FindAbility<CharacterRun>();
            SetupRope();
            SetupAfterimage();
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

        protected virtual void SetupAfterimage()
        {
            if (!EnableAfterimageOnBoost) return;
            
            if (AfterimageEffect == null)
            {
                AfterimageEffect = GetComponent<AfterimageEffect>();
                
                if (AfterimageEffect == null)
                {
                    AfterimageEffect = gameObject.AddComponent<AfterimageEffect>();
                }
            }
            
            AfterimageEffect.EffectEnabled = true;
            AfterimageEffect.SpawnInterval = 0.04f;
            AfterimageEffect.FadeDuration = 0.25f;
            AfterimageEffect.InitialAlpha = 0.6f;
            AfterimageEffect.TintColor = new Color(0.4f, 0.7f, 1f, 1f);
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
                if (_isSwinging || _isFiring || _isRetracting)
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
            
            if (_isRetracting)
            {
                ProcessHookRetract();
            }
            
            if (_isSwinging)
            {
                ProcessSwing();
            }
            
            ProcessExitState();
            ProcessAfterimage();
            UpdateRopeVisual();
        }

protected virtual void ProcessExitState()
        {
            if (!_isExiting) return;
            
            // 在惯性飞行阶段
            if (_applyingExitMomentum)
            {
                float timeSinceExit = Time.time - _exitStartTime;
                float momentumDuration = ExitAnimationDuration * 0.8f;
                
                if (timeSinceExit < momentumDuration)
                {
                    // 计算惯性衰减系数（随时间逐渐减少惯性影响）
                    float momentumFactor = 1f - (timeSinceExit / momentumDuration);
                    
                    // 获取当前速度
                    float currentHorizontal = _controller.Speed.x;
                    float currentVertical = _controller.Speed.y;
                    
                    // 计算玩家输入的微调
                    float inputInfluence = _horizontalInput * ExitAirControlAcceleration * Time.deltaTime;
                    
                    // 混合惯性速度和玩家输入
                    // 惯性速度随时间衰减，玩家控制随时间增强
                    float baseVelocity = _exitVelocity.x * momentumFactor;
                    float playerControl = inputInfluence * (ExitAirControlStrength + (1f - momentumFactor) * (1f - ExitAirControlStrength));
                    
                    float newHorizontal = baseVelocity + currentHorizontal * (1f - momentumFactor) + playerControl;
                    
                    // 限制最大水平速度
                    newHorizontal = Mathf.Clamp(newHorizontal, -MaxExitSpeed, MaxExitSpeed);
                    
                    _controller.SetForce(new Vector2(newHorizontal, currentVertical));
                }
                else
                {
                    // 惯性期结束，允许正常控制
                    _applyingExitMomentum = false;
                }
            }
            
            // 落地或超时后结束退出状态
            if (_controller.State.IsGrounded || (Time.time - _exitStartTime) >= ExitAnimationDuration)
            {
                _isExiting = false;
                _applyingExitMomentum = false;
            }
        }

        protected virtual void ProcessAfterimage()
        {
            if (AfterimageEffect == null) return;
            
            if (Time.time >= _afterimageEndTime)
            {
                AfterimageEffect.StopEffect();
            }
        }

        #endregion

        #region 发射钩爪

        protected virtual void TryFireGrapple()
        {
            if (!AbilityAuthorized || _isSwinging || _isFiring || _isRetracting) return;
            
            if (_movement.CurrentState == CharacterStates.MovementStates.Gripping ||
                _movement.CurrentState == CharacterStates.MovementStates.LedgeHanging ||
                _movement.CurrentState == CharacterStates.MovementStates.Dashing)
                return;
            
            Vector2 aimDir = GetAimDirection();
            Vector2? target = FindGrappleTarget(aimDir);
            
            _fireOrigin = (Vector2)transform.position + HookOffset;
            _hookDirection = aimDir;
            
            if (target.HasValue)
            {
                _hookTarget = target.Value;
                _hasValidTarget = true;
            }
            else
            {
                // 没有目标，设置为最大距离位置
                _hookTarget = _fireOrigin + aimDir * MaxGrappleDistance;
                _hasValidTarget = false;
            }
            
            StartFiring();
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

        protected virtual void FlipTowardsTarget(Vector2 targetPosition)
        {
            float targetX = targetPosition.x;
            float characterX = transform.position.x;
            
            bool targetIsRight = targetX > characterX;
            
            if (targetIsRight && !_character.IsFacingRight)
            {
                _character.Flip();
            }
            else if (!targetIsRight && _character.IsFacingRight)
            {
                _character.Flip();
            }
        }

        protected virtual void StartFiring()
        {
            _isFiring = true;
            _isRetracting = false;
            _isExiting = false;
            _hookPosition = (Vector2)transform.position + HookOffset;
            
            FlipTowardsTarget(_hookTarget);
            
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
            Vector2 currentOrigin = (Vector2)transform.position + HookOffset;
            Vector2 dir = (_hookTarget - _hookPosition).normalized;
            float distToTarget = Vector2.Distance(_hookPosition, _hookTarget);
            float moveDist = HookTravelSpeed * Time.deltaTime;
            
            // 检测飞行途中是否命中可钩物体
            if (!_hasValidTarget)
            {
                RaycastHit2D hit = Physics2D.Raycast(_hookPosition, dir, moveDist + 0.1f, GrappleLayerMask);
                if (hit.collider != null)
                {
                    // 途中命中了！
                    _hookTarget = hit.point;
                    _hasValidTarget = true;
                }
            }
            
            if (moveDist >= distToTarget)
            {
                _hookPosition = _hookTarget;
                
                if (_hasValidTarget)
                {
                    OnHookHit();
                }
                else
                {
                    // 没命中，开始收回
                    StartRetracting();
                }
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

        protected virtual void StartRetracting()
        {
            _isFiring = false;
            _isRetracting = true;
            MissFeedback?.PlayFeedbacks(transform.position);
        }

        protected virtual void ProcessHookRetract()
        {
            Vector2 currentOrigin = (Vector2)transform.position + HookOffset;
            Vector2 dir = (currentOrigin - _hookPosition).normalized;
            float distToOrigin = Vector2.Distance(_hookPosition, currentOrigin);
            float moveDist = HookRetractSpeed * Time.deltaTime;
            
            if (moveDist >= distToOrigin)
            {
                // 收回完成
                _isRetracting = false;
                CleanupVisuals();
                StopStartFeedbacks();
            }
            else
            {
                _hookPosition += dir * moveDist;
                
                if (_hookInstance != null)
                {
                    _hookInstance.transform.position = _hookPosition;
                    Vector2 rotDir = (currentOrigin - _hookPosition).normalized;
                    _hookInstance.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(rotDir.y, rotDir.x) * Mathf.Rad2Deg);
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
            _isExiting = false;
            
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
            
            // === 2. 加速输入 ===
            bool shiftDown = _inputManager.RunButton.State.CurrentState == MMInput.ButtonStates.ButtonDown;
            bool canBoost = (Time.time - _lastBoostTime) >= BoostCooldown;
            
            if (shiftDown && canBoost && Mathf.Abs(_horizontalInput) > 0.1f)
            {
                float boostDirection = Mathf.Sign(_horizontalInput);
                float impulse = BoostImpulse / _ropeLength;
                
                _angularVelocity = boostDirection * Mathf.Max(Mathf.Abs(_angularVelocity), impulse);
                
                if (Mathf.Sign(_angularVelocity) != boostDirection)
                {
                    _angularVelocity = boostDirection * impulse;
                }
                
                _lastBoostTime = Time.time;
                BoostFeedback?.PlayFeedbacks(transform.position);
                TriggerAfterimage();
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
            
            BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
            Vector2 boxSize = boxCollider != null ? boxCollider.size * 0.9f : new Vector2(0.5f, 0.8f);
            float halfHeight = boxSize.y * 0.5f;
            
            if (moveDist > 0.001f)
            {
                moveDir = moveDir.normalized;
                
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
                    
                    // 根据碰撞方向调整角速度
                    float dotProduct = Vector2.Dot(moveDir, hit.normal);
                    if (dotProduct < -0.5f)
                    {
                        _angularVelocity = -_angularVelocity * 0.5f;
                    }
                    else
                    {
                        _angularVelocity *= 0.7f;
                    }
                }
            }
            
            // === 6. 额外的地面穿透修正 ===
            // 向下发射射线检测地面
            RaycastHit2D groundCheck = Physics2D.Raycast(
                newPos, Vector2.down, halfHeight + 0.1f, _controller.PlatformMask
            );
            
            if (groundCheck.collider != null)
            {
                float minY = groundCheck.point.y + halfHeight + 0.02f;
                if (newPos.y < minY)
                {
                    newPos.y = minY;
                    
                    // 重新计算角度
                    Vector2 toPlayer = newPos - _grapplePoint;
                    newAngle = Mathf.Atan2(toPlayer.x, -toPlayer.y);
                    
                    // 如果向下运动，减少角速度
                    if (_angularVelocity * Mathf.Cos(_currentAngle) > 0)
                    {
                        _angularVelocity *= 0.8f;
                    }
                }
            }
            
            // === 7. 应用位置 ===
            _currentAngle = newAngle;
            _controller.SetTransformPosition(newPos);
            _controller.SetForce(Vector2.zero);
            
            _lastPosition = newPos;
        }

        protected virtual void TriggerAfterimage()
        {
            if (!EnableAfterimageOnBoost || AfterimageEffect == null) return;
            
            AfterimageEffect.StartEffect();
            _afterimageEndTime = Time.time + AfterimageEffectDuration;
        }

        #endregion

        #region 释放

protected virtual void Release()
        {
            if (_isFiring || _isRetracting)
            {
                CancelFiring();
                return;
            }
            
            if (!_isSwinging) return;
            
            // 计算并存储飞出速度
            _exitVelocity = CalculateExitVelocity();
            
            _isSwinging = false;
            _angularVelocity = 0f;
            
            _isExiting = true;
            _applyingExitMomentum = true;  // 开始应用惯性
            _exitStartTime = Time.time;
            
            _controller.GravityActive(true);
            
            // 立即设置飞出速度
            _controller.SetForce(_exitVelocity);
            
            _movement.ChangeState(CharacterStates.MovementStates.Falling);
            
            if (AfterimageEffect != null)
            {
                AfterimageEffect.StopEffect();
            }
            
            CleanupVisuals();
            
            ReleaseFeedback?.PlayFeedbacks(transform.position);
            StopStartFeedbacks();
            PlayAbilityStopFeedbacks();
            
            Debug.Log($"[Grapple] Released! Exit velocity: {_exitVelocity}, speed: {_exitVelocity.magnitude}");
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
            _isRetracting = false;
            CleanupVisuals();
            StopStartFeedbacks();
        }

        #endregion

        #region 视觉

        protected virtual void UpdateRopeVisual()
        {
            if (RopeRenderer == null) return;
            
            if (_isFiring || _isRetracting || _isSwinging)
            {
                RopeRenderer.enabled = true;
                Vector2 start = (Vector2)transform.position + HookOffset;
                Vector2 end = (_isFiring || _isRetracting) ? _hookPosition : _grapplePoint;
                
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
            if (_isFiring || _isRetracting) CancelFiring();
            
            if (_isSwinging)
            {
                _isSwinging = false;
                _controller.GravityActive(true);
                _movement.ChangeState(CharacterStates.MovementStates.Falling);
            }
            
            _isExiting = false;
            _applyingExitMomentum = false;
            _exitVelocity = Vector2.zero;
            
            if (AfterimageEffect != null)
            {
                AfterimageEffect.StopEffect();
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
            RegisterAnimatorParameter(_firingParam, AnimatorControllerParameterType.Bool, out _firingHash);
            RegisterAnimatorParameter(_swingingParam, AnimatorControllerParameterType.Bool, out _swingingHash);
            RegisterAnimatorParameter(_exitingParam, AnimatorControllerParameterType.Bool, out _exitingHash);
        }

        public override void UpdateAnimator()
        {
            // 发射和收回都算Firing状态
            bool firingState = _isFiring || _isRetracting;
            
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _firingHash, firingState, 
                _character._animatorParameters, _character.PerformAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _swingingHash, _isSwinging, 
                _character._animatorParameters, _character.PerformAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _exitingHash, _isExiting, 
                _character._animatorParameters, _character.PerformAnimatorSanityChecks);
        }

        #endregion
    }
}
