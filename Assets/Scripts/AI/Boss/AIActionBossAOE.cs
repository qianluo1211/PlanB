using UnityEngine;
using MoreMountains.Tools;
using System.Collections.Generic;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss AOE行为 - 修复：进入时确保Boss可见和可碰撞
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss AOE")]
    public class AIActionBossAOE : AIAction
    {
        [Header("AOE设置")]
        public float ChargeUpDuration = 0.8f;
        public float EndDelay = 0.5f;

        [Header("子弹设置")]
        public GameObject ProjectilePrefab;
        public int ProjectileCount = 12;
        public float ProjectileSpeed = 10f;
        public float ProjectileDamage = 15f;
        public float SpawnHeightOffset = 1f;
        public float SpawnRadius = 1f;

        [Header("多波次设置")]
        public bool MultipleWaves = false;
        public int WaveCount = 2;
        public float WaveInterval = 0.3f;
        public float WaveAngleOffset = 15f;

        [Header("动画")]
        public string AOEAnimationParameter = "AOE";

        [Header("无敌设置")]
        public bool InvulnerableDuringAOE = true;

        [Header("调试")]
        public bool DebugMode = false;

        public bool AOEComplete { get; protected set; }

        protected Character _character;
        protected Animator _animator;
        protected Health _health;
        protected SpriteRenderer _spriteRenderer;
        protected DamageOnTouch _damageOnTouch;
        protected BoxCollider2D _boxCollider;
        protected CorgiController _controller;
        protected int _aoeAnimationHash;
        protected float _actionStartTime;
        protected bool _hasFired;
        protected int _currentWave;
        protected float _lastWaveTime;
        protected bool _isStateActive;

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
            _health = GetComponentInParent<Health>();
            _spriteRenderer = GetComponentInParent<SpriteRenderer>();
            _damageOnTouch = GetComponentInParent<DamageOnTouch>();
            _boxCollider = GetComponentInParent<BoxCollider2D>();
            _controller = GetComponentInParent<CorgiController>();

            if (_spriteRenderer == null && _character?.CharacterModel != null)
            {
                _spriteRenderer = _character.CharacterModel.GetComponent<SpriteRenderer>();
            }

            if (!string.IsNullOrEmpty(AOEAnimationParameter))
            {
                _aoeAnimationHash = Animator.StringToHash(AOEAnimationParameter);
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            AOEComplete = false;
            _hasFired = false;
            _currentWave = 0;
            _lastWaveTime = 0f;
            _isStateActive = true;
            _actionStartTime = Time.time;

            Debug.Log($"[BossAOE] ===== ENTER at {Time.time:F3} =====");

            // ★ 关键：确保Boss可见和可碰撞（因为可能跳过了Dive状态）
            EnsureBossVisible();

            // 设置无敌
            if (InvulnerableDuringAOE && _health != null)
            {
                _health.DamageDisabled();
            }

            // 启用重力
            if (_controller != null)
            {
                _controller.GravityActive(true);
            }

            // 播放AOE动画
            ResetAllAnimationParameters();
            if (_animator != null && _aoeAnimationHash != 0)
            {
                _animator.SetBool(_aoeAnimationHash, true);
            }
        }

        /// <summary>
        /// 确保Boss可见和可碰撞
        /// </summary>
        protected virtual void EnsureBossVisible()
        {
            if (_spriteRenderer != null && !_spriteRenderer.enabled)
            {
                _spriteRenderer.enabled = true;
                Debug.Log("[BossAOE] Enabled SpriteRenderer");
            }

            if (_boxCollider != null && !_boxCollider.enabled)
            {
                _boxCollider.enabled = true;
                Debug.Log("[BossAOE] Enabled BoxCollider2D");
            }

            if (_damageOnTouch != null && !_damageOnTouch.enabled)
            {
                _damageOnTouch.enabled = true;
                Debug.Log("[BossAOE] Enabled DamageOnTouch");
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
            if (!_isStateActive) return;

            float elapsed = Time.time - _actionStartTime;

            // 蓄力阶段
            if (elapsed < ChargeUpDuration)
            {
                return;
            }

            // 发射子弹
            if (!MultipleWaves)
            {
                if (!_hasFired)
                {
                    FireProjectiles(0f);
                    _hasFired = true;
                    Debug.Log("[BossAOE] Fired projectiles!");
                }
            }
            else
            {
                if (_currentWave < WaveCount)
                {
                    if (_currentWave == 0 || (Time.time - _lastWaveTime >= WaveInterval))
                    {
                        float angleOffset = _currentWave * WaveAngleOffset;
                        FireProjectiles(angleOffset);
                        _currentWave++;
                        _lastWaveTime = Time.time;
                        Debug.Log($"[BossAOE] Fired wave {_currentWave}/{WaveCount}");
                    }
                }
                else
                {
                    _hasFired = true;
                }
            }

            // 检查是否完成
            float totalDuration = ChargeUpDuration + EndDelay;
            if (MultipleWaves)
            {
                totalDuration += WaveInterval * (WaveCount - 1);
            }

            if (elapsed >= totalDuration && _hasFired)
            {
                AOEComplete = true;
                Debug.Log("[BossAOE] COMPLETE");
            }
        }

        protected virtual void FireProjectiles(float angleOffset)
        {
            if (!_isStateActive) return;

            if (ProjectilePrefab == null)
            {
                Debug.LogWarning("[BossAOE] No projectile prefab!");
                return;
            }

            Vector3 spawnCenter = transform.position + Vector3.up * SpawnHeightOffset;
            float angleStep = 360f / ProjectileCount;

            for (int i = 0; i < ProjectileCount; i++)
            {
                float angle = (i * angleStep + angleOffset) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector3 spawnPos = spawnCenter + (Vector3)(direction * SpawnRadius);
                float rotAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0f, 0f, rotAngle);

                GameObject projectile = Instantiate(ProjectilePrefab, spawnPos, rotation);
                SetupProjectile(projectile, direction);
            }
        }

        protected virtual void SetupProjectile(GameObject projectile, Vector2 direction)
        {
            Projectile proj = projectile.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.SetDirection(direction, projectile.transform.rotation);
                proj.SetOwner(gameObject);
                return;
            }

            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = direction * ProjectileSpeed;
            }

            DamageOnTouch dot = projectile.GetComponent<DamageOnTouch>();
            if (dot != null)
            {
                dot.MinDamageCaused = ProjectileDamage;
                dot.MaxDamageCaused = ProjectileDamage;
            }
        }

        public void OnAOEAnimationEvent()
        {
            if (!_isStateActive) return;

            if (!_hasFired)
            {
                FireProjectiles(0f);
                _hasFired = true;
                Debug.Log("[BossAOE] AnimationEvent triggered!");
            }
        }

        public override void OnExitState()
        {
            base.OnExitState();

            _isStateActive = false;

            // 取消无敌
            if (InvulnerableDuringAOE && _health != null)
            {
                _health.DamageEnabled();
            }

            Debug.Log($"[BossAOE] ===== EXIT at {Time.time:F3} =====");
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Vector3 center = transform.position + Vector3.up * SpawnHeightOffset;
            
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(center, SpawnRadius);

            if (ProjectileCount > 0)
            {
                Gizmos.color = Color.red;
                float angleStep = 360f / ProjectileCount;
                for (int i = 0; i < ProjectileCount; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    Gizmos.DrawLine(center, center + dir * 3f);
                }
            }
        }
        #endif
    }
}
