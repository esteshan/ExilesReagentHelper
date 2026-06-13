using System.Collections.Generic;
using System.Linq;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;

namespace ExilesReagentHelper.State;

/// <summary>
/// A single flask slot's state. <see cref="Active"/> is whether its effect is currently running,
/// <see cref="CanBeUsed"/> is whether it has enough charges to use right now.
/// </summary>
public sealed record FlaskInfo(
    bool Active,
    bool CanBeUsed,
    int Charges,
    int MaxCharges,
    int ChargesPerUse,
    string ClassName,
    string BaseName,
    string UniqueName)
{
    /// <summary>The unique name if the flask is unique, otherwise its base type name.</summary>
    public string Name => !string.IsNullOrEmpty(UniqueName) ? UniqueName : BaseName;

    /// <summary>An empty slot (no flask equipped, or memory not yet readable).</summary>
    public static FlaskInfo Empty => new(false, false, 0, 1, 1, "", "", "");

    public static FlaskInfo From(GameController state, ServerInventory.InventSlotItem flaskItem)
    {
        if (flaskItem?.Address is 0 or null || flaskItem.Item?.Address is null or 0)
        {
            return Empty;
        }

        var charges = flaskItem.Item.GetComponent<Charges>();

        var active = false;
        var canBeUsed = false;
        if (state.Player.TryGetComponent<Buffs>(out var playerBuffs) &&
            flaskItem.Item.TryGetComponent<Flask>(out var flask))
        {
            active = GetFlaskBuffNames(flask).Any(playerBuffs.HasBuff);
            canBeUsed = (charges?.NumCharges ?? 0) >= (charges?.ChargesPerUse ?? 1);
        }

        var className = "";
        var baseName = "";
        if (flaskItem.Item.TryGetComponent<Base>(out var baseComponent))
        {
            className = baseComponent.Info?.BaseItemTypeDat?.ClassName ?? "";
            baseName = baseComponent.Name;
        }

        var uniqueName = flaskItem.Item.TryGetComponent<Mods>(out var mods) ? mods.UniqueName : "";

        return new FlaskInfo(
            active,
            canBeUsed,
            charges?.NumCharges ?? 0,
            charges?.ChargesMax ?? 1,
            charges?.ChargesPerUse ?? 1,
            className,
            baseName,
            uniqueName);
    }

    private static readonly string[] LifeFlaskBuffs = { "flask_effect_life" };

    private static readonly string[] ManaFlaskBuffs =
    {
        "flask_effect_mana",
        "flask_effect_mana_not_removed_when_full",
        "flask_instant_mana_recovery_at_end_of_effect",
    };

    // The flask "type" byte lives at a fixed memory offset and tells us which buff(s) the flask grants.
    private static IEnumerable<string> GetFlaskBuffNames(Flask flask)
    {
        var type = flask.M.Read<int>(flask.Address + 0x28, 0x20);
        return type switch
        {
            1 => LifeFlaskBuffs,
            2 => ManaFlaskBuffs,
            3 => LifeFlaskBuffs.Concat(ManaFlaskBuffs),
            4 when flask.M.ReadStringU(flask.M.Read<long>(flask.Address + 0x28, 0x18, 0x0)) is { } s and not "" => new[] { s },
            _ => Enumerable.Empty<string>(),
        };
    }
}
