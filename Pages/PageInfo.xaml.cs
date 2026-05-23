using System.Diagnostics;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Assistools.Pages;

public sealed partial class PageInfo : Page
{
    public PageInfo()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        string court = v is null ? "3.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        VersionBadge.Text = "v" + court;
        VersionDetail.Text = $"{court} (build {System.DateTime.Now:yyyy.MM})";
    }

    private void BtnSiteWeb_Click(object sender, RoutedEventArgs e)
    {
        OuvrirUrl("https://assistouest.fr/logiciel-maintenance-informatique/");
    }

    private void BtnSupport_Click(object sender, RoutedEventArgs e)
    {
        OuvrirUrl("mailto:support@assistouest.fr?subject=Assistools%20%E2%80%94%20Demande%20de%20support");
    }

    private static void OuvrirUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { }
    }
}
