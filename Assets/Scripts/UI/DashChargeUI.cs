using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 闪现充能UI显示
    /// 支持多段独立Image模式：每个充能对应一个独立的Image
    /// 监听DashChargeChangedEvent自动更新显示
    /// </summary>
    [AddComponentMenu("Corgi Engine/GUI/Dash Charge UI")]
    public class DashChargeUI : MonoBehaviour, MMEventListener<DashChargeChangedEvent>
    {
        [Header("=== 引用设置 ===")]
        [Tooltip("如果为空，会自动查找Player的DashChargeManager")]
        public DashChargeManager TargetChargeManager;
        
        [Header("=== 显示模式 ===")]
        [Tooltip("显示模式")]
        public DisplayMode Mode = DisplayMode.MultipleImages;
        
        public enum DisplayMode
        {
            MultipleImages, // 多个独立Image模式（每段一个Image）
            Icons,          // 图标模式（动态创建多个图标）
            ProgressBar,    // 进度条模式（连续fillAmount）
            VerticalBar,    // 垂直条模式（改变高度）
            Text            // 文字模式
        }

        [Header("=== 多Image模式设置 ===")]
        [Tooltip("充能条Image数组（按顺序放入每段的Image）")]
        public Image[] ChargeSegmentImages;
        
        [Tooltip("每段需要多少充能（0=自动计算：MaxCharges/段数）")]
        public int ChargesPerSegment = 1;
        
        [Tooltip("充满时的颜色/透明度")]
        public Color SegmentActiveColor = Color.white;
        
        [Tooltip("未充满时的颜色/透明度")]
        public Color SegmentInactiveColor = new Color(1f, 1f, 1f, 0.2f);
        
        [Tooltip("使用透明度切换（true）还是显示/隐藏（false）")]
        public bool UseAlphaTransition = true;

        [Header("=== 技能图标设置 ===")]
        [Tooltip("技能图标")]
        public Image SkillIcon;
        
        [Tooltip("充能不足时技能图标变暗")]
        public bool DimIconWhenInsufficient = true;
        
        [Tooltip("图标变暗时的透明度")]
        [Range(0.2f, 1f)]
        public float DimmedIconAlpha = 0.4f;

        [Header("=== 图标模式设置 ===")]
        [Tooltip("充能图标容器")]
        public Transform IconContainer;
        
        [Tooltip("单个充能图标预制体")]
        public GameObject ChargeIconPrefab;
        
        [Tooltip("充满时的图标颜色")]
        public Color ChargedColor = Color.cyan;
        
        [Tooltip("未充满时的图标颜色")]
        public Color EmptyColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        [Header("=== 进度条模式设置 ===")]
        [Tooltip("充能填充图像（单个fillAmount控制的Image）")]
        public Image ChargeFillImage;

        [Header("=== 垂直条模式设置 ===")]
        [Tooltip("填充条的最大高度（像素）")]
        public float FillMaxHeight = 100f;
        
        [Tooltip("填充条的最小高度（像素）")]
        public float FillMinHeight = 0f;

        [Header("=== 动画设置 ===")]
        [Tooltip("充能变化时播放动画")]
        public Animator UIAnimator;
        
        [Tooltip("获得充能的Trigger名称")]
        public string GainTriggerName = "Gain";
        
        [Tooltip("消耗充能的Trigger名称")]
        public string ConsumeTriggerName = "Consume";
        
        [Tooltip("充满时的Trigger名称")]
        public string FullTriggerName = "Full";

        [Header("=== 颜色设置（进度条模式）===")]
        [Tooltip("充满时的颜色")]
        public Color FullChargeColor = new Color(0.2f, 0.8f, 1f, 1f);
        
        [Tooltip("空时的颜色")]
        public Color EmptyChargeColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        [Header("=== 文字模式设置 ===")]
        [Tooltip("充能文字")]
        public TextMeshProUGUI ChargeText;
        
        [Tooltip("文字格式，{0}=当前充能，{1}=最大充能")]
        public string TextFormat = "{0}/{1}";

        [Header("=== 调试 ===")]
        public bool DebugMode = false;
        
        [MMReadOnly]
        public int CurrentDisplayCharges = 0;
        
        [MMReadOnly]
        public int CurrentActiveSegments = 0;

        // 内部变量
        protected Image[] _chargeIcons;
        protected int _cachedMaxCharges = 0;
        protected RectTransform _fillRectTransform;
        protected int _calculatedChargesPerSegment = 1;

        protected virtual void Awake()
        {
            if (ChargeFillImage != null)
            {
                _fillRectTransform = ChargeFillImage.GetComponent<RectTransform>();
            }
        }

        protected virtual void Start()
        {
            // 自动查找ChargeManager
            if (TargetChargeManager == null)
            {
                var player = LevelManager.Instance?.Players?[0];
                if (player != null)
                {
                    TargetChargeManager = player.GetComponent<DashChargeManager>();
                }
            }
            
            // 初始化显示
            if (TargetChargeManager != null)
            {
                InitializeDisplay(TargetChargeManager.MaxCharges);
                UpdateDisplay(TargetChargeManager.Charges, TargetChargeManager.MaxCharges, true);
            }
            else
            {
                InitializeDisplay(3);
                UpdateDisplay(0, 3, true);
            }
        }

        protected virtual void OnEnable()
        {
            this.MMEventStartListening<DashChargeChangedEvent>();
        }

        protected virtual void OnDisable()
        {
            this.MMEventStopListening<DashChargeChangedEvent>();
        }

        public void OnMMEvent(DashChargeChangedEvent chargeEvent)
        {
            // 如果还没有设置 TargetChargeManager，尝试从事件中获取
            if (TargetChargeManager == null && chargeEvent.Player != null)
            {
                TargetChargeManager = chargeEvent.Player.GetComponent<DashChargeManager>();
            }
            
            // 如果有指定目标，只响应该目标的事件
            if (TargetChargeManager != null && chargeEvent.Player != TargetChargeManager.gameObject)
                return;
            
            // 如果最大充能变化了，重新初始化
            if (chargeEvent.MaxCharges != _cachedMaxCharges)
            {
                InitializeDisplay(chargeEvent.MaxCharges);
            }
            
            UpdateDisplay(chargeEvent.CurrentCharges, chargeEvent.MaxCharges, false);
            
            // 播放动画
            if (UIAnimator != null)
            {
                switch (chargeEvent.ChangeType)
                {
                    case DashChargeChangeType.Gained:
                        UIAnimator.SetTrigger(GainTriggerName);
                        if (chargeEvent.CurrentCharges >= chargeEvent.MaxCharges)
                        {
                            UIAnimator.SetTrigger(FullTriggerName);
                        }
                        break;
                    case DashChargeChangeType.Consumed:
                        UIAnimator.SetTrigger(ConsumeTriggerName);
                        break;
                }
            }
            
            if (DebugMode)
            {
                Debug.Log("[DashChargeUI] 充能更新: " + chargeEvent.CurrentCharges + "/" + chargeEvent.MaxCharges + 
                    " 激活段数: " + CurrentActiveSegments + "/" + GetSegmentCount() +
                    " (" + chargeEvent.ChangeType + ")");
            }
        }

        protected virtual int GetSegmentCount()
        {
            if (ChargeSegmentImages != null)
                return ChargeSegmentImages.Length;
            return 0;
        }

        protected virtual void InitializeDisplay(int maxCharges)
        {
            _cachedMaxCharges = maxCharges;
            
            // 计算每段需要的充能数
            int segmentCount = GetSegmentCount();
            if (ChargesPerSegment > 0)
            {
                _calculatedChargesPerSegment = ChargesPerSegment;
            }
            else if (segmentCount > 0)
            {
                _calculatedChargesPerSegment = Mathf.Max(1, maxCharges / segmentCount);
            }
            else
            {
                _calculatedChargesPerSegment = 1;
            }
            
            if (DebugMode)
            {
                Debug.Log("[DashChargeUI] 初始化: MaxCharges=" + maxCharges + 
                    ", SegmentCount=" + segmentCount + 
                    ", ChargesPerSegment=" + _calculatedChargesPerSegment);
            }
            
            // Icons模式初始化
            if (Mode == DisplayMode.Icons && IconContainer != null && ChargeIconPrefab != null)
            {
                foreach (Transform child in IconContainer)
                {
                    Destroy(child.gameObject);
                }
                
                _chargeIcons = new Image[maxCharges];
                for (int i = 0; i < maxCharges; i++)
                {
                    var icon = Instantiate(ChargeIconPrefab, IconContainer);
                    icon.name = "ChargeIcon_" + i;
                    _chargeIcons[i] = icon.GetComponent<Image>();
                }
            }
        }

        protected virtual void UpdateDisplay(int current, int max, bool immediate)
        {
            CurrentDisplayCharges = current;
            
            switch (Mode)
            {
                case DisplayMode.MultipleImages:
                    UpdateMultipleImagesDisplay(current, max);
                    break;
                case DisplayMode.Icons:
                    UpdateIconsDisplay(current, max);
                    break;
                case DisplayMode.ProgressBar:
                    UpdateProgressBarDisplay(current, max);
                    break;
                case DisplayMode.VerticalBar:
                    UpdateVerticalBarDisplay(current, max);
                    break;
                case DisplayMode.Text:
                    UpdateTextDisplay(current, max);
                    break;
            }
            
            // 检查是否有足够充能使用Dash
            bool canUseDash = TargetChargeManager != null ? TargetChargeManager.HasSufficientCharge : current > 0;
            UpdateSkillIconState(canUseDash);
        }

        /// <summary>
        /// 多Image模式：根据充能数量激活对应数量的Image
        /// </summary>
        protected virtual void UpdateMultipleImagesDisplay(int current, int max)
        {
            if (ChargeSegmentImages == null || ChargeSegmentImages.Length == 0) return;
            
            // 计算应该激活几段
            int activeSegments = 0;
            if (_calculatedChargesPerSegment > 0)
            {
                activeSegments = current / _calculatedChargesPerSegment;
            }
            activeSegments = Mathf.Clamp(activeSegments, 0, ChargeSegmentImages.Length);
            CurrentActiveSegments = activeSegments;
            
            // 更新每段的显示状态
            for (int i = 0; i < ChargeSegmentImages.Length; i++)
            {
                if (ChargeSegmentImages[i] == null) continue;
                
                bool isActive = i < activeSegments;
                
                if (UseAlphaTransition)
                {
                    // 使用颜色/透明度切换
                    ChargeSegmentImages[i].color = isActive ? SegmentActiveColor : SegmentInactiveColor;
                }
                else
                {
                    // 使用显示/隐藏
                    ChargeSegmentImages[i].gameObject.SetActive(isActive);
                }
            }
            
            if (DebugMode)
            {
                Debug.Log("[DashChargeUI] 多Image更新: 充能=" + current + 
                    ", 每段需要=" + _calculatedChargesPerSegment + 
                    ", 激活段数=" + activeSegments + "/" + ChargeSegmentImages.Length);
            }
        }

        protected virtual void UpdateIconsDisplay(int current, int max)
        {
            if (_chargeIcons == null) return;
            
            for (int i = 0; i < _chargeIcons.Length; i++)
            {
                if (_chargeIcons[i] != null)
                {
                    _chargeIcons[i].color = i < current ? ChargedColor : EmptyColor;
                }
            }
        }

        protected virtual void UpdateProgressBarDisplay(int current, int max)
        {
            if (ChargeFillImage == null) return;
            
            float percentage = max > 0 ? (float)current / max : 0f;
            ChargeFillImage.fillAmount = percentage;
            ChargeFillImage.color = Color.Lerp(EmptyChargeColor, FullChargeColor, percentage);
        }

        protected virtual void UpdateVerticalBarDisplay(int current, int max)
        {
            if (_fillRectTransform == null) return;
            
            float percentage = max > 0 ? (float)current / max : 0f;
            float height = Mathf.Lerp(FillMinHeight, FillMaxHeight, percentage);
            
            Vector2 sizeDelta = _fillRectTransform.sizeDelta;
            sizeDelta.y = height;
            _fillRectTransform.sizeDelta = sizeDelta;
        }

        protected virtual void UpdateSkillIconState(bool canUseDash)
        {
            if (!DimIconWhenInsufficient || SkillIcon == null) return;
            
            Color iconColor = SkillIcon.color;
            iconColor.a = canUseDash ? 1f : DimmedIconAlpha;
            SkillIcon.color = iconColor;
        }

        protected virtual void UpdateTextDisplay(int current, int max)
        {
            if (ChargeText == null) return;
            ChargeText.text = string.Format(TextFormat, current, max);
        }

        [ContextMenu("Refresh Display")]
        public virtual void RefreshDisplay()
        {
            if (TargetChargeManager != null)
            {
                InitializeDisplay(TargetChargeManager.MaxCharges);
                UpdateDisplay(TargetChargeManager.Charges, TargetChargeManager.MaxCharges, true);
            }
        }

        [ContextMenu("Test: Show 1 Segment")]
        public virtual void TestOneSegment()
        {
            UpdateMultipleImagesDisplay(1, 3);
        }

        [ContextMenu("Test: Show 2 Segments")]
        public virtual void TestTwoSegments()
        {
            UpdateMultipleImagesDisplay(2, 3);
        }

        [ContextMenu("Test: Show Full")]
        public virtual void TestFullCharge()
        {
            UpdateMultipleImagesDisplay(3, 3);
        }

        [ContextMenu("Test: Show Empty")]
        public virtual void TestEmpty()
        {
            UpdateMultipleImagesDisplay(0, 3);
        }
    }
}
