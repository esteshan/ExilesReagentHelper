using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;

namespace ExilesReagentHelper.State;

/// <summary>
/// All buffs/debuffs on an entity for the current frame, looked up by internal buff name.
/// <paramref name="playerSkills"/> is optional and only used to attribute a buff back to the
/// skill that applied it; pass null when that link isn't needed (e.g. for monster buffs).
/// </summary>
public sealed class BuffDictionary
{
    private readonly SkillDictionary _playerSkills;
    private readonly Dictionary<string, Buff> _byName;
    private readonly List<Buff> _all;
    private readonly Lazy<List<StatusEffect>> _allEffects;

    public BuffDictionary(List<Buff> source, SkillDictionary playerSkills)
    {
        _playerSkills = playerSkills;
        _all = source.Where(x => x.Name != null).ToList();
        _byName = _all.DistinctBy(x => x.Name).ToDictionary(x => x.Name);
        _allEffects = new Lazy<List<StatusEffect>>(() => _all.Select(ToStatusEffect).ToList(), LazyThreadSafetyMode.None);
    }

    /// <summary>Look up a buff by name. Returns an empty (Exists == false) effect if absent.</summary>
    public StatusEffect this[string id] =>
        _byName.TryGetValue(id, out var value)
            ? ToStatusEffect(value)
            : new StatusEffect("", "", false, 0, 0, 0, 0, new Lazy<SkillInfo>(() => SkillInfo.Empty("")));

    /// <summary>Whether a buff with this name is present.</summary>
    public bool Has(string id) => _byName.ContainsKey(id);

    public List<StatusEffect> AllBuffs => _allEffects.Value;

    private StatusEffect ToStatusEffect(Buff buff)
    {
        return new StatusEffect(
            buff.Name,
            buff.DisplayName,
            Exists: true,
            buff.Timer,
            buff.MaxTime,
            buff.BuffCharges,
            buff.FlaskSlot,
            new Lazy<SkillInfo>(() =>
                Entity.Player.Equals(buff.SourceEntity)
                    ? _playerSkills?.ByNumericId(buff.SourceSkillId, buff.SourceSkillId2) ?? SkillInfo.Empty("")
                    : SkillInfo.Empty("")));
    }
}
