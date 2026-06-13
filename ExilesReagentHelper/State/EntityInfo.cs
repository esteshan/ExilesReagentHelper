using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

namespace ExilesAutoCore.State;

/// <summary>
/// Common read-only information about any world entity (monster, chest, player, ...).
/// Trimmed to what skill automation needs — position, distance, alive/targetable, stats.
/// <see cref="MonsterInfo"/> extends this with combat-specific data.
/// </summary>
public class EntityInfo
{
    protected readonly GameController Controller;
    protected readonly Entity Entity;
    private readonly Lazy<StatDictionary> _stats;
    private readonly Lazy<StateDictionary> _states;

    public EntityInfo(GameController controller, Entity entity)
    {
        Controller = controller;
        Entity = entity;
        _stats = new Lazy<StatDictionary>(
            () => new StatDictionary(Entity.Stats ?? new Dictionary<GameStat, int>()), LazyThreadSafetyMode.None);
        _states = new Lazy<StateDictionary>(
            () => new StateDictionary(Entity.GetComponent<StateMachine>()?.States.ToDictionary(x => x.Name, x => (int)x.Value) ?? []),
            LazyThreadSafetyMode.None);
    }

    public uint Id => Entity.Id;
    public string Path => Entity.Path;
    public string Metadata => Entity.Metadata;

    public Vector3 Position => Entity.Pos;
    public Vector2 GridPosition => Entity.GridPos;

    /// <summary>Distance from the player, in world units.</summary>
    public float Distance => Entity.DistancePlayer;

    public float Scale => Entity?.GetComponent<Positioned>()?.Scale ?? 0;

    public bool IsAlive => Entity.IsAlive;
    public bool IsTargetable => Entity.TryGetComponent<Targetable>(out var t) && t.isTargetable;
    public bool IsTargeted => Entity.TryGetComponent<Targetable>(out var t) && t.isTargeted;
    public bool IsUsingAbility => Entity.TryGetComponent<Actor>(out var a) && a.Action == ActionFlags.UsingAbility;

    public string PlayerName => Entity.GetComponent<Player>()?.PlayerName ?? string.Empty;

    public StatDictionary Stats => _stats.Value;
    public StateDictionary States => _states.Value;
}
