using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断Boss AOE是否完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision AOE Complete")]
    public class AIDecisionAOEComplete : AIDecision
    {
        [Tooltip("目标Action组件")]
        public AIActionBossAOE TargetAction;

        public override void Initialization()
        {
            base.Initialization();
            if (TargetAction == null)
            {
                TargetAction = GetComponent<AIActionBossAOE>();
            }
        }

        public override bool Decide()
        {
            return TargetAction != null && TargetAction.AOEComplete;
        }
    }
}
