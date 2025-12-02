using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 延迟转向组件 - 只有当玩家在背后超过一定时间才转向
    /// 独立于 AI 状态机运行
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Abilities/Character Delayed Turn")]
    public class CharacterDelayedTurn : MonoBehaviour
    {
        [Header("设置")]
        [Tooltip("玩家在背后多久后才转向（秒）")]
        public float TurnDelay = 1.5f;

        [Tooltip("转向后的冷却时间（秒）")]
        public float TurnCooldown = 0.5f;

        [Header("状态（只读）")]
        [MMReadOnly]
        public bool IsTargetBehind;
        [MMReadOnly]
        public float TimeBehind;

        protected Character _character;
        protected AIBrain _brain;
        protected float _lastTurnTime;

        protected virtual void Awake()
        {
            _character = GetComponent<Character>();
            _brain = GetComponent<AIBrain>();
        }

        protected virtual void Update()
        {
            if (_character == null || _brain?.Target == null) return;

            // 检测玩家是否在背后
            bool targetOnRight = _brain.Target.position.x > transform.position.x;
            bool facingRight = _character.IsFacingRight;
            IsTargetBehind = (targetOnRight && !facingRight) || (!targetOnRight && facingRight);

            if (IsTargetBehind)
            {
                TimeBehind += Time.deltaTime;

                // 超过延迟时间且不在冷却中，转向
                if (TimeBehind >= TurnDelay && Time.time - _lastTurnTime >= TurnCooldown)
                {
                    _character.Flip();
                    _lastTurnTime = Time.time;
                    TimeBehind = 0f;
                }
            }
            else
            {
                TimeBehind = 0f;
            }
        }
    }
}
