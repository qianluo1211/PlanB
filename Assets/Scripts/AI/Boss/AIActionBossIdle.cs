using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss待机行为 - 站在原地，面向玩家
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Idle")]
    public class AIActionBossIdle : AIActionBossBase
    {
        [Header("设置")]
        public bool FaceTarget = true;
        public string IdleAnimationParameter = "Idle";

        protected override string ActionTag => "BossIdle";

        protected CharacterHorizontalMovement _horizontalMovement;

        protected override void CacheComponents()
        {
            base.CacheComponents();
            _horizontalMovement = _character?.FindAbility<CharacterHorizontalMovement>();
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            _horizontalMovement?.SetHorizontalMove(0f);
            SetAnimationParameter(IdleAnimationParameter);

            LogEnter();
        }

        public override void PerformAction()
        {
            _horizontalMovement?.SetHorizontalMove(0f);

            if (FaceTarget && _brain.Target != null)
            {
                FaceTargetDirection();
            }
        }

        protected virtual void FaceTargetDirection()
        {
            if (_brain.Target == null || _character == null) return;

            bool shouldFaceRight = _brain.Target.position.x > transform.position.x;

            if (shouldFaceRight && !_character.IsFacingRight)
                _character.Flip();
            else if (!shouldFaceRight && _character.IsFacingRight)
                _character.Flip();
        }

        public override void OnExitState()
        {
            base.OnExitState();
            LogExit();
        }
    }
}
