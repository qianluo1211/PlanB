using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断Boss起跳是否完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Take Off Complete")]
    public class AIDecisionTakeOffComplete : AIDecision
    {
        [Tooltip("目标Action组件")]
        public AIActionBossTakeOff TargetAction;

        public override void Initialization()
        {
            base.Initialization();
            if (TargetAction == null)
            {
                TargetAction = GetComponent<AIActionBossTakeOff>();
            }
        }

        public override bool Decide()
        {
            return TargetAction != null && TargetAction.TakeOffComplete;
        }
    }
}
