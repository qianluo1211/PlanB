using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 判断玩家是否在指定距离范围内
    /// 用于Boss判断：玩家太近需要后退 / 玩家在射程内可以攻击
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Player Distance")]
    public class AIDecisionPlayerDistance : AIDecision
    {
        public enum CompareMode
        {
            LessThan,      // 玩家距离 < 设定值（太近了）
            GreaterThan,   // 玩家距离 > 设定值（太远了）
            InRange        // 玩家在 Min ~ Max 范围内
        }

        [Header("距离判断设置")]
        [Tooltip("比较模式")]
        public CompareMode Mode = CompareMode.LessThan;

        [Tooltip("距离阈值（LessThan/GreaterThan模式使用）")]
        public float Distance = 3f;

        [Tooltip("最小距离（InRange模式使用）")]
        public float MinDistance = 2f;

        [Tooltip("最大距离（InRange模式使用）")]
        public float MaxDistance = 8f;

        [Header("检测设置")]
        [Tooltip("是否只检测水平距离（忽略Y轴）")]
        public bool HorizontalOnly = false;

        [Tooltip("检测原点偏移")]
        public Vector3 DetectionOffset = Vector3.zero;

        /// <summary>
        /// 当前与目标的距离（供其他脚本读取）
        /// </summary>
        public float CurrentDistance { get; protected set; }

        public override bool Decide()
        {
            return EvaluateDistance();
        }

        protected virtual bool EvaluateDistance()
        {
            if (_brain.Target == null)
            {
                CurrentDistance = float.MaxValue;
                return false;
            }

            Vector3 myPosition = transform.position + DetectionOffset;
            Vector3 targetPosition = _brain.Target.position;

            if (HorizontalOnly)
            {
                // 只计算水平距离
                CurrentDistance = Mathf.Abs(targetPosition.x - myPosition.x);
            }
            else
            {
                // 计算完整距离
                CurrentDistance = Vector3.Distance(myPosition, targetPosition);
            }

            switch (Mode)
            {
                case CompareMode.LessThan:
                    return CurrentDistance < Distance;

                case CompareMode.GreaterThan:
                    return CurrentDistance > Distance;

                case CompareMode.InRange:
                    return CurrentDistance >= MinDistance && CurrentDistance <= MaxDistance;

                default:
                    return false;
            }
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position + DetectionOffset;

            switch (Mode)
            {
                case CompareMode.LessThan:
                    // 红色圆圈表示"太近"的范围
                    Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
                    Gizmos.DrawWireSphere(center, Distance);
                    break;

                case CompareMode.GreaterThan:
                    // 蓝色圆圈表示"太远"的范围
                    Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.3f);
                    Gizmos.DrawWireSphere(center, Distance);
                    break;

                case CompareMode.InRange:
                    // 绿色圆环表示有效范围
                    Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.3f);
                    Gizmos.DrawWireSphere(center, MinDistance);
                    Gizmos.DrawWireSphere(center, MaxDistance);
                    break;
            }
        }
        #endif
    }
}
