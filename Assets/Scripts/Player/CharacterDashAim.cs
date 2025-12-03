using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 两段式攻击Dash能力
    /// 按住F键进入瞄准模式（时间减慢），松开执行第一段Dash攻击
    /// 如果击杀敌人 → 自动进入第二段Dash
    /// 如果未击杀 → 播放Miss动画并结束
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Abilities/Character Dash Aim")]
    public class CharacterDashAim : CharacterAbility
    {
        public override string HelpBoxText() { return "两段式攻击Dash。按住F键瞄准（时间减慢），松开执行攻击。击杀敌人后自动进入第二段Dash。"; }

        [Header("=== 第一段Dash ===")]
        [Tooltip("第一段Dash距离")]
        public float DashDistance = 5f;
        [Tooltip("第一段Dash力度")]
        public float DashForce = 40f;

        [Header("=== 第二段Dash ===")]
        [Tooltip("第二段Dash距离")]
        public float SecondDashDistance = 4f;
        [Tooltip("第二段Dash力度")]
        public float SecondDashForce = 35f;
        [Tooltip("击杀成功后进入第二段的延迟时间")]
        public float SecondDashDelay = 0.15f;
        [Tooltip("第一段结束后等待击杀判定的时间")]
        public float KillCheckWindow = 0.1f;

        [Header("=== 伤害设置 ===")]
        [Tooltip("Dash造成的伤害")]
        public float DashDamage = 999f;
        [Tooltip("可被伤害的层级")]
        public LayerMask DamageLayerMask = 1 << 13;
        [Tooltip("伤害区域大小")]
        public Vector2 DamageBoxSize = new Vector2(1.2f, 1.8f);
        [Tooltip("伤害区域偏移")]
        public Vector2 DamageBoxOffset = new Vector2(0.6f, 0f);
        [Tooltip("击退力度")]
        public Vector2 KnockbackForce = new Vector2(10f, 5f);

        [Header("=== 瞄准设置 ===")]
        [Tooltip("瞄准时的时间缩放")]
        [Range(0.01f, 1f)]
        public float AimingTimeScale = 0.05f;
        [Tooltip("瞄准按键")]
        public KeyCode AimKey = KeyCode.F;
        [Tooltip("是否根据瞄准方向翻转角色")]
        public bool FlipCharacterIfNeeded = true;
        [Tooltip("最小输入阈值")]
        public float MinimumInputThreshold = 0.1f;
        [Tooltip("取消瞄准/Dash的按键")]
        public KeyCode CancelKey = KeyCode.Mouse1;
        [Tooltip("取消后延迟恢复钩爪的时间（防止误触发）")]
        public float CancelGrappleDelay = 0.15f;



        [Header("=== 能力禁用设置 ===")]
        [Tooltip("瞄准和Dash期间禁用武器")]
        public bool DisableWeaponWhileActive = true;
        [Tooltip("瞄准和Dash期间禁用跳跃")]
        public bool DisableJumpWhileActive = true;
        [Tooltip("瞄准和Dash期间禁用移动")]
        public bool DisableMovementWhileAiming = true;
        [Tooltip("瞄准和Dash期间禁用钩爪")]
        public bool DisableGrappleWhileActive = true;

        [Header("=== 冷却与限制 ===")]
        [Tooltip("Dash冷却时间")]
        public float DashCooldown = 1f;
        [Tooltip("Dash期间无敌")]
        public bool InvincibleWhileDashing = true;
        [Tooltip("Dash结束后重置速度")]
        public bool ResetForcesOnExit = true;

        [Header("=== 瞄准视觉效果 ===")]
        public Color AimLineColor = new Color(0.5f, 0.8f, 1f, 0.8f);
        public float AimLineWidth = 0.08f;
        public Color RangeCircleColor = new Color(0.5f, 0.8f, 1f, 0.5f);
        public float RangeCircleWidth = 0.05f;
        public int RangeCircleSegments = 64;
        public Color EndPointColor = new Color(1f, 0.8f, 0.5f, 0.9f);
        public float EndPointSize = 0.3f;

        [Header("=== 残影效果 ===")]
        [Tooltip("启用残影")]
        public bool EnableAfterimageOnDash = true;
        [Tooltip("残影持续时间")]
        public float AfterimageEffectDuration = 0.3f;
        public AfterimageEffect AfterimageEffect;

        [Header("=== 反馈 ===")]
        public MMFeedbacks AimStartFeedback;
        public MMFeedbacks DashStartFeedback;
        public MMFeedbacks DashStopFeedback;
        public MMFeedbacks EnemyHitFeedback;
        public MMFeedbacks EnemyKillFeedback;
        public MMFeedbacks SecondDashFeedback;
        public MMFeedbacks CancelFeedback;


        [Header("=== 音效 ===")]
        [Tooltip("Dash攻击音效")]
        public AudioClip[] DashAttackSounds;
        [Tooltip("音效音量")]
        [Range(0f, 1f)]
        public float SoundVolume = 1f;
        [Tooltip("音效音调随机范围")]
        public Vector2 PitchRange = new Vector2(0.95f, 1.05f);
        
        protected AudioSource _audioSource;

        public enum DashPhaseEnum { None = 0, FirstDash = 1, WaitingForKill = 2, SecondDash = 3 }

        public bool IsAiming => _isAiming;
        public bool IsDashing => _currentPhase != DashPhaseEnum.None;
        public DashPhaseEnum CurrentPhase => _currentPhase;

        // === 充能系统便利属性（供UI使用） ===
        /// <summary>获取充能管理器引用</summary>
        public DashChargeManager ChargeManager => _chargeManager;
        
        /// <summary>当前充能数量</summary>
        public int CurrentCharges => _chargeManager != null ? _chargeManager.Charges : 0;
        
        /// <summary>最大充能数量</summary>
        public int MaxCharges => _chargeManager != null ? _chargeManager.MaxCharges : 0;
        
        /// <summary>是否有足够充能使用Dash</summary>
        public bool HasSufficientCharge => _chargeManager == null || _chargeManager.HasSufficientCharge;
        
        /// <summary>充能百分比 (0-1)</summary>
        public float ChargePercentage => _chargeManager != null ? _chargeManager.ChargePercentage : 1f;


        protected bool _isAiming;
        protected DashPhaseEnum _currentPhase = DashPhaseEnum.None;
        protected float _savedTimeScale = 1f;
        protected float _cooldownTimeStampUnscaled = 0f;
        
        protected Vector2 _dashDirection;
        protected Vector2 _initialPosition;
        protected float _distanceTraveled = 0f;
        protected float _currentDashDistance;
        protected float _currentDashForce;
        protected float _slopeAngleSave = 0f;
        protected IEnumerator _dashCoroutine;
        
        protected bool _hasKilledEnemy;
        protected bool _hasHitEnemy;
        
        protected GameObject _damageZone;
        protected BoxCollider2D _damageCollider;
        protected DamageOnTouch _damageOnTouch;
        
        protected LineRenderer _aimLineRenderer;
        protected LineRenderer _rangeCircleRenderer;
        protected LineRenderer _endPointRenderer;
        protected GameObject _aimVisualsContainer;
        protected Material _lineMaterial;
        
        protected float _afterimageEndTime = 0f;
        
        protected const string _dashingParamName = "Dashing";
        protected const string _dashAimingParamName = "DashAiming";
        protected const string _dashPhaseParamName = "DashPhase";
        protected int _dashingParam;
        protected int _dashAimingParam;
        protected int _dashPhaseParam;
        
        protected CharacterHandleWeapon _handleWeapon;
        protected CharacterJump _jump;
        protected CharacterHorizontalMovement _horizontalMovement;
        protected CharacterGrapple _grapple;
        protected DashChargeManager _chargeManager;


protected override void Initialization()
        {
            base.Initialization();
            CreateLineMaterial();
            SetupAimVisuals();
            SetupAfterimage();
            CreateDamageZone();
            SetupAudioSource();
            
            _handleWeapon = _character?.FindAbility<CharacterHandleWeapon>();
            _jump = _character?.FindAbility<CharacterJump>();
            _horizontalMovement = _character?.FindAbility<CharacterHorizontalMovement>();
            _grapple = _character?.FindAbility<CharacterGrapple>();
            _chargeManager = _character?.FindAbility<DashChargeManager>();
        }

        protected virtual void CreateDamageZone()
        {
            _damageZone = new GameObject("DashDamageZone");
            _damageZone.transform.SetParent(transform);
            _damageZone.transform.localPosition = Vector3.zero;
            _damageZone.layer = gameObject.layer;
            
            // 添加标记，让DashChargeManager忽略Dash击杀
            _damageZone.AddComponent<DashDamageMarker>();

            _damageCollider = _damageZone.AddComponent<BoxCollider2D>();
            _damageCollider.size = DamageBoxSize;
            _damageCollider.offset = DamageBoxOffset;
            _damageCollider.isTrigger = true;

            _damageOnTouch = _damageZone.AddComponent<DamageOnTouch>();
            _damageOnTouch.TargetLayerMask = DamageLayerMask;
            _damageOnTouch.MinDamageCaused = DashDamage;
            _damageOnTouch.MaxDamageCaused = DashDamage;
            _damageOnTouch.DamageCausedKnockbackType = DamageOnTouch.KnockbackStyles.SetForce;
            _damageOnTouch.DamageCausedKnockbackForce = KnockbackForce;
            _damageOnTouch.InvincibilityDuration = 0.2f;
            _damageOnTouch.Owner = gameObject;

            _damageOnTouch.OnHitDamageable += OnHitEnemy;
            _damageOnTouch.OnKill += OnKillEnemy;

            _damageZone.SetActive(false);
        }

        protected virtual void CreateLineMaterial()
        {
            _lineMaterial = new Material(Shader.Find("Sprites/Default"));
            if (_lineMaterial == null)
                _lineMaterial = new Material(Shader.Find("Unlit/Color"));
            if (_lineMaterial == null)
                _lineMaterial = new Material(Shader.Find("UI/Default"));
        }

        protected virtual void SetupAimVisuals()
        {
            _aimVisualsContainer = new GameObject("DashAimVisuals");
            _aimVisualsContainer.transform.SetParent(null);

            var aimLineObj = new GameObject("AimLine");
            aimLineObj.transform.SetParent(_aimVisualsContainer.transform);
            _aimLineRenderer = aimLineObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_aimLineRenderer, AimLineColor, AimLineWidth, 2);

            var rangeCircleObj = new GameObject("RangeCircle");
            rangeCircleObj.transform.SetParent(_aimVisualsContainer.transform);
            _rangeCircleRenderer = rangeCircleObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_rangeCircleRenderer, RangeCircleColor, RangeCircleWidth, RangeCircleSegments);
            _rangeCircleRenderer.loop = true;

            var endPointObj = new GameObject("EndPoint");
            endPointObj.transform.SetParent(_aimVisualsContainer.transform);
            _endPointRenderer = endPointObj.AddComponent<LineRenderer>();
            SetupLineRenderer(_endPointRenderer, EndPointColor, EndPointSize * 0.3f, 16);
            _endPointRenderer.loop = true;

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
            lr.SetPositions(new Vector3[positionCount]);
        }

        protected virtual void SetupAfterimage()
        {
            if (!EnableAfterimageOnDash) return;

            if (AfterimageEffect == null)
            {
                AfterimageEffect = GetComponent<AfterimageEffect>();
                if (AfterimageEffect == null)
                    AfterimageEffect = gameObject.AddComponent<AfterimageEffect>();
            }

            AfterimageEffect.EffectEnabled = true;
            AfterimageEffect.SpawnInterval = 0.03f;
            AfterimageEffect.FadeDuration = 0.2f;
            AfterimageEffect.InitialAlpha = 0.7f;
            AfterimageEffect.TintColor = new Color(0.6f, 0.9f, 1f, 1f);
        }

        protected virtual void SetupAudioSource()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        protected virtual void DisableOtherAbilities()
        {
            if (DisableWeaponWhileActive && _handleWeapon != null)
                _handleWeapon.AbilityPermitted = false;
            if (DisableJumpWhileActive && _jump != null)
                _jump.AbilityPermitted = false;
            if (DisableMovementWhileAiming && _horizontalMovement != null)
                _horizontalMovement.AbilityPermitted = false;
            if (DisableGrappleWhileActive && _grapple != null)
                _grapple.AbilityPermitted = false;
        }
        
        protected virtual void EnableOtherAbilities()
        {
            if (DisableWeaponWhileActive && _handleWeapon != null)
                _handleWeapon.AbilityPermitted = true;
            if (DisableJumpWhileActive && _jump != null)
                _jump.AbilityPermitted = true;
            if (DisableMovementWhileAiming && _horizontalMovement != null)
                _horizontalMovement.AbilityPermitted = true;
            if (DisableGrappleWhileActive && _grapple != null)
                _grapple.AbilityPermitted = true;
        }

        public virtual void PlayDashAttackSound()
        {
            if (DashAttackSounds == null || DashAttackSounds.Length == 0) return;
            if (_audioSource == null) return;
            
            AudioClip clip = DashAttackSounds[Random.Range(0, DashAttackSounds.Length)];
            if (clip == null) return;
            
            _audioSource.pitch = Random.Range(PitchRange.x, PitchRange.y);
            _audioSource.PlayOneShot(clip, SoundVolume);
        }

        public virtual void PlayDashSoundByIndex(int index)
        {
            if (DashAttackSounds == null || DashAttackSounds.Length == 0) return;
            if (_audioSource == null) return;
            if (index < 0 || index >= DashAttackSounds.Length) return;
            
            AudioClip clip = DashAttackSounds[index];
            if (clip == null) return;
            
            _audioSource.pitch = Random.Range(PitchRange.x, PitchRange.y);
            _audioSource.PlayOneShot(clip, SoundVolume);
        }

        protected virtual void Update()
        {
            HandleAimingInput();
            if (_isAiming)
                UpdateAimVisuals();
        }

protected virtual void HandleAimingInput()
        {
            // 检测取消输入
            if (Input.GetKeyDown(CancelKey))
            {
                if (_isAiming || _currentPhase != DashPhaseEnum.None)
                {
                    ForceStopWithGrappleDelay();
                    CancelFeedback?.PlayFeedbacks(transform.position);
                    return;
                }
            }
            
            if (Input.GetKeyDown(AimKey))
                StartAiming();
            if (Input.GetKeyUp(AimKey) && _isAiming)
                ExecuteDash();
        }

        public override void ProcessAbility()
        {
            base.ProcessAbility();
            ProcessAfterimage();
        }

        protected virtual void ProcessAfterimage()
        {
            if (AfterimageEffect == null) return;
            if (_afterimageEndTime <= 0f) return;
            
            if (Time.unscaledTime >= _afterimageEndTime)
            {
                AfterimageEffect.StopEffect();
                _afterimageEndTime = 0f;
            }
        }

        protected virtual void StartAiming()
        {
            if (!CanStartDash()) return;

            _isAiming = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = AimingTimeScale;

            DisableOtherAbilities();
            ShowAimVisuals();
            UpdateAimVisuals();

            AimStartFeedback?.PlayFeedbacks(transform.position);
        }

protected virtual bool CanStartDash()
        {
            if (!AbilityAuthorized) return false;
            if (_condition.CurrentState != CharacterStates.CharacterConditions.Normal) return false;
            if (_movement.CurrentState == CharacterStates.MovementStates.Dashing) return false;
            if (Time.unscaledTime < _cooldownTimeStampUnscaled) return false;
            
            // 检查充能系统
            if (_chargeManager != null && !_chargeManager.CanUseDash())
            {
                return false;
            }
            
            return true;
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
                    return dirToMouse.normalized;
            }
            return new Vector2(_character.IsFacingRight ? 1f : -1f, 0f);
        }

        protected virtual void UpdateAimVisuals()
        {
            if (!_isAiming) return;

            Vector2 playerPos = transform.position;
            Vector2 aimDirection = GetAimDirection();
            Vector2 targetPos = playerPos + aimDirection * DashDistance;

            if (_aimLineRenderer != null)
            {
                _aimLineRenderer.SetPosition(0, new Vector3(playerPos.x, playerPos.y, 0));
                _aimLineRenderer.SetPosition(1, new Vector3(targetPos.x, targetPos.y, 0));
            }

            if (_rangeCircleRenderer != null)
            {
                float angleStep = 360f / RangeCircleSegments;
                for (int i = 0; i < RangeCircleSegments; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    float x = playerPos.x + Mathf.Cos(angle) * DashDistance;
                    float y = playerPos.y + Mathf.Sin(angle) * DashDistance;
                    _rangeCircleRenderer.SetPosition(i, new Vector3(x, y, 0));
                }
            }

            if (_endPointRenderer != null)
            {
                float angleStep = 360f / 16;
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    float x = targetPos.x + Mathf.Cos(angle) * EndPointSize;
                    float y = targetPos.y + Mathf.Sin(angle) * EndPointSize;
                    _endPointRenderer.SetPosition(i, new Vector3(x, y, 0));
                }
            }
        }

        protected virtual void ShowAimVisuals()
        {
            if (_aimLineRenderer != null) _aimLineRenderer.enabled = true;
            if (_rangeCircleRenderer != null) _rangeCircleRenderer.enabled = true;
            if (_endPointRenderer != null) _endPointRenderer.enabled = true;
        }

        protected virtual void HideAimVisuals()
        {
            if (_aimLineRenderer != null) _aimLineRenderer.enabled = false;
            if (_rangeCircleRenderer != null) _rangeCircleRenderer.enabled = false;
            if (_endPointRenderer != null) _endPointRenderer.enabled = false;
        }

protected virtual void ExecuteDash()
        {
            if (!_isAiming) return;

            _isAiming = false;
            Time.timeScale = _savedTimeScale;
            HideAimVisuals();

            // 消耗充能
            if (_chargeManager != null && !_chargeManager.TryConsumeCharges())
            {
                // 充能不足，取消Dash
                EnableOtherAbilities();
                return;
            }

            _dashDirection = GetAimDirection();
            StartFirstDash();
        }

        protected virtual void StartFirstDash()
        {
            _currentPhase = DashPhaseEnum.FirstDash;
            _hasKilledEnemy = false;
            _hasHitEnemy = false;
            _currentDashDistance = DashDistance;
            _currentDashForce = DashForce;

            InitializeDash();

            if (_animator != null)
            {
                _animator.SetTrigger("DashStart");
                _animator.SetInteger(_dashPhaseParam, 1);
            }

            DashStartFeedback?.PlayFeedbacks(transform.position);
            
            _dashCoroutine = DashCoroutine();
            StartCoroutine(_dashCoroutine);
        }

        protected virtual void StartSecondDash()
        {
            _currentPhase = DashPhaseEnum.SecondDash;
            _hasKilledEnemy = false;
            _hasHitEnemy = false;
            _currentDashDistance = SecondDashDistance;
            _currentDashForce = SecondDashForce;

            _initialPosition = transform.position;
            _distanceTraveled = 0f;

            if (_animator != null)
            {
                _animator.SetTrigger("DashHit");
                _animator.SetInteger(_dashPhaseParam, 2);
            }

            SecondDashFeedback?.PlayFeedbacks(transform.position);

            SetDamageZoneActive(true);
            TriggerAfterimage();

            _dashCoroutine = DashCoroutine();
            StartCoroutine(_dashCoroutine);
        }

        protected virtual void InitializeDash()
        {
            _movement.ChangeState(CharacterStates.MovementStates.Dashing);

            PlayAbilityStartFeedbacks();
            MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.Dash, MMCharacterEvent.Moments.Start);

            _initialPosition = transform.position;
            _distanceTraveled = 0;
            _cooldownTimeStampUnscaled = Time.unscaledTime + DashCooldown;

            if (InvincibleWhileDashing && _health != null)
                _health.DamageDisabled();

            _slopeAngleSave = _controller.Parameters.MaximumSlopeAngle;
            _controller.Parameters.MaximumSlopeAngle = 0;
            _controller.SlowFall(0f);

            if (FlipCharacterIfNeeded && Mathf.Abs(_dashDirection.x) > 0.05f)
            {
                if (_character.IsFacingRight != (_dashDirection.x > 0f))
                    _character.Flip();
            }

            SetDamageZoneActive(true);
            TriggerAfterimage();
        }

        protected virtual IEnumerator DashCoroutine()
        {
            _controller.GravityActive(false);

            while (_distanceTraveled < _currentDashDistance && 
                   _movement.CurrentState == CharacterStates.MovementStates.Dashing)
            {
                _distanceTraveled = Vector2.Distance(_initialPosition, transform.position);

                bool hitWall = (_controller.State.IsCollidingLeft && _dashDirection.x < -0.1f) ||
                               (_controller.State.IsCollidingRight && _dashDirection.x > 0.1f) ||
                               (_controller.State.IsCollidingAbove && _dashDirection.y > 0.1f) ||
                               (_controller.State.IsCollidingBelow && _dashDirection.y < -0.1f);

                if (hitWall)
                {
                    _controller.SetForce(Vector2.zero);
                    break;
                }

                _controller.SetForce(_dashDirection * _currentDashForce);
                UpdateDamageZoneDirection();

                yield return null;
            }

            OnDashMovementEnd();
        }

        protected virtual void OnDashMovementEnd()
        {
            SetDamageZoneActive(false);
            _controller.SetForce(Vector2.zero);

            if (_currentPhase == DashPhaseEnum.FirstDash)
            {
                StartCoroutine(CheckKillAndDecide());
            }
            else if (_currentPhase == DashPhaseEnum.SecondDash)
            {
                StopDash();
            }
        }

        protected virtual IEnumerator CheckKillAndDecide()
        {
            _currentPhase = DashPhaseEnum.WaitingForKill;

            yield return new WaitForSeconds(KillCheckWindow);

            if (_hasKilledEnemy)
            {
                EnemyKillFeedback?.PlayFeedbacks(transform.position);
                yield return new WaitForSeconds(SecondDashDelay);
                StartSecondDash();
            }
            else
            {
                if (_animator != null)
                    _animator.SetTrigger("DashMiss");
                StopDash();
            }
        }

        public virtual void StopDash()
        {
            if (_dashCoroutine != null)
            {
                StopCoroutine(_dashCoroutine);
                _dashCoroutine = null;
            }

            _currentPhase = DashPhaseEnum.None;

            _controller.DefaultParameters.MaximumSlopeAngle = _slopeAngleSave;
            _controller.Parameters.MaximumSlopeAngle = _slopeAngleSave;
            _controller.GravityActive(true);

            if (ResetForcesOnExit)
                _controller.SetForce(Vector2.zero);

            if (InvincibleWhileDashing && _health != null)
                _health.DamageEnabled();

            SetDamageZoneActive(false);
            EnableOtherAbilities();

            StopStartFeedbacks();
            DashStopFeedback?.PlayFeedbacks(transform.position);
            MMCharacterEvent.Trigger(_character, MMCharacterEventTypes.Dash, MMCharacterEvent.Moments.End);
            PlayAbilityStopFeedbacks();

            ResetAnimatorState();

            if (_movement.CurrentState == CharacterStates.MovementStates.Dashing)
            {
                _movement.ChangeState(_controller.State.IsGrounded 
                    ? CharacterStates.MovementStates.Idle 
                    : CharacterStates.MovementStates.Falling);
            }
        }

        protected virtual void SetDamageZoneActive(bool active)
        {
            if (_damageZone != null)
                _damageZone.SetActive(active);
        }

        protected virtual void UpdateDamageZoneDirection()
        {
            if (_damageCollider == null) return;
            float sign = _dashDirection.x >= 0 ? 1f : -1f;
            _damageCollider.offset = new Vector2(DamageBoxOffset.x * sign, DamageBoxOffset.y);
        }

        protected virtual void OnHitEnemy()
        {
            _hasHitEnemy = true;
            EnemyHitFeedback?.PlayFeedbacks(transform.position);
        }

        protected virtual void OnKillEnemy()
        {
            _hasKilledEnemy = true;
        }

        protected virtual void TriggerAfterimage()
        {
            if (!EnableAfterimageOnDash || AfterimageEffect == null) return;
            AfterimageEffect.StartEffect();
            _afterimageEndTime = Time.unscaledTime + AfterimageEffectDuration;
        }

        protected override void InitializeAnimatorParameters()
        {
            RegisterAnimatorParameter(_dashingParamName, AnimatorControllerParameterType.Bool, out _dashingParam);
            RegisterAnimatorParameter(_dashAimingParamName, AnimatorControllerParameterType.Bool, out _dashAimingParam);
            RegisterAnimatorParameter(_dashPhaseParamName, AnimatorControllerParameterType.Int, out _dashPhaseParam);
        }

        public override void UpdateAnimator()
        {
            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _dashingParam,
                (_movement.CurrentState == CharacterStates.MovementStates.Dashing),
                _character._animatorParameters, _character.PerformAnimatorSanityChecks);

            MMAnimatorExtensions.UpdateAnimatorBool(_animator, _dashAimingParam,
                _isAiming,
                _character._animatorParameters, _character.PerformAnimatorSanityChecks);
        }

        protected virtual void ResetAnimatorState()
        {
            if (_animator == null) return;
            
            _animator.ResetTrigger("DashStart");
            _animator.ResetTrigger("DashHit");
            _animator.ResetTrigger("DashMiss");
            
            _animator.SetBool(_dashingParam, false);
            _animator.SetBool(_dashAimingParam, false);
            _animator.SetInteger(_dashPhaseParam, 0);
        }

        public virtual void ForceStopAiming()
        {
            if (_isAiming)
            {
                _isAiming = false;
                Time.timeScale = _savedTimeScale;
                HideAimVisuals();
                
                if (_currentPhase == DashPhaseEnum.None)
                {
                    EnableOtherAbilities();
                }
            }
        }

        public virtual void ForceStop()
        {
            ForceStopAiming();
            
            if (_currentPhase != DashPhaseEnum.None)
            {
                if (_dashCoroutine != null)
                {
                    StopCoroutine(_dashCoroutine);
                    _dashCoroutine = null;
                }
                
                _currentPhase = DashPhaseEnum.None;
                
                _controller.DefaultParameters.MaximumSlopeAngle = _slopeAngleSave;
                _controller.Parameters.MaximumSlopeAngle = _slopeAngleSave;
                _controller.GravityActive(true);
                _controller.SetForce(Vector2.zero);
                
                if (InvincibleWhileDashing && _health != null)
                    _health.DamageEnabled();
                
                SetDamageZoneActive(false);
                EnableOtherAbilities();
                ResetAnimatorState();
                
                if (_movement.CurrentState == CharacterStates.MovementStates.Dashing)
                {
                    _movement.ChangeState(_controller.State.IsGrounded 
                        ? CharacterStates.MovementStates.Idle 
                        : CharacterStates.MovementStates.Falling);
                }
            }
        }

/// <summary>
        /// 强制停止，但延迟恢复钩爪能力（防止取消时误触发钩爪）
        /// </summary>
        public virtual void ForceStopWithGrappleDelay()
        {
            // 先保存钩爪状态
            bool grappleWasDisabled = DisableGrappleWhileActive && _grapple != null;
            
            // 处理瞄准状态
            if (_isAiming)
            {
                _isAiming = false;
                Time.timeScale = _savedTimeScale;
                HideAimVisuals();
            }
            
            // 处理Dash状态
            if (_currentPhase != DashPhaseEnum.None)
            {
                if (_dashCoroutine != null)
                {
                    StopCoroutine(_dashCoroutine);
                    _dashCoroutine = null;
                }
                
                _currentPhase = DashPhaseEnum.None;
                
                _controller.DefaultParameters.MaximumSlopeAngle = _slopeAngleSave;
                _controller.Parameters.MaximumSlopeAngle = _slopeAngleSave;
                _controller.GravityActive(true);
                _controller.SetForce(Vector2.zero);
                
                if (InvincibleWhileDashing && _health != null)
                    _health.DamageEnabled();
                
                SetDamageZoneActive(false);
                ResetAnimatorState();
                
                if (_movement.CurrentState == CharacterStates.MovementStates.Dashing)
                {
                    _movement.ChangeState(_controller.State.IsGrounded 
                        ? CharacterStates.MovementStates.Idle 
                        : CharacterStates.MovementStates.Falling);
                }
            }
            
            // 恢复除钩爪外的其他能力
            if (DisableWeaponWhileActive && _handleWeapon != null)
                _handleWeapon.AbilityPermitted = true;
            if (DisableJumpWhileActive && _jump != null)
                _jump.AbilityPermitted = true;
            if (DisableMovementWhileAiming && _horizontalMovement != null)
                _horizontalMovement.AbilityPermitted = true;
            
            // 延迟恢复钩爪
            if (grappleWasDisabled)
            {
                StartCoroutine(DelayedGrappleEnable());
            }
        }

/// <summary>
        /// 延迟恢复钩爪能力
        /// </summary>
        protected virtual IEnumerator DelayedGrappleEnable()
        {
            yield return new WaitForSeconds(CancelGrappleDelay);
            
            if (_grapple != null)
            {
                _grapple.AbilityPermitted = true;
            }
        }



        public override void ResetAbility()
        {
            base.ResetAbility();
            ForceStop();
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
            if (_damageOnTouch != null)
            {
                _damageOnTouch.OnHitDamageable -= OnHitEnemy;
                _damageOnTouch.OnKill -= OnKillEnemy;
            }

            if (_damageZone != null) Destroy(_damageZone);
            if (_aimVisualsContainer != null) Destroy(_aimVisualsContainer);
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }
    }
}
