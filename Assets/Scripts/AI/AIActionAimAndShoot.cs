using System.Collections;
using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.CorgiEngine;

/// <summary>
/// AI动作：先瞄准一段时间，显示激光预警线，然后射击
/// 可以替代或配合AIActionShoot使用
/// </summary>
[AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Aim And Shoot")]
public class AIActionAimAndShoot : AIAction
{
    [Header("Aiming Settings")]
    [Tooltip("瞄准持续时间（秒）")]
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

    [Header("Target Offset")]
    [Tooltip("瞄准目标的偏移量")]
    public Vector3 TargetOffset = Vector3.zero;

    [Header("References")]
    [Tooltip("CharacterHandleWeapon组件（如果为空会自动查找）")]
    public CharacterHandleWeapon TargetHandleWeapon;

    // 内部状态
    protected Character _character;
    protected WeaponAim _weaponAim;
    protected WeaponAimingLaser _aimingLaser;
    protected ProjectileWeapon _projectileWeapon;
    
    protected float _aimStartTime;
    protected bool _isAiming = false;
    protected bool _hasFired = false;
    protected int _shotsFired = 0;
    protected Vector3 _weaponAimDirection;
    protected Coroutine _shootCoroutine;

    /// <summary>
    /// 初始化
    /// </summary>
    public override void Initialization()
    {
        if (!ShouldInitialize) return;
        
        _character = GetComponentInParent<Character>();
        if (TargetHandleWeapon == null)
        {
            TargetHandleWeapon = _character?.FindAbility<CharacterHandleWeapon>();
        }
    }

    /// <summary>
    /// 进入状态时
    /// </summary>
    public override void OnEnterState()
    {
        base.OnEnterState();
        
        _isAiming = true;
        _hasFired = false;
        _shotsFired = 0;
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
    }

    /// <summary>
    /// 执行动作
    /// </summary>
    public override void PerformAction()
    {
        if (_brain.Target == null) return;

        // 面向目标
        if (FaceTarget)
        {
            FaceTheTarget();
        }

        // 瞄准目标
        AimAtTarget();

        // 计算瞄准进度
        float aimProgress = (Time.time - _aimStartTime) / AimDuration;

        if (_isAiming)
        {
            // 更新激光效果
            UpdateLaserEffect(aimProgress);

            // 瞄准完成，开始射击
            if (aimProgress >= 1f && !_hasFired)
            {
                _isAiming = false;
                _hasFired = true;
                StartShooting();
            }
        }
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

        if (_weaponAim != null && (TrackTargetWhileAiming || !_isAiming))
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
        for (int i = 0; i < ShotsToFire; i++)
        {
            // 射击
            TargetHandleWeapon?.ShootStart();
            _shotsFired++;

            // 等待间隔
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
    }

    /// <summary>
    /// 退出状态时
    /// </summary>
    public override void OnExitState()
    {
        base.OnExitState();

        // 停止射击
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

        _isAiming = false;
    }
}
