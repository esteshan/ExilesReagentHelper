using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;

namespace ExilesReagentHelper.State;

/// <summary>
/// All of an entity's skills for the current frame, looked up by name or slot.
/// Pass <c>isActiveSkillSet: true</c> for the currently equipped weapon set, false for the swap set.
/// The skill list is built lazily, so constructing this is cheap until something is actually read.
/// </summary>
public sealed class SkillDictionary
{
    private readonly Lazy<Dictionary<string, SkillInfo>> _byName;
    private readonly Lazy<List<SkillInfo>> _allSkills;
    private readonly Lazy<Actor> _actor;
    private readonly Lazy<PoolInfo> _poolInfo;
    private readonly GameController _controller;

    /// <summary>Current resource pools, used to decide whether a skill's cost can be paid.</summary>
    private readonly record struct PoolInfo(int ManaPool, int HpPool, int EsPool);

    public SkillDictionary(GameController controller, Entity entity, bool isActiveSkillSet)
    {
        _controller = controller;
        _actor = new Lazy<Actor>(() => entity?.GetComponent<Actor>(), LazyThreadSafetyMode.None);

        _poolInfo = new Lazy<PoolInfo>(() =>
        {
            var life = entity?.GetComponent<Life>();
            return life == null
                ? new PoolInfo(10000, 10000, 10000)
                : new PoolInfo(life.CurMana, life.CurHP, life.CurES);
        }, LazyThreadSafetyMode.None);

        _byName = new Lazy<Dictionary<string, SkillInfo>>(() =>
        {
            var actor = _actor.Value;
            if (actor == null)
            {
                return new Dictionary<string, SkillInfo>(StringComparer.OrdinalIgnoreCase);
            }

            var activeSetIndex = entity.GetComponent<Stats>()?.ActiveWeaponSetIndex;
            var pools = _poolInfo.Value;

            // A skill name can appear once per weapon set; pick the variant matching the requested set.
            var skillsByName = actor.ActorSkills
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToLookup(x => x.Name, StringComparer.OrdinalIgnoreCase);

            return skillsByName
                .Select(group => group.FirstOrDefault(s =>
                    (s.WeaponSetBinding == null || activeSetIndex == null || activeSetIndex == s.WeaponSetBinding) == isActiveSkillSet)
                    ?? group.First())
                .Select(skill => CreateSkillInfo(skill, controller, pools))
                .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        }, LazyThreadSafetyMode.None);

        _allSkills = new Lazy<List<SkillInfo>>(() =>
        {
            var actor = _actor.Value;
            if (actor == null)
            {
                return [];
            }

            var pools = _poolInfo.Value;
            return actor.ActorSkills
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(skill => CreateSkillInfo(skill, controller, pools))
                .OrderBy(x => x.Name)
                .ToList();
        }, LazyThreadSafetyMode.None);
    }

    /// <summary>Look up a skill by name. Returns an empty (Exists == false) skill if it isn't slotted.</summary>
    public SkillInfo this[string id] =>
        _byName.Value.TryGetValue(id, out var value) ? value : SkillInfo.Empty(id);

    /// <summary>Whether a skill with this name is slotted.</summary>
    public bool Has(string id) => _byName.Value.ContainsKey(id);

    /// <summary>Every slotted skill, alphabetically — the source list for the UI's skill dropdown.</summary>
    public List<SkillInfo> AllSkills => _allSkills.Value;

    public SkillInfo ByNumericId(int id, int id2) =>
        _byName.Value.Values.FirstOrDefault(x => x.Id == id && x.Id2 == id2);

    public SkillInfo BySlotIndex(int slotIndex)
    {
        var actor = _actor.Value;
        var skill = actor?.ActorSkills.FirstOrDefault(s => s.SkillSlotIndex == slotIndex && !string.IsNullOrWhiteSpace(s.Name));
        return skill == null ? SkillInfo.Empty("") : CreateSkillInfo(skill, _controller, _poolInfo.Value);
    }

    private static SkillInfo CreateSkillInfo(ActorSkill skill, GameController controller, PoolInfo pools)
    {
        return new SkillInfo(
            skill.Name,
            skill.Id,
            skill.Id2,
            Exists: true,
            CanBeUsed: skill.CanBeUsed
                       && skill.CanBeUsedWithWeapon
                       && skill.Cost <= pools.ManaPool
                       && skill.LifeCost <= pools.HpPool
                       && skill.EsCost <= pools.EsPool,
            IsUsing: skill.IsUsing,
            UseStage: skill.SkillUseStage,
            ManaCost: skill.Cost,
            LifeCost: skill.LifeCost,
            EsCost: skill.EsCost,
            MaxUses: skill.CooldownInfo?.MaxUses ?? 1,
            MaxCooldown: skill.Cooldown,
            RemainingUses: skill.RemainingUses,
            Cooldowns: skill.CooldownInfo?.SkillCooldowns.Select(c => c.Remaining).ToList() ?? [],
            CastTime: (float)skill.CastTime.TotalSeconds,
            new Lazy<List<MonsterInfo>>(() => skill.DeployedObjects
                    .Select(d => d?.Entity)
                    .Where(e => e != null)
                    .Select(e => new MonsterInfo(controller, e))
                    .ToList(),
                LazyThreadSafetyMode.None),
            new Lazy<StatDictionary>(() => new StatDictionary(skill.Stats), LazyThreadSafetyMode.None));
    }
}
