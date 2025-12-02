using UnityEngine;
using System.Collections.Generic;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 死亡细胞风格的敌人死亡特效
    /// 敌人死亡时爆发出物理碎片 + 粒子效果
    /// 挂载到任何有 Health 组件的敌人上
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Effects/Enemy Death Effect")]
    public class EnemyDeathEffect : MonoBehaviour
    {
        [Header("=== 碎片设置 ===")]
        public Sprite[] FragmentSprites;
        public Vector2Int FragmentCountRange = new Vector2Int(5, 10);
        public float ExplosionForce = 8f;
        public float ExplosionForceVariance = 3f;
        [Range(0f, 1f)] public float UpwardBias = 0.4f;
        public Vector2 RotationSpeedRange = new Vector2(-720f, 720f);
        public Vector2 FragmentScaleRange = new Vector2(0.8f, 1.2f);

        [Header("=== 碎片物理 ===")]
        public float FragmentGravity = 20f;
        [Range(0f, 1f)] public float BounceFactor = 0.3f;
        public float GroundFriction = 5f;
        public LayerMask GroundLayerMask;

        [Header("=== 生命周期 ===")]
        public float FragmentLifetime = 2f;
        public float FadeOutDuration = 0.5f;

        [Header("=== 粒子爆发 ===")]
        public bool EnableParticleBurst = true;
        public int ParticleCount = 20;
        public Color ParticleColor = Color.clear;
        public float ParticleSize = 0.1f;
        public float ParticleSpeed = 5f;
        public float ParticleLifetime = 0.5f;

        [Header("=== 击退方向 ===")]
        public bool UseLastHitDirection = true;
        [Range(0f, 1f)] public float HitDirectionWeight = 0.5f;

        [Header("=== 反馈 ===")]
        public MoreMountains.Feedbacks.MMFeedbacks DeathFeedbacks;

        [Header("=== 对象池 ===")]
        public int PoolSize = 30;

        [Header("=== 调试 ===")]
        public bool DebugMode = false;

        protected Health _health;
        protected SpriteRenderer _spriteRenderer;
        protected Character _character;
        protected Vector2 _lastHitDirection = Vector2.right;
        protected Color _enemyColor = Color.white;
        
        protected static List<DeathFragment> _fragmentPool = new List<DeathFragment>();
        protected static Transform _poolParent;
        protected static bool _poolInitialized = false;

        protected virtual void Awake()
        {
            _health = GetComponent<Health>();
            _character = GetComponent<Character>();
            
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
            if (_spriteRenderer == null && _character != null && _character.CharacterModel != null)
                _spriteRenderer = _character.CharacterModel.GetComponent<SpriteRenderer>();

            if (_spriteRenderer != null)
                _enemyColor = _spriteRenderer.color;

            if (GroundLayerMask == 0)
                GroundLayerMask = LayerMask.GetMask("Platforms");

            InitializePool();
        }

        protected virtual void Start()
        {
            if (_health != null)
            {
                _health.OnDeath += OnDeath;
                _health.OnHit += OnHit;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDeath -= OnDeath;
                _health.OnHit -= OnHit;
            }
        }

        protected virtual void OnHit()
        {
            if (_health != null)
            {
                Vector3 damageDirection = _health.LastDamageDirection;
                if (damageDirection != Vector3.zero)
                    _lastHitDirection = damageDirection.normalized;
            }
        }

        protected virtual void OnDeath()
        {
            if (DebugMode)
                Debug.Log($"[EnemyDeathEffect] {gameObject.name} died!");

            DeathFeedbacks?.PlayFeedbacks(transform.position);
            SpawnFragments();
            
            if (EnableParticleBurst)
                SpawnParticleBurst();

            HideEnemy();
        }

        protected virtual void SpawnFragments()
        {
            if (FragmentSprites == null || FragmentSprites.Length == 0)
            {
                if (DebugMode) Debug.LogWarning("[EnemyDeathEffect] No fragment sprites!");
                return;
            }

            int count = Random.Range(FragmentCountRange.x, FragmentCountRange.y + 1);
            Vector2 spawnPos = GetSpawnPosition();
            
            for (int i = 0; i < count; i++)
            {
                DeathFragment fragment = GetFragmentFromPool();
                if (fragment == null) continue;

                Sprite sprite = FragmentSprites[Random.Range(0, FragmentSprites.Length)];
                Vector2 direction = CalculateExplosionDirection();
                float force = ExplosionForce + Random.Range(-ExplosionForceVariance, ExplosionForceVariance);
                float rotSpeed = Random.Range(RotationSpeedRange.x, RotationSpeedRange.y);
                float scale = Random.Range(FragmentScaleRange.x, FragmentScaleRange.y);

                fragment.Initialize(
                    sprite, spawnPos + Random.insideUnitCircle * 0.3f, direction * force,
                    rotSpeed, scale, _enemyColor, FragmentGravity, BounceFactor, GroundFriction,
                    GroundLayerMask, FragmentLifetime, FadeOutDuration,
                    _spriteRenderer != null ? _spriteRenderer.sortingLayerID : 0,
                    _spriteRenderer != null ? _spriteRenderer.sortingOrder - 1 : 0
                );
                fragment.gameObject.SetActive(true);
            }
        }

        protected virtual Vector2 CalculateExplosionDirection()
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            dir = Vector2.Lerp(dir, Vector2.up, UpwardBias);
            
            if (UseLastHitDirection && _lastHitDirection != Vector2.zero)
                dir = Vector2.Lerp(dir, -_lastHitDirection, HitDirectionWeight);
            
            return dir.normalized;
        }

        protected virtual Vector2 GetSpawnPosition()
        {
            return _spriteRenderer != null ? (Vector2)_spriteRenderer.bounds.center : (Vector2)transform.position;
        }

        protected virtual void SpawnParticleBurst()
        {
            Vector2 spawnPos = GetSpawnPosition();
            Color col = ParticleColor == Color.clear ? _enemyColor : ParticleColor;

            for (int i = 0; i < ParticleCount; i++)
            {
                GameObject p = new GameObject("DeathParticle");
                p.transform.position = spawnPos + Random.insideUnitCircle * 0.2f;
                
                SpriteRenderer sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = CreatePixelSprite();
                sr.color = col;
                sr.sortingLayerID = _spriteRenderer != null ? _spriteRenderer.sortingLayerID : 0;
                sr.sortingOrder = _spriteRenderer != null ? _spriteRenderer.sortingOrder + 1 : 1;
                p.transform.localScale = Vector3.one * ParticleSize;
                
                SimpleParticle sp = p.AddComponent<SimpleParticle>();
                Vector2 vel = Random.insideUnitCircle.normalized * ParticleSpeed * Random.Range(0.5f, 1f);
                vel.y = Mathf.Abs(vel.y) * 0.5f + vel.y * 0.5f;
                sp.Initialize(vel, ParticleLifetime, FragmentGravity * 0.5f);
            }
        }

        protected virtual Sprite CreatePixelSprite()
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }

        protected virtual void InitializePool()
        {
            if (_poolInitialized) return;

            GameObject poolObj = new GameObject("DeathFragmentPool");
            poolObj.transform.SetParent(null);
            DontDestroyOnLoad(poolObj);
            _poolParent = poolObj.transform;

            for (int i = 0; i < PoolSize; i++)
                CreatePooledFragment();

            _poolInitialized = true;
        }

        protected virtual DeathFragment CreatePooledFragment()
        {
            GameObject obj = new GameObject("DeathFragment");
            obj.transform.SetParent(_poolParent);
            
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            DeathFragment fragment = obj.AddComponent<DeathFragment>();
            fragment.SpriteRenderer = sr;
            
            obj.SetActive(false);
            _fragmentPool.Add(fragment);
            return fragment;
        }

        protected virtual DeathFragment GetFragmentFromPool()
        {
            foreach (var f in _fragmentPool)
                if (f != null && !f.gameObject.activeInHierarchy)
                    return f;
            return CreatePooledFragment();
        }

        protected virtual void HideEnemy()
        {
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }
    }

    public class SimpleParticle : MonoBehaviour
    {
        protected Vector2 _velocity;
        protected float _lifetime;
        protected float _gravity;
        protected float _timer;
        protected SpriteRenderer _sr;

        public void Initialize(Vector2 velocity, float lifetime, float gravity)
        {
            _velocity = velocity;
            _lifetime = lifetime;
            _gravity = gravity;
            _sr = GetComponent<SpriteRenderer>();
        }

        protected virtual void Update()
        {
            _timer += Time.deltaTime;
            _velocity.y -= _gravity * Time.deltaTime;
            transform.position += (Vector3)_velocity * Time.deltaTime;
            
            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = 1f - (_timer / _lifetime);
                _sr.color = c;
            }
            
            if (_timer >= _lifetime)
                Destroy(gameObject);
        }
    }
}
