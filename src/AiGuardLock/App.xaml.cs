using System.Windows;
using AiGuardLock.Core;

namespace AiGuardLock;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try { UiText.Configure(e.Args); }
        catch (ArgumentException error)
        {
            MessageBox.Show(error.Message, "AI Guard Lock", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        base.OnStartup(e);
    }
}
