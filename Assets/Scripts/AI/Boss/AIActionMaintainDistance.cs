using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss保持与玩家的距离 - 当玩家太近时后退
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Maintain Distance")]
    public class AIActionMaintainDistance : AIAction
    {
        [Header("距离设置")]
        public float IdealDistance = 6f;
        public float SafeDistance = 8f;
        public float MinRetreatDistance = 2f;

        [Header("移动设置")]
        public float RetreatSpeedMultiplier = 0.8f;
        public bool FaceTargetWhileRetreating = true;

        [Header("动画")]
        public string WalkAnimationParameter = "Walking";

        [Header("边界检测")]
        public bool CheckForWalls = true;
        public float WallCheckDistance = 1f;
        public bool CheckForCliffs = false;
        public float CliffCheckDistance = 2f;

        [Header("调试")]
        public bool DebugMode = false;

        protected CharacterHorizontalMovement _characterHorizontalMovement;
        protected Character _character;
        protected CorgiController _controller;
        protected Animator _animator;
        protected Vector3 _retreatStartPosition;
        protected float _retreatDirection;
        protected int _walkAnimationParameterHash;
        protected bool _originalFlipSetting;

        protected string[] _allAnimationParameters = new string[] 
        { 
            "Idle", "Walking", "RangeAttack", "MeleeAttack", 
            "AOE", "Jump", "Fall", "Land", "Dead" 
        };

        public bool RetreatComplete { get; protected set; }
        public bool IsBlocked { get; protected set; }

        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();

            _character = GetComponentInParent<Character>();
            _characterHorizontalMovement = _character?.FindAbility<CharacterHorizontalMovement>();
            _controller = GetComponentInParent<CorgiController>();
            _animator = _character?.CharacterAnimator;

            if (!string.IsNullOrEmpty(WalkAnimationParameter))
            {
                _walkAnimationParameterHash = Animator.StringToHash(WalkAnimationParameter);
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            if (_characterHorizontalMovement == null)
            {
                _character = GetComponentInParent<Character>();
                _characterHorizontalMovement = _character?.FindAbility<CharacterHorizontalMovement>();
                _controller = GetComponentInParent<CorgiController>();
                _animator = _character?.CharacterAnimator;
            }

            RetreatComplete = false;
            IsBlocked = false;
            _retreatStartPosition = transform.position;

            if (_brain.Target != null)
            {
                _retreatDirection = transform.position.x > _brain.Target.position.x ? 1f : -1f;
            }

            if (FaceTargetWhileRetreating && _characterHorizontalMovement != null)
            {
                _originalFlipSetting = _characterHorizontalMovement.FlipCharacterToFaceDirection;
                _characterHorizontalMovement.FlipCharacterToFaceDirection = false;
            }

            if (FaceTargetWhileRetreating)
            {
                FaceTarget();
            }

            // 重置所有动画参数，然后只设置Walking
            ResetAllAnimationParameters();
            if (_animator != null && _walkAnimationParameterHash != 0)
            {
                _animator.SetBool(_walkAnimationParameterHash, true);
            }

            if (DebugMode)
            {
                Debug.Log("[MaintainDistance] ENTER - Walking animation ON");
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
            if (_characterHorizontalMovement == null) return;

            if (CheckRetreatComplete())
            {
                StopMovement();
                RetreatComplete = true;
                if (DebugMode) Debug.Log("[MaintainDistance] COMPLETE");
                return;
            }

            if (CheckBlocked())
            {
                StopMovement();
                IsBlocked = true;
                RetreatComplete = true;
                if (DebugMode) Debug.Log("[MaintainDistance] BLOCKED");
                return;
            }

            PerformRetreat();

            if (FaceTargetWhileRetreating)
            {
                FaceTarget();
            }
        }

        protected virtual bool CheckRetreatComplete()
        {
            if (_brain.Target == null) return true;

            float horizontalDistance = Mathf.Abs(transform.position.x - _brain.Target.position.x);
            float retreatedDistance = Mathf.Abs(transform.position.x - _retreatStartPosition.x);

            if (horizontalDistance >= SafeDistance) return true;
            if (retreatedDistance >= MinRetreatDistance && horizontalDistance >= IdealDistance) return true;

            return false;
        }

        protected virtual bool CheckBlocked()
        {
            LayerMask groundMask = _controller != null ? _controller.PlatformMask : (1 << 8);

            if (CheckForWalls)
            {
                Vector2 rayOrigin = (Vector2)transform.position + Vector2.up * 0.5f;
                Vector2 rayDirection = new Vector2(_retreatDirection, 0f);
                RaycastHit2D wallHit = Physics2D.Raycast(rayOrigin, rayDirection, WallCheckDistance, groundMask);
                if (wallHit.collider != null) return true;
            }

            if (CheckForCliffs)
            {
                Vector2 cliffCheckOrigin = (Vector2)transform.position + new Vector2(_retreatDirection * 2f, 0f);
                RaycastHit2D cliffHit = Physics2D.Raycast(cliffCheckOrigin, Vector2.down, CliffCheckDistance, groundMask);
                if (cliffHit.collider == null) return true;
            }

            return false;
        }

        protected virtual void PerformRetreat()
        {
            float moveValue = _retreatDirection * RetreatSpeedMultiplier;
            _characterHorizontalMovement.SetHorizontalMove(moveValue);
        }

        protected virtual void FaceTarget()
        {
            if (_brain.Target == null || _character == null) return;

            bool shouldFaceRight = _brain.Target.position.x > transform.position.x;

            if (shouldFaceRight && !_character.IsFacingRight)
                _character.Flip();
            else if (!shouldFaceRight && _character.IsFacingRight)
                _character.Flip();
        }

        protected virtual void StopMovement()
        {
            _characterHorizontalMovement?.SetHorizontalMove(0f);
        }

        public override void OnExitState()
        {
            base.OnExitState();
            StopMovement();

            if (FaceTargetWhileRetreating && _characterHorizontalMovement != null)
            {
                _characterHorizontalMovement.FlipCharacterToFaceDirection = _originalFlipSetting;
            }
            
            // 不要在这里重置动画参数，让下一个状态来处理

            if (DebugMode) Debug.Log("[MaintainDistance] EXIT");
        }
    }
}
