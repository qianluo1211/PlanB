using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 一体化像素风格交互区域
    /// </summary>
    [AddComponentMenu("Corgi Engine/Environment/Pixel Interact Zone")]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PixelInteractZone : ButtonActivated
    {
        [MMInspectorGroup("按键提示外观", true, 30)]
        
        [Tooltip("显示的按键文字")]
        public string KeyDisplayText = "E";
        
        [Tooltip("提示框世界大小")]
        public float PromptWorldSize = 0.5f;
        
        [Tooltip("提示框相对位置")]
        public Vector3 PromptPosition = new Vector3(0, 1.5f, 0);
        
        [Tooltip("背景颜色")]
        public Color BackgroundColor = new Color(0.12f, 0.12f, 0.18f, 0.95f);
        
        [Tooltip("边框颜色")]
        public Color BorderColor = new Color(0.85f, 0.85f, 0.95f, 1f);
        
        [Tooltip("文字颜色")]
        public Color TextColor = Color.white;
        
        [Tooltip("边框宽度比例")]
        [Range(0.02f, 0.15f)]
        public float BorderRatio = 0.08f;
        
        [Tooltip("自定义字体（可选）")]
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
        
        // 内部引用
        protected GameObject _promptRoot;
        protected CanvasGroup _canvasGroup;
        protected Text _promptText;
        protected Image _innerBgImage;
        
        // 状态
        protected bool _isPromptVisible = false;
        protected float _animTime = 0f;
        protected Vector3 _promptBaseLocalPos;
        protected Color _originalTextColor;
        protected Coroutine _fadeCoroutine;
        
        protected override void OnEnable()
        {
            base.OnEnable();
            UseVisualPrompt = false;
            AlwaysShowPrompt = false;
            ShowPromptWhenColliding = false;
        }
        
        protected virtual void Start()
        {
            CreatePromptUI();
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
            base.TriggerButtonAction(instigator);
            StartCoroutine(PlayPressedEffect());
        }
        
        protected virtual void CreatePromptUI()
        {
            float pixelSize = 100f;
            float borderPixels = pixelSize * BorderRatio;
            
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
            
            RectTransform canvasRect = _promptRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(pixelSize, pixelSize);
            float scale = PromptWorldSize / pixelSize;
            _promptRoot.transform.localScale = new Vector3(scale, scale, scale);
            
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
            innerRect.offsetMin = new Vector2(borderPixels, borderPixels);
            innerRect.offsetMax = new Vector2(-borderPixels, -borderPixels);
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
            _promptText.fontSize = Mathf.RoundToInt(pixelSize * 0.6f);
            _promptText.fontStyle = FontStyle.Bold;
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.color = TextColor;
            _promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _promptText.verticalOverflow = VerticalWrapMode.Overflow;
            _promptText.resizeTextForBestFit = false;
            
            _originalTextColor = TextColor;
        }
        
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
            
            // 如果 GameObject 未激活，无法启动协程，直接隐藏
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
        
        protected virtual IEnumerator PlayPressedEffect()
        {
            if (_promptRoot == null || _innerBgImage == null) yield break;
            
            Vector3 originalScale = _promptRoot.transform.localScale;
            Color originalBgColor = _innerBgImage.color;
            
            _promptRoot.transform.localScale = originalScale * 0.85f;
            _innerBgImage.color = Color.white;
            yield return new WaitForSeconds(0.05f);
            
            _promptRoot.transform.localScale = originalScale * 1.1f;
            _innerBgImage.color = originalBgColor;
            yield return new WaitForSeconds(0.05f);
            
            _promptRoot.transform.localScale = originalScale;
        }
        
        public virtual void SetKeyText(string newText)
        {
            KeyDisplayText = newText;
            if (_promptText != null)
                _promptText.text = newText;
        }
        
#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Vector3 promptPos = transform.position + PromptPosition;
            Gizmos.DrawWireCube(promptPos, new Vector3(PromptWorldSize, PromptWorldSize, 0.1f));
            UnityEditor.Handles.Label(promptPos + Vector3.up * 0.3f, KeyDisplayText);
        }
#endif
    }
}
