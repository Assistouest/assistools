using Assistools.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Assistools.Pages;

public sealed partial class PageSettings : Page
{
    private bool _initDone;

    public PageSettings()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ToggleRappel.IsOn = NotificationService.EstRappelActif();
            _initDone = true;
        };
    }

    private void ToggleRappel_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_initDone) return;
        NotificationService.DefinirRappelActif(ToggleRappel.IsOn);
    }
}
