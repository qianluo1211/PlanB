using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 鼠标瞄准闪现能力 - 按住F键减慢时间，显示瞄准指示器，松开后向鼠标方向冲刺
    /// Animator parameters: Dashing, DashAiming
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Abilities/Character Dash Aim")]
    public class CharacterDashAim : CharacterAbility
    {
        public override string HelpBoxText() { return "鼠标瞄准闪现能力。按住F键减慢时间并显示瞄准指示器（圆圈+射线），松开后向鼠标方向冲刺。"; }

        [Header("=== 闪现设置 ===")]

        [Tooltip("闪现最大距离")]
        public float DashDistance = 5f;

        [Tooltip("闪现力度（影响速度）")]
        public float DashForce = 40f;

        [Tooltip("闪现结束后重置力量")]
        public bool ResetForcesOnExit = true;

        [Tooltip("如果为true，会强制精确到达目标距离")]
        public bool ForceExactDistance = false;

        [Tooltip("闪现时脱离移动平台")]
        public bool DetachFromMovingPlatformsOnDash = true;

        [Header("=== 瞄准设置 ===")]

        [Tooltip("瞄准时的时间缩放（0.01-0.1 推荐，太小可能导致问题）")]
        [Range(0.01f, 1f)]
        public float AimingTimeScale = 0.05f;

        [Tooltip("瞄准时使用的按键")]
        public KeyCode AimKey = KeyCode.F;

        [Tooltip("是否根据闪现方向翻转角色")]
        public bool FlipCharacterIfNeeded = true;

        [Tooltip("最小输入阈值")]
        public float MinimumInputThreshold = 0.1f;

        [Header("=== 冷却 ===")]

        [Tooltip("两次闪现之间的冷却时间（秒）")]
        public float DashCooldown = 1f;

        [Header("=== 使用次数 ===")]

        [Tooltip("是否限制闪现次数")]
        public bool LimitedDashes = false;

        [Tooltip("连续闪现次数")]
        [MMCondition("LimitedDashes", true)]
        public int SuccessiveDashAmount = 1;

        [Tooltip("剩余闪现次数（运行时）")]
        [MMCondition("LimitedDashes", true)]
        [MMReadOnly]
        public int SuccessiveDashesLeft = 1;

        public enum SuccessiveDashResetMethods { Grounded, Time }

        [Tooltip("闪现次数重置方式")]
        [MMCondition("LimitedDashes", true)]
        public SuccessiveDashResetMethods SuccessiveDashResetMethod = SuccessiveDashResetMethods.Grounded;

        [Tooltip("时间重置模式下的重置持续时间")]
        [MMEnumCondition("SuccessiveDashResetMethod", (int)SuccessiveDashResetMethods.Time)]
        public float SuccessiveDashResetDuration = 2f;

        [Header("=== 无敌 ===")]

        [Tooltip("闪现期间无敌")]
        public bool InvincibleWhileDashing = false;

        [Header("=== 瞄准视觉效果 ===")]

        [Tooltip("瞄准线颜色")]
        public Color AimLineColor = new Color(0.5f, 0.8f, 1f, 0.8f);

        [Tooltip("瞄准线宽度")]
        public float AimLineWidth = 0.08f;

        [Tooltip("范围圈颜色")]
        public Color RangeCircleColor = new Color(0.5f, 0.8f, 1f, 0.5f);

        [Tooltip("范围圈宽度")]
        public float RangeCircleWidth = 0.05f;

        [Tooltip("范围圈分段数（越多越圆）")]
        public int RangeCircleSegments = 64;

        [Tooltip("终点指示器颜色")]
        public Color EndPointColor = new Color(1f, 0.8f, 0.5f, 0.9f);

        [Tooltip("终点指示器大小")]
        public float EndPointSize = 0.3f;

        [Header("=== 残影效果 ===")]

        [Tooltip("闪现时启用残影效果")]
        public bool EnableAfterimageOnDash = true;

        [Tooltip("残影持续时间")]
        public float AfterimageEffectDuration = 0.3f;

        [Tooltip("残影组件引用")]
        public AfterimageEffect AfterimageEffect;

        [Header("=== 反馈 ===")]

        public MMFeedbacks AimStartFeedback;
        public MMFeedbacks DashStartFeedback;
        public MMFeedbacks DashStopFeedback;

        // === 状态 ===
        public bool IsAiming => _isAiming;
        public bool IsDashing => _isDashing;

        // 内部状态
        protected bool _isAiming;
        protected bool _isDashing;
        protected float _cooldownTimeStamp = 0;
        protected float _startTime;
        protected Vector2 _initialPosition;
        protected Vector2 _dashDirection;
        protected float _distanceTraveled = 0f;
        protected bool _shouldKeepDashing = true;
        protected float _slopeAngleSave = 0f;
        protected bool _dashEndedNaturally = true;
        protected IEnumerator _dashCoroutine;
        protected CharacterDive _characterDive;
        protected float _lastDashAt = 0f;
        protected float _averageDistancePerFrame;
        protected int _startFrame;
        protected Bounds _bounds;
        protected float _savedTimeScale = 1f;
        protected float _cooldownTimeStampUnscaled = 0f;

        // 瞄准视觉组件
        protected LineRenderer _aimLineRenderer;
        protected LineRenderer _rangeCircleRenderer;
        protected LineRenderer _endPointRenderer;
        protected GameObject _aimVisualsContainer;
        protected Material _lineMaterial;

        // 残影效果
        protected float _afterimageEndTime = 0f;

        // 动画参数
        protected const string _dashingAnimationParameterName = "Dashing";
        protected const string _dashAimingAnimationParameterName = "DashAiming";
        protected int _dashingAnimationParameter;
        protected int _dashAimingAnimationParameter;

        #region 初始化

        protected override void Initialization()
        {
            base.Initialization();
            _characterDive = _character?.FindAbility<CharacterDive>();
            SuccessiveDashesLeft = SuccessiveDashAmount;
            CreateLineMaterial();
            SetupAimVisuals();
            SetupAfterimage();
        }

        protected virtual void CreateLineMaterial()
        {
            // 创建一个简单的无光照材质
            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
            if (_lineMaterial == null)
            {
                // 备用方案：使用内置的 Unlit/Color shader
                _lineMaterial = new Material(Shader.Find("Unlit/Color"));
            }
            if (_lineMaterial == null)
            {
                // 最后的备用方案
                _lineMaterial = new Material(Shader.Find("UI/Default"));
            }
        }

        protected virtual void SetupAimVisuals()
        {
            // 创建瞄准视觉容器
            _aimVisualsContainer = new GameObject("DashAimVisuals");
            _aimVisualsContainer.transform.SetParent(null);

            // 创建瞄准线
            var aimLineObj = new GameObject("AimLine");
            aimLineObj.transform.SetParent(_aimVisualsContainer.transform);
            _aimLineRenderer = aimLineObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_aimLineRenderer, AimLineColor, AimLineWidth, 2);

            // 创建范围圈
            var rangeCircleObj = new GameObject("RangeCircle");
            rangeCircleObj.transform.SetParent(_aimVisualsContainer.transform);
            _rangeCircleRenderer = rangeCircleObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_rangeCircleRenderer, RangeCircleColor, RangeCircleWidth, RangeCircleSegments);
            _rangeCircleRenderer.loop = true;

            // 创建终点指示器（小圆）
            var endPointObj = new GameObject("EndPoint");
            endPointObj.transform.SetParent(_aimVisualsContainer.transform);
            _endPointRenderer = endPointObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_endPointRenderer, EndPointColor, EndPointSize * 0.3f, 16);
            _endPointRenderer.loop = true;

            // 隐藏所有视觉效果
            HideAimVisuals();
        }

        protected virtual void SetupLineRenderer(LineRenderer lr, Color color, float width, int positionCount)
        {
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = positionCount;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.sortingLayerName = "Default";
            lr.sortingOrder = 1000;
            lr.useWorldSpace = true;

            // 初始化位置数组
            Vector3[] positions = new Vector3[positionCount];
            for (int i = 0; i < positionCount; i++)
            {
                positions[i] = Vector3.zero;
            }
            lr.SetPositions(positions);
        }

        protected virtual void SetupAfterimage()
        {
            if (!EnableAfterimageOnDash) return;

            if (AfterimageEffect == null)
            {
                AfterimageEffect = GetComponent<AfterimageEffect>();

                if (AfterimageEffect == null)
                {
                    AfterimageEffect = gameObject.AddComponent<AfterimageEffect>();
                }
            }

            AfterimageEffect.EffectEnabled = true;
            AfterimageEffect.SpawnInterval = 0.03f;
            AfterimageEffect.FadeDuration = 0.2f;
            AfterimageEffect.InitialAlpha = 0.7f;
            AfterimageEffect.TintColor = new Color(0.6f, 0.9f, 1f, 1f);
        }

        #endregion

        #region Unity生命周期 - 关键：使用Update处理瞄准

        protected virtual void Update()
        {
            // 使用 Unity 的 Update 来处理瞄准，不受 timeScale 影响的输入检测
            HandleAimingInput();

            // 瞄准时更新视觉效果
            if (_isAiming)
            {
                UpdateAimVisuals();
            }
        }

        protected virtual void HandleAimingInput()
        {
            // 直接使用 Input.GetKeyDown/Up，不受 InputManager 影响
            if (Input.GetKeyDown(AimKey))
            {
                StartAiming();
            }

            if (Input.GetKeyUp(AimKey))
            {
                if (_isAiming)
                {
                    ExecuteDash();
                }
            }
        }

        #endregion

        #region 主循环（受timeScale影响的逻辑）

        public override void ProcessAbility()
        {
            base.ProcessAbility();

            if (_isDashing)
            {
                ProcessDashing();
            }

            ProcessAfterimage();
            HandleAmountOfDashesLeft();
        }

        protected virtual void ProcessDashing()
        {
            // 闪现期间禁用重力
            if (_movement.CurrentState == CharacterStates.MovementStates.Dashing)
            {
                _controller.GravityActive(false);
            }

            // 如果闪现非正常结束，恢复斜坡角度
            if (!_dashEndedNaturally && _movement.CurrentState != CharacterStates.MovementStates.Dashing)
            {
                _dashEndedNaturally = true;
                _controller.Parameters.MaximumSlopeAngle = _slopeAngleSave;
            }
        }

        protected virtual void ProcessAfterimage()
        {
            if (AfterimageEffect == null) return;

            if (Time.unscaledTime >= _afterimageEndTime)
            {
                AfterimageEffect.StopEffect();
            }
        }

        #endregion

        #region 瞄准

        protected virtual void StartAiming()
        {
            if (!DashAuthorized()) return;
            if (!DashConditions()) return;

            _isAiming = true;

            // 保存并设置时间缩放
            _savedTimeScale = Time.timeScale;
            Time.timeScale = AimingTimeScale;

            // 显示瞄准视觉效果
            ShowAimVisuals();

            // 立即更新一次视觉效果
            UpdateAimVisuals();

            // 播放反馈
            AimStartFeedback?.PlayFeedbacks(transform.position);

            Debug.Log("[CharacterDashAim] 开始瞄准，时间缩放: " + Time.timeScale);
        }

        protected virtual void UpdateAimVisuals()
        {
            if (!_isAiming) return;
            if (_aimLineRenderer == null || _rangeCircleRenderer == null || _endPointRenderer == null) return;

            Vector2 playerPos = transform.position;
            Vector2 aimDirection = GetAimDirection();
            Vector2 targetPos = playerPos + aimDirection * DashDistance;

            // 更新瞄准线
            _aimLineRenderer.SetPosition(0, new Vector3(playerPos.x, playerPos.y, 0));
            _aimLineRenderer.SetPosition(1, new Vector3(targetPos.x, targetPos.y, 0));

            // 更新范围圈
            UpdateRangeCircle(playerPos);

            // 更新终点指示器
            UpdateEndPoint(targetPos);
        }

        protected virtual void UpdateRangeCircle(Vector2 center)
        {
            if (_rangeCircleRenderer == null) return;

            int segments = RangeCircleSegments;
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = center.x + Mathf.Cos(angle) * DashDistance;
                float y = center.y + Mathf.Sin(angle) * DashDistance;
                _rangeCircleRenderer.SetPosition(i, new Vector3(x, y, 0));
            }
        }

        protected virtual void UpdateEndPoint(Vector2 center)
        {
            if (_endPointRenderer == null) return;

            int segments = 16;
            float angleStep = 360f / segments;
            float radius = EndPointSize;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = center.x + Mathf.Cos(angle) * radius;
                float y = center.y + Mathf.Sin(angle) * radius;
                _endPointRenderer.SetPosition(i, new Vector3(x, y, 0));
            }
        }

        protected virtual Vector2 GetAimDirection()
        {
            Camera cam = Camera.main;

            if (cam != null)
            {
                Vector3 mouseScreenPos = Input.mousePosition;
                mouseScreenPos.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
                Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

                Vector2 dirToMouse = (Vector2)mouseWorldPos - (Vector2)transform.position;

                if (dirToMouse.magnitude > MinimumInputThreshold)
                {
                    return dirToMouse.normalized;
                }
            }

            // 默认方向为角色面朝方向
            float facing = _character.IsFacingRight ? 1f : -1f;
            return new Vector2(facing, 0f);
        }

        protected virtual void ShowAimVisuals()
        {
            if (_aimLineRenderer != null)
            {
                _aimLineRenderer.enabled = true;
                Debug.Log("[CharacterDashAim] 显示瞄准线");
            }
            if (_rangeCircleRenderer != null)
            {
                _rangeCircleRenderer.enabled = true;
                Debug.Log("[CharacterDashAim] 显示范围圈");
            }
            if (_endPointRenderer != null)
            {
                _endPointRenderer.enabled = true;
                Debug.Log("[CharacterDashAim] 显示终点指示器");
            }
        }

        protected virtual void HideAimVisuals()
        {
            if (_aimLineRenderer != null) _aimLineRenderer.enabled = false;
            if (_rangeCircleRenderer != null) _rangeCircleRenderer.enabled = false;
            if (_endPointRenderer != null) _endPointRenderer.enabled = false;
        }

        #endregion

        #region 闪现执行

        protected virtual void ExecuteDash()
        {
            if (!_isAiming) return;

            _isAiming = false;

            // 恢复时间
            Time.timeScale = _savedTimeScale;

            // 隐藏瞄准视觉效果
            HideAimVisuals();

            // 获取闪现方向
            _dashDirection = GetAimDirection();

            Debug.Log("[CharacterDashAim] 执行闪现，方向: " + _dashDirection);

            // 开始闪现
            InitiateDash();
        }

        public virtual bool DashConditions()
        {
            // 使用 unscaled time 进行冷却检查
            if (_cooldownTimeStampUnscaled > Time.unscaledTime)
            {
                return false;
            }

            // 次数检查
            if (LimitedDashes && SuccessiveDashesLeft <= 0)
            {
                return false;
            }

            return true;
        }

        public virtual bool DashAuthorized()
        {
            if (!AbilityAuthorized
                || (_condition.CurrentState != CharacterStates.CharacterConditions.Normal)
                || (_movement.CurrentState == CharacterStates.MovementStates.LedgeHanging)
                || (_movement.CurrentState == CharacterStates.MovementStates.Gripping)
                || (_movement.CurrentState == CharacterStates.MovementStates.Dashing))
            {
                return false;
            }

            return true;
        }

        public virtual void InitiateDash()
        {
            if (DetachFromMovingPlatformsOnDash)
            {
                _controller.DetachFromMovingPlatform();
            }

            // 设置闪现状态
            _isDashing = true;
            _movement.ChangeState(CharacterStates.MovementStates.Dashing);

            // 播放反馈
            PlayAbilityStartFeedbacks();
            DashStartFeedback?.PlayFeedbacks(transform.position);
            MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.Dash, MMCharacterEvent.Moments.Start);

            // 初始化闪现参数
            _startTime = Time.time;
            _startFrame = Time.frameCount;
            _dashEndedNaturally = false;
            _initialPosition = _characterTransform.position;
            _distanceTraveled = 0;
            _shouldKeepDashing = true;
            _cooldownTimeStamp = Time.time + DashCooldown;
            _cooldownTimeStampUnscaled = Time.unscaledTime + DashCooldown;
            _lastDashAt = Time.time;

            if (LimitedDashes)
            {
                SuccessiveDashesLeft -= 1;
            }

            if (InvincibleWhileDashing)
            {
                _health.DamageDisabled();
            }

            // 保存并设置斜坡角度
            _slopeAngleSave = _controller.Parameters.MaximumSlopeAngle;
            _controller.Parameters.MaximumSlopeAngle = 0;
            _controller.SlowFall(0f);

            // 翻转角色
            CheckFlipCharacter();

            // 触发残影效果
            TriggerAfterimage();

            // 启动闪现协程
            _dashCoroutine = Dash();
            StartCoroutine(_dashCoroutine);
        }

        protected virtual void CheckFlipCharacter()
        {
            if (FlipCharacterIfNeeded && (Mathf.Abs(_dashDirection.x) > 0.05f))
            {
                if (_character.IsFacingRight != (_dashDirection.x > 0f))
                {
                    _character.Flip();
                }
            }
        }

        protected virtual IEnumerator Dash()
        {
            if (!AbilityAuthorized || (_condition.CurrentState != CharacterStates.CharacterConditions.Normal))
            {
                yield break;
            }

            while (_distanceTraveled < DashDistance
                   && _shouldKeepDashing
                   && TestForLevelBounds()
                   && TestForExactDistance()
                   && _movement.CurrentState == CharacterStates.MovementStates.Dashing)
            {
                _distanceTraveled = Vector3.Distance(_initialPosition, _characterTransform.position);

                // 碰撞检测
                if ((_controller.State.IsCollidingLeft && _dashDirection.x < -0.1f)
                    || (_controller.State.IsCollidingRight && _dashDirection.x > 0.1f)
                    || (_controller.State.IsCollidingAbove && _dashDirection.y > 0.1f)
                    || (_controller.State.IsCollidingBelow && _dashDirection.y < -0.1f))
                {
                    _shouldKeepDashing = false;
                    _controller.SetForce(Vector2.zero);
                }
                else
                {
                    _controller.GravityActive(false);
                    _controller.SetForce(_dashDirection * DashForce);
                }

                yield return null;
            }

            StopDash();
        }

        protected virtual bool TestForLevelBounds()
        {
            if (!_controller.State.TouchingLevelBounds)
            {
                return true;
            }
            else
            {
                if (LevelManager.Instance == null) return true;
                _bounds = LevelManager.Instance.LevelBounds;
                return (_character.IsFacingRight) ? (_character.transform.position.x < _bounds.center.x) : (_character.transform.position.x > _bounds.center.x);
            }
        }

        protected virtual bool TestForExactDistance()
        {
            if (!ForceExactDistance)
            {
                return true;
            }

            int framesSinceStart = Time.frameCount - _startFrame;
            if (framesSinceStart <= 0) return true;

            _averageDistancePerFrame = _distanceTraveled / framesSinceStart;

            if (DashDistance - _distanceTraveled < _averageDistancePerFrame)
            {
                _characterTransform.position = _initialPosition + (_dashDirection * DashDistance);
                return false;
            }

            return true;
        }

        public virtual void StopDash()
        {
            if (_dashCoroutine != null)
            {
                StopCoroutine(_dashCoroutine);
            }

            _isDashing = false;

            // 恢复设置
            _controller.DefaultParameters.MaximumSlopeAngle = _slopeAngleSave;
            _controller.Parameters.MaximumSlopeAngle = _slopeAngleSave;
            _controller.GravityActive(true);
            _dashEndedNaturally = true;

            // 重置力量
            if (ResetForcesOnExit)
            {
                _controller.SetForce(Vector2.zero);
            }

            if (InvincibleWhileDashing)
            {
                _health.DamageEnabled();
            }

            // 播放反馈
            StopStartFeedbacks();
            DashStopFeedback?.PlayFeedbacks(transform.position);
            MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.Dash, MMCharacterEvent.Moments.End);
            PlayAbilityStopFeedbacks();

            // 恢复移动状态
            if (_movement.CurrentState == CharacterStates.MovementStates.Dashing)
            {
                if (_controller.State.IsGrounded)
                {
                    _movement.ChangeState(CharacterStates.MovementStates.Idle);
                }
                else
                {
                    _movement.RestorePreviousState();
                }
            }
        }

        protected virtual void TriggerAfterimage()
        {
            if (!EnableAfterimageOnDash || AfterimageEffect == null) return;

            AfterimageEffect.StartEffect();
            _afterimageEndTime = Time.unscaledTime + AfterimageEffectDuration;
        }

        #endregion

        #region 闪现次数管理

        protected virtual void HandleAmountOfDashesLeft()
        {
            if (!LimitedDashes) return;
            if ((SuccessiveDashesLeft >= SuccessiveDashAmount) || (Time.time - _lastDashAt < DashCooldown))
            {
                return;
            }

            switch (SuccessiveDashResetMethod)
            {
                case SuccessiveDashResetMethods.Time:
                    if (Time.time - _lastDashAt > SuccessiveDashResetDuration)
                    {
                        SetSuccessiveDashesLeft(SuccessiveDashAmount);
                    }
                    break;
                case SuccessiveDashResetMethods.Grounded:
                    if (_controller.State.IsGrounded)
                    {
                        SetSuccessiveDashesLeft(SuccessiveDashAmount);
                    }
                    break;
            }
        }

        public virtual void SetSuccessiveDashesLeft(int newAmount)
        {
            SuccessiveDashesLeft = newAmount;
        }

        #endregion

        #region 动画

        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(_dashingAnimationParameterName, AnimatorControllerParameterType.Bool, out _dashingAnimationParameter);
            RegisterAnimatorParameter(_dashAimingAnimationParameterName, AnimatorControllerParameterType.Bool, out _dashAimingAnimationParameter);
        }

        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _dashingAnimationParameter,
                (_movement.CurrentState == CharacterStates.MovementStates.Dashing),
                _character._animatorParameters, _character.PerformAnimatorSanityChecks);
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _dashAimingAnimationParameter,
                _isAiming,
                _character._animatorParameters, _character.PerformAnimatorSanityChecks);
        }

        #endregion

        #region 清理

        public virtual void ForceStopAiming()
        {
            if (_isAiming)
            {
                _isAiming = false;
                Time.timeScale = _savedTimeScale;
                HideAimVisuals();
            }
        }

        public virtual void ForceStop()
        {
            ForceStopAiming();

            if (_isDashing)
            {
                StopDash();
            }
        }

        public override void ResetAbility()
        {
            base.ResetAbility();
            ForceStop();

            if (_animator != null)
            {
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _dashingAnimationParameter, false, _character._animatorParameters, _character.PerformAnimatorSanityChecks);
                MMAnimatorExtensions.UpdateAnimatorBool(_animator, _dashAimingAnimationParameter, false, _character._animatorParameters, _character.PerformAnimatorSanityChecks);
            }
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

        protected virtual void OnDestroy()
        {
            // 清理瞄准视觉对象
            if (_aimVisualsContainer != null)
            {
                Destroy(_aimVisualsContainer);
            }

            // 清理材质
            if (_lineMaterial != null)
            {
                Destroy(_lineMaterial);
            }
        }

        #endregion
    }
}