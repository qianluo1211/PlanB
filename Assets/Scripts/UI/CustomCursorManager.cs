using UnityEngine;
using UnityEngine.UI;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 自定义光标管理器（软件光标版本）
    /// 使用UI Image实现，可自由调整大小，带阴影效果
    /// 游戏中显示自定义瞄准光标，暂停时显示系统默认光标
    /// </summary>
    [AddComponentMenu("Corgi Engine/GUI/Custom Cursor Manager")]
    public class CustomCursorManager : MonoBehaviour, MMEventListener<CorgiEngineEvent>
    {
        [Header("Cursor Settings")]
        [Tooltip("光标图片的Sprite")]
        public Sprite CursorSprite;
        
        [Tooltip("光标大小（像素）")]
        public Vector2 CursorSize = new Vector2(64f, 64f);
        
        [Tooltip("光标颜色")]
        public Color CursorColor = Color.white;
        
        [Header("Shadow Settings")]
        [Tooltip("是否启用阴影")]
        public bool EnableShadow = true;
        
        [Tooltip("阴影颜色")]
        public Color ShadowColor = new Color(0f, 0f, 0f, 0.6f);
        
        [Tooltip("阴影偏移（像素）")]
        public Vector2 ShadowOffset = new Vector2(3f, -3f);
        
        [Tooltip("阴影大小倍数（1=相同大小）")]
        public float ShadowScale = 1.05f;
        
        [Header("Behavior")]
        [Tooltip("暂停时隐藏自定义光标")]
        public bool HideOnPause = true;
        
        [Tooltip("是否隐藏系统光标")]
        public bool HideSystemCursor = true;
        
        [Header("Debug")]
        public bool DebugMode = false;
        
        // 内部引用
        protected Canvas _canvas;
        protected GameObject _cursorObject;
        protected GameObject _shadowObject;
        protected RectTransform _cursorRectTransform;
        protected RectTransform _shadowRectTransform;
        protected Image _cursorImage;
        protected Image _shadowImage;
        protected bool _isPaused = false;
        protected bool _isInitialized = false;
        
        /// <summary>
        /// 初始化
        /// </summary>
        protected virtual void Awake()
        {
            CreateCursorCanvas();
            CreateShadowImage();  // 先创建阴影（在下层）
            CreateCursorImage();  // 再创建光标（在上层）
            _isInitialized = true;
        }
        
        protected virtual void Start()
        {
            // 游戏开始时显示自定义光标
            ShowGameCursor();
        }
        
        /// <summary>
        /// 创建专用Canvas（确保光标始终在最上层）
        /// </summary>
        protected virtual void CreateCursorCanvas()
        {
            // 创建Canvas GameObject
            GameObject canvasObject = new GameObject("CursorCanvas");
            canvasObject.transform.SetParent(transform);
            
            // 添加Canvas组件
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999; // 确保在最上层
            
            // 添加CanvasScaler
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            
            // 添加GraphicRaycaster（但禁用以防止阻挡点击）
            GraphicRaycaster raycaster = canvasObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
        }
        
        /// <summary>
        /// 创建阴影Image
        /// </summary>
        protected virtual void CreateShadowImage()
        {
            // 创建阴影GameObject
            _shadowObject = new GameObject("CursorShadow");
            _shadowObject.transform.SetParent(_canvas.transform);
            
            // 添加RectTransform
            _shadowRectTransform = _shadowObject.AddComponent<RectTransform>();
            _shadowRectTransform.sizeDelta = CursorSize * ShadowScale;
            _shadowRectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            // 添加Image组件
            _shadowImage = _shadowObject.AddComponent<Image>();
            _shadowImage.sprite = CursorSprite;
            _shadowImage.color = ShadowColor;
            _shadowImage.raycastTarget = false;
            
            // 设置阴影不显示（如果禁用）
            _shadowObject.SetActive(EnableShadow);
            
            if (DebugMode)
            {
                Debug.Log("[CustomCursorManager] 阴影Image已创建");
            }
        }
        
        /// <summary>
        /// 创建光标Image
        /// </summary>
        protected virtual void CreateCursorImage()
        {
            // 创建光标GameObject
            _cursorObject = new GameObject("CustomCursor");
            _cursorObject.transform.SetParent(_canvas.transform);
            
            // 添加RectTransform
            _cursorRectTransform = _cursorObject.AddComponent<RectTransform>();
            _cursorRectTransform.sizeDelta = CursorSize;
            _cursorRectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            // 添加Image组件
            _cursorImage = _cursorObject.AddComponent<Image>();
            _cursorImage.sprite = CursorSprite;
            _cursorImage.color = CursorColor;
            _cursorImage.raycastTarget = false;
            
            if (DebugMode)
            {
                Debug.Log("[CustomCursorManager] 光标Image已创建");
            }
        }
        
        /// <summary>
        /// 每帧更新光标位置
        /// </summary>
        protected virtual void Update()
        {
            if (!_isInitialized) return;
            
            Vector3 mousePos = Input.mousePosition;
            
            // 更新光标位置
            if (_cursorObject != null && _cursorObject.activeSelf)
            {
                _cursorRectTransform.position = mousePos;
            }
            
            // 更新阴影位置（带偏移）
            if (_shadowObject != null && _shadowObject.activeSelf)
            {
                _shadowRectTransform.position = mousePos + (Vector3)ShadowOffset;
            }
        }
        
        /// <summary>
        /// 显示游戏自定义光标
        /// </summary>
        public virtual void ShowGameCursor()
        {
            if (_cursorObject != null)
            {
                _cursorObject.SetActive(true);
            }
            
            if (_shadowObject != null)
            {
                _shadowObject.SetActive(EnableShadow);
            }
            
            if (HideSystemCursor)
            {
                Cursor.visible = false;
            }
            
            if (DebugMode)
            {
                Debug.Log("[CustomCursorManager] 显示游戏光标");
            }
        }
        
        /// <summary>
        /// 隐藏游戏光标，显示系统光标
        /// </summary>
        public virtual void ShowSystemCursor()
        {
            if (_cursorObject != null)
            {
                _cursorObject.SetActive(false);
            }
            
            if (_shadowObject != null)
            {
                _shadowObject.SetActive(false);
            }
            
            Cursor.visible = true;
            
            if (DebugMode)
            {
                Debug.Log("[CustomCursorManager] 显示系统光标");
            }
        }
        
        /// <summary>
        /// 设置光标大小
        /// </summary>
        public virtual void SetCursorSize(Vector2 size)
        {
            CursorSize = size;
            if (_cursorRectTransform != null)
            {
                _cursorRectTransform.sizeDelta = size;
            }
            if (_shadowRectTransform != null)
            {
                _shadowRectTransform.sizeDelta = size * ShadowScale;
            }
        }
        
        /// <summary>
        /// 设置光标大小（统一缩放）
        /// </summary>
        public virtual void SetCursorSize(float size)
        {
            SetCursorSize(new Vector2(size, size));
        }
        
        /// <summary>
        /// 设置光标颜色
        /// </summary>
        public virtual void SetCursorColor(Color color)
        {
            CursorColor = color;
            if (_cursorImage != null)
            {
                _cursorImage.color = color;
            }
        }
        
        /// <summary>
        /// 设置阴影颜色
        /// </summary>
        public virtual void SetShadowColor(Color color)
        {
            ShadowColor = color;
            if (_shadowImage != null)
            {
                _shadowImage.color = color;
            }
        }
        
        /// <summary>
        /// 设置阴影偏移
        /// </summary>
        public virtual void SetShadowOffset(Vector2 offset)
        {
            ShadowOffset = offset;
        }
        
        /// <summary>
        /// 启用/禁用阴影
        /// </summary>
        public virtual void SetShadowEnabled(bool enabled)
        {
            EnableShadow = enabled;
            if (_shadowObject != null)
            {
                _shadowObject.SetActive(enabled && _cursorObject.activeSelf);
            }
        }
        
        /// <summary>
        /// 设置光标Sprite
        /// </summary>
        public virtual void SetCursorSprite(Sprite sprite)
        {
            CursorSprite = sprite;
            if (_cursorImage != null)
            {
                _cursorImage.sprite = sprite;
            }
            if (_shadowImage != null)
            {
                _shadowImage.sprite = sprite;
            }
        }
        
        /// <summary>
        /// 监听Corgi Engine事件
        /// </summary>
        public virtual void OnMMEvent(CorgiEngineEvent engineEvent)
        {
            switch (engineEvent.EventType)
            {
                case CorgiEngineEventTypes.Pause:
                    OnPause();
                    break;
                    
                case CorgiEngineEventTypes.UnPause:
                    OnUnPause();
                    break;
            }
        }
        
        /// <summary>
        /// 暂停时调用
        /// </summary>
        protected virtual void OnPause()
        {
            _isPaused = true;
            
            if (HideOnPause)
            {
                ShowSystemCursor();
            }
            
            if (DebugMode)
            {
                Debug.Log("[CustomCursorManager] 游戏暂停");
            }
        }
        
        /// <summary>
        /// 取消暂停时调用
        /// </summary>
        protected virtual void OnUnPause()
        {
            _isPaused = false;
            ShowGameCursor();
            
            if (DebugMode)
            {
                Debug.Log("[CustomCursorManager] 游戏恢复");
            }
        }
        
        /// <summary>
        /// 启用时开始监听事件
        /// </summary>
        protected virtual void OnEnable()
        {
            this.MMEventStartListening<CorgiEngineEvent>();
        }
        
        /// <summary>
        /// 禁用时停止监听事件
        /// </summary>
        protected virtual void OnDisable()
        {
            this.MMEventStopListening<CorgiEngineEvent>();
            
            // 禁用时恢复系统光标
            Cursor.visible = true;
        }
        
        /// <summary>
        /// 应用退出时恢复系统光标
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            Cursor.visible = true;
        }
        
        /// <summary>
        /// 编辑器中验证参数变化
        /// </summary>
        protected virtual void OnValidate()
        {
            if (!Application.isPlaying) return;
            if (!_isInitialized) return;
            
            // 实时更新光标
            if (_cursorRectTransform != null)
            {
                _cursorRectTransform.sizeDelta = CursorSize;
            }
            if (_cursorImage != null)
            {
                _cursorImage.color = CursorColor;
                _cursorImage.sprite = CursorSprite;
            }
            
            // 实时更新阴影
            if (_shadowRectTransform != null)
            {
                _shadowRectTransform.sizeDelta = CursorSize * ShadowScale;
            }
            if (_shadowImage != null)
            {
                _shadowImage.color = ShadowColor;
                _shadowImage.sprite = CursorSprite;
            }
            if (_shadowObject != null)
            {
                _shadowObject.SetActive(EnableShadow && _cursorObject.activeSelf);
            }
        }
    }
}
