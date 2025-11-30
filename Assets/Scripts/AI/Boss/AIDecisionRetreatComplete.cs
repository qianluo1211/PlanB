using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断AIActionMaintainDistance是否已完成后退
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Retreat Complete")]
    public class AIDecisionRetreatComplete : AIDecision
    {
        [Header("设置")]
        [Tooltip("要检查的MaintainDistance组件（留空自动查找）")]
        public AIActionMaintainDistance TargetAction;

        protected override void Awake()
        {
            base.Awake();
            
            if (TargetAction == null)
            {
                TargetAction = GetComponent<AIActionMaintainDistance>();
            }
        }

        public override bool Decide()
        {
            if (TargetAction == null) return true;
            
            return TargetAction.RetreatComplete;
        }
    }
}
