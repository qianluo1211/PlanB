using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 飞行返回原位行为 - 敌人会飞回指定的原点位置
    /// 可以自动从 AIActionFlyDash 获取起始位置，或手动指定
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Fly To Origin")]
    public class AIActionFlyToOrigin : AIAction
    {
        public enum OriginSourceMode
        {
            /// 从 AIActionFlyDash 自动获取
            FromFlyDash,
            /// 使用初始生成位置
            InitialSpawnPosition,
            /// 手动指定的 Transform
            CustomTransform
        }
        
        [Header("Origin Settings")]
        /// 原点来源模式
        [Tooltip("原点来源模式")]
        public OriginSourceMode OriginSource = OriginSourceMode.FromFlyDash;
        
        /// 自定义原点 Transform（当 OriginSource 为 CustomTransform 时使用）
        [Tooltip("自定义原点 Transform")]
        public Transform CustomOriginTransform;
        
        /// 到达原点的判定距离
        [Tooltip("到达原点的判定距离")]
        public float ArrivalDistance = 0.5f;
        
        /// 返回速度倍率
        [Tooltip("返回速度倍率")]
        public float ReturnSpeedMultiplier = 1f;

        [Header("Debug")]
        /// 是否显示调试信息
        [Tooltip("是否显示调试信息")]
        public bool ShowDebugGizmos = true;

        // 组件引用
        protected CharacterFly _characterFly;
        protected Character _character;
        protected AIActionFlyDash _flyDashAction;
        
        // 状态变量
        protected Vector3 _originPosition;
        protected Vector3 _initialSpawnPosition;
        protected bool _reachedOrigin;
        
        /// <summary>
        /// 是否已到达原点
        /// </summary>
        public bool ReachedOrigin => _reachedOrigin;
        
        /// <summary>
        /// 当前目标原点位置
        /// </summary>
        public Vector3 OriginPosition => _originPosition;

        /// <summary>
        /// 初始化
        /// </summary>
        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();
            
            _character = this.gameObject.GetComponentInParent<Character>();
            _characterFly = _character?.FindAbility<CharacterFly>();
            _flyDashAction = this.gameObject.GetComponent<AIActionFlyDash>();
            
            // 记录初始生成位置
            _initialSpawnPosition = this.transform.position;
        }

        /// <summary>
        /// 进入返回状态时，确定目标原点
        /// </summary>
        public override void OnEnterState()
        {
            base.OnEnterState();
            
            _reachedOrigin = false;
            
            // 根据模式确定原点位置
            switch (OriginSource)
            {
                case OriginSourceMode.FromFlyDash:
                    if (_flyDashAction != null)
                    {
                        _originPosition = _flyDashAction.DashOriginPosition;
                    }
                    else
                    {
                        _originPosition = _initialSpawnPosition;
                        Debug.LogWarning("AIActionFlyToOrigin: AIActionFlyDash not found, using initial spawn position.");
                    }
                    break;
                    
                case OriginSourceMode.InitialSpawnPosition:
                    _originPosition = _initialSpawnPosition;
                    break;
                    
                case OriginSourceMode.CustomTransform:
                    if (CustomOriginTransform != null)
                    {
                        _originPosition = CustomOriginTransform.position;
                    }
                    else
                    {
                        _originPosition = _initialSpawnPosition;
                        Debug.LogWarning("AIActionFlyToOrigin: CustomOriginTransform not set, using initial spawn position.");
                    }
                    break;
            }
        }

        /// <summary>
        /// 执行返回行为
        /// </summary>
        public override void PerformAction()
        {
            FlyToOrigin();
        }

        /// <summary>
        /// 飞向原点逻辑
        /// </summary>
        protected virtual void FlyToOrigin()
        {
            if (_characterFly == null || _reachedOrigin)
            {
                return;
            }
            
            // 计算到原点的距离
            float distanceToOrigin = Vector3.Distance(this.transform.position, _originPosition);
            
            // 检查是否到达原点
            if (distanceToOrigin <= ArrivalDistance)
            {
                _reachedOrigin = true;
                StopMovement();
                return;
            }
            
            // 计算方向并移动
            Vector3 direction = (_originPosition - this.transform.position).normalized;
            
            _characterFly.SetHorizontalMove(direction.x * ReturnSpeedMultiplier);
            _characterFly.SetVerticalMove(direction.y * ReturnSpeedMultiplier);
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        protected virtual void StopMovement()
        {
            _characterFly?.SetHorizontalMove(0f);
            _characterFly?.SetVerticalMove(0f);
        }

        /// <summary>
        /// 退出状态时停止移动
        /// </summary>
        public override void OnExitState()
        {
            base.OnExitState();
            StopMovement();
        }

        /// <summary>
        /// 绘制调试信息
        /// </summary>
        protected virtual void OnDrawGizmosSelected()
        {
            if (!ShowDebugGizmos) return;
            
            if (ActionInProgress && !_reachedOrigin)
            {
                // 绘制原点位置
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_originPosition, ArrivalDistance);
                Gizmos.DrawLine(this.transform.position, _originPosition);
            }
            
            // 始终显示初始生成位置
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_initialSpawnPosition, 0.2f);
        }
    }
}
