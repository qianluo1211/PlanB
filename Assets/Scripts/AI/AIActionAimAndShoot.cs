using System.Collections;
using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.CorgiEngine;

/// <summary>
/// AI动作：先瞄准一段时间，显示激光预警线，然后射击
/// 支持两种瞄准完成触发方式：
/// 1. 固定时间 (AimDuration)
/// 2. 动画事件 (在瞄准动画最后一帧调用 OnAimingComplete)
/// </summary>
[AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Aim And Shoot")]
public class AIActionAimAndShoot : AIAction
{
    [Header("Aiming Settings")]
    [Tooltip("是否使用动画事件触发射击（否则使用固定时间）")]
    public bool UseAnimationEvent = true;
    
    [Tooltip("瞄准持续时间（仅在 UseAnimationEvent = false 时生效）")]
    public float AimDuration = 1.5f;
    
    [Tooltip("瞄准时是否面向目标")]
    public bool FaceTarget = true;
    
    [Tooltip("瞄准时是否追踪目标（激光跟随目标移动）")]
    public bool TrackTargetWhileAiming = true;

    [Header("Shooting Settings")]
    [Tooltip("瞄准完成后的射击次数")]
    public int ShotsToFire = 1;
    
    [Tooltip("多次射击之间的间隔")]
    public float TimeBetweenShots = 0.2f;
    
    [Tooltip("射击后是否立即隐藏激光")]
    public bool HideLaserAfterShoot = true;

    [Header("Laser Settings")]
    [Tooltip("是否使用激光瞄准线")]
    public bool UseLaser = true;
    
    [Tooltip("激光颜色渐变（从瞄准到准备射击）")]
    public bool LerpLaserColor = true;
    
    [Tooltip("即将射击时激光闪烁")]
    public bool FlashBeforeShoot = true;
    
    [Tooltip("开始闪烁的时机（0-1，1表示最后时刻）")]
    public float FlashStartProgress = 0.8f;

    [Header("Animation Parameters")]
    [Tooltip("是否使用动画")]
    public bool UseAnimations = true;
    
    [Tooltip("瞄准动画参数名（Bool）")]
    public string AimingParameter = "Aiming";
    
    [Tooltip("射击动画参数名（Bool）")]
    public string ShootingParameter = "Shooting";

    [Header("Target Offset")]
    [Tooltip("瞄准目标的偏移量")]
    public Vector3 TargetOffset = Vector3.zero;

    [Header("References")]
    [Tooltip("CharacterHandleWeapon组件（如果为空会自动查找）")]
    public CharacterHandleWeapon TargetHandleWeapon;

    // 内部状态
    protected Character _character;
    protected Animator _animator;
    protected WeaponAim _weaponAim;
    protected WeaponAimingLaser _aimingLaser;
    protected ProjectileWeapon _projectileWeapon;
    
    protected float _aimStartTime;
    protected bool _isAiming = false;
    protected bool _isShooting = false;
    protected bool _hasFired = false;
    protected Vector3 _weaponAimDirection;
    protected Coroutine _shootCoroutine;

    // 动画参数哈希
    protected int _aimingParameterHash;
    protected int _shootingParameterHash;

    /// <summary>
    /// 初始化
    /// </summary>
    public override void Initialization()
    {
        if (!ShouldInitialize) return;
        
        _character = GetComponentInParent<Character>();
        _animator = GetComponentInParent<Animator>();
        
        if (TargetHandleWeapon == null)
        {
            TargetHandleWeapon = _character?.FindAbility<CharacterHandleWeapon>();
        }

        // 缓存动画参数哈希
        if (UseAnimations)
        {
            _aimingParameterHash = Animator.StringToHash(AimingParameter);
            _shootingParameterHash = Animator.StringToHash(ShootingParameter);
        }
    }

    /// <summary>
    /// 进入状态时
    /// </summary>
    public override void OnEnterState()
    {
        base.OnEnterState();
        
        _isAiming = true;
        _isShooting = false;
        _hasFired = false;
        _aimStartTime = Time.time;

        // 获取武器组件
        if (TargetHandleWeapon?.CurrentWeapon != null)
        {
            _weaponAim = TargetHandleWeapon.CurrentWeapon.gameObject.GetComponent<WeaponAim>();
            _aimingLaser = TargetHandleWeapon.CurrentWeapon.gameObject.GetComponent<WeaponAimingLaser>();
            _projectileWeapon = TargetHandleWeapon.CurrentWeapon.gameObject.GetComponent<ProjectileWeapon>();
        }

        // 激活激光
        if (UseLaser && _aimingLaser != null)
        {
            _aimingLaser.ActivateLaser();
            if (_brain.Target != null)
            {
                _aimingLaser.SetTarget(_brain.Target);
            }
        }

        // 播放瞄准动画
        SetAnimationParameter(_aimingParameterHash, true);
    }

    /// <summary>
    /// 执行动作
    /// </summary>
    public override void PerformAction()
    {
        if (_brain.Target == null) return;

        // 始终面向目标
        if (FaceTarget)
        {
            FaceTheTarget();
        }

        // 始终瞄准目标（射击时也要追踪）
        AimAtTarget();

        // 瞄准阶段的逻辑
        if (_isAiming)
        {
            // 更新激光效果
            if (UseLaser && _aimingLaser != null)
            {
                float aimProgress;
                if (UseAnimationEvent)
                {
                    // 使用动画事件时，用动画的 normalizedTime 作为进度
                    aimProgress = GetAimingAnimationProgress();
                }
                else
                {
                    // 使用固定时间
                    aimProgress = (Time.time - _aimStartTime) / AimDuration;
                }
                
                UpdateLaserEffect(aimProgress);
            }

            // 如果不使用动画事件，则用计时器触发射击
            if (!UseAnimationEvent)
            {
                float aimProgress = (Time.time - _aimStartTime) / AimDuration;
                if (aimProgress >= 1f && !_hasFired)
                {
                    TriggerShooting();
                }
            }
        }
    }

    /// <summary>
    /// 获取瞄准动画的播放进度 (0-1)
    /// </summary>
    protected virtual float GetAimingAnimationProgress()
    {
        if (_animator == null) return 0f;
        
        // 获取 Combat Layer (index 1) 的当前状态信息
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(1);
        
        // 确保是在 Aiming 状态
        if (stateInfo.IsName(AimingParameter) || stateInfo.IsName("Aiming"))
        {
            return Mathf.Clamp01(stateInfo.normalizedTime);
        }
        
        return 0f;
    }

    /// <summary>
    /// 动画事件调用：瞄准动画播放完成
    /// 在瞄准动画的最后一帧添加动画事件，调用此方法
    /// </summary>
    public virtual void OnAimingComplete()
    {
        if (_isAiming && !_hasFired)
        {
            TriggerShooting();
        }
    }

    /// <summary>
    /// 触发射击
    /// </summary>
    protected virtual void TriggerShooting()
    {
        _isAiming = false;
        _hasFired = true;
        StartShooting();
    }

    /// <summary>
    /// 面向目标
    /// </summary>
    protected virtual void FaceTheTarget()
    {
        if (_brain.Target == null || _character == null) return;

        if (transform.position.x > _brain.Target.position.x)
        {
            _character.Face(Character.FacingDirections.Left);
        }
        else
        {
            _character.Face(Character.FacingDirections.Right);
        }
    }

    /// <summary>
    /// 瞄准目标
    /// </summary>
    protected virtual void AimAtTarget()
    {
        if (_brain.Target == null || TargetHandleWeapon?.CurrentWeapon == null) return;

        if (_weaponAim != null)
        {
            Vector3 targetPosition = _brain.Target.position + TargetOffset;
            
            if (_projectileWeapon != null)
            {
                _projectileWeapon.DetermineSpawnPosition();
                _weaponAimDirection = targetPosition - _projectileWeapon.SpawnPosition;
            }
            else
            {
                _weaponAimDirection = targetPosition - TargetHandleWeapon.CurrentWeapon.transform.position;
            }
            
            _weaponAim.SetCurrentAim(_weaponAimDirection);
        }
    }

    /// <summary>
    /// 更新激光效果
    /// </summary>
    protected virtual void UpdateLaserEffect(float progress)
    {
        if (!UseLaser || _aimingLaser == null) return;

        // 更新目标（如果追踪）
        if (TrackTargetWhileAiming && _brain.Target != null)
        {
            _aimingLaser.SetTarget(_brain.Target);
        }

        // 颜色渐变
        if (LerpLaserColor)
        {
            _aimingLaser.SetAimProgress(progress);
        }

        // 闪烁效果
        if (FlashBeforeShoot && progress >= FlashStartProgress)
        {
            _aimingLaser.FlashLaser(15f);
        }
    }

    /// <summary>
    /// 开始射击
    /// </summary>
    protected virtual void StartShooting()
    {
        _isShooting = true;

        // 切换动画：关闭瞄准，开启射击
        SetAnimationParameter(_aimingParameterHash, false);
        SetAnimationParameter(_shootingParameterHash, true);

        if (_shootCoroutine != null)
        {
            StopCoroutine(_shootCoroutine);
        }
        _shootCoroutine = StartCoroutine(ShootCoroutine());
    }

    /// <summary>
    /// 射击协程
    /// </summary>
    protected virtual IEnumerator ShootCoroutine()
    {
        // 发射所有子弹
        for (int i = 0; i < ShotsToFire; i++)
        {
            TargetHandleWeapon?.ShootStart();

            // 多发射击之间的间隔
            if (i < ShotsToFire - 1)
            {
                yield return new WaitForSeconds(TimeBetweenShots);
            }
        }

        // 射击完成后隐藏激光
        if (HideLaserAfterShoot && _aimingLaser != null)
        {
            _aimingLaser.DeactivateLaser();
        }

        // Shooting 保持 true，直到 OnExitState 被调用
    }

    /// <summary>
    /// 设置动画参数
    /// </summary>
    protected virtual void SetAnimationParameter(int parameterHash, bool value)
    {
        if (!UseAnimations || _animator == null) return;
        _animator.SetBool(parameterHash, value);
    }

    /// <summary>
    /// 退出状态时
    /// </summary>
    public override void OnExitState()
    {
        base.OnExitState();

        // 停止射击协程
        if (_shootCoroutine != null)
        {
            StopCoroutine(_shootCoroutine);
            _shootCoroutine = null;
        }
        
        TargetHandleWeapon?.ForceStop();

        // 隐藏激光
        if (_aimingLaser != null)
        {
            _aimingLaser.DeactivateLaser();
            _aimingLaser.ClearTarget();
        }

        // 关闭所有动画参数
        SetAnimationParameter(_aimingParameterHash, false);
        SetAnimationParameter(_shootingParameterHash, false);

        _isAiming = false;
        _isShooting = false;
    }
}
