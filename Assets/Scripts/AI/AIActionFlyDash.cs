using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 飞行冲刺攻击 - 进入状态时锁定目标位置，高速冲向该位置
    /// 支持冲刺音效和残影效果
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Fly Dash")]
    public class AIActionFlyDash : AIAction
    {
        [Header("Dash Settings")]
        [Tooltip("冲刺速度倍率")]
        public float DashSpeedMultiplier = 3f;
        
        [Tooltip("到达判定距离")]
        public float ArrivalDistance = 0.5f;
        
        [Tooltip("最大冲刺时间")]
        public float MaxDashDuration = 2f;
        
        [Tooltip("记录起始位置用于返回")]
        public bool RecordOriginOnDash = true;

        [Header("Feedbacks")]
        [Tooltip("冲刺开始时的反馈（音效、特效等）")]
        public MMFeedbacks DashFeedback;
        
        [Header("Afterimage Effect")]
        [Tooltip("残影效果组件（留空则自动查找）")]
        public AfterimageEffect AfterimageEffectComponent;

        protected CharacterFly _characterFly;
        protected Vector3 _dashTargetPosition;
        protected Vector3 _dashOriginPosition;
        protected float _dashStartTime;
        protected bool _dashComplete;
        protected AfterimageEffect _afterimageEffect;
        
        public bool DashComplete => _dashComplete;
        public Vector3 DashOriginPosition => _dashOriginPosition;

        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();
            _characterFly = this.gameObject.GetComponentInParent<Character>()?.FindAbility<CharacterFly>();
            
            // 初始化残影效果引用
            _afterimageEffect = AfterimageEffectComponent;
            if (_afterimageEffect == null)
                _afterimageEffect = this.gameObject.GetComponentInParent<AfterimageEffect>();
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            
            if (_characterFly == null)
                _characterFly = this.gameObject.GetComponentInParent<Character>()?.FindAbility<CharacterFly>();
            
            _dashComplete = false;
            _dashStartTime = Time.time;
            
            if (RecordOriginOnDash)
                _dashOriginPosition = this.transform.position;
            
            if (_brain != null && _brain.Target != null)
                _dashTargetPosition = _brain.Target.position;
            else
                _dashComplete = true;
            
            // 播放冲刺音效反馈
            DashFeedback?.PlayFeedbacks(this.transform.position);
            
            // 开始残影效果
            _afterimageEffect?.StartEffect();
        }

        public override void PerformAction()
        {
            if (_characterFly == null || _dashComplete) return;
            
            // 超时检查
            if (Time.time - _dashStartTime > MaxDashDuration)
            {
                _dashComplete = true;
                StopMovement();
                return;
            }
            
            // 到达检查
            float distance = Vector3.Distance(this.transform.position, _dashTargetPosition);
            if (distance <= ArrivalDistance)
            {
                _dashComplete = true;
                StopMovement();
                return;
            }
            
            // 冲刺移动
            Vector3 direction = (_dashTargetPosition - this.transform.position).normalized;
            _characterFly.SetHorizontalMove(direction.x * DashSpeedMultiplier);
            _characterFly.SetVerticalMove(direction.y * DashSpeedMultiplier);
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
            
            // 停止残影效果
            _afterimageEffect?.StopEffect();
        }
    }
}
