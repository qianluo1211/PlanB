using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 分段血条UI组件 - 显示10个独立的血量格子
    /// 每个格子代表10%的血量，血量减少时格子会淡出（保持位置不变）
    /// </summary>
    [AddComponentMenu("Corgi Engine/GUI/Segmented Health Bar")]
    public class SegmentedHealthBar : MonoBehaviour, MMEventListener<HealthChangeEvent>
    {
        [Header("玩家绑定")]
        [Tooltip("目标玩家的PlayerID，用于匹配")]
        public string PlayerID = "Player1";
        
        [Header("分段设置")]
        [Tooltip("血量分段数量")]
        public int SegmentCount = 10;
        
        [Tooltip("血量格子的Image数组（从左到右，索引0-9）")]
        public Image[] HealthSegments;
        
        [Header("动画设置")]
        [Tooltip("格子消失时是否使用淡出动画")]
        public bool UseFadeAnimation = true;
        
        [Tooltip("淡出动画持续时间")]
        public float FadeDuration = 0.2f;
        
        [Tooltip("格子变化时的缩放动画曲线")]
        public AnimationCurve ScaleCurve = AnimationCurve.EaseInOut(0, 1.2f, 1, 1f);
        
        [Tooltip("缩放动画持续时间")]
        public float ScaleDuration = 0.15f;
        
        [Header("反馈")]
        [Tooltip("血量减少时的反馈")]
        public MMFeedbacks DamageFeedback;
        
        [Tooltip("血量恢复时的反馈")]
        public MMFeedbacks HealFeedback;
        
        [Header("调试")]
        public bool DebugMode = false;
        [MMReadOnly] public int CurrentVisibleSegments = 10;
        [MMReadOnly] public float CurrentHealthPercent = 1f;
        
        // 内部状态
        protected int _lastVisibleSegments = 10;
        protected float[] _targetAlphas;        // 目标透明度
        protected float[] _currentAlphas;       // 当前透明度
        protected Color[] _originalColors;      // 原始颜色
        protected Vector3[] _segmentOriginalScales;
        protected float[] _segmentScaleTimers;
        protected bool _initialized = false;
        
        protected virtual void Awake()
        {
            Initialize();
        }
        
        /// <summary>
        /// 初始化组件
        /// </summary>
        protected virtual void Initialize()
        {
            if (_initialized) return;
            
            // 验证分段数组
            if (HealthSegments == null || HealthSegments.Length == 0)
            {
                Debug.LogError("[SegmentedHealthBar] HealthSegments数组未设置!");
                enabled = false;
                return;
            }
            
            SegmentCount = HealthSegments.Length;
            
            // 初始化动画状态数组
            _targetAlphas = new float[SegmentCount];
            _currentAlphas = new float[SegmentCount];
            _originalColors = new Color[SegmentCount];
            _segmentOriginalScales = new Vector3[SegmentCount];
            _segmentScaleTimers = new float[SegmentCount];
            
            for (int i = 0; i < SegmentCount; i++)
            {
                if (HealthSegments[i] != null)
                {
                    _targetAlphas[i] = 1f;
                    _currentAlphas[i] = 1f;
                    _originalColors[i] = HealthSegments[i].color;
                    _segmentOriginalScales[i] = HealthSegments[i].transform.localScale;
                    _segmentScaleTimers[i] = -1f;
                }
            }
            
            _lastVisibleSegments = SegmentCount;
            CurrentVisibleSegments = SegmentCount;
            _initialized = true;
        }
        
        protected virtual void Update()
        {
            if (!_initialized) return;
            UpdateAnimations();
        }
        
        /// <summary>
        /// 更新所有动画效果
        /// </summary>
        protected virtual void UpdateAnimations()
        {
            float deltaTime = Time.deltaTime;
            float fadeSpeed = 1f / FadeDuration;
            
            for (int i = 0; i < SegmentCount; i++)
            {
                if (HealthSegments[i] == null) continue;
                
                // 透明度动画 - 平滑过渡到目标透明度
                if (!Mathf.Approximately(_currentAlphas[i], _targetAlphas[i]))
                {
                    if (UseFadeAnimation)
                    {
                        // 平滑过渡
                        _currentAlphas[i] = Mathf.MoveTowards(_currentAlphas[i], _targetAlphas[i], fadeSpeed * deltaTime);
                    }
                    else
                    {
                        // 直接设置
                        _currentAlphas[i] = _targetAlphas[i];
                    }
                    
                    // 应用透明度
                    Color color = _originalColors[i];
                    color.a = _currentAlphas[i];
                    HealthSegments[i].color = color;
                }
                
                // 缩放动画
                if (_segmentScaleTimers[i] >= 0)
                {
                    _segmentScaleTimers[i] += deltaTime;
                    float progress = Mathf.Clamp01(_segmentScaleTimers[i] / ScaleDuration);
                    float scale = ScaleCurve.Evaluate(progress);
                    HealthSegments[i].transform.localScale = _segmentOriginalScales[i] * scale;
                    
                    if (progress >= 1f)
                    {
                        _segmentScaleTimers[i] = -1f;
                        HealthSegments[i].transform.localScale = _segmentOriginalScales[i];
                    }
                }
            }
        }
        
        /// <summary>
        /// 更新血条显示
        /// </summary>
        /// <param name="currentHealth">当前血量</param>
        /// <param name="minHealth">最小血量</param>
        /// <param name="maxHealth">最大血量</param>
        public virtual void UpdateHealthBar(float currentHealth, float minHealth, float maxHealth)
        {
            if (!_initialized) Initialize();
            
            // 计算血量百分比
            float healthPercent = Mathf.Clamp01((currentHealth - minHealth) / (maxHealth - minHealth));
            CurrentHealthPercent = healthPercent;
            
            // 计算应该显示的格子数量
            // 使用Ceiling确保即使只有1%血量也显示1格
            int targetSegments = Mathf.CeilToInt(healthPercent * SegmentCount);
            
            // 特殊情况：血量为0时不显示任何格子
            if (currentHealth <= minHealth)
            {
                targetSegments = 0;
            }
            
            CurrentVisibleSegments = targetSegments;
            
            // 检测变化方向
            bool isHealing = targetSegments > _lastVisibleSegments;
            bool isDamage = targetSegments < _lastVisibleSegments;
            
            // 更新每个格子的目标透明度
            for (int i = 0; i < SegmentCount; i++)
            {
                if (HealthSegments[i] == null) continue;
                
                bool shouldBeVisible = i < targetSegments;
                bool wasVisible = i < _lastVisibleSegments;
                
                // 设置目标透明度
                _targetAlphas[i] = shouldBeVisible ? 1f : 0f;
                
                // 状态变化时触发缩放动画（仅出现时）
                if (shouldBeVisible && !wasVisible)
                {
                    // 格子出现 - 触发缩放动画
                    _segmentScaleTimers[i] = 0f;
                }
            }
            
            // 触发反馈
            if (isDamage && DamageFeedback != null)
            {
                DamageFeedback.PlayFeedbacks(transform.position);
            }
            else if (isHealing && HealFeedback != null)
            {
                HealFeedback.PlayFeedbacks(transform.position);
            }
            
            _lastVisibleSegments = targetSegments;
            
            if (DebugMode)
            {
                Debug.Log("[SegmentedHealthBar] Health: " + currentHealth + "/" + maxHealth + " = " + targetSegments + " segments");
            }
        }
        
        /// <summary>
        /// 立即设置血条（无动画）
        /// </summary>
        public virtual void SetHealthBarInstant(float currentHealth, float minHealth, float maxHealth)
        {
            if (!_initialized) Initialize();
            
            float healthPercent = Mathf.Clamp01((currentHealth - minHealth) / (maxHealth - minHealth));
            int targetSegments = Mathf.CeilToInt(healthPercent * SegmentCount);
            
            if (currentHealth <= minHealth)
            {
                targetSegments = 0;
            }
            
            for (int i = 0; i < SegmentCount; i++)
            {
                if (HealthSegments[i] == null) continue;
                
                bool shouldBeVisible = i < targetSegments;
                float alpha = shouldBeVisible ? 1f : 0f;
                
                _targetAlphas[i] = alpha;
                _currentAlphas[i] = alpha;
                
                Color color = _originalColors[i];
                color.a = alpha;
                HealthSegments[i].color = color;
            }
            
            _lastVisibleSegments = targetSegments;
            CurrentVisibleSegments = targetSegments;
            CurrentHealthPercent = healthPercent;
        }
        
        /// <summary>
        /// 重置血条到满血状态
        /// </summary>
        public virtual void ResetToFull()
        {
            for (int i = 0; i < SegmentCount; i++)
            {
                if (HealthSegments[i] == null) continue;
                
                _targetAlphas[i] = 1f;
                _currentAlphas[i] = 1f;
                
                HealthSegments[i].color = _originalColors[i];
                HealthSegments[i].transform.localScale = _segmentOriginalScales[i];
                _segmentScaleTimers[i] = -1f;
            }
            
            _lastVisibleSegments = SegmentCount;
            CurrentVisibleSegments = SegmentCount;
            CurrentHealthPercent = 1f;
        }
        
        #region Event Listeners
        
        protected virtual void OnEnable()
        {
            this.MMEventStartListening<HealthChangeEvent>();
        }
        
        protected virtual void OnDisable()
        {
            this.MMEventStopListening<HealthChangeEvent>();
        }
        
        /// <summary>
        /// 监听血量变化事件
        /// </summary>
        public virtual void OnMMEvent(HealthChangeEvent healthChangeEvent)
        {
            // 检查是否是我们要监听的玩家
            if (healthChangeEvent.AffectedHealth == null) return;
            
            Character character = healthChangeEvent.AffectedHealth.GetComponent<Character>();
            if (character == null) return;
            if (character.PlayerID != PlayerID) return;
            
            // 更新血条
            UpdateHealthBar(
                healthChangeEvent.NewHealth,
                0f,
                healthChangeEvent.AffectedHealth.MaximumHealth
            );
        }
        
        #endregion
    }
}
