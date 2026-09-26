using System.Collections.Generic;

namespace GearsetGallery.Data;

public enum GearSlot
{
    Head,
    Body,
    Hands,
    Legs,
    Feet,
}

public enum GenderLock
{
    Any,
    Male,
    Female,
    RaceSpecific,
}

public sealed record GearPiece(uint ItemId, string Name, uint IconId, GearSlot Slot, int Level, GenderLock Gender);

public sealed class Gearset
{
    /// <summary>Model set id (high bits) + variant (low 8 bits). Stable across patches.</summary>
    public required uint Key { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyDictionary<GearSlot, GearPiece> Pieces { get; init; }
    public required string Jobs { get; init; }

    /// <summary>Every item in the game that uses this set's look (all slots, all versions).</summary>
    public required IReadOnlyList<GearPiece> AllItems { get; init; }

    public int Level { get; init; }
    public GenderLock Gender { get; init; }

    /// <summary>Lower-cased set name + every piece name, for searching.</summary>
    public required string SearchText { get; init; }
}
