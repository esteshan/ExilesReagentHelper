using System;
using System.Collections.Generic;

namespace ExilesAutoCore.State;

/// <summary>
/// A snapshot of one of the player's skills for the current frame.
/// <see cref="CanBeUsed"/> already accounts for cooldown and mana/life/ES cost and weapon
/// requirements, so most rules only need to check that single flag before pressing the key.
/// </summary>
public sealed record SkillInfo(
    string Name,
    ushort Id,
    ushort Id2,
    bool Exists,
    bool CanBeUsed,
    bool IsUsing,
    int UseStage,
    int ManaCost,
    int LifeCost,
    int EsCost,
    int MaxUses,
    float MaxCooldown,
    int RemainingUses,
    List<float> Cooldowns,
    float CastTime,
    Lazy<List<MonsterInfo>> DeployedEntitiesFunc,
    Lazy<StatDictionary> StatsFunc)
{
    /// <summary>Minions, totems, mines, etc. that this skill has placed in the world.</summary>
    public List<MonsterInfo> DeployedEntities => DeployedEntitiesFunc.Value;

    public StatDictionary Stats => StatsFunc.Value;

    /// <summary>Placeholder returned when the requested skill isn't slotted, so callers never get null.</summary>
    public static SkillInfo Empty(string name) =>
        new(name, 0, 0, Exists: false, CanBeUsed: false, IsUsing: false, UseStage: 0,
            ManaCost: 0, LifeCost: 0, EsCost: 0, MaxUses: 0, MaxCooldown: 0f, RemainingUses: 0,
            Cooldowns: [], CastTime: 0f,
            new Lazy<List<MonsterInfo>>([]), new Lazy<StatDictionary>(new StatDictionary([])));
}
