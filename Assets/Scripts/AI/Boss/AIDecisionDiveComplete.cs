using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断Boss俯冲是否完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Dive Complete")]
    public class AIDecisionDiveComplete : AIDecision
    {
        [Tooltip("目标Action组件")]
        public AIActionBossDive TargetAction;

        public override void Initialization()
        {
            base.Initialization();
            if (TargetAction == null)
            {
                TargetAction = GetComponent<AIActionBossDive>();
            }
        }

        public override bool Decide()
        {
            return TargetAction != null && TargetAction.DiveComplete;
        }
    }
}
