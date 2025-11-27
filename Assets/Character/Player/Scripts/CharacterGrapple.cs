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
        public override string HelpBoxText() { return "钩爪摆荡能力。按住右键发射钩爪，命中后开始摆荡，方向键+Shift加速，松开飞出。"; }

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
        
        [Tooltip("方向键加速力")]
        public float SwingAcceleration = 8f;
        
        [Tooltip("Shift加速倍率")]
        public float BoostMultiplier = 2.5f;
        
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

        // === 状态 ===
        public bool IsSwinging => _isSwinging;
        public bool IsFiring => _isFiring;
        
        // 内部状态
        protected bool _isSwinging;
        protected bool _isFiring;
        protected Vector2 _grapplePoint;
        protected float _ropeLength;
        protected float _currentAngle;      // 弧度，0 = 正下方
        protected float _angularVelocity;   // 弧度/秒
        
        // 钩爪飞行
        protected Vector2 _hookPosition;
        protected Vector2 _hookTarget;
        protected GameObject _hookInstance;
        
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
            
            // 右键按下 - 发射钩爪
            if (_inputManager.SecondaryShootButton.State.CurrentState == MMInput.ButtonStates.ButtonDown)
            {
                TryFireGrapple();
            }
            
            // 右键松开 - 释放
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
            
            // 不能在某些状态下使用
            if (_movement.CurrentState == CharacterStates.MovementStates.Gripping ||
                _movement.CurrentState == CharacterStates.MovementStates.LedgeHanging ||
                _movement.CurrentState == CharacterStates.MovementStates.Dashing)
                return;
            
            // 寻找钩点
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
            // 获取鼠标世界坐标
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
            
            // 备用：使用面朝方向
            float facing = _character.IsFacingRight ? 1f : -1f;
            return new Vector2(facing * 0.6f, 0.8f).normalized;
        }

        protected virtual Vector2? FindGrappleTarget(Vector2 aimDir)
        {
            Vector2 origin = (Vector2)transform.position + HookOffset;
            
            // 在锥形范围内发射多条射线
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
                
                // Debug显示
                Color debugColor = hit.collider != null ? Color.green : Color.red;
                Debug.DrawRay(origin, rayDir * MaxGrappleDistance, debugColor, 0.3f);
                
                if (hit.collider != null)
                {
                    // 评分：优先选择正对方向且距离适中的点
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
            
            // 生成钩爪视觉
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
            // 移动钩爪
            Vector2 dir = (_hookTarget - _hookPosition).normalized;
            float dist = Vector2.Distance(_hookPosition, _hookTarget);
            float moveDist = HookTravelSpeed * Time.deltaTime;
            
            if (moveDist >= dist)
            {
                // 到达目标
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
            
            // 销毁或隐藏钩爪
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
            
            // 计算初始状态
            Vector2 toPlayer = (Vector2)transform.position - _grapplePoint;
            _ropeLength = toPlayer.magnitude;
            
            // 角度：0 = 正下方，正值 = 右侧，负值 = 左侧
            _currentAngle = Mathf.Atan2(toPlayer.x, -toPlayer.y);
            
            // 将当前速度转换为角速度
            Vector2 currentVel = _controller.Speed;
            Vector2 tangent = new Vector2(-toPlayer.y, toPlayer.x).normalized;
            float tangentSpeed = Vector2.Dot(currentVel, tangent);
            _angularVelocity = tangentSpeed / _ropeLength;
            
            // 改变状态
            _movement.ChangeState(CharacterStates.MovementStates.Swinging);
            
            // 禁用重力（我们手动处理）
            _controller.GravityActive(false);
            
            _lastPosition = transform.position;
        }

protected virtual void ProcessSwing()
        {
            // === 1. 计算重力引起的角加速度 ===
            float gravityAccel = -(SwingGravity / _ropeLength) * Mathf.Sin(_currentAngle);
            
            // === 2. 玩家输入加速 ===
            float inputAccel = 0f;
            if (Mathf.Abs(_horizontalInput) > 0.1f)
            {
                inputAccel = SwingAcceleration * Mathf.Sign(_horizontalInput);
                
                if (_runAbility != null && 
                    _inputManager.RunButton.State.CurrentState == MMInput.ButtonStates.ButtonPressed)
                {
                    inputAccel *= BoostMultiplier;
                }
                
                inputAccel = inputAccel / _ropeLength;
            }
            
            // === 3. 更新角速度 ===
            _angularVelocity += (gravityAccel + inputAccel) * Time.deltaTime;
            _angularVelocity *= (1f - SwingDamping * Time.deltaTime);
            _angularVelocity = Mathf.Clamp(_angularVelocity, -MaxAngularVelocity, MaxAngularVelocity);
            
            // === 4. 计算新角度和位置 ===
            float newAngle = _currentAngle + _angularVelocity * Time.deltaTime;
            Vector2 newPos;
            newPos.x = _grapplePoint.x + Mathf.Sin(newAngle) * _ropeLength;
            newPos.y = _grapplePoint.y - Mathf.Cos(newAngle) * _ropeLength;
            
            // === 5. 碰撞检测（使用BoxCast检测角色体积） ===
            Vector2 currentPos = transform.position;
            Vector2 moveDir = (newPos - currentPos);
            float moveDist = moveDir.magnitude;
            
            if (moveDist > 0.001f)
            {
                moveDir = moveDir.normalized;
                
                // 获取角色碰撞体尺寸
                BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
                Vector2 boxSize = boxCollider != null ? boxCollider.size * 0.9f : new Vector2(0.5f, 0.8f);
                
                // BoxCast检测
                RaycastHit2D hit = Physics2D.BoxCast(
                    currentPos,
                    boxSize,
                    0f,
                    moveDir,
                    moveDist + 0.05f,
                    _controller.PlatformMask
                );
                
                if (hit.collider != null && hit.distance < moveDist)
                {
                    // 撞到东西了
                    
                    // 计算安全位置（在碰撞点之前）
                    float safeDistance = Mathf.Max(0, hit.distance - 0.1f);
                    Vector2 safePos = currentPos + moveDir * safeDistance;
                    
                    // 尝试沿墙滑动
                    Vector2 slideDir = Vector2.Perpendicular(hit.normal);
                    float slideAmount = Vector2.Dot(moveDir * (moveDist - safeDistance), slideDir);
                    
                    // 检查滑动方向是否安全
                    if (Mathf.Abs(slideAmount) > 0.01f)
                    {
                        RaycastHit2D slideHit = Physics2D.BoxCast(
                            safePos,
                            boxSize,
                            0f,
                            slideDir * Mathf.Sign(slideAmount),
                            Mathf.Abs(slideAmount),
                            _controller.PlatformMask
                        );
                        
                        if (slideHit.collider == null)
                        {
                            safePos += slideDir * slideAmount * 0.8f;
                        }
                    }
                    
                    newPos = safePos;
                    
                    // 根据新位置重新计算角度
                    Vector2 toPlayer = newPos - _grapplePoint;
                    newAngle = Mathf.Atan2(toPlayer.x, -toPlayer.y);
                    
                    // 减少角速度（碰撞损失）
                    _angularVelocity *= 0.3f;
                    
                    // 如果正面撞墙，反弹
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
            
            // 计算飞出速度
            Vector2 exitVelocity = CalculateExitVelocity();
            
            // 清理状态
            _isSwinging = false;
            _angularVelocity = 0f;
            
            // 恢复控制器
            _controller.GravityActive(true);
            _controller.SetForce(exitVelocity);
            
            // 改变状态
            _movement.ChangeState(CharacterStates.MovementStates.Falling);
            
            // 清理视觉
            CleanupVisuals();
            
            ReleaseFeedback?.PlayFeedbacks(transform.position);
            StopStartFeedbacks();
            PlayAbilityStopFeedbacks();
        }

        protected virtual Vector2 CalculateExitVelocity()
        {
            // 线速度 = 角速度 × 半径
            float linearSpeed = _angularVelocity * _ropeLength;
            
            // 切线方向（垂直于绳子）
            Vector2 ropeDir = ((Vector2)transform.position - _grapplePoint).normalized;
            Vector2 tangent = new Vector2(-ropeDir.y, ropeDir.x);
            
            // 速度方向与角速度方向一致
            Vector2 velocity = tangent * linearSpeed * ExitVelocityMultiplier;
            
            // 保证最小向上速度（如果在上升阶段）
            if (velocity.y > -1f)
            {
                velocity.y = Mathf.Max(velocity.y, MinUpwardBoost);
            }
            
            // 限制最大速度
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
