using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// Boss空中瞄准行为 - Boss隐身，垂直光柱从天而降追踪玩家
    /// 流程: Boss保持隐身 → 垂直光柱追踪玩家 → 锁定位置 → 完成
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/AI/Actions/AI Action Boss Air Target")]
    public class AIActionBossAirTarget : AIAction
    {
        [Header("瞄准设置")]
        [Tooltip("瞄准总时间")]
        public float TargetingDuration = 2f;

        [Tooltip("追踪玩家的时间（之后锁定位置）")]
        public float TrackingDuration = 1.5f;

        [Header("光柱设置")]
        [Tooltip("光柱高度（从落点向上延伸）")]
        public float BeamHeight = 15f;

        [Tooltip("光柱宽度")]
        public float BeamWidth = 1f;

        [Tooltip("光柱预制体（可选，留空则自动创建）")]
        public GameObject BeamPrefab;

        [Tooltip("光柱颜色 - 追踪中")]
        public Color TrackingColor = new Color(1f, 1f, 0f, 0.5f);

        [Tooltip("光柱颜色 - 已锁定")]
        public Color LockedColor = new Color(1f, 0f, 0f, 0.8f);

        [Tooltip("落点标记预制体（可选）")]
        public GameObject LandingMarkerPrefab;

        [Tooltip("落点标记大小")]
        public float MarkerSize = 2f;

        [Header("调试")]
        public bool DebugMode = false;

        public bool TargetingComplete { get; protected set; }
        public Vector3 LockedTargetPosition { get; protected set; }
        public bool IsLocked { get; protected set; }

        protected Character _character;
        protected CorgiController _controller;
        protected float _actionStartTime;
        protected GameObject _beamInstance;
        protected GameObject _markerInstance;
        protected SpriteRenderer _beamRenderer;
        protected SpriteRenderer _markerRenderer;

        public override void Initialization()
        {
            if (!ShouldInitialize) return;
            base.Initialization();

            _character = GetComponentInParent<Character>();
            _controller = GetComponentInParent<CorgiController>();
        }

        public override void OnEnterState()
        {
            base.OnEnterState();

            TargetingComplete = false;
            IsLocked = false;
            _actionStartTime = Time.time;

            // Boss保持隐身（由TakeOff状态设置）
            // 确保重力关闭
            if (_controller != null)
            {
                _controller.GravityActive(false);
                _controller.SetForce(Vector2.zero);
            }

            // 初始化目标位置
            if (_brain.Target != null)
            {
                LockedTargetPosition = GetGroundPosition(_brain.Target.position);
            }
            else
            {
                LockedTargetPosition = GetGroundPosition(transform.position);
            }

            // 创建光柱和落点标记
            CreateTargetingVisuals();

            if (DebugMode)
            {
                Debug.Log($"[BossAirTarget] ENTER - Starting vertical beam targeting");
            }
        }

        protected virtual void CreateTargetingVisuals()
        {
            // 创建垂直光柱
            if (BeamPrefab != null)
            {
                _beamInstance = Instantiate(BeamPrefab);
                _beamRenderer = _beamInstance.GetComponent<SpriteRenderer>();
            }
            else
            {
                _beamInstance = new GameObject("BossTargetBeam");
                _beamRenderer = _beamInstance.AddComponent<SpriteRenderer>();
                _beamRenderer.sprite = CreateBeamSprite();
                _beamRenderer.sortingOrder = 100;
            }

            // 创建落点标记
            if (LandingMarkerPrefab != null)
            {
                _markerInstance = Instantiate(LandingMarkerPrefab);
                _markerRenderer = _markerInstance.GetComponent<SpriteRenderer>();
            }
            else
            {
                _markerInstance = new GameObject("BossLandingMarker");
                _markerRenderer = _markerInstance.AddComponent<SpriteRenderer>();
                _markerRenderer.sprite = CreateCircleSprite();
                _markerRenderer.sortingOrder = 99;
                _markerInstance.transform.localScale = Vector3.one * MarkerSize;
            }

            UpdateTargetingColor(TrackingColor);
        }

        /// <summary>
        /// 创建光柱精灵（垂直矩形）
        /// </summary>
        protected virtual Sprite CreateBeamSprite()
        {
            int width = 32;
            int height = 256;
            Texture2D tex = new Texture2D(width, height);
            Color[] colors = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 中心渐变，边缘透明
                    float centerDist = Mathf.Abs(x - width / 2f) / (width / 2f);
                    float alpha = 1f - centerDist;
                    
                    // 顶部渐隐
                    float topFade = (float)y / height;
                    alpha *= topFade;

                    colors[y * width + x] = new Color(1f, 1f, 1f, alpha * 0.8f);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 32f);
        }

        /// <summary>
        /// 创建圆形落点标记
        /// </summary>
        protected virtual Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            Color[] colors = new Color[size * size];

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 2f;
            float innerRadius = outerRadius - 6f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < outerRadius && dist > innerRadius)
                    {
                        colors[y * size + x] = Color.white;
                    }
                    else if (dist <= innerRadius)
                    {
                        // 内部半透明填充
                        colors[y * size + x] = new Color(1f, 1f, 1f, 0.3f);
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
            if (_beamRenderer != null)
            {
                _beamRenderer.color = color;
            }
            if (_markerRenderer != null)
            {
                _markerRenderer.color = color;
            }
        }

        public override void PerformAction()
        {
            float elapsed = Time.time - _actionStartTime;

            // 追踪阶段 - 光柱跟随玩家
            if (elapsed < TrackingDuration)
            {
                if (_brain.Target != null)
                {
                    LockedTargetPosition = GetGroundPosition(_brain.Target.position);
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
            if (elapsed >= TargetingDuration)
            {
                TargetingComplete = true;
                if (DebugMode) Debug.Log("[BossAirTarget] COMPLETE");
            }
        }

        protected virtual void UpdateTargetingVisuals()
        {
            // 更新光柱位置和大小
            if (_beamInstance != null)
            {
                // 光柱底部在落点，向上延伸
                _beamInstance.transform.position = LockedTargetPosition;
                _beamInstance.transform.localScale = new Vector3(BeamWidth, BeamHeight, 1f);
            }

            // 更新落点标记
            if (_markerInstance != null)
            {
                _markerInstance.transform.position = LockedTargetPosition;
            }
        }

        protected virtual Vector3 GetGroundPosition(Vector3 position)
        {
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

        public virtual void CleanupVisuals()
        {
            if (_beamInstance != null)
            {
                Destroy(_beamInstance);
                _beamInstance = null;
            }
            if (_markerInstance != null)
            {
                Destroy(_markerInstance);
                _markerInstance = null;
            }
        }

        public override void OnExitState()
        {
            base.OnExitState();

            // 不在这里清理视觉效果，让Dive状态继续使用
            // Dive落地后再清理

            if (DebugMode) Debug.Log("[BossAirTarget] EXIT");
        }

        protected virtual void OnDestroy()
        {
            CleanupVisuals();
        }
    }
}
