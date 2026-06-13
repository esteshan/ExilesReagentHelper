using System;
using System.Collections.Generic;
using System.Threading;
using ExileCore2;
using ExileCore2.PoEMemory.Components;

namespace ExilesReagentHelper.State;

/// <summary>
/// A read-only snapshot of everything the rules can inspect, for a single frame.
/// Build a fresh one each tick from the live <see cref="GameController"/>; the engine and the
/// settings UI both read from it. Expensive lookups (nearby monsters) are <see cref="Lazy{T}"/>,
/// so constructing a snapshot is cheap until a rule actually asks for that data.
///
/// This is the de-coupled replacement for ReAgent's RuleState: same useful read surface, but with
/// no rule-engine plumbing, no scripting sandbox, and no dependency on the plugin object.
/// </summary>
public sealed class GameState
{
    private readonly Lazy<NearbyMonsterInfo> _nearbyMonsters;

    /// <summary>True only when we're in-game with a valid, living player. Rules should no-op otherwise.</summary>
    public bool IsValid { get; }

    public VitalsInfo Vitals { get; }
    public BuffDictionary Buffs { get; }
    public SkillDictionary Skills { get; }
    public FlasksInfo Flasks { get; }

    public bool IsMoving { get; }
    public bool IsInTown { get; }
    public bool IsInHideout { get; }
    public bool IsInPeacefulArea { get; }
    public string AreaName { get; } = "";

    public GameState(GameController controller, int maxMonsterRange)
    {
        // Default everything to safe/empty so callers never have to null-check the snapshot.
        Skills = new SkillDictionary(null, null, isActiveSkillSet: true);
        Buffs = new BuffDictionary([], null);
        _nearbyMonsters = new Lazy<NearbyMonsterInfo>(
            () => new NearbyMonsterInfo(controller, maxMonsterRange), LazyThreadSafetyMode.None);

        var player = controller?.Player;
        if (player == null || !player.IsValid)
        {
            return;
        }

        var area = controller.Area.CurrentArea;
        IsInTown = area.IsTown;
        IsInHideout = area.IsHideout;
        IsInPeacefulArea = area.IsPeaceful;
        AreaName = area.Name;

        if (player.TryGetComponent<Actor>(out var actor))
        {
            IsMoving = actor.isMoving;
            Skills = new SkillDictionary(controller, player, isActiveSkillSet: true);
        }

        if (player.TryGetComponent<Life>(out var life))
        {
            Vitals = new VitalsInfo(life);
        }

        player.TryGetComponent<Buffs>(out var buffs);
        Buffs = new BuffDictionary(buffs?.BuffsList ?? [], Skills);

        Flasks = new FlasksInfo(controller);

        // We have the data rules actually depend on; mark the snapshot usable.
        IsValid = Vitals != null;
    }

    /// <summary>Number of hostile monsters within <paramref name="range"/> matching <paramref name="rarity"/>.</summary>
    public int MonsterCount(int range, MonsterRarity rarity) => _nearbyMonsters.Value.GetMonsterCount(range, rarity);

    public int MonsterCount(int range) => MonsterCount(range, MonsterRarity.Any);

    /// <summary>Hostile monsters within <paramref name="range"/> matching <paramref name="rarity"/>.</summary>
    public IEnumerable<MonsterInfo> Monsters(int range, MonsterRarity rarity) => _nearbyMonsters.Value.GetMonsters(range, rarity);

    public IEnumerable<MonsterInfo> Monsters(int range) => Monsters(range, MonsterRarity.Any);
}
