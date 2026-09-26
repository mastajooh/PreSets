using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using GearsetGallery.Data;
using Glamourer.Api.Enums;
using Glamourer.Api.IpcSubscribers;

namespace GearsetGallery.Integration;

/// <summary>Thin wrapper over Glamourer's public IPC. Object index 0 is always your own character.</summary>
public sealed class GlamourerBridge
{
    private const int LocalPlayer = 0;
    private const uint NoLock = 0;
    private static readonly List<byte> Undyed = new() { 0, 0 };

    private readonly ApiVersion apiVersion;
    private readonly SetItem setItem;
    private readonly RevertState revertState;

    private DateTime lastCheck = DateTime.MinValue;
    private bool available;

    public GlamourerBridge(IDalamudPluginInterface pi)
    {
        apiVersion = new ApiVersion(pi);
        setItem = new SetItem(pi);
        revertState = new RevertState(pi);
    }

    /// <summary>Checked at most every 5 seconds so a missing Glamourer doesn't throw every frame.</summary>
    public bool IsAvailable
    {
        get
        {
            if (DateTime.UtcNow - lastCheck < TimeSpan.FromSeconds(5))
                return available;

            lastCheck = DateTime.UtcNow;
            try
            {
                available = apiVersion.Invoke().Major >= 1;
            }
            catch
            {
                available = false;
            }

            return available;
        }
    }

    /// <summary>
    /// temporary = true: ApplyFlag.Once, the game drops it on your next real gear change (used for previews).
    /// temporary = false: stays applied until you revert it in Glamourer or here.
    /// </summary>
    public string Apply(Gearset set, bool temporary)
    {
        var flags = ApplyFlag.Equipment | (temporary ? ApplyFlag.Once : 0);
        var failures = new List<string>();

        foreach (var (slot, piece) in set.Pieces)
        {
            try
            {
                var ec = setItem.Invoke(LocalPlayer, ToApiSlot(slot), piece.ItemId, Undyed, NoLock, flags);
                if (ec is not (GlamourerApiEc.Success or GlamourerApiEc.NothingDone))
                    failures.Add($"{slot}: {ec}");
            }
            catch (Exception ex)
            {
                Services.Log.Warning(ex, $"Glamourer SetItem failed for {piece.Name}.");
                failures.Add($"{slot}: IPC error");
            }
        }

        return failures.Count == 0
            ? $"{(temporary ? "Previewing" : "Applied")} {set.Name}."
            : $"{set.Name}: some pieces failed ({string.Join(", ", failures)}).";
    }

    public string Revert()
    {
        try
        {
            var ec = revertState.Invoke(LocalPlayer, NoLock, ApplyFlag.Equipment);
            return ec is GlamourerApiEc.Success or GlamourerApiEc.NothingDone ? "Reverted to your normal gear." : $"Revert failed: {ec}.";
        }
        catch (Exception ex)
        {
            Services.Log.Warning(ex, "Glamourer RevertState failed.");
            return "Revert failed: IPC error.";
        }
    }

    private static ApiEquipSlot ToApiSlot(GearSlot slot) => slot switch
    {
        GearSlot.Head => ApiEquipSlot.Head,
        GearSlot.Body => ApiEquipSlot.Body,
        GearSlot.Hands => ApiEquipSlot.Hands,
        GearSlot.Legs => ApiEquipSlot.Legs,
        GearSlot.Feet => ApiEquipSlot.Feet,
        _ => throw new ArgumentOutOfRangeException(nameof(slot)),
    };
}
