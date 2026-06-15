using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.Shared.Enums;
using ExileCore2.Shared.Helpers;
using ExilesReagentHelper.State;
using ImGuiNET;
using MonsterRarity = ExilesReagentHelper.State.MonsterRarity;

namespace ExilesReagentHelper;

public sealed class ExilesReagentHelper : BaseSettingsPlugin<ExilesReagentHelperSettings>
{
    // Cap how many monsters the buff preview lists, so a big pack doesn't flood the panel.
    private const int MaxMonstersInBuffPreview = 15;

    // When non-null, the player buff preview shows this frozen snapshot instead of live values,
    // so brief / fast-ticking buffs and debuffs can actually be read.
    private List<BuffRow> _frozenPlayerBuffs;

    // When non-null, the monster buff preview shows this frozen snapshot instead of live values,
    // so fast-changing debuffs (or monsters that die quickly) can actually be read.
    private List<MonsterSnapshot> _frozenMonsters;

    // When non-null, the stun/poise panel shows these frozen snapshots — lets the brief "Primed for
    // Stun" instant be read after the fact.
    private List<StunSnapshot> _frozenStun;

    // The copyable cell most recently clicked, and when (ms), so it can flash a green border as
    // confirmation that its text was copied to the clipboard.
    private static string _copiedCellId;
    private static long _copiedCellTick;
    private const long CopyFlashMs = 1200;

    public override void DrawSettings()
    {
        base.DrawSettings();

        // Only show the live diagnostic while the plugin is enabled — the Enable toggle (drawn by
        // base.DrawSettings above) gates everything below it.
        if (!Settings.Enable.Value)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "Plugin disabled — enable it to see the live state preview.");
            return;
        }

        // Build a fresh snapshot every frame the settings window is open and show what we read.
        // This is a read-only diagnostic that proves the State layer works against the live game.
        var state = new GameState(GameController, Settings.MaxMonsterRange);
        DrawStatePreview(state);
    }

    public override void Render()
    {
        // Per-frame automation (evaluate rules, press keys) will live here in a later step.

        if (Settings.Enable.Value && Settings.DrawNearestMonsterLine.Value)
        {
            DrawNearestMonsterLine();
        }
    }

    // Draws a line from the player to the nearest hostile monster, labelled with its distance.
    private void DrawNearestMonsterLine()
    {
        var playerRender = GameController?.Player?.GetComponent<Render>();
        if (playerRender == null)
        {
            return;
        }

        var state = new GameState(GameController, Settings.MaxMonsterRange);
        var nearest = state.Monsters(Settings.MaxMonsterRange.Value).FirstOrDefault();
        if (nearest == null)
        {
            return;
        }

        var camera = GameController.IngameState.Camera;
        var from = camera.WorldToScreen(playerRender.Pos with { Z = playerRender.UnclampedHeight });
        var to = camera.WorldToScreen(nearest.Position);

        var color = Settings.NearestMonsterLineColor.Value;
        Graphics.DrawLine(from, to, 2f, color);
        Graphics.DrawTextWithBackground($"{nearest.Distance:0}", (from + to) / 2f, color, FontAlign.Center, Color.Black);
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
        if (!ImGui.CollapsingHeader("Vitals", ImGuiTreeNodeFlags.None))
        {
            return;
        }

        DrawVital("Life", state.Vitals.HP, Color.IndianRed);
        DrawVital("ES", state.Vitals.ES, Color.LightSkyBlue);
        DrawVital("Mana", state.Vitals.Mana, Color.MediumSlateBlue);
    }

    private void DrawPlayerSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Player", ImGuiTreeNodeFlags.None))
        {
            return;
        }

        ImGui.Text($"Area: {state.AreaName}");
        ImGui.Text($"Moving: {state.IsMoving}");
        ImGui.Text($"Town: {state.IsInTown}    Hideout: {state.IsInHideout}    Peaceful: {state.IsInPeacefulArea}");
    }

    private void DrawPlayerBuffsSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Player buffs / debuffs", ImGuiTreeNodeFlags.None))
        {
            return;
        }

        var frozen = _frozenPlayerBuffs != null;

        // Freeze pauses the values so brief / fast-ticking buffs and debuffs can be read.
        if (ImGui.Button(frozen ? "Unfreeze##playerBuffs" : "Freeze##playerBuffs"))
        {
            _frozenPlayerBuffs = frozen ? null : state.Buffs.AllBuffs.Select(ToBuffRow).ToList();
            frozen = !frozen;
        }

        ImGui.SameLine();
        ImGui.TextColored(
            frozen ? Color.Orange.ToImguiVec4() : Color.Gray.ToImguiVec4(),
            frozen
                ? "Frozen snapshot — press Unfreeze for live values."
                : "The \"Name\" column is the internal id you'll match in conditions.");

        DrawBuffTable("playerBuffs", frozen ? _frozenPlayerBuffs : state.Buffs.AllBuffs.Select(ToBuffRow).ToList());
    }

    private void DrawMonstersSection(GameState state)
    {
        if (!ImGui.CollapsingHeader("Nearby monsters", ImGuiTreeNodeFlags.None))
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
        if (!ImGui.CollapsingHeader("Stats & States — monsters in range"))
        {
            return;
        }

        var frozen = _frozenStun != null;

        // Freeze captures every monster's values so the transient "Primed for Stun" instant can
        // actually be read — and the snapshot survives monsters dying or leaving range.
        if (ImGui.Button(frozen ? "Unfreeze" : "Freeze"))
        {
            _frozenStun = frozen ? null : CaptureStunSnapshots(state);
            frozen = _frozenStun != null;
        }

        ImGui.SameLine();
        ImGui.TextColored(
            frozen ? Color.Orange.ToImguiVec4() : Color.Gray.ToImguiVec4(),
            frozen
                ? "Frozen snapshot — press Unfreeze for live values."
                : "Hit a monster until it's Primed for Stun, then Freeze to read the values.");

        var snapshots = frozen ? _frozenStun : CaptureStunSnapshots(state);
        if (snapshots.Count == 0)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "No monsters in range.");
            return;
        }

        foreach (var snap in snapshots)
        {
            if (ImGui.TreeNode($"{snap.Label}##{snap.Id}"))
            {
                DrawStunSnapshot(snap);
                ImGui.TreePop();
            }
        }
    }

    // Effect/Daemon/Light/etc. entities scanned near each monster — the "Primed for Stun" orb is
    // probably one of these attached to the model rather than a stat on the monster itself.
    private static readonly EntityType[] OrbEntityTypes =
        [EntityType.Effect, EntityType.Daemon, EntityType.Light, EntityType.ServerObject, EntityType.MiscellaneousObjects];

    // How far (world units) from a monster an effect entity can be and still be considered "on" it.
    private const float OrbScanRadius = 30f;

    // Snapshots every monster in range (closest first) for the diagnostic panel.
    private List<StunSnapshot> CaptureStunSnapshots(GameState state)
    {
        // Project candidate effect entities to plain data once, so the frozen snapshot doesn't
        // reference live entities and the per-monster distance filter stays cheap.
        var effects = (GameController.EntityListWrapper?.OnlyValidEntities ?? [])
            .Where(e => OrbEntityTypes.Contains(e.Type))
            .Select(e => new EffectEntity(e.Type.ToString(), string.IsNullOrEmpty(e.Path) ? e.Metadata : e.Path, e.Pos))
            .ToList();

        return state.Monsters(Settings.MaxMonsterRange.Value)
            .Select(m => CaptureStunSnapshot(m, effects))
            .ToList();
    }

    // Copies everything we can read off a monster into plain data, so we can hunt for whatever
    // flips when it's "Primed for Stun" (the orb on the model).
    private static StunSnapshot CaptureStunSnapshot(MonsterInfo monster, List<EffectEntity> effects)
    {
        // "Primed for Stun" is a Poise/Daze mechanic in PoE2, so we match those keywords too —
        // e.g. CurrentDazeBuildUpPct and PoiseThreshold, which don't contain "stun".
        string[] keywords = ["stun", "poise", "daze", "buildup", "stagger", "primed"];
        var allStats = monster.Stats.All
            .OrderBy(kv => kv.Key.ToString())
            .Select(kv => new KeyValueRow(kv.Key.ToString(), kv.Value))
            .ToList();
        var stunStats = allStats
            .Where(r => keywords.Any(k => r.Key.Contains(k, System.StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var states = monster.States.All
            .OrderBy(kv => kv.Key)
            .Select(kv => new KeyValueRow(kv.Key, kv.Value))
            .ToList();
        var nearbyEffects = effects
            .Select(e => new EffectRow(e.Type, ShortName(e.Path), Vector3.Distance(e.Pos, monster.Position)))
            .Where(r => r.Distance <= OrbScanRadius)
            .OrderBy(r => r.Distance)
            .ToList();
        var label = $"{ShortName(monster.Path)}  [{monster.Rarity}]  ({monster.Distance:0} units)";
        return new StunSnapshot(monster.Id, label, stunStats, allStats, states, nearbyEffects);
    }

    private static void DrawStunSnapshot(StunSnapshot snap)
    {
        ImGui.Text($"Stun / poise / daze stats ({snap.StunStats.Count}):");
        DrawKeyValueTable($"stunStats{snap.Id}", snap.StunStats);

        ImGui.Spacing();
        ImGui.Text($"States ({snap.States.Count}):");
        DrawKeyValueTable($"monStates{snap.Id}", snap.States);

        ImGui.Spacing();
        ImGui.Text($"Nearby effect entities within {OrbScanRadius:0} units ({snap.NearbyEffects.Count}):");
        DrawEffectTable($"effects{snap.Id}", snap.NearbyEffects);

        ImGui.Spacing();
        if (ImGui.TreeNode($"All stats ({snap.AllStats.Count})##allStats{snap.Id}"))
        {
            DrawKeyValueTable($"allStats{snap.Id}", snap.AllStats);
            ImGui.TreePop();
        }
    }

    private static void DrawEffectTable(string id, IReadOnlyList<EffectRow> rows)
    {
        if (rows.Count == 0)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "none present");
            return;
        }

        if (!ImGui.BeginTable(id, 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            return;
        }

        ImGui.TableSetupColumn("Type");
        ImGui.TableSetupColumn("Path");
        ImGui.TableSetupColumn("Dist");
        ImGui.TableHeadersRow();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Type);
            ImGui.TableNextColumn();
            DrawCopyableText(row.Path, $"{id}_path_{i}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{row.Distance:0}");
        }

        ImGui.EndTable();
    }

    private static void DrawKeyValueTable(string id, IReadOnlyList<KeyValueRow> rows)
    {
        if (rows.Count == 0)
        {
            ImGui.TextColored(Color.Gray.ToImguiVec4(), "none present");
            return;
        }

        if (!ImGui.BeginTable(id, 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            return;
        }

        ImGui.TableSetupColumn("Key");
        ImGui.TableSetupColumn("Value");
        ImGui.TableHeadersRow();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            DrawCopyableText(row.Key, $"{id}_key_{i}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Value.ToString());
        }

        ImGui.EndTable();
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
        if (!ImGui.CollapsingHeader("Slotted skills", ImGuiTreeNodeFlags.None))
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

        for (var i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            DrawCopyableText(skill.Name, $"skill_name_{i}");

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
        if (!ImGui.CollapsingHeader("Flasks", ImGuiTreeNodeFlags.None))
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

        for (var i = 0; i < buffs.Count; i++)
        {
            var buff = buffs[i];
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            DrawCopyableText(buff.Name, $"{id}_name_{i}");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(string.IsNullOrEmpty(buff.Display) ? "-" : buff.Display);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(buff.Charges.ToString());

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(double.IsPositiveInfinity(buff.TimeLeft) ? "permanent" : $"{buff.TimeLeft:0.0}s");
        }

        ImGui.EndTable();
    }

    // Renders text as a clickable cell that copies its value to the clipboard — lets buff/skill/stat
    // ids be grabbed for use in rules. The unique id keeps ImGui from conflating identical cells.
    private static void DrawCopyableText(string text, string uniqueId)
    {
        if (string.IsNullOrEmpty(text))
        {
            ImGui.TextUnformatted(text ?? "");
            return;
        }

        if (ImGui.Selectable($"{text}##{uniqueId}"))
        {
            ImGui.SetClipboardText(text);
            _copiedCellId = uniqueId;
            _copiedCellTick = System.Environment.TickCount64;
        }

        var justCopied = _copiedCellId == uniqueId && System.Environment.TickCount64 - _copiedCellTick < CopyFlashMs;

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(justCopied ? "Copied!" : "Click to copy");
        }

        // Flash a green border on the cell that was just copied, as visual confirmation.
        if (justCopied)
        {
            var green = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 1f, 0f, 1f));
            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), green, 0f, ImDrawFlags.None, 2f);
        }
    }

    private static BuffRow ToBuffRow(StatusEffect buff) => new(buff.Name, buff.DisplayName, buff.Charges, buff.TimeLeft);

    // Plain-data copies used by the preview so a frozen snapshot doesn't reference live game entities.
    private sealed record BuffRow(string Name, string Display, int Charges, double TimeLeft);

    private sealed record MonsterSnapshot(string Name, MonsterRarity Rarity, float Distance, uint Id, List<BuffRow> Buffs);

    // Plain-data copy of a monster's stats/states for the diagnostic panel.
    private sealed record KeyValueRow(string Key, int Value);

    // A candidate effect entity (captured live) and a per-monster row (path + distance to monster).
    private sealed record EffectEntity(string Type, string Path, Vector3 Pos);

    private sealed record EffectRow(string Type, string Path, float Distance);

    private sealed record StunSnapshot(
        uint Id,
        string Label,
        List<KeyValueRow> StunStats,
        List<KeyValueRow> AllStats,
        List<KeyValueRow> States,
        List<EffectRow> NearbyEffects);

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
