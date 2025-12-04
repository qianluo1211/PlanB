using UnityEngine;
using System.Collections.Generic;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 简单可靠的无限循环视差背景
    /// 支持镜像翻转模式，让非循环贴图也能无缝衔接
    /// </summary>
    [AddComponentMenu("Corgi Engine/Camera/Infinite Parallax Element")]
    public class InfiniteParallaxElement : MonoBehaviour
    {
        [Header("Parallax Settings")]
        [Tooltip("水平视差速度 (0 = 静止, 1 = 跟随相机)")]
        [Range(0f, 1f)]
        public float HorizontalSpeed = 0.1f;
        
        [Tooltip("反向移动")]
        public bool MoveInOppositeDirection = true;

        [Header("Loop Settings")]
        [Tooltip("自动计算背景宽度（从 Renderer.bounds 获取）")]
        public bool AutoCalculateWidth = true;
        
        [Tooltip("背景宽度 - 如果不自动计算，则手动设置")]
        public float BackgroundWidth = 200f;

        [Tooltip("启用镜像模式 - 相邻背景块会X轴翻转，适用于非循环贴图")]
        public bool UseMirrorMode = false;
        
        [Tooltip("微调间距 - 正数增加间距，负数减少间距（用于修复重叠或缝隙）")]
        public float SpacingAdjustment = 0f;

        [Header("Debug")]
        public bool DebugMode = false;

        // 内部变量
        protected Camera _camera;
        protected Transform _cameraTransform;
        protected Vector3 _previousCameraPosition;
        
        // 背景块数据
        protected class SegmentData
        {
            public Transform transform;
            public int index;
            public bool isFlipped;
        }
        
        protected SegmentData _leftSegment;
        protected SegmentData _middleSegment;
        protected SegmentData _rightSegment;
        
        protected bool _initialized = false;
        protected float _actualWidth;

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

            // 计算实际宽度
            _actualWidth = CalculateActualWidth() + SpacingAdjustment;

            // 自己是中间的（索引0，不翻转）
            _middleSegment = new SegmentData
            {
                transform = transform,
                index = 0,
                isFlipped = false
            };

            // 创建左边和右边的克隆
            _leftSegment = CreateClone("_Left", -_actualWidth, -1);
            _rightSegment = CreateClone("_Right", _actualWidth, 1);

            // 应用初始翻转状态
            if (UseMirrorMode)
            {
                ApplyFlip(_leftSegment);
                ApplyFlip(_rightSegment);
            }

            _initialized = true;

            if (DebugMode)
            {
                Debug.Log($"[InfiniteParallax] Initialized: actualWidth={_actualWidth}, mirror={UseMirrorMode}");
            }
        }

        protected virtual float CalculateActualWidth()
        {
            if (!AutoCalculateWidth)
            {
                return BackgroundWidth;
            }

            // 尝试从 MeshRenderer 获取精确宽度
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                float width = meshRenderer.bounds.size.x;
                if (DebugMode)
                {
                    Debug.Log($"[InfiniteParallax] Got width from MeshRenderer.bounds: {width}");
                }
                return width;
            }

            // 尝试从 SpriteRenderer 获取
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                float width = spriteRenderer.bounds.size.x;
                if (DebugMode)
                {
                    Debug.Log($"[InfiniteParallax] Got width from SpriteRenderer.bounds: {width}");
                }
                return width;
            }

            // 最后使用手动设置的值
            if (DebugMode)
            {
                Debug.Log($"[InfiniteParallax] Using manual BackgroundWidth: {BackgroundWidth}");
            }
            return BackgroundWidth;
        }

        protected virtual SegmentData CreateClone(string suffix, float offsetX, int index)
        {
            GameObject clone = Instantiate(gameObject, transform.parent);
            clone.name = gameObject.name + suffix;
            
            // 移除克隆体上的脚本
            InfiniteParallaxElement cloneScript = clone.GetComponent<InfiniteParallaxElement>();
            if (cloneScript != null)
            {
                Destroy(cloneScript);
            }

            // 设置位置 - 精确对齐
            Vector3 pos = transform.position;
            pos.x += offsetX;
            clone.transform.position = pos;

            if (DebugMode)
            {
                Debug.Log($"[InfiniteParallax] Created {suffix} at X={pos.x}");
            }

            return new SegmentData
            {
                transform = clone.transform,
                index = index,
                isFlipped = (index % 2 != 0)
            };
        }

        protected virtual void ApplyFlip(SegmentData segment)
        {
            if (!UseMirrorMode) return;
            
            Vector3 scale = segment.transform.localScale;
            if (segment.isFlipped)
            {
                scale.x = -Mathf.Abs(scale.x);
            }
            else
            {
                scale.x = Mathf.Abs(scale.x);
            }
            segment.transform.localScale = scale;
        }

        protected virtual void LateUpdate()
        {
            if (!_initialized || _cameraTransform == null) return;

            // 计算相机移动量
            Vector3 cameraDelta = _cameraTransform.position - _previousCameraPosition;
            _previousCameraPosition = _cameraTransform.position;

            // 应用视差
            ApplyParallax(cameraDelta);

            // 检查循环
            CheckLoop();
        }

        protected virtual void ApplyParallax(Vector3 cameraDelta)
        {
            float direction = MoveInOppositeDirection ? -1f : 1f;
            float moveX = cameraDelta.x * HorizontalSpeed * direction;
            
            Vector3 move = new Vector3(moveX, 0, 0);
            
            _leftSegment.transform.position += move;
            _middleSegment.transform.position += move;
            _rightSegment.transform.position += move;
        }

        protected virtual void CheckLoop()
        {
            float cameraX = _cameraTransform.position.x;
            
            // 获取三个背景块的X位置
            float leftX = _leftSegment.transform.position.x;
            float middleX = _middleSegment.transform.position.x;
            float rightX = _rightSegment.transform.position.x;

            // 找出最左和最右的
            float minX = Mathf.Min(leftX, middleX, rightX);
            float maxX = Mathf.Max(leftX, middleX, rightX);

            SegmentData leftmost = GetSegmentAtX(minX);
            SegmentData rightmost = GetSegmentAtX(maxX);

            // 相机边界
            float cameraHalfWidth = _camera.orthographicSize * _camera.aspect;
            float cameraLeft = cameraX - cameraHalfWidth - 50f;
            float cameraRight = cameraX + cameraHalfWidth + 50f;

            // 最左背景块的右边缘
            float leftmostRight = minX + _actualWidth / 2f;
            // 最右背景块的左边缘
            float rightmostLeft = maxX - _actualWidth / 2f;

            // 如果最左边的完全出了左边界，移到最右边
            if (leftmostRight < cameraLeft)
            {
                Vector3 newPos = leftmost.transform.position;
                newPos.x = maxX + _actualWidth;
                leftmost.transform.position = newPos;
                
                // 更新索引和翻转状态
                SegmentData rightmostCurrent = GetSegmentAtX(maxX);
                leftmost.index = rightmostCurrent.index + 1;
                leftmost.isFlipped = (leftmost.index % 2 != 0);
                ApplyFlip(leftmost);
                
                if (DebugMode)
                {
                    Debug.Log($"[InfiniteParallax] Moved to right: X={newPos.x}");
                }
            }
            // 如果最右边的完全出了右边界，移到最左边
            else if (rightmostLeft > cameraRight)
            {
                Vector3 newPos = rightmost.transform.position;
                newPos.x = minX - _actualWidth;
                rightmost.transform.position = newPos;
                
                // 更新索引和翻转状态
                SegmentData leftmostCurrent = GetSegmentAtX(minX);
                rightmost.index = leftmostCurrent.index - 1;
                rightmost.isFlipped = (rightmost.index % 2 != 0);
                ApplyFlip(rightmost);
                
                if (DebugMode)
                {
                    Debug.Log($"[InfiniteParallax] Moved to left: X={newPos.x}");
                }
            }
        }

        protected SegmentData GetSegmentAtX(float x)
        {
            float distLeft = Mathf.Abs(_leftSegment.transform.position.x - x);
            float distMiddle = Mathf.Abs(_middleSegment.transform.position.x - x);
            float distRight = Mathf.Abs(_rightSegment.transform.position.x - x);
            
            if (distLeft <= distMiddle && distLeft <= distRight) return _leftSegment;
            if (distMiddle <= distLeft && distMiddle <= distRight) return _middleSegment;
            return _rightSegment;
        }

        protected virtual void OnDestroy()
        {
            if (_leftSegment != null && _leftSegment.transform != null && _leftSegment.transform != transform)
            {
                Destroy(_leftSegment.transform.gameObject);
            }
            if (_rightSegment != null && _rightSegment.transform != null && _rightSegment.transform != transform)
            {
                Destroy(_rightSegment.transform.gameObject);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            float width = BackgroundWidth;
            
            // 尝试获取实际宽度
            if (AutoCalculateWidth)
            {
                MeshRenderer mr = GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    width = mr.bounds.size.x;
                }
            }
            
            // 显示当前背景块
            Gizmos.color = Color.green;
            Vector3 center = transform.position;
            Gizmos.DrawWireCube(center, new Vector3(width, 20f, 0.1f));
            
            // 显示左右位置
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center + Vector3.left * width, new Vector3(width, 20f, 0.1f));
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center + Vector3.right * width, new Vector3(width, 20f, 0.1f));
        }
    }
}
