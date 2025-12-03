using UnityEngine;
using MoreMountains.CorgiEngine;

/// <summary>
/// 方向性击退脚本 - 根据弹幕飞行方向计算击退力
/// 挂载到弹幕 prefab 上，会自动接管 DamageOnTouch 的击退功能
/// 
/// 特性：
/// - 普通状态：玩家被推向弹幕飞行方向
/// - 摆荡状态：将冲量转换为角速度变化，符合物理规律
/// </summary>
[RequireComponent(typeof(DamageOnTouch))]
public class DirectionalKnockback : MonoBehaviour
{
    [Header("击退设置")]
    [Tooltip("击退力度大小")]
    public float KnockbackForce = 10f;
    
    [Tooltip("额外的向上力（让击退有一点上扬感，设为0则完全跟随弹幕方向）")]
    public float AdditionalUpwardForce = 0f;
    
    [Tooltip("是否反转击退方向（false=玩家被推向弹幕飞行方向，true=玩家被推向弹幕来的反方向）")]
    public bool ReverseDirection = false;
    
    [Header("摆荡状态设置")]
    [Tooltip("摆荡时的击退力度倍率（摆荡状态下冲量可能需要调整）")]
    public float SwingKnockbackMultiplier = 1.5f;
    
    [Tooltip("是否在摆荡状态下也触发残影效果")]
    public bool TriggerAfterimageOnSwingHit = true;
    
    [Header("调试")]
    [Tooltip("显示调试信息")]
    public bool ShowDebugInfo = true;
    
    private Vector2 _lastPosition;
    private Vector2 _velocity;
    private DamageOnTouch _damageOnTouch;
    private Projectile _projectile;
    
    void Awake()
    {
        _damageOnTouch = GetComponent<DamageOnTouch>();
        _projectile = GetComponent<Projectile>();
        
        // 关闭 DamageOnTouch 的默认击退，由本脚本接管
        if (_damageOnTouch != null)
        {
            _damageOnTouch.DamageCausedKnockbackType = DamageOnTouch.KnockbackStyles.NoKnockback;
        }
    }
    
    void OnEnable()
    {
        _lastPosition = transform.position;
    }
    
    void Update()
    {
        // 计算实际速度方向
        Vector2 currentPosition = transform.position;
        if (Time.deltaTime > 0)
        {
            _velocity = (currentPosition - _lastPosition) / Time.deltaTime;
        }
        _lastPosition = currentPosition;
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        // 检查是否在目标层级
        if (_damageOnTouch != null)
        {
            if (((1 << other.gameObject.layer) & _damageOnTouch.TargetLayerMask) == 0)
            {
                return;
            }
        }
        
        // 获取目标的 Health 和 Controller
        Health health = other.GetComponent<Health>();
        if (health == null) return;
        
        CorgiController controller = health.AssociatedController;
        if (controller == null) return;
        
        // 检查是否免疫击退
        if (health.ImmuneToKnockback) return;
        
        // 计算击退方向
        Vector2 knockbackDirection = CalculateKnockbackDirection(other.transform.position);
        
        // 检查目标是否在摆荡状态
        Character character = other.GetComponent<Character>();
        if (character == null)
        {
            character = controller.gameObject.GetComponent<Character>();
        }
        
        CharacterGrapple grapple = character?.FindAbility<CharacterGrapple>();
        
        // 如果在摆荡状态，使用物理冲量系统
        if (grapple != null && grapple.IsSwinging)
        {
            ApplySwingKnockback(grapple, knockbackDirection);
        }
        else
        {
            // 普通状态，使用常规击退
            ApplyNormalKnockback(controller, character, knockbackDirection);
        }
    }
    
    /// <summary>
    /// 应用普通击退（非摆荡状态）
    /// </summary>
    private void ApplyNormalKnockback(CorgiController controller, Character character, Vector2 knockbackDirection)
    {
        // 计算最终击退力
        Vector2 finalKnockbackForce = knockbackDirection * KnockbackForce;
        
        // 添加额外的向上力
        finalKnockbackForce.y += AdditionalUpwardForce;
        
        // 应用击退
        controller.SetForce(finalKnockbackForce);
        
        // 处理跳跃状态
        CharacterJump characterJump = character?.FindAbility<CharacterJump>();
        if (characterJump != null)
        {
            characterJump.SetCanJumpStop(false);
            characterJump.SetJumpFlags();
        }
        
        if (ShowDebugInfo)
        {
            Debug.Log($"[DirectionalKnockback] 普通击退 - 弹幕速度: {_velocity}, 击退方向: {knockbackDirection}, 击退力: {finalKnockbackForce}");
        }
    }
    
    /// <summary>
    /// 应用摆荡状态击退（转换为角速度变化）
    /// </summary>
    private void ApplySwingKnockback(CharacterGrapple grapple, Vector2 knockbackDirection)
    {
        // 计算冲量（方向 × 力度 × 摆荡倍率）
        Vector2 impulse = knockbackDirection * KnockbackForce * SwingKnockbackMultiplier;
        
        // 添加额外的向上冲量
        impulse.y += AdditionalUpwardForce;
        
        // 应用到摆荡系统
        bool applied = grapple.ApplyExternalImpulse(impulse, transform.position);
        
        // 注：残影效果由 CharacterGrapple 内部处理
            // 如果需要在击退时触发残影，可以在 CharacterGrapple 中添加相关逻辑
        
        if (ShowDebugInfo)
        {
            Debug.Log($"[DirectionalKnockback] 摆荡击退 - 弹幕速度: {_velocity}, 冲量: {impulse}, 应用结果: {applied}");
        }
    }
    
    /// <summary>
    /// 计算击退方向
    /// </summary>
    private Vector2 CalculateKnockbackDirection(Vector3 targetPosition)
    {
        Vector2 direction;
        
        // 优先使用弹幕的飞行速度方向
        if (_velocity.magnitude > 0.1f)
        {
            direction = _velocity.normalized;
        }
        // 如果有 Projectile 组件，使用其 Direction
        else if (_projectile != null && _projectile.Direction.magnitude > 0.1f)
        {
            direction = _projectile.Direction.normalized;
        }
        // 最后使用弹幕到目标的方向
        else
        {
            direction = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        }
        
        // 是否反转方向
        if (ReverseDirection)
        {
            direction = -direction;
        }
        
        return direction;
    }
}
