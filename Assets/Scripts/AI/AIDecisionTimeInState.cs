using UnityEngine;
using MoreMountains.Tools;

/// <summary>
/// AI决策：检测是否在当前状态停留了指定时间
/// 可用于判断瞄准是否完成
/// </summary>
[AddComponentMenu("Corgi Engine/Character/AI/Decisions/AI Decision Time In State")]
public class AIDecisionTimeInState : AIDecision
{
    [Header("Time Settings")]
    [Tooltip("需要在状态中停留的时间（秒）")]
    public float RequiredTime = 1.5f;
    
    [Tooltip("是否添加随机偏移")]
    public bool RandomizeTime = false;
    
    [Tooltip("随机时间范围")]
    public Vector2 RandomTimeRange = new Vector2(1f, 2f);

    protected float _actualRequiredTime;

    /// <summary>
    /// 初始化时确定实际需要的时间
    /// </summary>
    public override void Initialization()
    {
        base.Initialization();
        ResetRequiredTime();
    }

    /// <summary>
    /// 进入状态时重置时间
    /// </summary>
    public override void OnEnterState()
    {
        base.OnEnterState();
        ResetRequiredTime();
    }

    /// <summary>
    /// 重置所需时间
    /// </summary>
    protected virtual void ResetRequiredTime()
    {
        if (RandomizeTime)
        {
            _actualRequiredTime = Random.Range(RandomTimeRange.x, RandomTimeRange.y);
        }
        else
        {
            _actualRequiredTime = RequiredTime;
        }
    }

    /// <summary>
    /// 判断是否满足条件
    /// </summary>
    public override bool Decide()
    {
        return _brain.TimeInThisState >= _actualRequiredTime;
    }
}
