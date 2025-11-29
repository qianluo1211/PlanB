using UnityEngine;

/// <summary>
/// 动画事件中转器 - 挂载到有 Animator 的对象上
/// 用于将动画事件转发到其他对象上的脚本
/// </summary>
public class AnimationEventRelay : MonoBehaviour
{
    [Header("脚步声")]
    [Tooltip("FootstepSound 脚本所在的对象（如 WalkFeedbacks）")]
    public FootstepSound FootstepHandler;
    
    /// <summary>
    /// 动画事件调用 - 播放脚步声
    /// </summary>
    public void PlayFootstep()
    {
        if (FootstepHandler != null)
        {
            FootstepHandler.PlayFootstep();
        }
    }
    
    /// <summary>
    /// 动画事件调用 - 播放脚步声（带音量参数）
    /// </summary>
    public void PlayFootstepWithVolume(float volume)
    {
        if (FootstepHandler != null)
        {
            FootstepHandler.PlayFootstepWithVolume(volume);
        }
    }
}
