using UnityEngine;
using UnityEditor;
using MoreMountains.CorgiEngine;
using System.Collections.Generic;

/// <summary>
/// 编辑器脚本 - 修复 SorcererAmmo 的击退配置
/// 使用方法：菜单 Tools -> Fix SorcererAmmo Knockback
/// </summary>
public class FixSorcererAmmoKnockback : Editor
{
    [MenuItem("Tools/Fix SorcererAmmo Knockback")]
    static void FixKnockback()
    {
        // 加载 SorcererAmmo prefab
        string prefabPath = "Assets/Perfab/Ammo/SorcererAmmo.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (prefab == null)
        {
            Debug.LogError($"找不到 prefab: {prefabPath}");
            return;
        }
        
        // 获取 DamageOnTouch 组件
        DamageOnTouch damageOnTouch = prefab.GetComponent<DamageOnTouch>();
        
        if (damageOnTouch == null)
        {
            Debug.LogError("SorcererAmmo 没有 DamageOnTouch 组件");
            return;
        }
        
        // 记录修改前的值
        Debug.Log("=== 修改前的配置 ===");
        Debug.Log($"DamageCausedKnockbackDirection: {damageOnTouch.DamageCausedKnockbackDirection}");
        Debug.Log($"DamageCausedKnockbackForce: {damageOnTouch.DamageCausedKnockbackForce}");
        Debug.Log($"DamageCausedKnockbackType: {damageOnTouch.DamageCausedKnockbackType}");
        Debug.Log($"TypedDamages Count: {damageOnTouch.TypedDamages?.Count ?? 0}");
        
        // 修改击退方向为 BasedOnDamageOnTouchPosition（更可靠）
        damageOnTouch.DamageCausedKnockbackDirection = DamageOnTouch.CausedKnockbackDirections.BasedOnDamageOnTouchPosition;
        
        // 确保击退力度正确
        damageOnTouch.DamageCausedKnockbackForce = new Vector2(10f, 2f);
        
        // 修改 TypedDamages 中的 ResetControllerForces 为 false
        if (damageOnTouch.TypedDamages != null && damageOnTouch.TypedDamages.Count > 0)
        {
            for (int i = 0; i < damageOnTouch.TypedDamages.Count; i++)
            {
                TypedDamage td = damageOnTouch.TypedDamages[i];
                Debug.Log($"TypedDamage[{i}] ResetControllerForces 修改前: {td.ResetControllerForces}");
                td.ResetControllerForces = false;  // 关键修改！不要重置力
                Debug.Log($"TypedDamage[{i}] ResetControllerForces 修改后: {td.ResetControllerForces}");
            }
        }
        
        // 标记为脏并保存
        EditorUtility.SetDirty(prefab);
        AssetDatabase.SaveAssets();
        
        Debug.Log("=== 修改后的配置 ===");
        Debug.Log($"DamageCausedKnockbackDirection: {damageOnTouch.DamageCausedKnockbackDirection}");
        Debug.Log($"DamageCausedKnockbackForce: {damageOnTouch.DamageCausedKnockbackForce}");
        Debug.Log("SorcererAmmo 击退配置已修复！");
    }
}
