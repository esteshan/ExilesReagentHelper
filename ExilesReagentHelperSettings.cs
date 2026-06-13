using System.Drawing;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace ExilesReagentHelper;

public sealed class ExilesReagentHelperSettings : ISettings
{
    // Master on/off toggle the Loader shows next to the plugin name.
    public ToggleNode Enable { get; set; } = new(true);

    // How far out (world units) we look for monsters when evaluating "monsters nearby" conditions.
    public RangeNode<int> MaxMonsterRange { get; set; } = new(200, 0, 500);

    // Draws an in-world line from the player to the nearest hostile monster, labelled with distance.
    public ToggleNode DrawNearestMonsterLine { get; set; } = new(false);

    // Colour of that line and its distance label.
    public ColorNode NearestMonsterLineColor { get; set; } = new(Color.Yellow);
}
