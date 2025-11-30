using UnityEngine;
using MoreMountains.CorgiEngine;

/// <summary>
/// 挂在Player上，检测使用指定武器攻击时生成斩击特效
/// </summary>
public class PlayerSlashEffect : MonoBehaviour
{
    [Header("特效设置")]
    [Tooltip("斩击特效预制体")]
    public GameObject SlashEffectPrefab;
    
    [Tooltip("特效偏移位置")]
    public Vector3 EffectOffset = new Vector3(0.8f, 0f, 0f);
    
    [Tooltip("特效缩放大小")]
    public float EffectScale = 1f;
    
    [Header("武器检测")]
    [Tooltip("要检测的武器名称（包含即可）")]
    public string WeaponNameFilter = "ComboSword";
    
    private CharacterHandleWeapon _handleWeapon;
    private Character _character;
    private Weapon.WeaponStates _lastWeaponState;
    
    void Start()
    {
        _character = GetComponent<Character>();
        _handleWeapon = GetComponent<CharacterHandleWeapon>();
    }
    
    void Update()
    {
        if (_handleWeapon == null || _handleWeapon.CurrentWeapon == null)
            return;
        
        Weapon currentWeapon = _handleWeapon.CurrentWeapon;
        
        // 检查是否是目标武器
        if (!currentWeapon.name.Contains(WeaponNameFilter))
            return;
        
        // 检测武器状态变为WeaponUse时触发
        if (currentWeapon.WeaponState.CurrentState == Weapon.WeaponStates.WeaponUse 
            && _lastWeaponState != Weapon.WeaponStates.WeaponUse)
        {
            SpawnSlashEffect();
        }
        
        _lastWeaponState = currentWeapon.WeaponState.CurrentState;
    }
    
private void SpawnSlashEffect()
    {
        if (SlashEffectPrefab == null)
            return;
        
        Weapon currentWeapon = _handleWeapon.CurrentWeapon;
        
        // 获取武器瞄准角度
        float aimAngle = 0f;
        WeaponAim weaponAim = currentWeapon.GetComponent<WeaponAim>();
        if (weaponAim != null)
        {
            aimAngle = weaponAim.CurrentAngle;
        }
        
        // 检查角色是否面向右边
        bool isFacingRight = _character != null && _character.IsFacingRight;
        
        // 计算世界角度
        float worldAngle = aimAngle;
        if (!isFacingRight)
        {
            // 修正：用加法而不是减法
            worldAngle = 180f + aimAngle;
        }
        
        // 根据世界角度计算偏移位置
        Vector3 offset = Quaternion.Euler(0, 0, worldAngle) * EffectOffset;
        
        // 实例化特效
        GameObject effect = Instantiate(SlashEffectPrefab, transform.position + offset, Quaternion.Euler(0, 0, worldAngle));
        
        // 设置缩放
        Vector3 scale = Vector3.one * EffectScale;
        
        // 用cos判断是否在左半边
        bool isLeftSide = Mathf.Cos(worldAngle * Mathf.Deg2Rad) < 0;
        if (isLeftSide)
        {
            scale.y = -scale.y;
        }
        effect.transform.localScale = scale;
        
        // 添加跟随组件
        EffectFollowPlayer follow = effect.AddComponent<EffectFollowPlayer>();
        follow.Target = transform;
        follow.Offset = offset;
    }
}
