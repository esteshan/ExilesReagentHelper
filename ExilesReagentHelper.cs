using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ExileCore2;
using ExileCore2.Shared.Helpers;
using ExilesReagentHelper.State;
using ImGuiNET;

namespace ExilesReagentHelper;

public sealed class ExilesReagentHelper : BaseSettingsPlugin<ExilesReagentHelperSettings>
{
    // Cap how many monsters the buff preview lists, so a big pack doesn't flood the panel.
    private const int MaxMonstersInBuffPreview = 15;

    // When non-null, the monster buff preview shows this frozen snapshot instead of live values,
    // so fast-changing debuffs (or monsters that die quickly) can actually be read.
    private List<MonsterSnapshot> _frozenMonsters;

    public override void DrawSettings()
    {
        base.DrawSettings();

        // Build a fresh snapshot every frame the settings window is open and show what we read.
        // This is a read-only diagnostic that proves the State layer works against the live game.
        var state = new GameState(GameController, Settings.MaxMonsterRange);
        DrawStatePreview(state);
    }

    public override void Render()
    {
        // Per-frame automation (evaluate rules, press keys) will live here in a later step.
    }

    private void DrawStatePreview(GameState state)
    {
        ImGui.Separator();
        ImGui.Text("Live state preview");
        ImGui.SameLine();
        if (!state.IsValid)
        {
            ImGui.TextColored(Color.Orange.ToImguiVec4(), "(not in game — log into a character)");
            return;
        }

        ImGui.TextColored(Color.Lime.ToImguiVec4(), "(reading live game)");

        DrawVitalsSection(state);
        DrawPlayerSection(state);
        DrawPlayerBuffsSection(state);
        DrawMonstersSection(state);
        DrawMonsterBuffsSection(state);
        DrawMonsterStunSection(state);
        DrawSkillsSection(state);
        DrawFlasksSection(state);
    }

    private static void DrawVitalsSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Vitals", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        DrawVital("Life", state.Vitals.HP, Color.IndianRed);
        DrawVital("ES", state.Vitals.ES, Color.LightSkyBlue);
        DrawVital("Mana", state.Vitals.Mana, Color.MediumSlateBlue);
    }

    private void DrawPlayerSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Player", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        ImGui.Text($"Area: {state.AreaName}");
        ImGui.Text($"Moving: {state.IsMoving}");
        ImGui.Text($"Town: {state.IsInTown}    Hideout: {state.IsInHideout}    Peaceful: {state.IsInPeacefulArea}");
    }

    private static void DrawPlayerBuffsSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Player buffs / debuffs", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        ImGui.TextColored(Color.Gray.ToImguiVec4(), "The \"Name\" column is the internal id you'll match in conditions.");
        DrawBuffTable("playerBuffs", state.Buffs.AllBuffs.Select(ToBuffRow).ToList());
    }

    private void DrawMonstersSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Nearby monsters", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        var range = Settings.MaxMonsterRange.Value;
        ImGui.Text($"Within {range} units:");
        ImGui.BulletText($"Total:  {state.MonsterCount(range)}");
        ImGui.BulletText($"Rare+:  {state.MonsterCount(range, MonsterRarity.AtLeastRare)}");
        ImGui.BulletText($"Unique: {state.MonsterCount(range, MonsterRarity.Unique)}");
    }

    private void DrawMonsterBuffsSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Nearby monster buffs / debuffs"))
        {
            return;
        }

        var frozen = _frozenMonsters != null;

        // Freeze pauses the values so fast-ticking debuffs / dying monsters can be read.
        if (ImGui.Button(frozen ? "Unfreeze" : "Freeze"))
        {
            _frozenMonsters = frozen ? null : CaptureMonsterSnapshots(state);
            frozen = !frozen;
        }

        ImGui.SameLine();
        if (frozen)
        {
            ImGui.TextColored(Color.Orange.ToImguiVec4(), "Frozen snapshot — press Unfreeze for live values.");
            DrawMonsterBuffList(_frozenMonsters);
        }
        else
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "Expand a monster to see its buff/debuff ids (closest first).");
            DrawMonsterBuffList(CaptureMonsterSnapshots(state));
        }
    }

    // Stun discovery panel. "Primed for Stun" is NOT a buff/debuff, so it never shows in the buff
    // tables above — it lives in the monster's GameStats and/or StateMachine states. This dumps the
    // stun-related stats and all states for the targeted (or nearest) monster so we can see exactly
    // which value flips when a monster becomes primed, then build a real condition off it.
    private void DrawMonsterStunSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Stats & States — targeted / nearest monster"))
        {
            return;
        }

        var monsters = state.Monsters(Settings.MaxMonsterRange.Value).ToList();
        // Prefer the monster under the cursor; otherwise the closest one (the list is closest-first).
        var monster = monsters.FirstOrDefault(m => m.IsTargeted) ?? monsters.FirstOrDefault();
        if (monster == null)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "No monster in range. Get close to one (ideally hover/target it).");
            return;
        }

        ImGui.Text($"{ShortName(monster.Path)}  [{monster.Rarity}]  ({monster.Distance:0} units)");
        ImGui.TextColored(Color.Gray.ToImguiVec4(),
            "Hit a monster until it's Primed for Stun and watch which value below changes.");

        // Stun-related GameStats (thresholds, IsLightStunned/IsHeavyStunned, buildup, etc.).
        var stunStats = monster.Stats.All
            .Where(kv => kv.Key.ToString().Contains("stun", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key.ToString())
            .ToList();

        ImGui.Spacing();
        ImGui.Text($"Stun-related stats ({stunStats.Count}):");
        if (stunStats.Count == 0)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "none present");
        }
        else if (ImGui.BeginTable("stunStats", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Stat");
            ImGui.TableSetupColumn("Value");
            ImGui.TableHeadersRow();
            foreach (var kv in stunStats)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(kv.Key.ToString());
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(kv.Value.ToString());
            }

            ImGui.EndTable();
        }

        // StateMachine states — "Primed for Stun" is a likely candidate to surface here.
        var states = monster.States.All.OrderBy(kv => kv.Key).ToList();
        ImGui.Spacing();
        ImGui.Text($"States ({states.Count}):");
        if (states.Count == 0)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "none present");
        }
        else if (ImGui.BeginTable("monStates", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("State");
            ImGui.TableSetupColumn("Value");
            ImGui.TableHeadersRow();
            foreach (var kv in states)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(kv.Key);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(kv.Value.ToString());
            }

            ImGui.EndTable();
        }
    }

    // Copies the nearby monsters and their buffs into plain data, so the snapshot stays readable
    // even after the monsters move or die.
    private List<MonsterSnapshot> CaptureMonsterSnapshots(GameState state)
    {
        return state.Monsters(Settings.MaxMonsterRange.Value)
            .Take(MaxMonstersInBuffPreview)
            .Select(m => new MonsterSnapshot(
                ShortName(m.Path),
                m.Rarity,
                m.Distance,
                m.Id,
                m.Buffs.AllBuffs.Select(ToBuffRow).ToList()))
            .ToList();
    }

    private static void DrawMonsterBuffList(IReadOnlyList<MonsterSnapshot> monsters)
    {
        if (monsters.Count == 0)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "No monsters in range.");
            return;
        }

        foreach (var monster in monsters)
        {
            var label = $"{monster.Name}  [{monster.Rarity}]  ({monster.Distance:0} units)##{monster.Id}";
            if (ImGui.TreeNode(label))
            {
                DrawBuffTable($"mb_{monster.Id}", monster.Buffs);
                ImGui.TreePop();
            }
        }
    }

    private static void DrawSkillsSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Slotted skills", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        var skills = state.Skills.AllSkills;
        if (skills.Count == 0)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "No skills found.");
            return;
        }

        if (!ImGui.BeginTable("skills", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            return;
        }

        ImGui.TableSetupColumn("Skill");
        ImGui.TableSetupColumn("Ready");
        ImGui.TableSetupColumn("Using");
        ImGui.TableSetupColumn("Cooldown");
        ImGui.TableHeadersRow();

        foreach (var skill in skills)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(skill.Name);

            ImGui.TableNextColumn();
            DrawBool(skill.CanBeUsed);

            ImGui.TableNextColumn();
            DrawBool(skill.IsUsing);

            ImGui.TableNextColumn();
            var cooldown = skill.Cooldowns.Count > 0 ? skill.Cooldowns.Max() : 0f;
            ImGui.TextUnformatted(cooldown > 0 ? $"{cooldown:0.0}s" : "-");
        }

        ImGui.EndTable();
    }

    private static void DrawFlasksSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Flasks", ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        DrawFlask("Flask 1", state.Flasks.Flask1);
        DrawFlask("Flask 2", state.Flasks.Flask2);
    }

    private static void DrawBuffTable(string id, IReadOnlyList<BuffRow> buffs)
    {
        if (buffs.Count == 0)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "none");
            return;
        }

        if (!ImGui.BeginTable(id, 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            return;
        }

        ImGui.TableSetupColumn("Name (use this)");
        ImGui.TableSetupColumn("Display");
        ImGui.TableSetupColumn("Charges");
        ImGui.TableSetupColumn("Time left");
        ImGui.TableHeadersRow();

        foreach (var buff in buffs)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(buff.Name);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(string.IsNullOrEmpty(buff.Display) ? "-" : buff.Display);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(buff.Charges.ToString());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(double.IsPositiveInfinity(buff.TimeLeft) ? "permanent" : $"{buff.TimeLeft:0.0}s");
        }

        ImGui.EndTable();
    }

    private static BuffRow ToBuffRow(StatusEffect buff) => new(buff.Name, buff.DisplayName, buff.Charges, buff.TimeLeft);

    // Plain-data copies used by the preview so a frozen snapshot doesn't reference live game entities.
    private sealed record BuffRow(string Name, string Display, int Charges, double TimeLeft);

    private sealed record MonsterSnapshot(string Name, MonsterRarity Rarity, float Distance, uint Id, List<BuffRow> Buffs);

    // Turns a full metadata path like "Metadata/Monsters/.../Zombie" into just "Zombie".
    private static string ShortName(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "(unknown)";
        }

        var lastSlash = path.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < path.Length - 1 ? path[(lastSlash + 1)..] : path;
    }

    private static void DrawVital(string label, Vital vital, Color color)
    {
        ImGui.TextColored(color.ToImguiVec4(), $"{label}: {vital.Current:0} / {vital.Max:0}  ({vital.Percent:0.0}%)");
    }

    private static void DrawBool(bool value)
    {
        ImGui.TextColored((value ? Color.Lime : Color.Gray).ToImguiVec4(), value ? "yes" : "no");
    }

    private static void DrawFlask(string label, FlaskInfo flask)
    {
        if (string.IsNullOrEmpty(flask.Name))
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), $"{label}: (empty)");
            return;
        }

        ImGui.Text($"{label}: {flask.Name}   charges {flask.Charges}/{flask.MaxCharges}   active {flask.Active}   ready {flask.CanBeUsed}");
    }
}
