using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss AI Action 基类 - 提取所有 Boss Action 的共用代码
    /// </summary>
    public abstract class AIActionBossBase : AIAction
    {
        /// <summary>
        /// 全局日志开关 - 设为 false 关闭所有 Boss Action 日志
        /// </summary>
        public static bool GlobalDebugEnabled = false;

        [Header("调试")]
        [Tooltip("启用此 Action 的调试日志（需要 GlobalDebugEnabled 也为 true）")]
        public bool DebugMode = false;

        // 共用组件缓存
        protected Character _character;
        protected Animator _animator;
        protected Health _health;
        protected CorgiController _controller;
        protected SpriteRenderer _spriteRenderer;
        protected DamageOnTouch _damageOnTouch;
        protected BoxCollider2D _boxCollider;

        // 状态管理
        protected bool _isStateActive;
        protected float _actionStartTime;

        // 共用动画参数列表
        protected static readonly string[] AllAnimationParameters = new string[]
        {
            "Idle", "Walking", "RangeAttack", "MeleeAttack",
            "AOE", "Jump", "Fall", "Land", "Dead"
        };

        /// <summary>
        /// 子类的标签名，用于日志输出
        /// </summary>
        protected abstract string ActionTag { get; }

        /// <summary>
        /// 是否应该输出日志（全局开关 AND 本地开关）
        /// </summary>
        protected bool ShouldLog => GlobalDebugEnabled && DebugMode;

        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();

            CacheComponents();
        }

        /// <summary>
        /// 缓存常用组件，子类可以覆盖并调用 base.CacheComponents() 后添加更多
        /// </summary>
        protected virtual void CacheComponents()
        {
            _character = GetComponentInParent<Character>();
            _animator = _character?.CharacterAnimator;
            _health = GetComponentInParent<Health>();
            _controller = GetComponentInParent<CorgiController>();
            _spriteRenderer = GetComponentInParent<SpriteRenderer>();
            _damageOnTouch = GetComponentInParent<DamageOnTouch>();
            _boxCollider = GetComponentInParent<BoxCollider2D>();

            // 尝试从 CharacterModel 获取 SpriteRenderer
            if (_spriteRenderer == null && _character?.CharacterModel != null)
            {
                _spriteRenderer = _character.CharacterModel.GetComponent<SpriteRenderer>();
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();
            _isStateActive = true;
            _actionStartTime = Time.time;
        }

        public override void OnExitState()
        {
            base.OnExitState();
            _isStateActive = false;
        }

        /// <summary>
        /// 重置所有动画参数为 false
        /// </summary>
        protected virtual void ResetAllAnimationParameters()
        {
            if (_animator == null) return;

            foreach (string param in AllAnimationParameters)
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

        /// <summary>
        /// 设置指定动画参数为 true（先重置所有参数）
        /// </summary>
        protected virtual void SetAnimationParameter(string parameterName)
        {
            ResetAllAnimationParameters();
            if (_animator != null && !string.IsNullOrEmpty(parameterName))
            {
                int hash = Animator.StringToHash(parameterName);
                _animator.SetBool(hash, true);
            }
        }

        /// <summary>
        /// 设置 Boss 可见性和碰撞状态
        /// </summary>
        protected virtual void SetBossVisible(bool visible)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = visible;
            }

            if (_damageOnTouch != null)
            {
                _damageOnTouch.enabled = visible;
            }

            if (_boxCollider != null)
            {
                _boxCollider.enabled = visible;
            }

            LogDebug($"SetBossVisible({visible})");
        }

        /// <summary>
        /// 确保 Boss 可见和可碰撞（用于从隐身状态恢复）
        /// </summary>
        protected virtual void EnsureBossVisible()
        {
            bool changed = false;

            if (_spriteRenderer != null && !_spriteRenderer.enabled)
            {
                _spriteRenderer.enabled = true;
                changed = true;
            }

            if (_boxCollider != null && !_boxCollider.enabled)
            {
                _boxCollider.enabled = true;
                changed = true;
            }

            if (_damageOnTouch != null && !_damageOnTouch.enabled)
            {
                _damageOnTouch.enabled = true;
                changed = true;
            }

            if (changed)
            {
                LogDebug("EnsureBossVisible - Components enabled");
            }
        }

        /// <summary>
        /// 设置无敌状态
        /// </summary>
        protected virtual void SetInvulnerable(bool invulnerable)
        {
            if (_health != null)
            {
                if (invulnerable)
                    _health.DamageDisabled();
                else
                    _health.DamageEnabled();
            }
        }

        /// <summary>
        /// 检查状态是否仍然激活，如果不激活则输出警告
        /// </summary>
        protected bool CheckStateActive(string methodName = null)
        {
            if (!_isStateActive)
            {
                if (ShouldLog && !string.IsNullOrEmpty(methodName))
                {
                    Debug.LogWarning($"[{ActionTag}] {methodName} blocked - state not active");
                }
                return false;
            }
            return true;
        }

        #region 日志工具方法

        protected void LogDebug(string message)
        {
            if (ShouldLog)
            {
                Debug.Log($"[{ActionTag}] {message}");
            }
        }

        protected void LogWarning(string message)
        {
            if (ShouldLog)
            {
                Debug.LogWarning($"[{ActionTag}] {message}");
            }
        }

        protected void LogError(string message)
        {
            Debug.LogError($"[{ActionTag}] {message}");
        }

        protected void LogEnter()
        {
            if (ShouldLog)
            {
                Debug.Log($"[{ActionTag}] ===== ENTER at {Time.time:F3} =====");
            }
        }

        protected void LogExit()
        {
            if (ShouldLog)
            {
                Debug.Log($"[{ActionTag}] ===== EXIT at {Time.time:F3} =====");
            }
        }

        protected void LogComplete()
        {
            if (ShouldLog)
            {
                Debug.Log($"[{ActionTag}] COMPLETE at {Time.time:F3}");
            }
        }

        #endregion
    }
}
