using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断冲刺攻击是否完成的决策
    /// 需要配合 AIActionFlyDash 使用
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Dash Complete")]
    public class AIDecisionDashComplete : AIDecision
    {
        protected AIActionFlyDash _flyDashAction;

        /// <summary>
        /// 初始化，获取 FlyDash 组件
        /// </summary>
        public override void Initialization()
        {
            base.Initialization();
            _flyDashAction = this.gameObject.GetComponent<AIActionFlyDash>();
            
            if (_flyDashAction == null)
            {
                Debug.LogWarning("AIDecisionDashComplete: AIActionFlyDash component not found on this GameObject!");
            }
        }

        /// <summary>
        /// 决策：冲刺是否完成
        /// </summary>
        /// <returns>如果冲刺完成返回 true</returns>
        public override bool Decide()
        {
            return EvaluateDashComplete();
        }

        /// <summary>
        /// 评估冲刺是否完成
        /// </summary>
        protected virtual bool EvaluateDashComplete()
        {
            if (_flyDashAction == null)
            {
                return true; // 如果没有 FlyDash 组件，直接返回 true
            }
            
            return _flyDashAction.DashComplete;
        }
    }
}
