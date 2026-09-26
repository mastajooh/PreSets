using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using GearsetGallery.Data;
using GearsetGallery.Integration;
using GearsetGallery.Windows;

namespace GearsetGallery;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/gearsets";

    private readonly WindowSystem windows = new("GearsetGallery");
    private readonly GalleryWindow gallery;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();

        var config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var catalog = new GearsetCatalog();
        catalog.StartLoading();

        var glamourer = new GlamourerBridge(pluginInterface);
        gallery = new GalleryWindow(catalog, glamourer, config);
        windows.AddWindow(gallery);

        Services.CommandManager.AddHandler(Command, new CommandInfo((_, _) => gallery.Toggle())
        {
            HelpMessage = "Open the gearset gallery.",
        });

        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi += gallery.Toggle;
    }

    public void Dispose()
    {
        Services.PluginInterface.UiBuilder.Draw -= windows.Draw;
        Services.PluginInterface.UiBuilder.OpenMainUi -= gallery.Toggle;
        Services.CommandManager.RemoveHandler(Command);
        windows.RemoveAllWindows();
    }
}
