using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断Boss远程攻击是否完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Attack Complete")]
    public class AIDecisionAttackComplete : AIDecision
    {
        [Header("设置")]
        [Tooltip("要检查的攻击Action（留空自动查找）")]
        public AIActionBossRangedAttack TargetAction;

        protected override void Awake()
        {
            base.Awake();

            if (TargetAction == null)
            {
                TargetAction = GetComponent<AIActionBossRangedAttack>();
            }
        }

        public override bool Decide()
        {
            if (TargetAction == null) return true;

            return TargetAction.AttackComplete;
        }
    }
}
