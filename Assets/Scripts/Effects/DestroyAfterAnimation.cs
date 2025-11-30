using UnityEngine;

/// <summary>
/// 播放完动画后自动销毁
/// </summary>
public class DestroyAfterAnimation : MonoBehaviour
{
    [Tooltip("额外延迟时间")]
    public float ExtraDelay = 0f;
    
    void Start()
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            // 获取当前动画长度
            float clipLength = animator.GetCurrentAnimatorStateInfo(0).length;
            Destroy(gameObject, clipLength + ExtraDelay);
        }
        else
        {
            // 没有动画则1秒后销毁
            Destroy(gameObject, 1f + ExtraDelay);
        }
    }
}
