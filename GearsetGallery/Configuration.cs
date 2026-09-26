using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace GearsetGallery;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Stable set keys (model set id + variant) the user starred.</summary>
    public HashSet<uint> Favorites { get; set; } = new();

    /// <summary>Hide "sets" with fewer pieces than this (2-5).</summary>
    public int MinPieces { get; set; } = 3;

    /// <summary>Undo a preview automatically when the gallery window closes.</summary>
    public bool RevertPreviewOnClose { get; set; } = true;

    public int PageSize { get; set; } = 24;

    /// <summary>Cards list each piece as icon + name. Off = compact strip of icons only.</summary>
    public bool ShowItemList { get; set; } = true;

    public void Save() => Services.PluginInterface.SavePluginConfig(this);
}
