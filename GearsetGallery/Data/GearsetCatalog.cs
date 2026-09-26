using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;

namespace GearsetGallery.Data;

/// <summary>
/// Builds the list of armor sets straight from the game's Item sheet.
/// Pieces are grouped by their shared 3D model (model set id + variant), which is
/// how the game itself ties a set's head/body/hands/legs/feet together.
/// </summary>
public sealed class GearsetCatalog
{
    private static readonly HashSet<string> RoleSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fending", "Maiming", "Striking", "Scouting", "Aiming", "Casting", "Healing", "Slaying", "Crafting", "Gathering",
    };

    public IReadOnlyList<Gearset> Sets { get; private set; } = Array.Empty<Gearset>();
    public bool IsLoaded { get; private set; }
    public string? Error { get; private set; }
    public int MaxLevel { get; private set; } = 100;

    public void StartLoading() => Task.Run(() =>
    {
        try
        {
            Sets = Build();
            MaxLevel = Sets.Count == 0 ? 100 : Math.Max(1, Sets.Max(s => s.Level));
            Services.Log.Information($"Gearset catalog built: {Sets.Count} sets.");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Services.Log.Error(ex, "Failed to build gearset catalog.");
        }
        finally
        {
            IsLoaded = true;
        }
    });

    private List<Gearset> Build()
    {
        var groups = new Dictionary<uint, List<GearPiece>>();

        foreach (var item in Services.DataManager.GetExcelSheet<Item>())
        {
            if (item.ModelMain == 0)
                continue;

            var category = item.EquipSlotCategory.ValueNullable;
            if (category == null)
                continue;

            GearSlot? slot = category.Value switch
            {
                { Head: 1 } => GearSlot.Head,
                { Body: 1 } => GearSlot.Body,
                { Gloves: 1 } => GearSlot.Hands,
                { Legs: 1 } => GearSlot.Legs,
                { Feet: 1 } => GearSlot.Feet,
                _ => null,
            };
            if (slot == null)
                continue;

            var name = item.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("Dated ", StringComparison.Ordinal))
                continue;

            var setId = (ushort)(item.ModelMain & 0xFFFF);
            var variant = (byte)((item.ModelMain >> 16) & 0xFF);
            if (setId == 0)
                continue;

            var key = ((uint)setId << 8) | variant;
               var piece = new GearPiece(item.RowId, name, item.Icon, slot.Value, item.LevelEquip, ToGender((byte)item.EquipRestriction.RowId));
            
            if (!groups.TryGetValue(key, out var list))
                groups[key] = list = new List<GearPiece>();
            list.Add(piece);
        }

        var result = new List<Gearset>(groups.Count);
        var itemSheet = Services.DataManager.GetExcelSheet<Item>();

        foreach (var (key, candidates) in groups)
        {
            // One representative item per slot: lowest level, then lowest id.
            var pieces = candidates
                .GroupBy(p => p.Slot)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Level).ThenBy(p => p.ItemId).First());

            if (pieces.Count < 2)
                continue;

            var body = pieces.GetValueOrDefault(GearSlot.Body) ?? pieces.Values.First();
            var jobs = itemSheet.GetRowOrDefault(body.ItemId)?.ClassJobCategory.ValueNullable?.Name.ExtractText() ?? string.Empty;
            var setName = DeriveName(pieces.Values.Select(p => p.Name).ToList(), body.Name);

            result.Add(new Gearset
            {
                Key = key,
                Name = setName,
                Pieces = pieces,
                Jobs = jobs,
                AllItems = candidates
                    .GroupBy(c => c.ItemId).Select(g => g.First())
                    .OrderBy(c => c.Slot).ThenBy(c => c.Level).ThenBy(c => c.Name)
                    .ToList(),
                Level = pieces.Values.Max(p => p.Level),
                Gender = pieces.Values.Select(p => p.Gender).FirstOrDefault(g => g != GenderLock.Any),
                SearchText = (setName + "\n" + string.Join("\n", candidates.Select(c => c.Name).Distinct())).ToLowerInvariant(),
            });
        }

        return result.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static GenderLock ToGender(byte equipRestriction) => equipRestriction switch
    {
        2 => GenderLock.Male,
        3 => GenderLock.Female,
        > 3 => GenderLock.RaceSpecific,
        _ => GenderLock.Any,
    };

    /// <summary>"Herald of Legend" + "Chiton of Legend" -> "Legend Set"; "Scaevan Magitek Coat" + "... Gloves" -> "Scaevan Magitek Set".</summary>
    private static string DeriveName(IReadOnlyList<string> names, string fallback)
    {
        var suffix = names
            .Select(n => { var i = n.LastIndexOf(" of ", StringComparison.Ordinal); return i > 0 ? n[(i + 4)..] : null; })
            .Where(s => s != null)
            .GroupBy(s => s!)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (suffix != null && suffix.Count() >= 2 && !RoleSuffixes.Contains(suffix.Key))
            return $"{suffix.Key} Set";

        var words = names.Select(n => n.Split(' ')).ToList();
        var prefix = new List<string>();
        for (var i = 0; words.All(w => w.Length > i + 1); i++)
        {
            var word = words[0][i];
            if (!words.All(w => w[i] == word))
                break;
            prefix.Add(word);
        }

        if (prefix.Count > 0)
        {
            var role = suffix != null && suffix.Count() >= 2 ? $" of {suffix.Key}" : string.Empty;
            return $"{string.Join(' ', prefix)} Set{role}";
        }

        return fallback;
    }
}
