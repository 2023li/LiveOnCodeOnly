using System;
using System.Collections;
using System.Collections.Generic;
using Moyo.Unity;
using Sirenix.OdinInspector.Editor.Drawers;
using Unity.VisualScripting;
using UnityEngine;

public enum TurnPhase
{
    // 在这里已经移除了 动画播放阶段
    结束准备阶段,
    资源消耗阶段,
    资源生产阶段,
    回合结束阶段, //用作数据整理 例如 计数
    开始准备阶段  //对
}

[AddComponentMenu("LifeOn/Turn System")]
public class TurnSystem : MonoSingleton<TurnSystem>
{
    public int NumberOfRounds { get; private set; }

    /// <summary>
    /// 回合阶段切换事件
    /// </summary>
    public static event Action<TurnPhase> OnTurnPhaseChange;

    /// <summary>
    /// 阻塞数量改变事件（参数为当前阻塞总数）
    /// </summary>
    public static event Action<int> OnTurnBlockCountChanged;

    private TurnPhase[] _phases;

    // 阻塞列表
    private readonly List<TurnBlock> _turnBlocks = new List<TurnBlock>(10);

    // 用于给每个阻塞分配唯一 ID
    private int _nextBlockId = 1;

    /// <summary>
    /// 当前是否被阻塞
    /// </summary>
    public bool IsBlocked => _turnBlocks.Count > 0;

    protected override void Initialize()
    {
        base.Initialize();
        _phases = (TurnPhase[])Enum.GetValues(typeof(TurnPhase));
    }

    /// <summary>
    /// 供 UI 或系统调用：结束本回合
    /// </summary>
    public void EndTurn()
    {
        // 有任何阻塞都不允许结束回合
        if (IsBlocked)
        {
            return;
        }

        foreach (TurnPhase phase in _phases)
        {
            OnTurnPhaseChange?.Invoke(phase);

            // 在“结束准备阶段”加一个 1 秒的自动阻塞（冷却）
            if (phase == TurnPhase.结束准备阶段)
            {
                AddTimedTurnBlock("结束准备阶段冷却，防止连续结束回合", 1f);
            }
        }

        NumberOfRounds++;
    }

    #region 阻塞相关 API

    /// <summary>
    /// 添加一个「定时自动解除」的阻塞，返回阻塞 ID（如果你想手动提前解除也可以用这个 ID 调用 RemoveTurnBlock）
    /// </summary>
    public int AddTimedTurnBlock(string reason, float durationSeconds)
    {
        var block = new TurnBlock
        {
            id = _nextBlockId++,
            reason = reason,
            durationSeconds = durationSeconds
        };

        _turnBlocks.Add(block);
        OnTurnBlockCountChanged?.Invoke(_turnBlocks.Count);

        // 定时自动移除
        StartCoroutine(RemoveTurnBlockAfterDelay(block.id, durationSeconds));

        return block.id;
    }

    /// <summary>
    /// 添加一个「必须手动解除」的阻塞，返回阻塞 ID
    /// </summary>
    public int AddManualTurnBlock(string reason)
    {
        var block = new TurnBlock
        {
            id = _nextBlockId++,
            reason = reason,
            durationSeconds = null   // null == 不自动解除
        };

        _turnBlocks.Add(block);
        OnTurnBlockCountChanged?.Invoke(_turnBlocks.Count);

        return block.id;
    }

    /// <summary>
    /// 手动移除一个阻塞（定时阻塞到时间后也会走这个函数）
    /// </summary>
    public void RemoveTurnBlock(int blockId)
    {
        int index = _turnBlocks.FindIndex(b => b.id == blockId);
        if (index >= 0)
        {
            _turnBlocks.RemoveAt(index);
            OnTurnBlockCountChanged?.Invoke(_turnBlocks.Count);
        }
    }

    private IEnumerator RemoveTurnBlockAfterDelay(int blockId, float delay)
    {
        yield return new WaitForSeconds(delay);
        // 可能在这段时间里被手动移除了，所以这里用 ID 再查一遍
        RemoveTurnBlock(blockId);
    }

    #endregion


    public TurnSystemSaveData Save()
    {
        return new TurnSystemSaveData { currentNumberOfRounds = NumberOfRounds };
    }

    internal void Load(TurnSystemSaveData turnSystemSaveData)
    {
        NumberOfRounds = turnSystemSaveData.currentNumberOfRounds;
    }
}

/// <summary>
/// 回合阻塞信息
/// </summary>
public struct TurnBlock
{
    /// <summary>唯一 ID，用于手动移除</summary>
    public int id;

    /// <summary>阻塞原因（仅用于调试或 UI 显示）</summary>
    public string reason;

    /// <summary>
    /// 若为 null 则表示不会自动移除；
    /// 若有值，则表示在 durationSeconds 秒后自动移除。
    /// </summary>
    public float? durationSeconds;
}

[Serializable]
public class TurnSystemSaveData
{
    public int currentNumberOfRounds;
}
