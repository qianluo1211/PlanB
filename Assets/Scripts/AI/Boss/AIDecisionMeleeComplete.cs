using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断Boss近战攻击是否完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Melee Complete")]
    public class AIDecisionMeleeComplete : AIDecision
    {
        [Tooltip("目标近战攻击Action（留空则自动查找）")]
        public AIActionBossMeleeAttack TargetAction;

        public override void Initialization()
        {
            base.Initialization();

            if (TargetAction == null)
            {
                TargetAction = GetComponent<AIActionBossMeleeAttack>();
            }
        }

        public override bool Decide()
        {
            if (TargetAction == null) return true;
            return TargetAction.AttackComplete;
        }
    }
}
