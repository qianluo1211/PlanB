using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss待机行为 - 站在原地，面向玩家
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Idle")]
    public class AIActionBossIdle : AIAction
    {
        [Header("设置")]
        public bool FaceTarget = true;
        public string IdleAnimationParameter = "Idle";

        [Header("调试")]
        public bool DebugMode = false;

        protected Character _character;
        protected Animator _animator;
        protected CharacterHorizontalMovement _horizontalMovement;
        protected int _idleAnimationParameterHash;

        protected string[] _allAnimationParameters = new string[] 
        { 
            "Idle", "Walking", "RangeAttack", "MeleeAttack", 
            "AOE", "Jump", "Fall", "Land", "Dead" 
        };

        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();

            _character = GetComponentInParent<Character>();
            _animator = _character?.CharacterAnimator;
            _horizontalMovement = _character?.FindAbility<CharacterHorizontalMovement>();

            if (!string.IsNullOrEmpty(IdleAnimationParameter))
            {
                _idleAnimationParameterHash = Animator.StringToHash(IdleAnimationParameter);
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            _horizontalMovement?.SetHorizontalMove(0f);

            // 重置所有动画参数，然后只设置Idle
            ResetAllAnimationParameters();
            if (_animator != null && _idleAnimationParameterHash != 0)
            {
                _animator.SetBool(_idleAnimationParameterHash, true);
            }

            if (DebugMode)
            {
                Debug.Log("[BossIdle] ENTER - Idle animation ON");
            }
        }

        protected virtual void ResetAllAnimationParameters()
        {
            if (_animator == null) return;

            foreach (string param in _allAnimationParameters)
            {
                int hash = Animator.StringToHash(param);
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Bool)
                    {
                        _animator.SetBool(hash, false);
                        break;
                    }
                }
            }
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
            // 不要在这里重置动画参数，让下一个状态来处理

            if (DebugMode)
            {
                Debug.Log("[BossIdle] EXIT");
            }
        }
    }
}
