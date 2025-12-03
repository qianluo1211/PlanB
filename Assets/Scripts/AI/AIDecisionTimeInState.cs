using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// AI决策：检测是否在当前状态停留了指定时间
    /// 可用于冷却时间判断
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Time In State")]
    public class AIDecisionTimeInState : AIDecision
    {
        [Header("Time Settings")]
        [Tooltip("需要在状态中停留的时间（秒）")]
        public float RequiredTime = 1.5f;
        
        [Tooltip("是否添加随机偏移")]
        public bool RandomizeTime = false;
        
        [Tooltip("随机时间范围")]
        public Vector2 RandomTimeRange = new Vector2(1f, 2f);

        protected float _actualRequiredTime;

        public override void Initialization()
        {
            base.Initialization();
            ResetRequiredTime();
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            ResetRequiredTime();
        }

        protected virtual void ResetRequiredTime()
        {
            _actualRequiredTime = RandomizeTime 
                ? Random.Range(RandomTimeRange.x, RandomTimeRange.y) 
                : RequiredTime;
        }

        public override bool Decide()
        {
            return _brain.TimeInThisState >= _actualRequiredTime;
        }
    }
}
