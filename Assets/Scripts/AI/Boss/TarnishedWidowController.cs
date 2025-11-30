using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// The Tarnished Widow 专用控制器
    /// 继承自通用BossController，可以添加这个Boss特有的行为
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Boss/Tarnished Widow Controller")]
    public class TarnishedWidowController : BossController
    {
        [Header("=== Tarnished Widow 专属设置 ===")]
        [Tooltip("蜘蛛网陷阱预制体（如果有的话）")]
        public GameObject WebTrapPrefab;

        [Tooltip("是否启用蜘蛛网陷阱")]
        public bool EnableWebTraps = false;

        [Tooltip("蜘蛛网陷阱间隔")]
        public float WebTrapInterval = 10f;

        // 如果这个Boss有特殊行为，在这里添加
        // 例如：蜘蛛网陷阱、召唤小蜘蛛等

        protected override void Awake()
        {
            // 设置默认名称
            if (string.IsNullOrEmpty(BossName))
            {
                BossName = "The Tarnished Widow";
            }

            base.Awake();
        }

        /// <summary>
        /// 可以重写阶段进入逻辑来添加特殊效果
        /// </summary>
        protected override void OnEnterPhase(BossPhase phase)
        {
            base.OnEnterPhase(phase);

            // Tarnished Widow 特有的阶段处理
            switch (phase)
            {
                case BossPhase.AOE:
                    // 可以在这里添加蜘蛛特有的AOE效果
                    break;

                case BossPhase.Airborne:
                    // 可以在这里添加空中吐丝等效果
                    break;
            }
        }
    }
}
