using UnityEngine;
using System.Collections.Generic;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 残影效果组件
    /// 在角色移动时生成半透明的残影副本
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Abilities/Afterimage Effect")]
    public class AfterimageEffect : MonoBehaviour
    {
        [Header("=== 残影设置 ===")]
        
        [Tooltip("是否启用残影效果")]
        public bool EffectEnabled = true;
        
        [Tooltip("残影生成间隔（秒）")]
        public float SpawnInterval = 0.05f;
        
        [Tooltip("残影持续时间（秒）")]
        public float FadeDuration = 0.3f;
        
        [Tooltip("残影初始透明度")]
        [Range(0f, 1f)]
        public float InitialAlpha = 0.5f;
        
        [Tooltip("残影颜色叠加")]
        public Color TintColor = new Color(0.5f, 0.8f, 1f, 1f);
        
        [Tooltip("对象池大小")]
        public int PoolSize = 20;

        [Header("=== 引用 ===")]
        
        [Tooltip("要复制的SpriteRenderer（留空则自动查找）")]
        public SpriteRenderer TargetRenderer;

        // 对象池
        protected List<AfterimageInstance> _pool = new List<AfterimageInstance>();
        protected float _lastSpawnTime;
        protected bool _isActive = false;
        protected Transform _poolParent;

        protected virtual void Awake()
        {
            // 自动查找SpriteRenderer
            if (TargetRenderer == null)
            {
                TargetRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            
            // 创建对象池父物体
            GameObject poolObj = new GameObject("AfterimagePool");
            poolObj.transform.SetParent(null);
            _poolParent = poolObj.transform;
            
            // 初始化对象池
            InitializePool();
        }

        protected virtual void InitializePool()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                CreatePooledInstance();
            }
        }

        protected virtual AfterimageInstance CreatePooledInstance()
        {
            GameObject obj = new GameObject("Afterimage");
            obj.transform.SetParent(_poolParent);
            
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = TargetRenderer.sortingLayerID;
            sr.sortingOrder = TargetRenderer.sortingOrder - 1;
            
            AfterimageInstance instance = obj.AddComponent<AfterimageInstance>();
            instance.SpriteRenderer = sr;
            instance.gameObject.SetActive(false);
            
            _pool.Add(instance);
            return instance;
        }

        protected virtual AfterimageInstance GetFromPool()
        {
            foreach (var instance in _pool)
            {
                if (!instance.gameObject.activeInHierarchy)
                {
                    return instance;
                }
            }
            
            // 池不够用，创建新的
            return CreatePooledInstance();
        }

        /// <summary>
        /// 开始生成残影
        /// </summary>
        public virtual void StartEffect()
        {
            if (!EffectEnabled) return;
            _isActive = true;
            _lastSpawnTime = Time.time;
        }

        /// <summary>
        /// 停止生成残影
        /// </summary>
        public virtual void StopEffect()
        {
            _isActive = false;
        }

        /// <summary>
        /// 生成一个残影
        /// </summary>
        public virtual void SpawnAfterimage()
        {
            if (TargetRenderer == null || TargetRenderer.sprite == null) return;
            
            AfterimageInstance instance = GetFromPool();
            
            // 复制当前状态
            instance.SpriteRenderer.sprite = TargetRenderer.sprite;
            instance.SpriteRenderer.flipX = TargetRenderer.flipX;
            instance.SpriteRenderer.flipY = TargetRenderer.flipY;
            instance.transform.position = TargetRenderer.transform.position;
            instance.transform.rotation = TargetRenderer.transform.rotation;
            instance.transform.localScale = TargetRenderer.transform.lossyScale;
            
            // 设置颜色和透明度
            Color color = TintColor;
            color.a = InitialAlpha;
            instance.SpriteRenderer.color = color;
            
            // 启动淡出
            instance.StartFade(FadeDuration, InitialAlpha);
            instance.gameObject.SetActive(true);
        }

        protected virtual void Update()
        {
            if (!_isActive) return;
            
            if (Time.time - _lastSpawnTime >= SpawnInterval)
            {
                SpawnAfterimage();
                _lastSpawnTime = Time.time;
            }
        }

        protected virtual void OnDestroy()
        {
            // 清理对象池
            if (_poolParent != null)
            {
                Destroy(_poolParent.gameObject);
            }
        }
    }

    /// <summary>
    /// 单个残影实例
    /// </summary>
    public class AfterimageInstance : MonoBehaviour
    {
        public SpriteRenderer SpriteRenderer;
        
        protected float _fadeDuration;
        protected float _startAlpha;
        protected float _fadeTimer;
        protected bool _isFading;

        public void StartFade(float duration, float startAlpha)
        {
            _fadeDuration = duration;
            _startAlpha = startAlpha;
            _fadeTimer = 0f;
            _isFading = true;
        }

        protected virtual void Update()
        {
            if (!_isFading) return;
            
            _fadeTimer += Time.deltaTime;
            float t = _fadeTimer / _fadeDuration;
            
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                _isFading = false;
                return;
            }
            
            // 淡出
            Color color = SpriteRenderer.color;
            color.a = Mathf.Lerp(_startAlpha, 0f, t);
            SpriteRenderer.color = color;
        }
    }
}
