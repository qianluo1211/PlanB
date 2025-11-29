using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.CorgiEngine;

/// <summary>
/// 增强版瞄准线组件，支持：
/// - 动态显示/隐藏
/// - 颜色渐变（瞄准时黄色 -> 即将射击时红色）
/// - 瞄准目标
/// </summary>
[RequireComponent(typeof(Weapon))]
[AddComponentMenu("Corgi Engine/Weapons/Weapon Aiming Laser")]
public class WeaponAimingLaser : MonoBehaviour
{
    [Header("Laser Settings")]
    [Tooltip("激光的最大距离")]
    public float LaserMaxDistance = 50f;
    
    [Tooltip("激光碰撞检测层")]
    public LayerMask LaserCollisionMask;
    
    [Tooltip("激光宽度")]
    public float LaserWidth = 0.05f;

    [Header("Appearance")]
    [Tooltip("瞄准时的颜色")]
    public Color AimingColor = Color.yellow;
    
    [Tooltip("即将射击时的颜色")]
    public Color ReadyToFireColor = Color.red;
    
    [Tooltip("激光材质（如果为空会使用默认材质）")]
    public Material LaserMaterial;

    [Header("Offsets")]
    [Tooltip("激光起点偏移")]
    public Vector3 LaserOriginOffset = Vector3.zero;

    [Header("State")]
    [Tooltip("是否在开始时激活激光")]
    public bool ActiveOnStart = false;

    // 公共属性
    public bool IsActive => _isActive;
    public Vector3 LaserOrigin => _origin;
    public Vector3 LaserDestination => _destination;

    protected Weapon _weapon;
    protected WeaponAim _weaponAim;
    protected LineRenderer _lineRenderer;
    protected bool _isActive = false;
    protected float _currentLerpValue = 0f;
    protected Vector3 _origin;
    protected Vector3 _destination;
    protected Vector3 _direction;
    protected RaycastHit2D _hit;
    protected Transform _target;

    protected virtual void Start()
    {
        Initialize();
    }

    protected virtual void Initialize()
    {
        _weapon = GetComponent<Weapon>();
        _weaponAim = GetComponent<WeaponAim>();

        // 创建LineRenderer
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
        _lineRenderer.startWidth = LaserWidth;
        _lineRenderer.endWidth = LaserWidth * 0.5f;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
        
        // 设置材质
        if (LaserMaterial != null)
        {
            _lineRenderer.material = LaserMaterial;
        }
        else
        {
            // 使用默认的发光材质
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        _lineRenderer.startColor = AimingColor;
        _lineRenderer.endColor = AimingColor;
        _lineRenderer.enabled = ActiveOnStart;
        _isActive = ActiveOnStart;
    }

    protected virtual void Update()
    {
        if (_isActive)
        {
            UpdateLaser();
        }
    }

    /// <summary>
    /// 更新激光位置和方向
    /// </summary>
    protected virtual void UpdateLaser()
    {
        if (_weapon == null) return;

        // 计算激光起点
        Vector3 weaponPosition = _weapon.transform.position;
        Quaternion weaponRotation = _weapon.transform.rotation;
        
        Vector3 offset = LaserOriginOffset;
        if (_weapon.Flipped)
        {
            offset.x = -offset.x;
        }
        
        _origin = weaponPosition + weaponRotation * offset;

        // 计算方向
        if (_target != null)
        {
            // 如果有目标，瞄准目标
            _direction = (_target.position - _origin).normalized;
        }
        else if (_weaponAim != null)
        {
            // 使用武器瞄准方向
            float angle = _weaponAim.CurrentAngle * Mathf.Deg2Rad;
            _direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
        }
        else
        {
            // 使用武器朝向
            _direction = _weapon.Flipped ? Vector3.left : Vector3.right;
            _direction = weaponRotation * _direction;
        }

        // 射线检测
        _hit = Physics2D.Raycast(_origin, _direction, LaserMaxDistance, LaserCollisionMask);
        
        if (_hit.collider != null)
        {
            _destination = _hit.point;
        }
        else
        {
            _destination = _origin + _direction * LaserMaxDistance;
        }

        // 更新LineRenderer
        _lineRenderer.SetPosition(0, _origin);
        _lineRenderer.SetPosition(1, _destination);
    }

    /// <summary>
    /// 激活激光
    /// </summary>
    public virtual void ActivateLaser()
    {
        _isActive = true;
        _lineRenderer.enabled = true;
        _currentLerpValue = 0f;
        SetLaserColor(AimingColor);
    }

    /// <summary>
    /// 停用激光
    /// </summary>
    public virtual void DeactivateLaser()
    {
        _isActive = false;
        _lineRenderer.enabled = false;
        _currentLerpValue = 0f;
    }

    /// <summary>
    /// 设置瞄准目标
    /// </summary>
    public virtual void SetTarget(Transform target)
    {
        _target = target;
    }

    /// <summary>
    /// 清除目标
    /// </summary>
    public virtual void ClearTarget()
    {
        _target = null;
    }

    /// <summary>
    /// 设置激光颜色
    /// </summary>
    public virtual void SetLaserColor(Color color)
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = new Color(color.r, color.g, color.b, color.a * 0.5f);
        }
    }

    /// <summary>
    /// 根据进度插值颜色（0=瞄准色，1=准备射击色）
    /// </summary>
    public virtual void SetAimProgress(float progress)
    {
        _currentLerpValue = Mathf.Clamp01(progress);
        Color currentColor = Color.Lerp(AimingColor, ReadyToFireColor, _currentLerpValue);
        SetLaserColor(currentColor);
    }

    /// <summary>
    /// 闪烁效果（即将射击时）
    /// </summary>
    public virtual void FlashLaser(float frequency = 10f)
    {
        float flash = Mathf.PingPong(Time.time * frequency, 1f);
        Color flashColor = Color.Lerp(AimingColor, ReadyToFireColor, flash);
        SetLaserColor(flashColor);
    }
}
