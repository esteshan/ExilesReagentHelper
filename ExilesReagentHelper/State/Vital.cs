using GameOffsets2;

namespace ExilesAutoCore.State;

/// <summary>One resource pool — life, energy shield, or mana — with its current and maximum value.</summary>
public sealed record Vital(double Current, double Max)
{
    /// <summary>Current value as a percentage of the maximum (0-100). Guards against divide-by-zero.</summary>
    public double Percent => Max <= 0 ? 0 : Current / Max * 100;

    public static Vital From(VitalStruct vital) => new(vital.Current, vital.Unreserved);
}
