using System;

namespace ExilesAutoCore.State;

/// <summary>
/// Monster rarity expressed as bit flags, so a single condition can match several rarities at once
/// (e.g. <see cref="AtLeastRare"/> matches both Rare and Unique monsters).
/// </summary>
[Flags]
public enum MonsterRarity
{
    Normal = 1 << 0,
    Magic = 1 << 1,
    Rare = 1 << 2,
    Unique = 1 << 3,
    Any = Normal | Magic | Rare | Unique,
    AtLeastRare = Rare | Unique,
}
