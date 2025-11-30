using UnityEngine;

/// <summary>
/// 让特效跟随目标对象，并在动画播放完毕后自动销毁
/// </summary>
public class EffectFollowPlayer : MonoBehaviour
{
    [Tooltip("跟随的目标")]
    public Transform Target;
    
    [Tooltip("相对于目标的偏移量")]
    public Vector3 Offset;
    
    [Tooltip("是否持续更新偏移方向（根据目标朝向）")]
    public bool UpdateOffsetDirection = false;
    
    [Tooltip("自动销毁时间（秒），0表示不自动销毁")]
    public float AutoDestroyTime = 1f;
    
    private float _spawnTime;
    private Animator _animator;
    private bool _hasAnimator;
    
    void Start()
    {
        _spawnTime = Time.time;
        _animator = GetComponent<Animator>();
        _hasAnimator = _animator != null;
        
        // 如果没有设置自动销毁时间，尝试从动画获取时长
        if (AutoDestroyTime <= 0 && _hasAnimator)
        {
            AnimatorClipInfo[] clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length > 0)
            {
                AutoDestroyTime = clipInfo[0].clip.length;
            }
            else
            {
                AutoDestroyTime = 1f; // 默认1秒
            }
        }
    }
    
    void Update()
    {
        // 跟随目标
        if (Target != null)
        {
            transform.position = Target.position + Offset;
        }
        
        // 检查是否需要销毁
        if (AutoDestroyTime > 0 && Time.time - _spawnTime >= AutoDestroyTime)
        {
            Destroy(gameObject);
        }
    }
    
    void LateUpdate()
    {
        // 确保特效位置正确（在所有移动更新之后）
        if (Target != null)
        {
            transform.position = Target.position + Offset;
        }
    }
}
