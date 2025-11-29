using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 魂钟 - 类魂游戏的存档点
    /// 
    /// 动画逻辑：
    /// - 初始状态：被藤蔓缠绕
    /// - 玩家首次靠近：播放藤蔓褪去动画
    /// - 之后：保持无藤蔓的激活状态
    /// 
    /// 功能：
    /// 1. 记录重生点（继承自 CheckPoint）
    /// 2. 恢复玩家生命值
    /// 3. 播放激活动画
    /// </summary>
    [AddComponentMenu("Corgi Engine/Spawn/Souls Bell")]
    public class SoulsBell : CheckPoint
    {
        [Header("生命恢复")]
        
        [Tooltip("触碰时恢复玩家生命值")]
        public bool RestoreHealthOnTouch = true;
        
        [Tooltip("只在首次激活时恢复（否则每次进入都恢复）")]
        public bool RestoreHealthOnlyOnFirstActivation = false;

        [Header("动画")]
        
        [Tooltip("钟的 Animator（留空则自动查找）")]
        public Animator BellAnimator;
        
        [Tooltip("激活动画的 Trigger 名称（藤蔓褪去）")]
        public string ActivateTrigger = "Activate";

        [Header("反馈效果")]
        
        [Tooltip("首次激活时的反馈（音效、粒子等）")]
        public MMFeedbacks ActivationFeedbacks;

        [Header("状态")]
        
        [MMReadOnly]
        [Tooltip("是否已激活")]
        public bool IsActivated = false;

        /// <summary>
        /// 初始化
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            
            if (BellAnimator == null)
            {
                BellAnimator = GetComponent<Animator>();
                if (BellAnimator == null)
                {
                    BellAnimator = GetComponentInChildren<Animator>();
                }
            }
        }

        /// <summary>
        /// 玩家进入触发区域
        /// </summary>
        protected override void OnTriggerEnter2D(Collider2D collider)
        {
            Character character = collider.GetComponent<Character>();
            if (character == null) return;
            if (character.CharacterType != Character.CharacterTypes.Player) return;
            if (!LevelManager.HasInstance) return;

            bool isFirstActivation = !IsActivated;
            
            // 调用父类（设置存档点）
            base.OnTriggerEnter2D(collider);
            
            // 首次激活
            if (isFirstActivation)
            {
                IsActivated = true;
                
                // 播放藤蔓褪去动画
                if (BellAnimator != null && !string.IsNullOrEmpty(ActivateTrigger))
                {
                    BellAnimator.SetTrigger(ActivateTrigger);
                }
                
                // 播放激活反馈
                ActivationFeedbacks?.PlayFeedbacks(transform.position);
            }
            
            // 恢复生命值
            if (RestoreHealthOnTouch)
            {
                if (!RestoreHealthOnlyOnFirstActivation || isFirstActivation)
                {
                    RestorePlayerHealth(character);
                }
            }
        }

        /// <summary>
        /// 恢复玩家满血
        /// </summary>
        protected virtual void RestorePlayerHealth(Character character)
        {
            Health playerHealth = character.CharacterHealth;
            if (playerHealth != null)
            {
                playerHealth.ResetHealthToMaxHealth();
            }
        }

        /// <summary>
        /// 玩家在此重生时
        /// </summary>
        public override void SpawnPlayer(Character player)
        {
            base.SpawnPlayer(player);
            
            // 重生时也恢复满血
            if (RestoreHealthOnTouch)
            {
                RestorePlayerHealth(player);
            }
        }

        /// <summary>
        /// 编辑器 Gizmos
        /// </summary>
        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            
            // 钟的状态指示
            Gizmos.color = IsActivated ? Color.yellow : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
