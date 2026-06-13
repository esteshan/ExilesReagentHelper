using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

namespace ExilesReagentHelper.State;

/// <summary>
/// A monster (or other combat entity) with the extra combat data rules care about:
/// its vitals, animation state, skills, buffs, rarity, and whether it can be damaged.
/// </summary>
public sealed class MonsterInfo : EntityInfo
{
    private bool? _isInvincible;

    public MonsterInfo(GameController controller, Entity entity) : base(controller, entity)
    {
        Vitals = new VitalsInfo(entity.GetComponent<Life>());
        Actor = new ActorInfo(entity);
        Skills = new SkillDictionary(controller, entity, isActiveSkillSet: true);
    }

    public VitalsInfo Vitals { get; }
    public ActorInfo Actor { get; }
    public SkillDictionary Skills { get; }

    public BuffDictionary Buffs => new(Entity.GetComponent<Buffs>()?.BuffsList ?? [], null);

    /// <summary>True while the monster is immune to damage (e.g. during certain boss phases).</summary>
    public bool IsInvincible => _isInvincible ??= Stats[GameStat.CannotBeDamaged].Value != 0;

    public MonsterRarity Rarity => Entity.Rarity switch
    {
        ExileCore2.Shared.Enums.MonsterRarity.White => MonsterRarity.Normal,
        ExileCore2.Shared.Enums.MonsterRarity.Magic => MonsterRarity.Magic,
        ExileCore2.Shared.Enums.MonsterRarity.Rare => MonsterRarity.Rare,
        ExileCore2.Shared.Enums.MonsterRarity.Unique => MonsterRarity.Unique,
        _ => MonsterRarity.Normal,
    };
}
