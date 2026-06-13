using System.Collections.Generic;
using ExileCore2.Shared.Enums;

namespace ExilesReagentHelper.State;

/// <summary>The result of a stat lookup: whether the stat was present, and its value.</summary>
public sealed record Stat(bool Exists, int Value);

/// <summary>Read-only view over an entity's numeric game stats, keyed by <see cref="GameStat"/>.</summary>
public sealed class StatDictionary
{
    private readonly Dictionary<GameStat, int> _source;

    public StatDictionary(Dictionary<GameStat, int> source) => _source = source;

    public Stat this[GameStat id] =>
        _source.TryGetValue(id, out var value) ? new Stat(true, value) : new Stat(false, 0);

    public bool Has(GameStat id) => _source.ContainsKey(id);
}

/// <summary>Read-only view over an entity's named states (from its StateMachine component).</summary>
public sealed class StateDictionary
{
    private readonly Dictionary<string, int> _source;

    public StateDictionary(Dictionary<string, int> source) => _source = source;

    public Stat this[string id] =>
        _source.TryGetValue(id, out var value) ? new Stat(true, value) : new Stat(false, 0);

    public bool Has(string id) => _source.ContainsKey(id);
}
