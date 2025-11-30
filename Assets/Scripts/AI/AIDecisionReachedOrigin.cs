using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断是否已返回原点的决策
    /// 需要配合 AIActionFlyToOrigin 使用
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Reached Origin")]
    public class AIDecisionReachedOrigin : AIDecision
    {
        protected AIActionFlyToOrigin _flyToOriginAction;

        /// <summary>
        /// 初始化，获取 FlyToOrigin 组件
        /// </summary>
        public override void Initialization()
        {
            base.Initialization();
            _flyToOriginAction = this.gameObject.GetComponent<AIActionFlyToOrigin>();
            
            if (_flyToOriginAction == null)
            {
                Debug.LogWarning("AIDecisionReachedOrigin: AIActionFlyToOrigin component not found on this GameObject!");
            }
        }

        /// <summary>
        /// 决策：是否已到达原点
        /// </summary>
        /// <returns>如果已到达原点返回 true</returns>
        public override bool Decide()
        {
            return EvaluateReachedOrigin();
        }

        /// <summary>
        /// 评估是否已到达原点
        /// </summary>
        protected virtual bool EvaluateReachedOrigin()
        {
            if (_flyToOriginAction == null)
            {
                return true; // 如果没有 FlyToOrigin 组件，直接返回 true
            }
            
            return _flyToOriginAction.ReachedOrigin;
        }
    }
}
