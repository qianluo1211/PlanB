using UnityEngine;
using System.Collections.Generic;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 简单可靠的无限循环视差背景
    /// 使用3个背景块：左、中、右，根据相机位置循环移动
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
        [Tooltip("背景宽度 - 必须手动设置！设为你的 Transform Scale X 值")]
        public float BackgroundWidth = 200f;

        [Header("Debug")]
        public bool DebugMode = false;

        // 内部变量
        protected Camera _camera;
        protected Transform _cameraTransform;
        protected Vector3 _previousCameraPosition;
        
        // 3个背景块：左、中、右
        protected Transform _leftSegment;
        protected Transform _middleSegment;
        protected Transform _rightSegment;
        
        protected bool _initialized = false;

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

            // 自己是中间的
            _middleSegment = transform;

            // 创建左边和右边的克隆
            _leftSegment = CreateClone("_Left", -BackgroundWidth);
            _rightSegment = CreateClone("_Right", BackgroundWidth);

            _initialized = true;

            if (DebugMode)
            {
                Debug.Log($"[InfiniteParallax] Initialized with width={BackgroundWidth}");
            }
        }

        protected virtual Transform CreateClone(string suffix, float offsetX)
        {
            GameObject clone = Instantiate(gameObject, transform.parent);
            clone.name = gameObject.name + suffix;
            
            // 移除克隆体上的脚本
            InfiniteParallaxElement cloneScript = clone.GetComponent<InfiniteParallaxElement>();
            if (cloneScript != null)
            {
                Destroy(cloneScript);
            }

            // 设置位置
            Vector3 pos = transform.position;
            pos.x += offsetX;
            clone.transform.position = pos;

            return clone.transform;
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
            
            _leftSegment.position += move;
            _middleSegment.position += move;
            _rightSegment.position += move;
        }

        protected virtual void CheckLoop()
        {
            float cameraX = _cameraTransform.position.x;
            
            // 获取三个背景块的X位置
            float leftX = _leftSegment.position.x;
            float middleX = _middleSegment.position.x;
            float rightX = _rightSegment.position.x;

            // 找出最左和最右的
            float minX = Mathf.Min(leftX, middleX, rightX);
            float maxX = Mathf.Max(leftX, middleX, rightX);

            Transform leftmost = GetSegmentAtX(minX);
            Transform rightmost = GetSegmentAtX(maxX);

            // 如果相机超过了中间背景块太多，需要移动
            // 当最左边的背景块右边缘离开相机视野时，移到最右边
            float cameraHalfWidth = _camera.orthographicSize * _camera.aspect;
            
            // 相机左边界
            float cameraLeft = cameraX - cameraHalfWidth - 50f; // 50是缓冲
            // 相机右边界  
            float cameraRight = cameraX + cameraHalfWidth + 50f;

            // 最左背景块的右边缘
            float leftmostRight = minX + BackgroundWidth / 2f;
            // 最右背景块的左边缘
            float rightmostLeft = maxX - BackgroundWidth / 2f;

            // 如果最左边的完全出了左边界，移到最右边
            if (leftmostRight < cameraLeft)
            {
                Vector3 newPos = leftmost.position;
                newPos.x = maxX + BackgroundWidth;
                leftmost.position = newPos;
                
                if (DebugMode)
                {
                    Debug.Log($"[InfiniteParallax] Moved {leftmost.name} to right: X={newPos.x}");
                }
            }
            // 如果最右边的完全出了右边界，移到最左边
            else if (rightmostLeft > cameraRight)
            {
                Vector3 newPos = rightmost.position;
                newPos.x = minX - BackgroundWidth;
                rightmost.position = newPos;
                
                if (DebugMode)
                {
                    Debug.Log($"[InfiniteParallax] Moved {rightmost.name} to left: X={newPos.x}");
                }
            }
        }

        protected Transform GetSegmentAtX(float x)
        {
            if (Mathf.Approximately(_leftSegment.position.x, x)) return _leftSegment;
            if (Mathf.Approximately(_middleSegment.position.x, x)) return _middleSegment;
            if (Mathf.Approximately(_rightSegment.position.x, x)) return _rightSegment;
            
            // 如果没有精确匹配，找最接近的
            float distLeft = Mathf.Abs(_leftSegment.position.x - x);
            float distMiddle = Mathf.Abs(_middleSegment.position.x - x);
            float distRight = Mathf.Abs(_rightSegment.position.x - x);
            
            if (distLeft <= distMiddle && distLeft <= distRight) return _leftSegment;
            if (distMiddle <= distLeft && distMiddle <= distRight) return _middleSegment;
            return _rightSegment;
        }

        protected virtual void OnDestroy()
        {
            // 清理克隆体
            if (_leftSegment != null && _leftSegment != transform)
            {
                Destroy(_leftSegment.gameObject);
            }
            if (_rightSegment != null && _rightSegment != transform)
            {
                Destroy(_rightSegment.gameObject);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            // 显示背景宽度
            Gizmos.color = Color.green;
            Vector3 center = transform.position;
            Gizmos.DrawWireCube(center, new Vector3(BackgroundWidth, 20f, 0.1f));
            
            // 显示左右位置
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center + Vector3.left * BackgroundWidth, new Vector3(BackgroundWidth, 20f, 0.1f));
            Gizmos.DrawWireCube(center + Vector3.right * BackgroundWidth, new Vector3(BackgroundWidth, 20f, 0.1f));
        }
    }
}
