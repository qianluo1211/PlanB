using UnityEngine;
using MoreMountains.CorgiEngine;
using MoreMountains.Tools;

/// <summary>
/// 脚步声系统 - 通过动画事件触发脚步声
/// 将此脚本挂载到角色的 Model 子对象上（有 Animator 的对象）
/// 然后在走路动画中添加 Animation Event 调用 PlayFootstep 方法
/// </summary>
public class FootstepSound : MonoBehaviour
{
    [Header("音频设置")]
    [Tooltip("脚步声音频文件列表（随机播放）")]
    public AudioClip[] FootstepClips;
    
    [Tooltip("单个脚步声（如果不需要随机）")]
    public AudioClip SingleFootstepClip;
    
    [Header("音量设置")]
    [Range(0f, 1f)]
    [Tooltip("最小音量")]
    public float MinVolume = 0.8f;
    
    [Range(0f, 1f)]
    [Tooltip("最大音量")]
    public float MaxVolume = 1f;
    
    [Header("音调设置")]
    [Range(0.5f, 1.5f)]
    [Tooltip("最小音调")]
    public float MinPitch = 0.9f;
    
    [Range(0.5f, 1.5f)]
    [Tooltip("最大音调")]
    public float MaxPitch = 1.1f;
    
    [Header("冷却设置")]
    [Tooltip("两次脚步声之间的最小间隔（秒）")]
    public float CooldownTime = 0.1f;
    
    [Header("条件设置")]
    [Tooltip("是否只在地面上播放脚步声")]
    public bool OnlyWhenGrounded = true;
    
    [Header("MMSoundManager 设置")]
    [Tooltip("使用 MMSoundManager 播放（推荐，与 Corgi Engine 集成）")]
    public bool UseMMSoundManager = true;
    
    [Tooltip("MMSoundManager 的音轨")]
    public MMSoundManager.MMSoundManagerTracks SoundTrack = MMSoundManager.MMSoundManagerTracks.Sfx;
    
    // 私有变量
    private float _lastFootstepTime;
    private CorgiController _controller;
    private Character _character;
    private AudioSource _audioSource;
    
    protected virtual void Start()
    {
        // 获取角色组件（可能在父对象上）
        _character = GetComponentInParent<Character>();
        if (_character != null)
        {
            _controller = _character.GetComponent<CorgiController>();
        }
        
        // 如果不使用 MMSoundManager，创建 AudioSource
        if (!UseMMSoundManager)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f; // 2D 声音
            }
        }
    }
    
    /// <summary>
    /// 播放脚步声 - 在动画事件中调用此方法
    /// </summary>
    public void PlayFootstep()
    {
        // 冷却检查
        if (Time.time - _lastFootstepTime < CooldownTime)
        {
            return;
        }
        
        // 地面检查
        if (OnlyWhenGrounded && _controller != null && !_controller.State.IsGrounded)
        {
            return;
        }
        
        // 获取要播放的音频
        AudioClip clipToPlay = GetFootstepClip();
        if (clipToPlay == null)
        {
            return;
        }
        
        // 随机音量和音调
        float volume = Random.Range(MinVolume, MaxVolume);
        float pitch = Random.Range(MinPitch, MaxPitch);
        
        // 播放声音
        if (UseMMSoundManager)
        {
            PlayWithMMSoundManager(clipToPlay, volume, pitch);
        }
        else
        {
            PlayWithAudioSource(clipToPlay, volume, pitch);
        }
        
        _lastFootstepTime = Time.time;
    }
    
    /// <summary>
    /// 播放脚步声（带音频参数重载）- 可从动画事件传入参数
    /// </summary>
    public void PlayFootstepWithVolume(float volume)
    {
        if (Time.time - _lastFootstepTime < CooldownTime)
        {
            return;
        }
        
        if (OnlyWhenGrounded && _controller != null && !_controller.State.IsGrounded)
        {
            return;
        }
        
        AudioClip clipToPlay = GetFootstepClip();
        if (clipToPlay == null)
        {
            return;
        }
        
        float pitch = Random.Range(MinPitch, MaxPitch);
        
        if (UseMMSoundManager)
        {
            PlayWithMMSoundManager(clipToPlay, volume, pitch);
        }
        else
        {
            PlayWithAudioSource(clipToPlay, volume, pitch);
        }
        
        _lastFootstepTime = Time.time;
    }
    
    /// <summary>
    /// 获取要播放的脚步声音频
    /// </summary>
    protected virtual AudioClip GetFootstepClip()
    {
        // 优先使用音频数组随机播放
        if (FootstepClips != null && FootstepClips.Length > 0)
        {
            return FootstepClips[Random.Range(0, FootstepClips.Length)];
        }
        
        // 否则使用单个音频
        return SingleFootstepClip;
    }
    
    /// <summary>
    /// 使用 MMSoundManager 播放
    /// </summary>
    protected virtual void PlayWithMMSoundManager(AudioClip clip, float volume, float pitch)
    {
        MMSoundManagerPlayOptions options = MMSoundManagerPlayOptions.Default;
        options.Volume = volume;
        options.Pitch = pitch;
        options.MmSoundManagerTrack = SoundTrack;
        options.Location = transform.position;
        
        MMSoundManagerSoundPlayEvent.Trigger(clip, options);
    }
    
    /// <summary>
    /// 使用 AudioSource 播放
    /// </summary>
    protected virtual void PlayWithAudioSource(AudioClip clip, float volume, float pitch)
    {
        if (_audioSource == null) return;
        
        _audioSource.pitch = pitch;
        _audioSource.PlayOneShot(clip, volume);
    }
}
