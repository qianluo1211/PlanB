using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 死亡碎片 - 带物理的尸体碎片
    /// 由 EnemyDeathEffect 通过对象池管理
    /// 
    /// 特性:
    /// - 自定义重力（不使用Rigidbody2D）
    /// - 地面碰撞检测和弹跳
    /// - 落地后摩擦减速
    /// - 旋转效果
    /// - 淡出销毁
    /// </summary>
    public class DeathFragment : MonoBehaviour
    {
        [Header("引用")]
        public SpriteRenderer SpriteRenderer;

        // 物理参数（由Initialize设置）
        protected Vector2 _velocity;
        protected float _rotationSpeed;
        protected float _gravity;
        protected float _bounceFactor;
        protected float _groundFriction;
        protected LayerMask _groundLayerMask;
        
        // 生命周期
        protected float _lifetime;
        protected float _fadeOutDuration;
        protected float _timer;
        
        // 状态
        protected bool _isGrounded;
        protected bool _isFading;
        protected float _initialAlpha;
        protected Color _baseColor;
        
        // 碰撞检测
        protected float _groundCheckDistance = 0.1f;
        protected float _fragmentRadius = 0.15f;

        /// <summary>
        /// 初始化碎片
        /// </summary>
        public virtual void Initialize(
            Sprite sprite,
            Vector2 position,
            Vector2 velocity,
            float rotationSpeed,
            float scale,
            Color color,
            float gravity,
            float bounceFactor,
            float groundFriction,
            LayerMask groundLayerMask,
            float lifetime,
            float fadeOutDuration,
            int sortingLayerID,
            int sortingOrder)
        {
            // 设置sprite
            if (SpriteRenderer == null)
            {
                SpriteRenderer = GetComponent<SpriteRenderer>();
            }
            SpriteRenderer.sprite = sprite;
            SpriteRenderer.color = color;
            SpriteRenderer.sortingLayerID = sortingLayerID;
            SpriteRenderer.sortingOrder = sortingOrder;
            
            // 设置transform
            transform.position = position;
            transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            transform.localScale = Vector3.one * scale;
            
            // 设置物理参数
            _velocity = velocity;
            _rotationSpeed = rotationSpeed;
            _gravity = gravity;
            _bounceFactor = bounceFactor;
            _groundFriction = groundFriction;
            _groundLayerMask = groundLayerMask;
            
            // 设置生命周期
            _lifetime = lifetime;
            _fadeOutDuration = fadeOutDuration;
            _timer = 0f;
            
            // 重置状态
            _isGrounded = false;
            _isFading = false;
            _baseColor = color;
            _initialAlpha = color.a;

            // 根据sprite大小调整碰撞半径
            if (sprite != null)
            {
                _fragmentRadius = Mathf.Max(sprite.bounds.extents.x, sprite.bounds.extents.y) * scale;
            }
        }

        protected virtual void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            float deltaTime = Time.deltaTime;
            _timer += deltaTime;

            // 物理更新
            UpdatePhysics(deltaTime);
            
            // 旋转更新
            UpdateRotation(deltaTime);
            
            // 淡出检测
            UpdateFading(deltaTime);
            
            // 生命周期检测
            if (_timer >= _lifetime)
            {
                ReturnToPool();
            }
        }

        /// <summary>
        /// 更新物理
        /// </summary>
        protected virtual void UpdatePhysics(float deltaTime)
        {
            // 应用重力
            if (!_isGrounded)
            {
                _velocity.y -= _gravity * deltaTime;
            }
            
            // 地面检测
            CheckGroundCollision(deltaTime);
            
            // 应用摩擦力（落地后）
            if (_isGrounded)
            {
                ApplyFriction(deltaTime);
            }
            
            // 移动
            transform.position += (Vector3)_velocity * deltaTime;
        }

        /// <summary>
        /// 检测地面碰撞
        /// </summary>
        protected virtual void CheckGroundCollision(float deltaTime)
        {
            // 向下射线检测
            Vector2 origin = (Vector2)transform.position;
            float checkDistance = _groundCheckDistance + Mathf.Abs(_velocity.y) * deltaTime;
            
            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                Vector2.down,
                checkDistance,
                _groundLayerMask
            );

            if (hit.collider != null && _velocity.y < 0)
            {
                // 碰到地面
                if (!_isGrounded)
                {
                    // 第一次落地 - 弹跳
                    _velocity.y = -_velocity.y * _bounceFactor;
                    
                    // 如果弹跳力度太小，就停止
                    if (Mathf.Abs(_velocity.y) < 0.5f)
                    {
                        _velocity.y = 0;
                        _isGrounded = true;
                        _rotationSpeed *= 0.3f; // 落地后减慢旋转
                    }
                    
                    // 落地时调整位置
                    transform.position = new Vector3(
                        transform.position.x,
                        hit.point.y + _fragmentRadius * 0.5f,
                        transform.position.z
                    );
                }
            }
            else
            {
                // 离开地面（可能是弹起来了）
                if (_isGrounded && _velocity.y > 0.1f)
                {
                    _isGrounded = false;
                }
            }

            // 侧面碰撞检测
            if (Mathf.Abs(_velocity.x) > 0.1f)
            {
                Vector2 sideDirection = _velocity.x > 0 ? Vector2.right : Vector2.left;
                RaycastHit2D sideHit = Physics2D.Raycast(
                    origin,
                    sideDirection,
                    _fragmentRadius + Mathf.Abs(_velocity.x) * deltaTime,
                    _groundLayerMask
                );

                if (sideHit.collider != null)
                {
                    // 侧面碰撞 - 反弹
                    _velocity.x = -_velocity.x * _bounceFactor * 0.5f;
                }
            }
        }

        /// <summary>
        /// 应用摩擦力
        /// </summary>
        protected virtual void ApplyFriction(float deltaTime)
        {
            // 水平减速
            if (Mathf.Abs(_velocity.x) > 0.01f)
            {
                float friction = _groundFriction * deltaTime;
                _velocity.x = Mathf.MoveTowards(_velocity.x, 0, friction);
            }
            else
            {
                _velocity.x = 0;
            }
            
            // 旋转减速
            if (_isGrounded && Mathf.Abs(_rotationSpeed) > 1f)
            {
                _rotationSpeed = Mathf.MoveTowards(_rotationSpeed, 0, _groundFriction * 50f * deltaTime);
            }
        }

        /// <summary>
        /// 更新旋转
        /// </summary>
        protected virtual void UpdateRotation(float deltaTime)
        {
            if (Mathf.Abs(_rotationSpeed) > 0.1f)
            {
                transform.Rotate(0, 0, _rotationSpeed * deltaTime);
            }
        }

        /// <summary>
        /// 更新淡出
        /// </summary>
        protected virtual void UpdateFading(float deltaTime)
        {
            float fadeStartTime = _lifetime - _fadeOutDuration;
            
            if (_timer >= fadeStartTime)
            {
                _isFading = true;
                
                // 计算淡出进度
                float fadeProgress = (_timer - fadeStartTime) / _fadeOutDuration;
                fadeProgress = Mathf.Clamp01(fadeProgress);
                
                // 应用淡出
                Color color = _baseColor;
                color.a = Mathf.Lerp(_initialAlpha, 0f, fadeProgress);
                SpriteRenderer.color = color;
            }
        }

        /// <summary>
        /// 返回对象池
        /// </summary>
        protected virtual void ReturnToPool()
        {
            gameObject.SetActive(false);
            
            // 重置状态
            _timer = 0f;
            _isGrounded = false;
            _isFading = false;
            _velocity = Vector2.zero;
            _rotationSpeed = 0f;
        }

        /// <summary>
        /// 外部调用 - 强制停止并返回池
        /// </summary>
        public virtual void ForceStop()
        {
            ReturnToPool();
        }

        /// <summary>
        /// 外部调用 - 施加额外力
        /// </summary>
        public virtual void AddForce(Vector2 force)
        {
            _velocity += force;
            _isGrounded = false;
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmosSelected()
        {
            // 显示碰撞半径
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _fragmentRadius);
            
            // 显示速度方向
            if (_velocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, _velocity * 0.2f);
            }
        }
#endif
    }
}
