using UnityEngine;
using MoreMountains.CorgiEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 攻击动画优先级控制器
    /// 确保攻击动画立即播放，不被其他动画打断
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Abilities/Attack Animation Priority")]
    public class AttackAnimationPriority : MonoBehaviour
    {
        [Header("动画状态名")]
        [Tooltip("攻击动画状态名（Combo武器的多个状态）")]
        public string[] AttackStateNames = new string[] { "Sword1", "Sword2", "Sword3" };
        
        [Header("设置")]
        [Tooltip("动画过渡时间")]
        public float CrossFadeDuration = 0.02f;
        
        [Tooltip("Layer索引（通常为0）")]
        public int LayerIndex = 0;

        [Header("调试")]
        public bool EnableDebug = false;

        protected Animator _animator;
        protected CharacterHandleWeapon _handleWeapon;
        protected Weapon _lastWeapon;
        protected Weapon.WeaponStates _lastWeaponState;

        protected virtual void Start()
        {
            _animator = GetComponentInChildren<Animator>();
            _handleWeapon = GetComponent<CharacterHandleWeapon>();
            
            if (_animator == null)
            {
                Debug.LogError("[AttackAnimationPriority] 未找到Animator");
                enabled = false;
            }
        }

        protected virtual void LateUpdate()
        {
            if (_handleWeapon == null || _handleWeapon.CurrentWeapon == null) return;
            
            Weapon currentWeapon = _handleWeapon.CurrentWeapon;
            Weapon.WeaponStates currentState = currentWeapon.WeaponState.CurrentState;
            
            // 检测武器状态变化到攻击状态
            if (currentState == Weapon.WeaponStates.WeaponStart || 
                currentState == Weapon.WeaponStates.WeaponUse)
            {
                // 只在状态刚变化时触发
                if (_lastWeaponState != currentState || _lastWeapon != currentWeapon)
                {
                    ForcePlayAttackAnimation(currentWeapon);
                }
            }
            
            _lastWeaponState = currentState;
            _lastWeapon = currentWeapon;
        }

        protected virtual void ForcePlayAttackAnimation(Weapon weapon)
        {
            if (_animator == null || AttackStateNames == null || AttackStateNames.Length == 0) return;

            // 获取当前combo索引
            int comboIndex = GetCurrentComboIndex(weapon);
            int stateIndex = Mathf.Clamp(comboIndex, 0, AttackStateNames.Length - 1);
            string stateName = AttackStateNames[stateIndex];

            if (EnableDebug)
            {
                Debug.Log($"[Attack] 强制播放: {stateName}");
            }

            // 使用CrossFade强制平滑过渡到攻击动画
            _animator.CrossFade(stateName, CrossFadeDuration, LayerIndex);
        }

        protected virtual int GetCurrentComboIndex(Weapon weapon)
        {
            ComboWeapon comboWeapon = weapon.GetComponent<ComboWeapon>();
            if (comboWeapon == null) return 0;
            
            // 遍历武器数组找到当前激活的武器索引
            for (int i = 0; i < comboWeapon.Weapons.Length; i++)
            {
                if (comboWeapon.Weapons[i] == weapon || comboWeapon.Weapons[i].enabled)
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
