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

        public override void Initialization()
        {
            base.Initialization();
            _flyDashAction = this.gameObject.GetComponent<AIActionFlyDash>();
        }

        public override bool Decide()
        {
            if (_flyDashAction == null) return true;
            return _flyDashAction.DashComplete;
        }
    }
}
