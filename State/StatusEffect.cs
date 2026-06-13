using System;

namespace ExilesReagentHelper.State;

/// <summary>
/// A buff or debuff currently on an entity (a herald, a charge, a curse, a flask effect, ...).
/// <see cref="Exists"/> is false for the placeholder returned when a buff isn't present.
/// </summary>
public sealed record StatusEffect(
    string Name,
    string DisplayName,
    bool Exists,
    double TimeLeft,
    double TotalTime,
    int Charges,
    int FlaskSlot,
    Lazy<SkillInfo> SkillInfoLazy)
{
    /// <summary>How much of the buff's duration remains, as a percentage (0-100).</summary>
    public double PercentTimeLeft =>
        Exists
            ? double.IsPositiveInfinity(TimeLeft) ? 100 : 100 * TimeLeft / TotalTime
            : 0;

    /// <summary>The player skill that applied this effect, if it could be identified.</summary>
    public SkillInfo Skill => SkillInfoLazy.Value;
}
