using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 无限循环视差背景
    /// 支持水平/垂直循环和镜像翻转模式
    /// </summary>
    [AddComponentMenu("Corgi Engine/Camera/Infinite Parallax Element")]
    public class InfiniteParallaxElement : MonoBehaviour
    {
        [Header("Parallax Settings")]
        [Tooltip("水平视差速度 (0 = 静止, 1 = 跟随相机)")]
        [Range(0f, 1f)]
        public float HorizontalSpeed = 0.1f;
        
        [Tooltip("垂直视差速度")]
        [Range(0f, 1f)]
        public float VerticalSpeed = 0f;
        
        [Tooltip("反向移动")]
        public bool MoveInOppositeDirection = true;

        [Header("Loop Settings")]
        [Tooltip("启用水平循环")]
        public bool EnableHorizontalLoop = true;
        
        [Tooltip("启用垂直循环（适用于天空等需要上下延伸的背景）")]
        public bool EnableVerticalLoop = false;
        
        [Tooltip("自动计算背景尺寸")]
        public bool AutoCalculateSize = true;
        
        [Tooltip("背景宽度")]
        public float BackgroundWidth = 200f;
        
        [Tooltip("背景高度")]
        public float BackgroundHeight = 75f;

        [Tooltip("启用镜像模式（水平方向）")]
        public bool UseMirrorModeHorizontal = false;
        
        [Tooltip("启用镜像模式（垂直方向）")]
        public bool UseMirrorModeVertical = false;
        
        [Tooltip("微调水平间距")]
        public float HorizontalSpacingAdjustment = 0f;
        
        [Tooltip("微调垂直间距")]
        public float VerticalSpacingAdjustment = 0f;
        
        [Tooltip("循环检测缓冲区")]
        public float LoopBuffer = 50f;

        [Header("Debug")]
        public bool DebugMode = false;

        // 背景块数据
        protected class SegmentData
        {
            public Transform transform;
            public int indexX;
            public int indexY;
            public bool isFlippedX;
            public bool isFlippedY;
        }

        // 3x3 网格 (如果同时启用水平和垂直) 或 1x3/3x1
        protected SegmentData[,] _segments;
        protected int _gridWidth;
        protected int _gridHeight;

        protected Camera _camera;
        protected Transform _cameraTransform;
        protected Vector3 _previousCameraPosition;
        protected float _actualWidth;
        protected float _actualHeight;
        protected float _cameraHalfWidth;
        protected float _cameraHalfHeight;
        protected bool _initialized;

        protected virtual void Start()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("[InfiniteParallax] Main Camera not found!");
                return;
            }
            
            _cameraTransform = _camera.transform;
            _previousCameraPosition = _cameraTransform.position;
            _cameraHalfWidth = _camera.orthographicSize * _camera.aspect;
            _cameraHalfHeight = _camera.orthographicSize;
            
            CalculateActualSize();

            // 确定网格大小
            _gridWidth = EnableHorizontalLoop ? 3 : 1;
            _gridHeight = EnableVerticalLoop ? 3 : 1;
            
            // 创建背景块网格
            CreateSegmentGrid();

            _initialized = true;

            if (DebugMode)
            {
                Debug.Log($"[InfiniteParallax] Initialized: size=({_actualWidth}, {_actualHeight}), grid={_gridWidth}x{_gridHeight}");
            }
        }

        protected virtual void CalculateActualSize()
        {
            if (!AutoCalculateSize)
            {
                _actualWidth = BackgroundWidth + HorizontalSpacingAdjustment;
                _actualHeight = BackgroundHeight + VerticalSpacingAdjustment;
                return;
            }

            // 从 MeshRenderer 获取
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                _actualWidth = meshRenderer.bounds.size.x + HorizontalSpacingAdjustment;
                _actualHeight = meshRenderer.bounds.size.y + VerticalSpacingAdjustment;
                return;
            }

            // 从 SpriteRenderer 获取
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                _actualWidth = spriteRenderer.bounds.size.x + HorizontalSpacingAdjustment;
                _actualHeight = spriteRenderer.bounds.size.y + VerticalSpacingAdjustment;
                return;
            }

            _actualWidth = BackgroundWidth + HorizontalSpacingAdjustment;
            _actualHeight = BackgroundHeight + VerticalSpacingAdjustment;
        }

        protected virtual void CreateSegmentGrid()
        {
            _segments = new SegmentData[_gridWidth, _gridHeight];
            
            // 中心点索引
            int centerX = _gridWidth / 2;
            int centerY = _gridHeight / 2;

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    int indexX = x - centerX;
                    int indexY = y - centerY;
                    
                    if (x == centerX && y == centerY)
                    {
                        // 中心是自己
                        _segments[x, y] = new SegmentData
                        {
                            transform = transform,
                            indexX = 0,
                            indexY = 0,
                            isFlippedX = false,
                            isFlippedY = false
                        };
                    }
                    else
                    {
                        // 创建克隆
                        _segments[x, y] = CreateClone(indexX, indexY);
                    }
                }
            }
        }

        protected virtual SegmentData CreateClone(int indexX, int indexY)
        {
            var clone = Instantiate(gameObject, transform.parent);
            clone.name = $"{gameObject.name}_({indexX},{indexY})";
            
            var cloneScript = clone.GetComponent<InfiniteParallaxElement>();
            if (cloneScript != null) Destroy(cloneScript);

            // 设置位置
            var pos = transform.position;
            pos.x += indexX * _actualWidth;
            pos.y += indexY * _actualHeight;
            clone.transform.position = pos;

            var segment = new SegmentData
            {
                transform = clone.transform,
                indexX = indexX,
                indexY = indexY,
                isFlippedX = (indexX % 2 != 0),
                isFlippedY = (indexY % 2 != 0)
            };

            ApplyFlip(segment);

            return segment;
        }

        protected virtual void ApplyFlip(SegmentData segment)
        {
            var scale = segment.transform.localScale;
            
            if (UseMirrorModeHorizontal)
            {
                scale.x = segment.isFlippedX ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            }
            
            if (UseMirrorModeVertical)
            {
                scale.y = segment.isFlippedY ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y);
            }
            
            segment.transform.localScale = scale;
        }

        protected virtual void LateUpdate()
        {
            if (!_initialized || _cameraTransform == null) return;

            var cameraDelta = _cameraTransform.position - _previousCameraPosition;
            _previousCameraPosition = _cameraTransform.position;

            if (cameraDelta.x != 0f || cameraDelta.y != 0f)
            {
                ApplyParallax(cameraDelta);
                
                if (EnableHorizontalLoop) CheckHorizontalLoop();
                if (EnableVerticalLoop) CheckVerticalLoop();
            }
        }

        protected virtual void ApplyParallax(Vector3 cameraDelta)
        {
            float directionMul = MoveInOppositeDirection ? -1f : 1f;
            float moveX = cameraDelta.x * HorizontalSpeed * directionMul;
            float moveY = cameraDelta.y * VerticalSpeed * directionMul;

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var pos = _segments[x, y].transform.position;
                    pos.x += moveX;
                    pos.y += moveY;
                    _segments[x, y].transform.position = pos;
                }
            }
        }

        protected virtual void CheckHorizontalLoop()
        {
            float cameraX = _cameraTransform.position.x;
            float cameraLeft = cameraX - _cameraHalfWidth - LoopBuffer;
            float cameraRight = cameraX + _cameraHalfWidth + LoopBuffer;
            float halfWidth = _actualWidth * 0.5f;

            for (int y = 0; y < _gridHeight; y++)
            {
                // 找这一行最左和最右
                float minX = float.MaxValue;
                float maxX = float.MinValue;
                int leftmostIdx = 0;
                int rightmostIdx = 0;

                for (int x = 0; x < _gridWidth; x++)
                {
                    float posX = _segments[x, y].transform.position.x;
                    if (posX < minX) { minX = posX; leftmostIdx = x; }
                    if (posX > maxX) { maxX = posX; rightmostIdx = x; }
                }

                // 最左边出界 -> 移到右边
                if (minX + halfWidth < cameraLeft)
                {
                    var segment = _segments[leftmostIdx, y];
                    var newPos = segment.transform.position;
                    newPos.x = maxX + _actualWidth;
                    segment.transform.position = newPos;
                    
                    segment.indexX = _segments[rightmostIdx, y].indexX + 1;
                    segment.isFlippedX = (segment.indexX % 2 != 0);
                    ApplyFlip(segment);
                }
                // 最右边出界 -> 移到左边
                else if (maxX - halfWidth > cameraRight)
                {
                    var segment = _segments[rightmostIdx, y];
                    var newPos = segment.transform.position;
                    newPos.x = minX - _actualWidth;
                    segment.transform.position = newPos;
                    
                    segment.indexX = _segments[leftmostIdx, y].indexX - 1;
                    segment.isFlippedX = (segment.indexX % 2 != 0);
                    ApplyFlip(segment);
                }
            }
        }

        protected virtual void CheckVerticalLoop()
        {
            float cameraY = _cameraTransform.position.y;
            float cameraBottom = cameraY - _cameraHalfHeight - LoopBuffer;
            float cameraTop = cameraY + _cameraHalfHeight + LoopBuffer;
            float halfHeight = _actualHeight * 0.5f;

            for (int x = 0; x < _gridWidth; x++)
            {
                // 找这一列最下和最上
                float minY = float.MaxValue;
                float maxY = float.MinValue;
                int bottomIdx = 0;
                int topIdx = 0;

                for (int y = 0; y < _gridHeight; y++)
                {
                    float posY = _segments[x, y].transform.position.y;
                    if (posY < minY) { minY = posY; bottomIdx = y; }
                    if (posY > maxY) { maxY = posY; topIdx = y; }
                }

                // 最下边出界 -> 移到上边
                if (minY + halfHeight < cameraBottom)
                {
                    var segment = _segments[x, bottomIdx];
                    var newPos = segment.transform.position;
                    newPos.y = maxY + _actualHeight;
                    segment.transform.position = newPos;
                    
                    segment.indexY = _segments[x, topIdx].indexY + 1;
                    segment.isFlippedY = (segment.indexY % 2 != 0);
                    ApplyFlip(segment);
                }
                // 最上边出界 -> 移到下边
                else if (maxY - halfHeight > cameraTop)
                {
                    var segment = _segments[x, topIdx];
                    var newPos = segment.transform.position;
                    newPos.y = minY - _actualHeight;
                    segment.transform.position = newPos;
                    
                    segment.indexY = _segments[x, bottomIdx].indexY - 1;
                    segment.isFlippedY = (segment.indexY % 2 != 0);
                    ApplyFlip(segment);
                }
            }
        }

        protected virtual void OnDestroy()
        {
            if (_segments == null) return;
            
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    var segment = _segments[x, y];
                    if (segment?.transform != null && segment.transform != transform)
                    {
                        Destroy(segment.transform.gameObject);
                    }
                }
            }
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            float width = BackgroundWidth;
            float height = BackgroundHeight;
            
            if (AutoCalculateSize)
            {
                var mr = GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    width = mr.bounds.size.x;
                    height = mr.bounds.size.y;
                }
            }
            
            Vector3 center = transform.position;
            Vector3 size = new Vector3(width, height, 0.1f);
            
            // 中心
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, size);
            
            // 水平
            if (EnableHorizontalLoop)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(center + Vector3.left * width, size);
                Gizmos.DrawWireCube(center + Vector3.right * width, size);
            }
            
            // 垂直
            if (EnableVerticalLoop)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(center + Vector3.up * height, size);
                Gizmos.DrawWireCube(center + Vector3.down * height, size);
            }
            
            // 四角（如果同时启用）
            if (EnableHorizontalLoop && EnableVerticalLoop)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(center + new Vector3(-width, height, 0), size);
                Gizmos.DrawWireCube(center + new Vector3(width, height, 0), size);
                Gizmos.DrawWireCube(center + new Vector3(-width, -height, 0), size);
                Gizmos.DrawWireCube(center + new Vector3(width, -height, 0), size);
            }
        }
#endif
    }
}
