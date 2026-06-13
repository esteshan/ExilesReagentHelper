using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore2;
using ExileCore2.Shared.Enums;

namespace ExilesAutoCore.State;

/// <summary>
/// The player's flask slots (PoE2 has two), exposed both by index and by friendly Flask1/Flask2
/// properties so conditions read naturally.
/// </summary>
public sealed class FlasksInfo
{
    private const int FlaskCount = 2;
    private readonly List<FlaskInfo> _flasks;

    public FlasksInfo(GameController controller)
    {
        var flaskInventory = controller.IngameState.ServerData.PlayerInventories
            .LastOrDefault(x => x.TypeId == InventoryNameE.Flask1);
        _flasks = Enumerable.Range(0, FlaskCount)
            .Select(i => FlaskInfo.From(controller, flaskInventory?.Inventory?[i, 0]))
            .ToList();
    }

    /// <summary>Zero-based flask slot accessor.</summary>
    public FlaskInfo this[int i] =>
        i >= 0 && i < FlaskCount
            ? _flasks[i]
            : throw new ArgumentOutOfRangeException(nameof(i), $"Flask index must be 0-{FlaskCount - 1}");

    public FlaskInfo Flask1 => this[0];
    public FlaskInfo Flask2 => this[1];
}
