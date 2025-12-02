using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 方向性伤害 Health 组件 - 可配置正面/背后的伤害倍率
    /// 用法：替换普通 Health 组件，配置伤害倍率
    /// 
    /// 应用场景：
    /// - Boss：正面 0，背后 1 = 只能从背后受伤
    /// - 盾牌兵：正面 0，背后 1 = 正面格挡
    /// - 重甲怪：正面 0.5，背后 1 = 正面减伤50%
    /// - 刺客型：正面 1，背后 2 = 背后暴击双倍
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Core/Directional Health")]
    public class DirectionalHealth : Health
    {
        [MMInspectorGroup("Directional Damage", true, 12)]
        
        [Tooltip("是否启用方向性伤害")]
        public bool DirectionalDamageEnabled = true;
        
        [Tooltip("正面受到的伤害倍率（0 = 免疫，0.5 = 减半，1 = 正常）")]
        [Range(0f, 3f)]
        public float FrontDamageMultiplier = 0f;
        
        [Tooltip("背后受到的伤害倍率（1 = 正常，2 = 双倍）")]
        [Range(0f, 3f)]
        public float BackDamageMultiplier = 1f;
        
        [Tooltip("正面受伤时是否播放格挡反馈")]
        public bool PlayBlockFeedbackOnFrontHit = true;
        
        [Tooltip("格挡时播放的反馈")]
        public MoreMountains.Feedbacks.MMFeedbacks BlockFeedbacks;

        [Header("Debug")]
        public bool DebugMode = false;

        protected Character _directionalCharacter;

        /// <summary>
        /// 初始化时获取 Character 引用
        /// </summary>
        protected override void Initialization()
        {
            base.Initialization();
            _directionalCharacter = GetComponent<Character>();
            
            if (_directionalCharacter == null)
            {
                _directionalCharacter = GetComponentInParent<Character>();
            }
        }

        /// <summary>
        /// 重写伤害方法，根据攻击方向调整伤害
        /// </summary>
        public override void Damage(float damage, GameObject instigator, float flickerDuration,
            float invincibilityDuration, Vector3 damageDirection, List<TypedDamage> typedDamages = null)
        {
            if (!DirectionalDamageEnabled || _directionalCharacter == null)
            {
                base.Damage(damage, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);
                return;
            }

            // 计算攻击者是否在背后
            bool isFromBehind = IsAttackFromBehind(instigator);
            
            // 根据方向选择伤害倍率
            float multiplier = isFromBehind ? BackDamageMultiplier : FrontDamageMultiplier;
            
            if (DebugMode)
            {
                Debug.Log($"[DirectionalHealth] Hit from {(isFromBehind ? "BEHIND" : "FRONT")}, " +
                         $"multiplier: {multiplier}, original damage: {damage}, final: {damage * multiplier}");
            }

            // 如果倍率为0（完全格挡）
            if (multiplier <= 0f)
            {
                if (PlayBlockFeedbackOnFrontHit)
                {
                    BlockFeedbacks?.PlayFeedbacks(transform.position);
                }
                
                // 触发 OnHitZero 事件（可用于其他系统响应）
                OnHitZero?.Invoke();
                return;
            }

            // 应用倍率后调用基类方法
            float modifiedDamage = damage * multiplier;
            base.Damage(modifiedDamage, instigator, flickerDuration, invincibilityDuration, damageDirection, typedDamages);
        }

        /// <summary>
        /// 判断攻击是否来自背后
        /// </summary>
        protected virtual bool IsAttackFromBehind(GameObject instigator)
        {
            if (instigator == null) return false;

            // 获取攻击者位置
            Vector3 attackerPos = instigator.transform.position;
            
            // 判断攻击者在左边还是右边
            bool attackerOnRight = attackerPos.x > transform.position.x;
            
            // 获取角色朝向
            bool facingRight = _directionalCharacter.IsFacingRight;
            
            // 攻击者在右边且角色朝左 = 从背后攻击
            // 攻击者在左边且角色朝右 = 从背后攻击
            bool isFromBehind = (attackerOnRight && !facingRight) || (!attackerOnRight && facingRight);
            
            return isFromBehind;
        }
    }
}
