using UnityEngine;
using MoreMountains.Tools;
using System.Collections.Generic;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss AOE行为 - 蓄力后从四周发射一圈子弹
    /// Boss在AOE期间完全无敌
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss AOE")]
    public class AIActionBossAOE : AIAction
    {
        [Header("AOE设置")]
        [Tooltip("蓄力时间（播放AOE动画准备）")]
        public float ChargeUpDuration = 0.8f;

        [Tooltip("发射子弹后的结束延迟")]
        public float EndDelay = 0.5f;

        [Header("子弹设置")]
        [Tooltip("子弹预制体")]
        public GameObject ProjectilePrefab;

        [Tooltip("子弹数量")]
        public int ProjectileCount = 12;

        [Tooltip("子弹速度")]
        public float ProjectileSpeed = 10f;

        [Tooltip("子弹伤害")]
        public float ProjectileDamage = 15f;

        [Tooltip("发射高度偏移")]
        public float SpawnHeightOffset = 1f;

        [Tooltip("发射半径")]
        public float SpawnRadius = 1f;

        [Header("多波次设置")]
        [Tooltip("是否发射多波")]
        public bool MultipleWaves = false;

        [Tooltip("波次数量")]
        public int WaveCount = 2;

        [Tooltip("波次间隔")]
        public float WaveInterval = 0.3f;

        [Tooltip("第二波角度偏移（度）")]
        public float WaveAngleOffset = 15f;

        [Header("动画")]
        public string AOEAnimationParameter = "AOE";

        [Header("无敌设置")]
        [Tooltip("AOE期间是否无敌")]
        public bool InvulnerableDuringAOE = true;

        [Header("调试")]
        public bool DebugMode = false;

        public bool AOEComplete { get; protected set; }

        protected Character _character;
        protected Animator _animator;
        protected Health _health;
        protected int _aoeAnimationHash;
        protected float _actionStartTime;
        protected bool _hasFired;
        protected int _currentWave;
        protected float _lastWaveTime;

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
            _actionStartTime = Time.time;

            // 设置无敌
            if (InvulnerableDuringAOE && _health != null)
            {
                _health.DamageDisabled();
            }

            // 播放AOE动画
            ResetAllAnimationParameters();
            if (_animator != null && _aoeAnimationHash != 0)
            {
                _animator.SetBool(_aoeAnimationHash, true);
            }

            if (DebugMode)
            {
                Debug.Log("[BossAOE] ENTER - Starting AOE charge up");
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
            float elapsed = Time.time - _actionStartTime;

            // 蓄力阶段
            if (elapsed < ChargeUpDuration)
            {
                return;
            }

            // 发射子弹
            if (!MultipleWaves)
            {
                // 单波模式
                if (!_hasFired)
                {
                    FireProjectiles(0f);
                    _hasFired = true;
                    if (DebugMode) Debug.Log("[BossAOE] Fired projectiles!");
                }
            }
            else
            {
                // 多波模式
                if (_currentWave < WaveCount)
                {
                    if (_currentWave == 0 || (Time.time - _lastWaveTime >= WaveInterval))
                    {
                        float angleOffset = _currentWave * WaveAngleOffset;
                        FireProjectiles(angleOffset);
                        _currentWave++;
                        _lastWaveTime = Time.time;
                        
                        if (DebugMode) Debug.Log($"[BossAOE] Fired wave {_currentWave}/{WaveCount}");
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
                if (DebugMode) Debug.Log("[BossAOE] COMPLETE");
            }
        }

        protected virtual void FireProjectiles(float angleOffset)
        {
            if (ProjectilePrefab == null)
            {
                if (DebugMode) Debug.LogWarning("[BossAOE] No projectile prefab assigned!");
                return;
            }

            Vector3 spawnCenter = transform.position + Vector3.up * SpawnHeightOffset;
            float angleStep = 360f / ProjectileCount;

            for (int i = 0; i < ProjectileCount; i++)
            {
                float angle = (i * angleStep + angleOffset) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // 计算生成位置
                Vector3 spawnPos = spawnCenter + (Vector3)(direction * SpawnRadius);

                // 设置旋转（让子弹朝向飞行方向）
                float rotAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0f, 0f, rotAngle);

                // 创建子弹
                GameObject projectile = Instantiate(ProjectilePrefab, spawnPos, rotation);

                // 设置子弹方向和速度
                SetupProjectile(projectile, direction);
            }
        }

        protected virtual void SetupProjectile(GameObject projectile, Vector2 direction)
        {
            // 尝试获取Projectile组件（Corgi Engine的）
            Projectile proj = projectile.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.SetDirection(direction, projectile.transform.rotation);
                proj.SetOwner(gameObject);
                return;
            }

            // 如果没有Projectile组件，尝试用Rigidbody2D
            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = direction * ProjectileSpeed;
            }

            // 尝试设置伤害
            DamageOnTouch dot = projectile.GetComponent<DamageOnTouch>();
            if (dot != null)
            {
                dot.MinDamageCaused = ProjectileDamage;
                dot.MaxDamageCaused = ProjectileDamage;
            }
        }

        /// <summary>
        /// 由Animation Event调用（可选）
        /// </summary>
        public void OnAOEAnimationEvent()
        {
            if (!_hasFired)
            {
                FireProjectiles(0f);
                _hasFired = true;
                
                if (DebugMode) Debug.Log("[BossAOE] Animation Event triggered - Fired!");
            }
        }

        public override void OnExitState()
        {
            base.OnExitState();

            // 取消无敌
            if (InvulnerableDuringAOE && _health != null)
            {
                _health.DamageEnabled();
            }

            if (DebugMode) Debug.Log("[BossAOE] EXIT - Invulnerability ended");
        }

        #if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            // 显示子弹发射范围
            Vector3 center = transform.position + Vector3.up * SpawnHeightOffset;
            
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(center, SpawnRadius);

            // 显示子弹方向
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
