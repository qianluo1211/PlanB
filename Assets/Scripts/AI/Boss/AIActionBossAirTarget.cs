using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss空中瞄准行为 - 悬停在空中，地面显示红线追踪玩家
    /// 流程: 悬停 → 红线追踪玩家 → 锁定位置 → 完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Air Target")]
    public class AIActionBossAirTarget : AIAction
    {
        [Header("悬停设置")]
        [Tooltip("悬停总时间")]
        public float HoverDuration = 2f;

        [Tooltip("追踪玩家的时间（之后锁定位置）")]
        public float TrackingDuration = 1.5f;

        [Tooltip("悬停时的轻微上下浮动幅度")]
        public float HoverAmplitude = 0.3f;

        [Tooltip("浮动速度")]
        public float HoverFrequency = 2f;

        [Header("瞄准线设置")]
        [Tooltip("瞄准线预制体（LineRenderer或SpriteRenderer）")]
        public GameObject TargetLinePrefab;

        [Tooltip("瞄准线颜色 - 追踪中")]
        public Color TrackingColor = new Color(1f, 1f, 0f, 0.5f);

        [Tooltip("瞄准线颜色 - 已锁定")]
        public Color LockedColor = new Color(1f, 0f, 0f, 0.8f);

        [Tooltip("落点标记预制体")]
        public GameObject LandingMarkerPrefab;

        [Header("动画")]
        public string FallAnimationParameter = "Fall";

        [Header("调试")]
        public bool DebugMode = false;

        public bool TargetingComplete { get; protected set; }
        public Vector3 LockedTargetPosition { get; protected set; }
        public bool IsLocked { get; protected set; }

        protected Character _character;
        protected CorgiController _controller;
        protected Animator _animator;
        protected int _fallAnimationHash;
        protected float _actionStartTime;
        protected Vector3 _hoverBasePosition;
        protected GameObject _targetLineInstance;
        protected GameObject _landingMarkerInstance;
        protected LineRenderer _lineRenderer;
        protected SpriteRenderer _markerRenderer;

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
            _controller = GetComponentInParent<CorgiController>();
            _animator = _character?.CharacterAnimator;

            if (!string.IsNullOrEmpty(FallAnimationParameter))
            {
                _fallAnimationHash = Animator.StringToHash(FallAnimationParameter);
            }
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            TargetingComplete = false;
            IsLocked = false;
            _actionStartTime = Time.time;
            _hoverBasePosition = transform.position;

            // 确保重力关闭
            if (_controller != null)
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }

            // 初始化瞄准目标为玩家位置
            if (_brain.Target != null)
            {
                LockedTargetPosition = GetGroundPosition(_brain.Target.position);
            }
            else
            {
                LockedTargetPosition = GetGroundPosition(transform.position + Vector3.down * 10f);
            }

            // 确保目标位置有效
            ValidateTargetPosition();

            // 创建瞄准线
            CreateTargetingVisuals();

            // 播放Fall动画（空中悬停姿态）
            ResetAllAnimationParameters();
            if (_animator != null && _fallAnimationHash != 0)
            {
                _animator.SetBool(_fallAnimationHash, true);
            }

            if (DebugMode)
            {
                Debug.Log($"[BossAirTarget] ENTER - Hovering at {_hoverBasePosition}, tracking player");
            }
        }

        protected virtual void ValidateTargetPosition()
        {
            if (float.IsNaN(LockedTargetPosition.x) || float.IsNaN(LockedTargetPosition.y))
            {
                LockedTargetPosition = new Vector3(transform.position.x, transform.position.y - 10f, 0f);
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

        protected virtual void CreateTargetingVisuals()
        {
            // 创建瞄准线
            if (TargetLinePrefab != null)
            {
                _targetLineInstance = Instantiate(TargetLinePrefab);
                _lineRenderer = _targetLineInstance.GetComponent<LineRenderer>();
            }
            else
            {
                // 动态创建简单的LineRenderer
                _targetLineInstance = new GameObject("BossTargetLine");
                _lineRenderer = _targetLineInstance.AddComponent<LineRenderer>();
                _lineRenderer.startWidth = 0.2f;
                _lineRenderer.endWidth = 0.2f;
                _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                _lineRenderer.positionCount = 2;
            }

            // 创建落点标记
            if (LandingMarkerPrefab != null)
            {
                _landingMarkerInstance = Instantiate(LandingMarkerPrefab);
                _markerRenderer = _landingMarkerInstance.GetComponent<SpriteRenderer>();
            }
            else
            {
                // 动态创建简单的落点标记
                _landingMarkerInstance = new GameObject("BossLandingMarker");
                _markerRenderer = _landingMarkerInstance.AddComponent<SpriteRenderer>();
                _markerRenderer.sprite = CreateCircleSprite();
                _landingMarkerInstance.transform.localScale = Vector3.one * 3f;
            }

            UpdateTargetingColor(TrackingColor);
        }

        protected virtual Sprite CreateCircleSprite()
        {
            // 创建一个简单的圆形纹理
            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            Color[] colors = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < radius && dist > radius - 4)
                    {
                        colors[y * size + x] = Color.white;
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }
            
            tex.SetPixels(colors);
            tex.Apply();
            
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        protected virtual void UpdateTargetingColor(Color color)
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = color;
                _lineRenderer.endColor = color;
            }
            if (_markerRenderer != null)
            {
                _markerRenderer.color = color;
            }
        }

        public override void PerformAction()
        {
            float elapsed = Time.time - _actionStartTime;

            // 悬停浮动效果 - 使用简单的力而不是除法
            float hoverOffset = Mathf.Sin(elapsed * HoverFrequency * Mathf.PI * 2f) * HoverAmplitude;
            float targetY = _hoverBasePosition.y + hoverOffset;
            float currentY = transform.position.y;
            float yDiff = targetY - currentY;
            
            if (_controller != null)
            {
                // 简单的悬停力，避免除法
                _controller.SetForce(new Vector2(0f, yDiff * 5f));
            }

            // 追踪阶段
            if (elapsed < TrackingDuration)
            {
                // 追踪玩家
                if (_brain.Target != null)
                {
                    LockedTargetPosition = GetGroundPosition(_brain.Target.position);
                    ValidateTargetPosition();
                }
            }
            // 锁定阶段
            else if (!IsLocked)
            {
                IsLocked = true;
                UpdateTargetingColor(LockedColor);
                
                if (DebugMode) Debug.Log($"[BossAirTarget] LOCKED at {LockedTargetPosition}");
            }

            // 更新视觉效果
            UpdateTargetingVisuals();

            // 检查是否完成
            if (elapsed >= HoverDuration)
            {
                TargetingComplete = true;
                if (DebugMode) Debug.Log("[BossAirTarget] COMPLETE");
            }
        }

        protected virtual void UpdateTargetingVisuals()
        {
            // 更新瞄准线（从Boss到落点）
            if (_lineRenderer != null)
            {
                _lineRenderer.SetPosition(0, transform.position);
                _lineRenderer.SetPosition(1, LockedTargetPosition);
            }

            // 更新落点标记
            if (_landingMarkerInstance != null)
            {
                _landingMarkerInstance.transform.position = LockedTargetPosition;
            }
        }

        protected virtual Vector3 GetGroundPosition(Vector3 position)
        {
            // 向下射线检测地面
            RaycastHit2D hit = Physics2D.Raycast(
                new Vector2(position.x, position.y + 1f),
                Vector2.down,
                50f,
                _controller != null ? _controller.PlatformMask : (1 << 8)
            );

            if (hit.collider != null)
            {
                return new Vector3(position.x, hit.point.y, 0f);
            }

            return new Vector3(position.x, position.y, 0f);
        }

        protected virtual void CleanupVisuals()
        {
            if (_targetLineInstance != null)
            {
                Destroy(_targetLineInstance);
                _targetLineInstance = null;
            }
            if (_landingMarkerInstance != null)
            {
                Destroy(_landingMarkerInstance);
                _landingMarkerInstance = null;
            }
        }

        public override void OnExitState()
        {
            base.OnExitState();

            // 停止移动
            if (_controller != null)
            {
                _controller.SetForce(Vector2.zero);
            }

            // 不要在这里清理视觉效果，让Dive状态继续使用
            // CleanupVisuals(); // 由Dive状态或Landing清理

            if (DebugMode) Debug.Log("[BossAirTarget] EXIT");
        }

        protected virtual void OnDestroy()
        {
            CleanupVisuals();
        }
    }
}
