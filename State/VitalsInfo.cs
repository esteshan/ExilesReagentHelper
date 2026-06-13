using ExileCore2.PoEMemory.Components;

namespace ExilesReagentHelper.State;

/// <summary>The three resource pools of an entity, read once from its Life component.</summary>
public sealed class VitalsInfo
{
    public Vital HP { get; }
    public Vital ES { get; }
    public Vital Mana { get; }

    public VitalsInfo(Life lifeComponent)
    {
        HP = Vital.From(lifeComponent.Health);
        ES = Vital.From(lifeComponent.EnergyShield);
        Mana = Vital.From(lifeComponent.Mana);
    }
}
