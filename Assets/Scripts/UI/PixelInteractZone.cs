using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 一体化像素风格交互区域
    /// 支持两种模式：
    /// 1. 自动生成模式：不设置 PromptPrefab，使用参数配置外观
    /// 2. Prefab 模式：设置 PromptPrefab，使用自定义 UI 设计
    /// </summary>
    [AddComponentMenu("Corgi Engine/Environment/Pixel Interact Zone")]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PixelInteractZone : ButtonActivated
    {
        [MMInspectorGroup("提示 UI", true, 29)]
        
        [Tooltip("自定义 Prefab（可选，留空则自动生成 UI）")]
        public GameObject PromptPrefab;
        
        [Tooltip("提示框相对位置")]
        public Vector3 PromptPosition = new Vector3(0, 1.5f, 0);
        
        [Tooltip("显示的按键文字")]
        public string KeyDisplayText = "E";
        
        [Tooltip("仅显示提示，禁用按键交互")]
        public bool DisplayOnly = false;
        
        [MMInspectorGroup("自动生成外观（仅无 Prefab 时生效）", true, 30)]
        
        [Tooltip("自动根据文字调整宽度")]
        public bool AutoSizeWidth = true;
        
        [Tooltip("提示框高度（世界单位）")]
        public float PromptHeight = 0.4f;
        
        [Tooltip("固定宽度（仅 AutoSizeWidth=false 时生效）")]
        public float PromptWidth = 0.5f;
        
        [Tooltip("文字内边距（像素）")]
        public float Padding = 20f;
        
        [Tooltip("背景颜色")]
        public Color BackgroundColor = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        
        [Tooltip("边框颜色")]
        public Color BorderColor = new Color(0.85f, 0.85f, 0.95f, 1f);
        
        [Tooltip("文字颜色")]
        public Color TextColor = Color.white;
        
        [Tooltip("边框宽度（像素）")]
        public float BorderWidth = 6f;
        
        [Tooltip("字体大小（像素）")]
        public int FontSize = 55;

        
        [Tooltip("自定义字体")]
        public Font CustomFont;
        
        [MMInspectorGroup("动画效果", true, 31)]
        
        [Tooltip("淡入时间")]
        public float FadeInTime = 0.15f;
        
        [Tooltip("淡出时间")]
        public float FadeOutTime = 0.1f;
        
        [Tooltip("启用上下弹跳")]
        public bool EnableBounce = true;
        
        [Tooltip("弹跳幅度")]
        public float BounceAmplitude = 0.08f;
        
        [Tooltip("弹跳速度")]
        public float BounceSpeed = 3f;
        
        [Tooltip("启用脉冲闪烁")]
        public bool EnablePulse = true;
        
        [Tooltip("脉冲速度")]
        public float PulseSpeed = 2.5f;
        
        [Tooltip("启用按压缩放效果")]
        public bool EnablePressEffect = true;
        
        // 内部引用
        protected GameObject _promptRoot;
        protected CanvasGroup _canvasGroup;
        protected Text _promptText;
        protected Image _innerBgImage;
        protected RectTransform _canvasRect;
        
        // 状态
        protected bool _isPromptVisible = false;
        protected float _animTime = 0f;
        protected Vector3 _promptBaseLocalPos;
        protected Color _originalTextColor;
        protected Coroutine _fadeCoroutine;
        protected bool _usingPrefab = false;
        
        // 常量
        protected const float PIXEL_HEIGHT = 100f;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            UseVisualPrompt = false;
            AlwaysShowPrompt = false;
            ShowPromptWhenColliding = false;
            
            // DisplayOnly 的检查在 TriggerButtonAction 中处理
        }
        
        protected virtual void Start()
        {
            if (PromptPrefab != null)
            {
                CreateFromPrefab();
            }
            else
            {
                CreatePromptUI();
            }
            HidePromptImmediate();
        }
        
        protected virtual void Update()
        {
            if (_isPromptVisible)
            {
                UpdateAnimations();
            }
        }
        
        protected virtual void UpdateAnimations()
        {
            if (_promptRoot == null) return;
            
            _animTime += Time.deltaTime;
            
            if (EnableBounce)
            {
                float bounceY = Mathf.Sin(_animTime * BounceSpeed) * BounceAmplitude;
                _promptRoot.transform.localPosition = _promptBaseLocalPos + new Vector3(0, bounceY, 0);
            }
            
            if (EnablePulse && _promptText != null)
            {
                float pulse = (Mathf.Sin(_animTime * PulseSpeed) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(0.65f, 1f, pulse);
                Color c = _originalTextColor;
                c.a = alpha;
                _promptText.color = c;
            }
        }
        
        protected override void TriggerEnter(GameObject collider)
        {
            base.TriggerEnter(collider);
            ShowPromptAnimated();
        }
        
        protected override void TriggerExit(GameObject collider)
        {
            base.TriggerExit(collider);
            HidePromptAnimated();
        }
        
        public override void TriggerButtonAction(GameObject instigator)
        {
            // 仅显示模式不触发任何交互
            if (DisplayOnly) return;
            
            base.TriggerButtonAction(instigator);
            if (EnablePressEffect)
            {
                StartCoroutine(PlayPressedEffect());
            }
        }
        
        #region === Prefab 模式 ===
        
        protected virtual void CreateFromPrefab()
        {
            _usingPrefab = true;
            
            _promptRoot = Instantiate(PromptPrefab, transform);
            _promptRoot.name = "PromptUI";
            _promptRoot.transform.localPosition = PromptPosition;
            _promptRoot.transform.localRotation = Quaternion.identity;
            _promptBaseLocalPos = PromptPosition;
            
            _canvasGroup = _promptRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = _promptRoot.AddComponent<CanvasGroup>();
            }
            
            Transform keyTextTransform = _promptRoot.transform.Find("KeyText");
            if (keyTextTransform != null)
            {
                _promptText = keyTextTransform.GetComponent<Text>();
            }
            else
            {
                _promptText = _promptRoot.GetComponentInChildren<Text>();
            }
            
            if (_promptText != null)
            {
                _promptText.text = KeyDisplayText;
                _originalTextColor = _promptText.color;
            }
            
            Transform innerBgTransform = _promptRoot.transform.Find("InnerBackground");
            if (innerBgTransform != null)
            {
                _innerBgImage = innerBgTransform.GetComponent<Image>();
            }
            else
            {
                Transform bgTransform = _promptRoot.transform.Find("Background");
                if (bgTransform != null)
                {
                    _innerBgImage = bgTransform.GetComponent<Image>();
                }
            }
        }
        
        #endregion
        
        #region === 自动生成模式 ===
        
        protected virtual void CreatePromptUI()
        {
            _usingPrefab = false;
            
            // === 根物体 ===
            _promptRoot = new GameObject("PromptUI");
            _promptRoot.transform.SetParent(transform);
            _promptRoot.transform.localPosition = PromptPosition;
            _promptRoot.transform.localRotation = Quaternion.identity;
            _promptRoot.transform.localScale = Vector3.one;
            _promptBaseLocalPos = PromptPosition;
            
            // === Canvas (WorldSpace) ===
            Canvas canvas = _promptRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            
            _canvasRect = _promptRoot.GetComponent<RectTransform>();
            
            _promptRoot.AddComponent<CanvasScaler>();
            _promptRoot.AddComponent<GraphicRaycaster>();
            _canvasGroup = _promptRoot.AddComponent<CanvasGroup>();
            
            // === 边框背景 ===
            GameObject border = new GameObject("Border");
            border.transform.SetParent(_promptRoot.transform, false);
            RectTransform borderRect = border.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            Image borderImage = border.AddComponent<Image>();
            borderImage.color = BorderColor;
            
            // === 内部背景 ===
            GameObject innerBg = new GameObject("InnerBackground");
            innerBg.transform.SetParent(_promptRoot.transform, false);
            RectTransform innerRect = innerBg.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(BorderWidth, BorderWidth);
            innerRect.offsetMax = new Vector2(-BorderWidth, -BorderWidth);
            _innerBgImage = innerBg.AddComponent<Image>();
            _innerBgImage.color = BackgroundColor;
            
            // === 文字 ===
            GameObject textObj = new GameObject("KeyText");
            textObj.transform.SetParent(_promptRoot.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            _promptText = textObj.AddComponent<Text>();
            
            if (CustomFont != null)
            {
                _promptText.font = CustomFont;
            }
            else
            {
                Font defaultFont = Resources.Load<Font>("DefaultPixelFont");
                if (defaultFont != null)
                {
                    _promptText.font = defaultFont;
                }
            }
            
            _promptText.text = KeyDisplayText;
            _promptText.fontSize = FontSize;
            _promptText.fontStyle = FontStyle.Bold;
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.color = TextColor;
            _promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _promptText.verticalOverflow = VerticalWrapMode.Overflow;
            _promptText.resizeTextForBestFit = false;
            
            _originalTextColor = TextColor;
            
            // 计算并设置大小
            UpdatePromptSize();
        }
        
        /// <summary>
        /// 根据文字内容更新提示框大小
        /// </summary>
/// <summary>
        /// 根据文字内容更新提示框大小
        /// </summary>
        protected virtual void UpdatePromptSize()
        {
            if (_canvasRect == null || _promptText == null) return;
            
            // 获取文字实际大小
            float textWidth = _promptText.preferredWidth;
            float textHeight = _promptText.preferredHeight;
            
            // 计算框大小（文字 + 内边距 + 边框）
            float pixelWidth;
            float pixelHeight = textHeight + Padding * 2 + BorderWidth * 2;
            
            if (AutoSizeWidth)
            {
                pixelWidth = textWidth + Padding * 2 + BorderWidth * 2;
                // 最小宽度等于高度（保证单字符时是正方形）
                pixelWidth = Mathf.Max(pixelWidth, pixelHeight);
            }
            else
            {
                // 使用固定宽度比例
                pixelWidth = PromptWidth / PromptHeight * pixelHeight;
            }
            
            // 设置 Canvas 大小
            _canvasRect.sizeDelta = new Vector2(pixelWidth, pixelHeight);
            
            // 缩放到世界大小
            float scale = PromptHeight / pixelHeight;
            _promptRoot.transform.localScale = new Vector3(scale, scale, scale);
        }
        
        #endregion
        
        #region === 显示/隐藏 ===
        
        protected virtual void HidePromptImmediate()
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
            if (_promptRoot != null)
                _promptRoot.SetActive(false);
            _isPromptVisible = false;
        }
        
        protected virtual void ShowPromptAnimated()
        {
            if (_promptRoot == null) return;
            
            _promptRoot.SetActive(true);
            _isPromptVisible = true;
            _animTime = 0f;
            
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadePrompt(0f, 1f, FadeInTime));
        }
        
        protected virtual void HidePromptAnimated()
        {
            if (_promptRoot == null) return;
            
            if (!gameObject.activeInHierarchy)
            {
                HidePromptImmediate();
                return;
            }
            
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutAndDisable());
        }
        
        protected virtual IEnumerator FadePrompt(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }
        
        protected virtual IEnumerator FadeOutAndDisable()
        {
            yield return FadePrompt(1f, 0f, FadeOutTime);
            _promptRoot.SetActive(false);
            _isPromptVisible = false;
        }
        
        #endregion
        
        #region === 按压效果 ===
        
        protected virtual IEnumerator PlayPressedEffect()
        {
            if (_promptRoot == null) yield break;
            
            Vector3 originalScale = _promptRoot.transform.localScale;
            Color originalBgColor = _innerBgImage != null ? _innerBgImage.color : Color.clear;
            
            _promptRoot.transform.localScale = originalScale * 0.85f;
            if (_innerBgImage != null)
                _innerBgImage.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            
            _promptRoot.transform.localScale = originalScale * 1.1f;
            if (_innerBgImage != null)
                _innerBgImage.color = originalBgColor;
            yield return new WaitForSeconds(0.05f);
            
            _promptRoot.transform.localScale = originalScale;
        }
        
        #endregion
        
        #region === 公共 API ===
        
        /// <summary>
        /// 运行时修改按键文字（自动更新大小）
        /// </summary>
        public virtual void SetKeyText(string newText)
        {
            KeyDisplayText = newText;
            if (_promptText != null)
            {
                _promptText.text = newText;
                
                // 自动生成模式下更新大小
                if (!_usingPrefab && AutoSizeWidth)
                {
                    UpdatePromptSize();
                }
            }
        }
        
        /// <summary>
        /// 获取是否正在使用 Prefab 模式
        /// </summary>
        public bool IsUsingPrefab => _usingPrefab;
        
        #endregion
        
#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Vector3 promptPos = transform.position + PromptPosition;
            float width = AutoSizeWidth ? 0.5f : PromptWidth;
            float height = PromptHeight;
            if (PromptPrefab != null)
            {
                width = height = 0.5f;
            }
            Gizmos.DrawWireCube(promptPos, new Vector3(width, height, 0.1f));
            UnityEditor.Handles.Label(promptPos + Vector3.up * 0.3f, KeyDisplayText);
        }
#endif
    }
}
