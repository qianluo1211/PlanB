using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 安全区触发器 - 玩家在此区域内不会受到生命流失
    /// 使用方法：
    /// 1. 创建一个空物体，添加 BoxCollider2D 或 CircleCollider2D
    /// 2. 设置 Collider 的 Is Trigger = true
    /// 3. 添加此脚本
    /// </summary>
    [AddComponentMenu("Corgi Engine/Spawn/Safe Zone Trigger")]
    public class SafeZoneTrigger : MonoBehaviour
    {
        [Header("设置")]
        [Tooltip("只检测玩家层")]
        public LayerMask PlayerLayerMask = 1 << 9; // 默认 Player 层

        [Header("反馈")]
        public MMFeedbacks EnterFeedbacks;
        public MMFeedbacks ExitFeedbacks;

        [Header("视觉效果")]
        [Tooltip("进入安全区时显示的指示器")]
        public GameObject SafeZoneIndicator;

        [Header("调试")]
        public bool EnableDebugLog = false;
        [MMReadOnly] public bool PlayerInside = false;

        protected HealthDrainAndKillRecover _cachedDrainComponent;
        protected bool _initialized = false;

        protected virtual void Start()
        {
            // 确保有 Collider 且是 Trigger
            Collider2D col = GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogError($"[SafeZoneTrigger] {gameObject.name} 缺少 Collider2D 组件！");
                enabled = false;
                return;
            }
            
            if (!col.isTrigger)
            {
                Debug.LogWarning($"[SafeZoneTrigger] {gameObject.name} 的 Collider2D 不是 Trigger，已自动修正");
                col.isTrigger = true;
            }

            if (SafeZoneIndicator != null)
                SafeZoneIndicator.SetActive(false);

            _initialized = true;
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized) return;
            if (!IsPlayer(other.gameObject)) return;

            HealthDrainAndKillRecover drainComponent = GetDrainComponent(other.gameObject);
            if (drainComponent == null) return;

            drainComponent.EnterSafeZone();
            PlayerInside = true;

            if (SafeZoneIndicator != null)
                SafeZoneIndicator.SetActive(true);

            EnterFeedbacks?.PlayFeedbacks(transform.position);

            if (EnableDebugLog)
                Debug.Log($"[SafeZoneTrigger] 玩家进入安全区 {gameObject.name}");
        }

        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            if (!_initialized) return;
            if (!IsPlayer(other.gameObject)) return;

            HealthDrainAndKillRecover drainComponent = GetDrainComponent(other.gameObject);
            if (drainComponent == null) return;

            drainComponent.ExitSafeZone();
            PlayerInside = false;

            if (SafeZoneIndicator != null)
                SafeZoneIndicator.SetActive(false);

            ExitFeedbacks?.PlayFeedbacks(transform.position);

            if (EnableDebugLog)
                Debug.Log($"[SafeZoneTrigger] 玩家离开安全区 {gameObject.name}");
        }

        protected virtual bool IsPlayer(GameObject obj)
        {
            // 检查层
            if (((1 << obj.layer) & PlayerLayerMask) == 0)
                return false;

            // 双重检查 Character 类型
            Character character = obj.GetComponent<Character>();
            if (character == null)
                character = obj.GetComponentInParent<Character>();

            return character != null && character.CharacterType == Character.CharacterTypes.Player;
        }

        protected virtual HealthDrainAndKillRecover GetDrainComponent(GameObject playerObj)
        {
            // 使用缓存提高性能
            if (_cachedDrainComponent != null)
                return _cachedDrainComponent;

            _cachedDrainComponent = playerObj.GetComponent<HealthDrainAndKillRecover>();
            if (_cachedDrainComponent == null)
                _cachedDrainComponent = playerObj.GetComponentInParent<HealthDrainAndKillRecover>();

            if (_cachedDrainComponent == null && EnableDebugLog)
                Debug.LogWarning($"[SafeZoneTrigger] 玩家缺少 HealthDrainAndKillRecover 组件");

            return _cachedDrainComponent;
        }

        /// <summary>
        /// 强制玩家离开安全区（用于特殊情况如传送）
        /// </summary>
        public virtual void ForcePlayerExit()
        {
            if (!PlayerInside) return;
            if (_cachedDrainComponent != null)
            {
                _cachedDrainComponent.ExitSafeZone();
                PlayerInside = false;
                if (SafeZoneIndicator != null)
                    SafeZoneIndicator.SetActive(false);
            }
        }

#if UNITY_EDITOR
        protected virtual void OnDrawGizmos()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col == null) return;

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f); // 半透明绿色

            if (col is BoxCollider2D box)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawCube(box.offset, box.size);
                Gizmos.color = new Color(0f, 1f, 0.5f, 1f);
                Gizmos.DrawWireCube(box.offset, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else if (col is CircleCollider2D circle)
            {
                Vector3 center = transform.position + (Vector3)circle.offset;
                Gizmos.DrawSphere(center, circle.radius * transform.lossyScale.x);
                Gizmos.color = new Color(0f, 1f, 0.5f, 1f);
                Gizmos.DrawWireSphere(center, circle.radius * transform.lossyScale.x);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            // 选中时显示更明显的边框
            Collider2D col = GetComponent<Collider2D>();
            if (col == null) return;

            Gizmos.color = Color.green;

            if (col is BoxCollider2D box)
            {
                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawWireCube(box.offset, box.size);
                Gizmos.matrix = oldMatrix;
            }
            else if (col is CircleCollider2D circle)
            {
                Vector3 center = transform.position + (Vector3)circle.offset;
                Gizmos.DrawWireSphere(center, circle.radius * transform.lossyScale.x);
            }
        }
#endif
    }
}
