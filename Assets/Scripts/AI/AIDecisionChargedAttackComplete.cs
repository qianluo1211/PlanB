using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 检测AIActionChargedMeleeAttack是否完成的Decision
    /// 用于状态转换：攻击完成后回到Patrol/Idle状态
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Charged Attack Complete")]
    public class AIDecisionChargedAttackComplete : AIDecision
    {
        [Header("Settings")]
        [Tooltip("目标Action（如果为空会自动查找）")]
        public AIActionChargedMeleeAttack TargetAction;

        public override void Initialization()
        {
            base.Initialization();
            
            if (TargetAction == null)
            {
                TargetAction = GetComponent<AIActionChargedMeleeAttack>();
            }
        }

        public override bool Decide()
        {
            if (TargetAction == null) return false;
            return TargetAction.AttackComplete;
        }
    }
}
