using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 飞行返回原位 - 智能判断是否需要返回
    /// 如果玩家已经拉开距离，跳过返回直接进入冷却
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Fly To Origin")]
    public class AIActionFlyToOrigin : AIAction
    {
        public enum OriginSourceMode { FromFlyDash, InitialSpawnPosition, CustomTransform }
        
        [Header("Origin Settings")]
        [Tooltip("原点来源")]
        public OriginSourceMode OriginSource = OriginSourceMode.FromFlyDash;
        
        [Tooltip("自定义原点")]
        public Transform CustomOriginTransform;
        
        [Tooltip("到达判定距离")]
        public float ArrivalDistance = 0.5f;
        
        [Tooltip("返回速度倍率")]
        public float ReturnSpeedMultiplier = 1f;

        [Header("Smart Return")]
        [Tooltip("如果距离玩家很远，跳过返回直接进入下一状态")]
        public bool SkipReturnIfFarFromTarget = true;
        
        [Tooltip("跳过返回的距离阈值（超过此距离跳过返回）")]
        public float SkipReturnDistance = 8f;

        protected CharacterFly _characterFly;
        protected AIActionFlyDash _flyDashAction;
        protected Vector3 _originPosition;
        protected Vector3 _initialSpawnPosition;
        protected bool _reachedOrigin;
        
        public bool ReachedOrigin => _reachedOrigin;
        public Vector3 OriginPosition => _originPosition;

        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();
            
            _characterFly = this.gameObject.GetComponentInParent<Character>()?.FindAbility<CharacterFly>();
            _flyDashAction = this.gameObject.GetComponent<AIActionFlyDash>();
            _initialSpawnPosition = this.transform.position;
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            _reachedOrigin = false;
            
            // 智能判断：如果距离玩家已经很远，跳过返回
            if (SkipReturnIfFarFromTarget && _brain != null && _brain.Target != null)
            {
                float distanceToTarget = Vector3.Distance(this.transform.position, _brain.Target.position);
                if (distanceToTarget >= SkipReturnDistance)
                {
                    _reachedOrigin = true; // 直接标记为完成，跳过返回
                    return;
                }
            }
            
            // 确定原点位置
            switch (OriginSource)
            {
                case OriginSourceMode.FromFlyDash:
                    _originPosition = _flyDashAction != null ? _flyDashAction.DashOriginPosition : _initialSpawnPosition;
                    break;
                case OriginSourceMode.InitialSpawnPosition:
                    _originPosition = _initialSpawnPosition;
                    break;
                case OriginSourceMode.CustomTransform:
                    _originPosition = CustomOriginTransform != null ? CustomOriginTransform.position : _initialSpawnPosition;
                    break;
            }
        }

        public override void PerformAction()
        {
            if (_characterFly == null || _reachedOrigin) return;
            
            float distance = Vector3.Distance(this.transform.position, _originPosition);
            if (distance <= ArrivalDistance)
            {
                _reachedOrigin = true;
                StopMovement();
                return;
            }
            
            Vector3 direction = (_originPosition - this.transform.position).normalized;
            _characterFly.SetHorizontalMove(direction.x * ReturnSpeedMultiplier);
            _characterFly.SetVerticalMove(direction.y * ReturnSpeedMultiplier);
        }

        protected virtual void StopMovement()
        {
            _characterFly?.SetHorizontalMove(0f);
            _characterFly?.SetVerticalMove(0f);
        }

        public override void OnExitState()
        {
            base.OnExitState();
            StopMovement();
        }
    }
}
