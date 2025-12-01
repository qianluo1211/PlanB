using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断Boss空中瞄准是否完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Air Target Complete")]
    public class AIDecisionAirTargetComplete : AIDecision
    {
        [Tooltip("目标Action组件")]
        public AIActionBossAirTarget TargetAction;

        public override void Initialization()
        {
            base.Initialization();
            if (TargetAction == null)
            {
                TargetAction = GetComponent<AIActionBossAirTarget>();
            }
        }

        public override bool Decide()
        {
            return TargetAction != null && TargetAction.TargetingComplete;
        }
    }
}
