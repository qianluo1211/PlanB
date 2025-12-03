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

        public override void Initialization()
        {
            base.Initialization();
            _flyToOriginAction = this.gameObject.GetComponent<AIActionFlyToOrigin>();
        }

        public override bool Decide()
        {
            if (_flyToOriginAction == null) return true;
            return _flyToOriginAction.ReachedOrigin;
        }
    }
}
