using System.Windows;
using RimForge.App.Startup;

namespace RimForge.App;

public partial class App : Application
{
    public App()
    {
        StartupTimeline.Mark("App constructor entered", "WPF");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        StartupTimeline.Mark("Application startup entered", "WPF");
        base.OnStartup(e);
        StartupTimeline.Mark("Application startup completed", "WPF");
    }
}
