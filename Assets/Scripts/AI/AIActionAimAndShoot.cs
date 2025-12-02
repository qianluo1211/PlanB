using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.CorgiEngine;

/// <summary>
/// AI动作：瞄准 → 射击 → 瞄准 → 射击 的循环行为
/// 
/// 设计理念：
/// - 敌人负责：瞄准时长、射击时长、循环次数、激光预警
/// - 武器负责：发射频率(TimeBetweenUses)、子弹类型等
/// - 这样任意武器都可以直接拖到敌人身上使用
/// 
/// 行为流程：
/// 1. 进入状态 → 开始瞄准（显示激光预警）
/// 2. 瞄准完成 → 开始射击（武器自己控制发射频率）
/// 3. 射击时间到 → 如果启用循环，回到步骤1；否则继续射击
/// 4. 退出状态 → 停止一切
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
    [Tooltip("射击阶段持续时间（仅在启用循环时生效）")]
    public float ShootDuration = 2f;

    [Header("Loop Settings")]
    [Tooltip("是否启用循环（射击后重新瞄准）")]
    public bool EnableLoop = false;
    
    [Tooltip("循环次数（0 = 无限循环，直到退出状态）")]
    public int LoopCount = 0;

    [Header("Laser Settings")]
    [Tooltip("是否使用激光瞄准线")]
    public bool UseLaser = true;
    
    [Tooltip("激光颜色渐变（从瞄准到准备射击）")]
    public bool LerpLaserColor = true;
    
    [Tooltip("即将射击时激光闪烁")]
    public bool FlashBeforeShoot = true;
    
    [Tooltip("开始闪烁的时机（0-1，1表示最后时刻）")]
    public float FlashStartProgress = 0.8f;
    
    [Tooltip("开始射击后是否隐藏激光")]
    public bool HideLaserOnShoot = true;

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
    protected float _shootStartTime;
    protected bool _isAiming = false;
    protected bool _isShooting = false;
    protected int _currentLoopCount = 0;
    protected Vector3 _weaponAimDirection;

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
        
        // 重置循环计数
        _currentLoopCount = 0;

        // 获取武器组件
        if (TargetHandleWeapon?.CurrentWeapon != null)
        {
            _weaponAim = TargetHandleWeapon.CurrentWeapon.gameObject.GetComponent<WeaponAim>();
            _aimingLaser = TargetHandleWeapon.CurrentWeapon.gameObject.GetComponent<WeaponAimingLaser>();
            _projectileWeapon = TargetHandleWeapon.CurrentWeapon.gameObject.GetComponent<ProjectileWeapon>();
        }

        // 开始瞄准
        StartAiming();
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

        // 始终瞄准目标
        AimAtTarget();

        // 瞄准阶段
        if (_isAiming)
        {
            HandleAimingPhase();
        }
        // 射击阶段
        else if (_isShooting)
        {
            HandleShootingPhase();
        }
    }

    /// <summary>
    /// 开始瞄准 - 进入瞄准阶段
    /// </summary>
    protected virtual void StartAiming()
    {
        _isAiming = true;
        _isShooting = false;
        _aimStartTime = Time.time;

        // 停止武器射击
        TargetHandleWeapon?.ForceStop();

        // 激活激光
        if (UseLaser && _aimingLaser != null)
        {
            _aimingLaser.ActivateLaser();
            if (_brain.Target != null)
            {
                _aimingLaser.SetTarget(_brain.Target);
            }
        }

        // 切换动画：开启瞄准，关闭射击
        SetAnimationParameter(_aimingParameterHash, true);
        SetAnimationParameter(_shootingParameterHash, false);
    }

    /// <summary>
    /// 处理瞄准阶段
    /// </summary>
    protected virtual void HandleAimingPhase()
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
            
            // 时间到了就触发射击
            if (aimProgress >= 1f)
            {
                StartShooting();
                return;
            }
        }
        
        // 更新激光效果
        UpdateLaserEffect(aimProgress);
    }

    /// <summary>
    /// 处理射击阶段
    /// </summary>
    protected virtual void HandleShootingPhase()
    {
        // 持续调用 ShootStart，武器会根据自己的 TimeBetweenUses 控制实际发射频率
        TargetHandleWeapon?.ShootStart();

        // 检查是否需要循环
        if (EnableLoop)
        {
            float shootElapsed = Time.time - _shootStartTime;
            if (shootElapsed >= ShootDuration)
            {
                // 检查是否达到循环次数限制
                if (LoopCount <= 0 || _currentLoopCount < LoopCount)
                {
                    // 重新开始瞄准
                    StartAiming();
                }
                // 如果达到循环次数，继续射击直到退出状态
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
        if (_isAiming)
        {
            StartShooting();
        }
    }

    /// <summary>
    /// 开始射击 - 从瞄准阶段切换到射击阶段
    /// </summary>
    protected virtual void StartShooting()
    {
        _isAiming = false;
        _isShooting = true;
        _shootStartTime = Time.time;
        _currentLoopCount++;

        // 切换动画：关闭瞄准，开启射击
        SetAnimationParameter(_aimingParameterHash, false);
        SetAnimationParameter(_shootingParameterHash, true);

        // 隐藏激光
        if (HideLaserOnShoot && _aimingLaser != null)
        {
            _aimingLaser.DeactivateLaser();
        }

        // 告诉武器开始射击（武器自己控制发射频率）
        TargetHandleWeapon?.ShootStart();
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
        
        // 告诉武器停止射击
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
