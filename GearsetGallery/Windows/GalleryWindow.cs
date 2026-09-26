using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using GearsetGallery.Data;
using GearsetGallery.Integration;

namespace GearsetGallery.Windows;

public sealed class GalleryWindow : Window
{
    private static readonly GearSlot[] SlotOrder = { GearSlot.Head, GearSlot.Body, GearSlot.Hands, GearSlot.Legs, GearSlot.Feet };
    private static readonly Vector2 ListCardSize = new(290, 238);
    private static readonly Vector2 CompactCardSize = new(250, 158);
    private static readonly Vector2 IconSize = new(40, 40);
    private static readonly Vector2 RowIconSize = new(24, 24);
    private static readonly Vector2 TooltipIconSize = new(20, 20);
    private static readonly Vector2 LargeIconSize = new(64, 64);
    private static readonly Vector4 Green = new(0.45f, 0.85f, 0.45f, 1f);
    private static readonly Vector4 Red = new(0.95f, 0.45f, 0.45f, 1f);
    private static readonly Vector4 Gold = new(1f, 0.8f, 0.3f, 1f);

    private readonly GearsetCatalog catalog;
    private readonly GlamourerBridge glamourer;
    private readonly Configuration config;

    private List<Gearset> filtered = new();
    private bool dirty = true;
    private string search = string.Empty;
    private int gender;        // 0 any, 1 male, 2 female
    private bool favoritesOnly;
    private bool sortByLevel;
    private int maxLevel = 100;
    private int page;
    private uint? previewKey;
    private string status = string.Empty;

    public GalleryWindow(GearsetCatalog catalog, GlamourerBridge glamourer, Configuration config)
        : base("Gearset Gallery##GearsetGallery")
    {
        this.catalog = catalog;
        this.glamourer = glamourer;
        this.config = config;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void OnClose()
    {
        if (previewKey != null && config.RevertPreviewOnClose && glamourer.IsAvailable)
        {
            glamourer.Revert();
            previewKey = null;
        }
    }

    public override void Draw()
    {
        if (!catalog.IsLoaded)
        {
            ImGui.TextUnformatted("Building the gearset catalog from game data...");
            return;
        }

        if (catalog.Error != null)
        {
            ImGui.TextColored(Red, $"Could not build the catalog: {catalog.Error}");
            return;
        }

        if (maxLevel > catalog.MaxLevel || maxLevel <= 0)
            maxLevel = catalog.MaxLevel;

        DrawToolbar();
        if (dirty)
            Refilter();

        DrawPager();
        ImGui.Separator();

        var footer = ImGui.GetFrameHeightWithSpacing();
        using (var grid = ImRaii.Child("##grid", new Vector2(-1, -footer), false))
        {
            if (grid)
                DrawGrid();
        }

        DrawStatus();
    }

    private void DrawToolbar()
    {
        var glamOk = glamourer.IsAvailable;
        ImGui.TextColored(glamOk ? Green : Red, glamOk ? "Glamourer connected" : "Glamourer not detected");
        ImGui.SameLine();
        using (ImRaii.Disabled(!glamOk))
        {
            if (ImGui.Button("Revert my gear"))
            {
                status = glamourer.Revert();
                previewKey = null;
            }
        }

        ImGui.SetNextItemWidth(240);
        if (ImGui.InputTextWithHint("##search", "Search set or piece name...", ref search, 128))
            MarkDirty();

        ImGui.SameLine();
        if (ImGui.Checkbox("Favorites", ref favoritesOnly))
            MarkDirty();

        ImGui.SameLine();
        if (ImGui.Checkbox("Sort by level", ref sortByLevel))
            MarkDirty();

        ImGui.SameLine();
        var showList = config.ShowItemList;
        if (ImGui.Checkbox("Show item names", ref showList))
        {
            config.ShowItemList = showList;
            config.Save();
        }

        if (ImGui.RadioButton("Any", gender == 0)) { gender = 0; MarkDirty(); }
        ImGui.SameLine();
        if (ImGui.RadioButton("Male", gender == 1)) { gender = 1; MarkDirty(); }
        ImGui.SameLine();
        if (ImGui.RadioButton("Female", gender == 2)) { gender = 2; MarkDirty(); }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(150);
        if (ImGui.SliderInt("Max level", ref maxLevel, 1, catalog.MaxLevel))
            MarkDirty();

        ImGui.SameLine();
        var minPieces = config.MinPieces;
        ImGui.SetNextItemWidth(110);
        if (ImGui.SliderInt("Min pieces", ref minPieces, 2, 5))
        {
            config.MinPieces = minPieces;
            config.Save();
            MarkDirty();
        }
    }

    private void DrawPager()
    {
        var pageSize = Math.Max(1, config.PageSize);
        var pages = Math.Max(1, (filtered.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 0, pages - 1);

        using (ImRaii.Disabled(page == 0))
        {
            if (ImGui.Button("<< First")) page = 0;
            ImGui.SameLine();
            if (ImGui.Button("< Prev")) page--;
        }

        ImGui.SameLine();
        ImGui.TextUnformatted($"Page {page + 1} / {pages}   ({filtered.Count} sets)");
        ImGui.SameLine();

        using (ImRaii.Disabled(page >= pages - 1))
        {
            if (ImGui.Button("Next >")) page++;
            ImGui.SameLine();
            if (ImGui.Button("Last >>")) page = pages - 1;
        }
    }

    private void DrawGrid()
    {
        var pageSize = Math.Max(1, config.PageSize);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var cardSize = config.ShowItemList ? ListCardSize : CompactCardSize;
        var columns = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + spacing) / (cardSize.X + spacing)));

        var visible = filtered.Skip(page * pageSize).Take(pageSize).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine();
            DrawCard(visible[i], cardSize);
        }

        if (visible.Count == 0)
            ImGui.TextDisabled("No sets match these filters.");
    }

    private void DrawCard(Gearset set, Vector2 cardSize)
    {
        using var id = ImRaii.PushId((int)set.Key);
        using var card = ImRaii.Child("##card", cardSize, true);
        if (!card)
            return;

        var isFavorite = config.Favorites.Contains(set.Key);
        var isPreviewing = previewKey == set.Key;

        if (isFavorite)
            ImGui.TextColored(Gold, set.Name);
        else
            ImGui.TextUnformatted(set.Name);
        if (ImGui.IsItemHovered())
            DrawSetTooltip(set);

        var genderText = set.Gender switch
        {
            GenderLock.Male => "  |  Male only",
            GenderLock.Female => "  |  Female only",
            GenderLock.RaceSpecific => "  |  Race-locked",
            _ => string.Empty,
        };
        ImGui.TextDisabled($"Lv. {set.Level}  |  {set.Pieces.Count} pieces{genderText}");

        if (config.ShowItemList)
            DrawPieceList(set);
        else
            DrawIconStrip(set);

        var glamOk = glamourer.IsAvailable;
        using (ImRaii.Disabled(!glamOk))
        {
            if (isPreviewing)
            {
                if (ImGui.Button("Stop preview"))
                {
                    status = glamourer.Revert();
                    previewKey = null;
                }
            }
            else if (ImGui.Button("Preview"))
            {
                status = glamourer.Apply(set, temporary: true);
                previewKey = set.Key;
            }

            ImGui.SameLine();
            if (ImGui.Button("Apply"))
            {
                status = glamourer.Apply(set, temporary: false);
                previewKey = null;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button(isFavorite ? "Unfavorite" : "Favorite"))
        {
            if (!config.Favorites.Remove(set.Key))
                config.Favorites.Add(set.Key);
            config.Save();
            if (favoritesOnly)
                MarkDirty();
        }
    }

    /// <summary>One row per slot: small icon + item name. Empty slots stay as a dim row so every card lines up.</summary>
    private void DrawPieceList(Gearset set)
    {
        foreach (var slot in SlotOrder)
        {
            var rowY = ImGui.GetCursorPosY();
            var textOffset = (RowIconSize.Y - ImGui.GetTextLineHeight()) / 2;

            if (set.Pieces.TryGetValue(slot, out var piece))
            {
                DrawIcon(piece.IconId, RowIconSize);
                var iconHovered = ImGui.IsItemHovered();
                var iconRightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);

                ImGui.SameLine();
                ImGui.SetCursorPosY(rowY + textOffset);
                ImGui.TextUnformatted(piece.Name);

                if (iconHovered || ImGui.IsItemHovered())
                    DrawPieceTooltip(set, piece);
                if (iconRightClicked || ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    OnPieceRightClick(piece);
            }
            else
            {
                ImGui.Dummy(RowIconSize);
                ImGui.SameLine();
                ImGui.SetCursorPosY(rowY + textOffset);
                ImGui.TextDisabled($"({slot}: not part of this set)");
            }
        }
    }

    /// <summary>Compact mode: one bigger icon per slot in a single row.</summary>
    private void DrawIconStrip(Gearset set)
    {
        for (var i = 0; i < SlotOrder.Length; i++)
        {
            if (i > 0)
                ImGui.SameLine();

            if (set.Pieces.TryGetValue(SlotOrder[i], out var piece))
            {
                DrawIcon(piece.IconId, IconSize);
                if (ImGui.IsItemHovered())
                    DrawPieceTooltip(set, piece);
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    OnPieceRightClick(piece);
            }
            else
            {
                ImGui.Dummy(IconSize);
            }
        }
    }

    /// <summary>Right-click a piece: wear just that piece (mix and match) and post its item link in your chat log.</summary>
    private void OnPieceRightClick(GearPiece piece)
    {
        if (glamourer.IsAvailable)
        {
            status = glamourer.ApplyPiece(piece);
            previewKey = null;
        }
        else
        {
            status = "Glamourer not detected; linked the item only.";
        }

        LinkInChat(piece);
    }

    /// <summary>Prints a clickable item link to your own chat log (only you can see it; nothing is sent to other players).</summary>
    private static void LinkInChat(GearPiece piece)
    {
        try
        {
            var message = new SeStringBuilder()
                .AddText("[Gearset Gallery] ")
                .AddItemLink(piece.ItemId, false)
                .Build();
            Services.Chat.Print(message);
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, $"Could not link {piece.Name} in chat.");
        }
    }

    private static void DrawIcon(uint iconId, Vector2 size)
    {
        if (iconId == 0)
        {
            ImGui.Dummy(size);
            return;
        }

        var icon = Services.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
        ImGui.Image(icon.Handle, size);
    }

    /// <summary>Large icon for the hovered piece, plus every other item that gives the same look in that slot.</summary>
    private static void DrawPieceTooltip(Gearset set, GearPiece piece)
    {
        using var tooltip = ImRaii.Tooltip();
        DrawIcon(piece.IconId, LargeIconSize);
        ImGui.SameLine();
        using (ImRaii.Group())
        {
            ImGui.TextUnformatted(piece.Name);
            ImGui.TextDisabled($"{piece.Slot}  |  Lv. {piece.Level}");
            ImGui.TextDisabled("Right-click: wear this piece + link in chat");
        }

        var sameLook = set.AllItems.Where(p => p.Slot == piece.Slot && p.ItemId != piece.ItemId).ToList();
        if (sameLook.Count == 0)
            return;

        ImGui.Separator();
        ImGui.TextDisabled("Same look:");
        foreach (var other in sameLook)
            DrawTooltipRow(other);
    }

    /// <summary>Hovering the set name: every item in the game that shares this look, each with its icon.</summary>
    private static void DrawSetTooltip(Gearset set)
    {
        using var tooltip = ImRaii.Tooltip();
        ImGui.TextUnformatted(set.Name);
        if (!string.IsNullOrEmpty(set.Jobs))
            ImGui.TextDisabled(set.Jobs);

        foreach (var slot in SlotOrder)
        {
            var items = set.AllItems.Where(p => p.Slot == slot).ToList();
            if (items.Count == 0)
                continue;

            ImGui.Separator();
            ImGui.TextDisabled(slot.ToString());
            foreach (var item in items)
                DrawTooltipRow(item);
        }
    }

    private static void DrawTooltipRow(GearPiece item)
    {
        var rowY = ImGui.GetCursorPosY();
        DrawIcon(item.IconId, TooltipIconSize);
        ImGui.SameLine();
        ImGui.SetCursorPosY(rowY + (TooltipIconSize.Y - ImGui.GetTextLineHeight()) / 2);
        ImGui.TextUnformatted($"{item.Name}  (Lv. {item.Level})");
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrEmpty(status))
            ImGui.TextUnformatted(status);
        else
            ImGui.TextDisabled("Right-click any piece to wear just that piece. Preview is temporary; Apply stays until you revert.");
    }

    private void MarkDirty()
    {
        dirty = true;
        page = 0;
    }

    private void Refilter()
    {
        dirty = false;
        var needle = search.Trim().ToLowerInvariant();

        IEnumerable<Gearset> query = catalog.Sets.Where(s =>
            s.Pieces.Count >= config.MinPieces
            && s.Level <= maxLevel
            && (needle.Length == 0 || s.SearchText.Contains(needle, StringComparison.Ordinal))
            && (!favoritesOnly || config.Favorites.Contains(s.Key))
            && gender switch
            {
                1 => s.Gender is GenderLock.Any or GenderLock.Male,
                2 => s.Gender is GenderLock.Any or GenderLock.Female,
                _ => true,
            });

        filtered = sortByLevel
            ? query.OrderByDescending(s => s.Level).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : query.ToList();
    }
}
