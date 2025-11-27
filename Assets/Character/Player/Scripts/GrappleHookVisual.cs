using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 简单的钩爪飞行物视觉组件
    /// 挂载到钩爪Prefab上，提供拖尾和旋转效果
    /// </summary>
    [AddComponentMenu("Corgi Engine/Character/Abilities/Grapple Hook Visual")]
    public class GrappleHookVisual : MonoBehaviour
    {
        [Header("设置")]
        [Tooltip("是否自动朝向移动方向")]
        public bool AutoRotate = true;
        
        [Tooltip("拖尾渲染器（可选）")]
        public TrailRenderer Trail;

        protected Vector3 _lastPos;

        protected virtual void Start()
        {
            _lastPos = transform.position;
        }

        protected virtual void Update()
        {
            if (AutoRotate)
            {
                Vector3 delta = transform.position - _lastPos;
                if (delta.magnitude > 0.001f)
                {
                    float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0, 0, angle);
                }
            }
            _lastPos = transform.position;
        }

        public virtual void OnHit()
        {
            if (Trail != null) Trail.emitting = false;
        }
    }
}
