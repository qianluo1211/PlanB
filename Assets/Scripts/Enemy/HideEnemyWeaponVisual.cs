using UnityEngine;
using MoreMountains.CorgiEngine;

/// <summary>
/// 添加此组件到敌人身上可以隐藏敌人手上的武器视觉效果
/// 武器仍然可以正常工作，只是不可见
/// </summary>
public class HideEnemyWeaponVisual : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("是否在开始时隐藏武器")]
    public bool HideOnStart = true;
    
    [Tooltip("延迟隐藏的时间（等待武器初始化）")]
    public float HideDelay = 0.1f;

    private CharacterHandleWeapon _characterHandleWeapon;

    private void Start()
    {
        _characterHandleWeapon = GetComponent<CharacterHandleWeapon>();
        
        if (HideOnStart && _characterHandleWeapon != null)
        {
            // 延迟调用以确保武器已经被实例化
            Invoke(nameof(HideWeaponVisual), HideDelay);
        }
    }

    /// <summary>
    /// 隐藏当前装备的武器的所有视觉组件
    /// </summary>
    public void HideWeaponVisual()
    {
        if (_characterHandleWeapon == null || _characterHandleWeapon.CurrentWeapon == null)
        {
            return;
        }

        GameObject weaponObject = _characterHandleWeapon.CurrentWeapon.gameObject;
        
        // 隐藏武器自身的SpriteRenderer
        SpriteRenderer weaponSprite = weaponObject.GetComponent<SpriteRenderer>();
        if (weaponSprite != null)
        {
            weaponSprite.enabled = false;
        }
        
        // 隐藏所有子对象的SpriteRenderer
        SpriteRenderer[] childSprites = weaponObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sprite in childSprites)
        {
            sprite.enabled = false;
        }
        
        // 隐藏所有MeshRenderer（如果有3D模型的话）
        MeshRenderer[] meshRenderers = weaponObject.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer mesh in meshRenderers)
        {
            mesh.enabled = false;
        }
    }

    /// <summary>
    /// 显示武器视觉效果
    /// </summary>
    public void ShowWeaponVisual()
    {
        if (_characterHandleWeapon == null || _characterHandleWeapon.CurrentWeapon == null)
        {
            return;
        }

        GameObject weaponObject = _characterHandleWeapon.CurrentWeapon.gameObject;
        
        SpriteRenderer weaponSprite = weaponObject.GetComponent<SpriteRenderer>();
        if (weaponSprite != null)
        {
            weaponSprite.enabled = true;
        }
        
        SpriteRenderer[] childSprites = weaponObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sprite in childSprites)
        {
            sprite.enabled = true;
        }
        
        MeshRenderer[] meshRenderers = weaponObject.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer mesh in meshRenderers)
        {
            mesh.enabled = true;
        }
    }
}
