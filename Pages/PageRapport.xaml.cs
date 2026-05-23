using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Assistools.Services;

namespace Assistools.Pages;

public sealed partial class PageRapport : Page
{
    private SystemReport? _report;

    public PageRapport()
    {
        InitializeComponent();
        Loaded += PageRapport_Loaded;
    }

    private async void PageRapport_Loaded(object sender, RoutedEventArgs e)
    {
        await ChargerRapportAsync();
    }

    private async Task ChargerRapportAsync()
    {
        try
        {
            LoadingPanel.Visibility = Visibility.Visible;
            LoadingRing.IsActive = true;
            ReportPanel.Visibility = Visibility.Collapsed;
            BtnExportPdf.IsEnabled = false;

            _report = await RapportService.GenererRapportAsync();

            if (_report == null)
            {
                LoadingRing.IsActive = false;
                if (LoadingPanel.Children.Count > 0 && LoadingPanel.Children[0] is StackPanel sp)
                {
                    foreach (var child in sp.Children)
                    {
                        if (child is TextBlock tb)
                        {
                            tb.Text = "Impossible de générer le rapport système. Veuillez réessayer.";
                            tb.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                        }
                    }
                }
                return;
            }

            PopulateUi(_report);

            LoadingPanel.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = false;
            ReportPanel.Visibility = Visibility.Visible;
            BtnExportPdf.IsEnabled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PageRapport] Exception : {ex.Message}");
            LoadingRing.IsActive = false;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static SolidColorBrush OkBrush => new(Colors.ForestGreen);
    private static SolidColorBrush WarnBrush => new(Colors.DarkOrange);
    private static SolidColorBrush AlertBrush => new(Colors.Crimson);

    private static TextBlock MakeKv(string label, string value)
    {
        return new TextBlock
        {
            Text = $"{label} : {value}",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private void AddKvRow(StackPanel panel, string label, string value)
    {
        var row = new Grid { Margin = new Thickness(16, 4, 16, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lbl = new TextBlock
        {
            Text = label,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12
        };
        var val = new TextBlock
        {
            Text = value,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(val, 1);
        row.Children.Add(lbl);
        row.Children.Add(val);
        panel.Children.Add(row);
    }

    // ─── Main populate ─────────────────────────────────────────────────────────

    private void PopulateUi(SystemReport report)
    {
        // ── SYNTHÈSE CARDS ──────────────────────────────────────────────────

        TxtOsName.Text = report.OS.Name ?? "Windows (Inconnu)";
        TxtBoardName.Text = $"{report.Motherboard.Manufacturer ?? "Inconnu"} {report.Motherboard.Product ?? "Inconnu"}";

        TxtCpuName.Text = report.CPU.Name ?? "Processeur Inconnu";
        TxtCpuDetails.Text = $"{report.CPU.Cores} cœurs / {report.CPU.Threads} threads · {report.CPU.MaxSpeed} MHz";

        long totalRamBytes = 0;
        string ddrType = "DDR";
        int ramSpeed = 0;
        if (report.RAM.Modules != null)
        {
            foreach (var m in report.RAM.Modules)
            {
                totalRamBytes += m.CapacityBytes;
                ddrType = RapportService.ObtenirDdrType(m);
                ramSpeed = m.ConfiguredClockSpeed > 0 ? m.ConfiguredClockSpeed : m.Speed;
            }
        }
        TxtRamSummary.Text = $"{RapportService.FormaterTaille(totalRamBytes)} {ddrType} ({ramSpeed} MHz)";
        TxtRamSlots.Text = $"{report.RAM.UsedSlots} / {report.RAM.TotalSlots} slots occupés";

        if (report.GPUs != null && report.GPUs.Count > 0)
        {
            var gpuNames = new List<string>();
            long totalVram = 0;
            foreach (var g in report.GPUs) { gpuNames.Add(g.Name); totalVram += g.VramBytes; }
            TxtGpuName.Text = string.Join(" + ", gpuNames);
            TxtGpuVram.Text = totalVram > 0 ? RapportService.FormaterTaille(totalVram) : "Partagée / Dynamique";
        }
        else
        {
            TxtGpuName.Text = "Aucun processeur graphique détecté";
            TxtGpuVram.Text = "";
        }

        if (report.Disks != null && report.Disks.Count > 0)
        {
            long totalDiskSize = 0; int ssdCount = 0; int hddCount = 0;
            foreach (var d in report.Disks)
            {
                totalDiskSize += d.SizeBytes;
                if (d.MediaType?.Contains("SSD", StringComparison.OrdinalIgnoreCase) == true) ssdCount++;
                else hddCount++;
            }
            string diskTypeDesc = ssdCount > 0 && hddCount > 0 ? $"({ssdCount} SSD, {hddCount} HDD)"
                : ssdCount > 0 ? $"({ssdCount} SSD)"
                : hddCount > 0 ? $"({hddCount} HDD)" : "";
            TxtStorageSummary.Text = $"{RapportService.FormaterTaille(totalDiskSize)} total {diskTypeDesc}";
        }
        else TxtStorageSummary.Text = "Aucun disque détecté";

        int partitionCount = report.Volumes?.Count ?? 0;
        TxtStoragePartitions.Text = $"{partitionCount} partition{(partitionCount > 1 ? "s" : "")} détectée{(partitionCount > 1 ? "s" : "")}";

        // ── SÉCURITÉ CARDS ──────────────────────────────────────────────────

        var sec = report.Security ?? new SecurityInfo();

        bool defOk = sec.DefenderEnabled && sec.RealtimeProtection;
        BorderDefender.Background = defOk ? OkBrush : (sec.DefenderEnabled ? WarnBrush : AlertBrush);
        TxtDefenderStatus.Text = sec.DefenderEnabled
            ? (sec.RealtimeProtection ? "Activé, temps réel actif" : "Activé, temps réel inactif")
            : "Désactivé";
        TxtDefenderEngine.Text = !string.IsNullOrEmpty(sec.DefenderEngineVersion)
            ? $"Moteur {sec.DefenderEngineVersion} · Signatures : {sec.SignaturesLastUpdated}"
            : "";

        bool allFirewall = sec.FirewallDomain && sec.FirewallPrivate && sec.FirewallPublic;
        bool anyFirewall = sec.FirewallDomain || sec.FirewallPrivate || sec.FirewallPublic;
        BorderFirewall.Background = allFirewall ? OkBrush : anyFirewall ? WarnBrush : AlertBrush;
        TxtFirewallStatus.Text = allFirewall ? "Actif sur tous les profils"
            : anyFirewall ? "Partiellement actif" : "Désactivé";
        TxtFirewallProfiles.Text = $"Domaine {(sec.FirewallDomain ? "✓" : "✗")}  Privé {(sec.FirewallPrivate ? "✓" : "✗")}  Public {(sec.FirewallPublic ? "✓" : "✗")}";

        var bios = report.Bios ?? new BiosInfo();
        BorderBios.Background = bios.TpmReady ? OkBrush : WarnBrush;
        TxtBiosMode.Text = bios.Mode ?? "Inconnu";
        TxtTpmStatus.Text = sec.TpmStatus + (bios.SecureBootEnabled ? " · Secure Boot ON" : " · Secure Boot OFF");

        // ── EXPANDER : MODULES RAM ───────────────────────────────────────────

        PanelRamDetails.Children.Clear();
        if (report.RAM.Modules != null && report.RAM.Modules.Count > 0)
        {
            bool first = true;
            foreach (var m in report.RAM.Modules)
            {
                if (!first) PanelRamDetails.Children.Add(new MenuFlyoutSeparator());
                first = false;

                var row = new Grid { Padding = new Thickness(16, 10, 16, 10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txtLoc = new TextBlock { Text = m.DeviceLocator ?? "Slot", Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(txtLoc, 0); row.Children.Add(txtLoc);

                string mfgDetail = $"{m.Manufacturer ?? "Inconnu"}  |  {m.PartNumber}  |  S/N {m.SerialNumber}";
                var txtMfg = new TextBlock { Text = mfgDetail, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(txtMfg, 1); row.Children.Add(txtMfg);

                int speed = m.ConfiguredClockSpeed > 0 ? m.ConfiguredClockSpeed : m.Speed;
                var txtCap = new TextBlock { Text = $"{RapportService.FormaterTaille(m.CapacityBytes)} {RapportService.ObtenirDdrType(m)} @ {speed} MHz", Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(txtCap, 2); row.Children.Add(txtCap);

                PanelRamDetails.Children.Add(row);
            }
        }
        else
        {
            PanelRamDetails.Children.Add(new TextBlock { Text = "Aucun module physique détecté.", Margin = new Thickness(16), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        }

        // ── EXPANDER : DISQUES ───────────────────────────────────────────────

        PanelDisksDetails.Children.Clear();
        if (report.Disks != null && report.Disks.Count > 0)
        {
            bool first = true;
            foreach (var d in report.Disks)
            {
                if (!first) PanelDisksDetails.Children.Add(new MenuFlyoutSeparator());
                first = false;

                var row = new Grid { Padding = new Thickness(16, 10, 16, 10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel { Spacing = 2 };
                info.Children.Add(new TextBlock { Text = d.Model ?? "Disque inconnu", Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });

                string details = $"{d.MediaType ?? "Inconnu"} · {d.BusType}";
                if (d.TemperatureC > 0) details += $" · {d.TemperatureC:F0} °C";
                if (d.PowerOnHours > 0) details += $" · {d.PowerOnHours:N0} h d'utilisation";
                if (!string.IsNullOrEmpty(d.FirmwareVersion)) details += $" · Firmware {d.FirmwareVersion}";
                info.Children.Add(new TextBlock { Text = details, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
                if (!string.IsNullOrEmpty(d.SerialNumber))
                    info.Children.Add(new TextBlock { Text = $"S/N : {d.SerialNumber}", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });

                Grid.SetColumn(info, 0); row.Children.Add(info);

                var txtSize = new TextBlock { Text = RapportService.FormaterTaille(d.SizeBytes), Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(txtSize, 1); row.Children.Add(txtSize);

                PanelDisksDetails.Children.Add(row);
            }
        }
        else PanelDisksDetails.Children.Add(new TextBlock { Text = "Aucun disque physique détecté.", Margin = new Thickness(16), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

        // ── EXPANDER : VOLUMES ───────────────────────────────────────────────

        PanelVolumesDetails.Children.Clear();
        if (report.Volumes != null && report.Volumes.Count > 0)
        {
            bool first = true;
            foreach (var vol in report.Volumes)
            {
                if (!first) PanelVolumesDetails.Children.Add(new MenuFlyoutSeparator());
                first = false;

                var row = new Grid { Padding = new Thickness(16, 10, 16, 10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel { Spacing = 2 };
                string labelDesc = string.IsNullOrEmpty(vol.Label) ? "Sans nom" : vol.Label;
                info.Children.Add(new TextBlock { Text = $"{vol.DriveLetter}: {labelDesc} ({vol.FileSystem})", Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });

                double usedPct = vol.SizeBytes > 0 ? (1.0 - ((double)vol.FreeBytes / vol.SizeBytes)) * 100.0 : 0;
                info.Children.Add(new TextBlock { Text = $"{RapportService.FormaterTaille(vol.FreeBytes)} libres sur {RapportService.FormaterTaille(vol.SizeBytes)}, {usedPct:F1} % utilisé", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
                if (!string.IsNullOrEmpty(vol.BitlockerStatus))
                    info.Children.Add(new TextBlock { Text = $"BitLocker : {vol.BitlockerStatus}", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });

                Grid.SetColumn(info, 0); row.Children.Add(info);

                var barStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
                barStack.Children.Add(new ProgressBar { Value = usedPct, Width = 100, Height = 6, CornerRadius = new CornerRadius(3) });
                Grid.SetColumn(barStack, 1); row.Children.Add(barStack);

                PanelVolumesDetails.Children.Add(row);
            }
        }
        else PanelVolumesDetails.Children.Add(new TextBlock { Text = "Aucune partition détectée.", Margin = new Thickness(16), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

        // ── EXPANDER : COMPOSANTS INTÉGRÉS ──────────────────────────────────

        PanelOnboard.Children.Clear();
        if (report.OnboardComponents != null && report.OnboardComponents.Count > 0)
        {
            bool first = true;
            foreach (var ob in report.OnboardComponents)
            {
                if (!first) PanelOnboard.Children.Add(new MenuFlyoutSeparator());
                first = false;

                var row = new Grid { Padding = new Thickness(16, 10, 16, 10) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Badge catégorie
                var catBadge = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = ob.Category switch
                    {
                        "Wi-Fi" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 252, 231)),
                        "Ethernet" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 241, 245, 249)),
                        "Bluetooth" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 237, 233, 254)),
                        "Audio" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 252, 231, 243)),
                        _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 241, 245, 249))
                    }
                };
                catBadge.Child = new TextBlock
                {
                    Text = ob.Category,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                };
                Grid.SetColumn(catBadge, 0);
                row.Children.Add(catBadge);

                // Nom + fabricant
                var info = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                info.Children.Add(new TextBlock { Text = ob.Name, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
                if (!string.IsNullOrEmpty(ob.Manufacturer))
                    info.Children.Add(new TextBlock { Text = ob.Manufacturer, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
                if (!string.IsNullOrEmpty(ob.PnpDeviceId))
                    info.Children.Add(new TextBlock { Text = ob.PnpDeviceId, FontSize = 10, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(info, 1);
                row.Children.Add(info);

                // Version driver
                var drvTb = new TextBlock
                {
                    Text = !string.IsNullOrEmpty(ob.DriverVersion) ? $"v{ob.DriverVersion}" : "Driver inconnu",
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources[!string.IsNullOrEmpty(ob.DriverVersion) ? "TextFillColorSecondaryBrush" : "TextFillColorTertiaryBrush"],
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(drvTb, 2);
                row.Children.Add(drvTb);

                PanelOnboard.Children.Add(row);
            }
        }
        else
        {
            PanelOnboard.Children.Add(new TextBlock { Text = "Aucun composant intégré détecté.", Margin = new Thickness(16), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        }

        // ── EXPANDER : GPU DETAILS ───────────────────────────────────────────

        PanelGpuDetails.Children.Clear();
        if (report.GPUs != null && report.GPUs.Count > 0)
        {
            bool first = true;
            foreach (var g in report.GPUs)
            {
                if (!first) PanelGpuDetails.Children.Add(new MenuFlyoutSeparator());
                first = false;

                var row = new StackPanel { Padding = new Thickness(16, 10, 16, 10), Spacing = 4 };
                row.Children.Add(new TextBlock { Text = g.Name, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });

                string gpuLine = $"{g.Vendor}  ·  VRAM : {(g.VramBytes > 0 ? RapportService.FormaterTaille(g.VramBytes) : "Partagée")}";
                if (g.TemperatureC > 0) gpuLine += $"  ·  Temp : {g.TemperatureC:F0} °C";
                if (g.LoadPercent > 0) gpuLine += $"  ·  Charge : {g.LoadPercent:F0} %";
                row.Children.Add(new TextBlock { Text = gpuLine, FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

                if (!string.IsNullOrEmpty(g.DriverVersion))
                    row.Children.Add(new TextBlock { Text = $"Pilote : {g.DriverVersion}  ({g.DriverDate})", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });
                if (!string.IsNullOrEmpty(g.CurrentResolution))
                    row.Children.Add(new TextBlock { Text = $"Résolution active : {g.CurrentResolution}", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });

                PanelGpuDetails.Children.Add(row);
            }
        }
        else PanelGpuDetails.Children.Add(new TextBlock { Text = "Aucun GPU détecté.", Margin = new Thickness(16), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

        // ── EXPANDER : CPU + CARTE MÈRE + BIOS ──────────────────────────────

        DetailCpuName.Text = report.CPU.Name ?? "Inconnu";
        DetailCpuMfg.Text = $"{report.CPU.Manufacturer} · {report.CPU.Architecture}";
        DetailCpuCores.Text = $"{report.CPU.Cores} cœurs physiques / {report.CPU.Threads} processeurs logiques";
        DetailCpuSpeed.Text = $"{report.CPU.MaxSpeed} MHz max / {report.CPU.CurrentSpeed} MHz courant";
        DetailCpuSocket.Text = $"{report.CPU.Socket} · L2 {report.CPU.L2CacheKB} Ko / L3 {report.CPU.L3CacheKB} Ko";
        DetailCpuVirt.Text = report.CPU.VirtualizationEnabled ? "Activée (VT-x / AMD-V)" : "Désactivée ou non supportée";
        DetailBoardManufacturer.Text = report.Motherboard.Manufacturer ?? "Inconnu";
        DetailBoardProduct.Text = report.Motherboard.Product ?? "Inconnu";
        DetailBoardSerial.Text = !string.IsNullOrEmpty(report.Motherboard.SerialNumber) ? report.Motherboard.SerialNumber : "Non disponible";
        DetailBiosMfg.Text = bios.Manufacturer;
        DetailBiosVersion.Text = $"{bios.Version}  ({bios.ReleaseDate})";
        DetailBiosMode.Text = bios.Mode;
        DetailTpm.Text = bios.TpmReady ? $"Activé et prêt · Version {bios.TpmVersion?.Trim('\0')}"
            : bios.TpmPresent ? "Présent (non configuré)" : "Absent";
        DetailSecureBoot.Text = bios.SecureBootEnabled ? "Activé" : "Désactivé";

        // ── EXPANDER : OS DETAILS ────────────────────────────────────────────

        DetailOsName.Text = report.OS.Edition ?? report.OS.Name ?? "Inconnu";
        DetailOsVersion.Text = $"{report.OS.Version}  (build {report.OS.Build})";
        DetailOsArch.Text = report.OS.Arch ?? "Inconnu";
        DetailOsInstall.Text = report.OS.InstallDate;
        DetailOsBoot.Text = report.OS.LastBootTime;
        DetailOsUptime.Text = report.OS.Uptime;
        DetailOsTimezone.Text = report.OS.TimeZone;
        DetailOsComputer.Text = report.OS.ComputerName;
        DetailOsDomain.Text = string.IsNullOrEmpty(report.OS.Domain) ? "WORKGROUP" : report.OS.Domain;
        DetailOsUser.Text = report.OS.CurrentUser;

        // ── EXPANDER : SÉCURITÉ DÉTAILS + MAJ ────────────────────────────────

        DetailDefEnabled.Text = sec.DefenderEnabled ? "Oui" : "Non";
        DetailDefEnabled.Foreground = sec.DefenderEnabled ? OkBrush : AlertBrush;
        DetailDefRealtime.Text = sec.RealtimeProtection ? "Active" : "Inactive";
        DetailDefRealtime.Foreground = sec.RealtimeProtection ? OkBrush : AlertBrush;
        DetailDefUpToDate.Text = sec.AntivirusUpToDate ? "À jour" : "Obsolètes, mise à jour recommandée";
        DetailDefUpToDate.Foreground = sec.AntivirusUpToDate ? OkBrush : WarnBrush;
        DetailDefEngine.Text = sec.DefenderEngineVersion;
        DetailDefSigs.Text = sec.SignaturesLastUpdated;
        DetailDefFullScan.Text = string.IsNullOrEmpty(sec.LastFullScan) ? "Jamais réalisée" : sec.LastFullScan;
        DetailDefQuickScan.Text = string.IsNullOrEmpty(sec.LastQuickScan) ? "Jamais réalisée" : sec.LastQuickScan;
        DetailUac.Text = sec.UacEnabled ? "Activé" : "Désactivé";
        DetailUac.Foreground = sec.UacEnabled ? OkBrush : AlertBrush;
        DetailThirdPartyAv.Text = string.IsNullOrEmpty(sec.ThirdPartyAntivirus) ? "Aucun détecté" : sec.ThirdPartyAntivirus;

        PanelUpdates.Children.Clear();
        if (report.RecentUpdates != null && report.RecentUpdates.Count > 0)
        {
            bool firstUpd = true;
            foreach (var u in report.RecentUpdates)
            {
                if (!firstUpd) PanelUpdates.Children.Add(new MenuFlyoutSeparator());
                firstUpd = false;

                var updRow = new Grid { Padding = new Thickness(0, 6, 0, 6) };
                updRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                updRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                updRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var kbTb = new TextBlock { Text = u.KbArticle, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], VerticalAlignment = VerticalAlignment.Center };
                var titleTb = new TextBlock { Text = u.Title, FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], VerticalAlignment = VerticalAlignment.Center };
                var dateTb = new TextBlock { Text = u.InstalledOn, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], VerticalAlignment = VerticalAlignment.Center };

                Grid.SetColumn(kbTb, 0); updRow.Children.Add(kbTb);
                Grid.SetColumn(titleTb, 1); updRow.Children.Add(titleTb);
                Grid.SetColumn(dateTb, 2); updRow.Children.Add(dateTb);
                PanelUpdates.Children.Add(updRow);
            }
        }
        else PanelUpdates.Children.Add(new TextBlock { Text = "Aucune mise à jour récente trouvée.", FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

        // ── EXPANDER : MAINTENANCE ──────────────────────────────────────────

        var mh = report.Maintenance;
        if (mh != null)
        {
            TxtMaintDate.Text = string.IsNullOrEmpty(mh.DerniereMaintenance) ? "Jamais réalisée" : mh.DerniereMaintenance;
            TxtMaintDate.Foreground = string.IsNullOrEmpty(mh.DerniereMaintenance) ? WarnBrush
                : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

            TxtMaintClean.Text = mh.OctetsLiberes > 0 ? NettoyageService.FormatTaille(mh.OctetsLiberes) : "—";
            TxtMaintCleanDetail.Text = mh.OctetsLiberes > 0
                ? $"{mh.FichiersSupprimes} fichier(s) supprimé(s)" : "Aucun nettoyage enregistré";

            TxtMaintRepair.Text = (mh.ReparationSfc || mh.ReparationDism) ? "Effectuée" : "Non réalisée";
            TxtMaintRepairDetail.Text = $"{(mh.ReparationSfc ? "SFC  " : "")}{(mh.ReparationDism ? "DISM" : "")}".Trim();

            PanelMaintOps.Children.Clear();
            if (mh.Operations != null && mh.Operations.Count > 0)
            {
                TxtMaintOpsTitle.Visibility = Visibility.Visible;
                bool first = true;
                foreach (var op in mh.Operations)
                {
                    if (!first) PanelMaintOps.Children.Add(new MenuFlyoutSeparator());
                    first = false;
                    PanelMaintOps.Children.Add(new TextBlock
                    {
                        Text = op,
                        FontSize = 12,
                        Padding = new Thickness(0, 6, 0, 6),
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    });
                }
            }
            else
            {
                TxtMaintOpsTitle.Visibility = Visibility.Visible;
                PanelMaintOps.Children.Add(new TextBlock
                {
                    Text = "Aucune opération enregistrée dans cette session. Lancez une maintenance depuis le tableau de bord.",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
                });
            }
        }

        // ── EXPANDER : RÉSEAU ────────────────────────────────────────────────

        PanelNetwork.Children.Clear();
        if (report.Network != null && report.Network.Count > 0)
        {
            bool first = true;
            foreach (var net in report.Network)
            {
                if (!first) PanelNetwork.Children.Add(new MenuFlyoutSeparator());
                first = false;

                var row = new StackPanel { Padding = new Thickness(16, 10, 16, 10), Spacing = 3 };

                string netTitle = net.Name;
                if (!string.IsNullOrEmpty(net.Ssid)) netTitle += $" : {net.Ssid}";
                row.Children.Add(new TextBlock { Text = netTitle, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"] });
                row.Children.Add(new TextBlock { Text = net.Description, FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

                string netDetails = $"IP : {net.IpAddress}";
                if (!string.IsNullOrEmpty(net.Gateway)) netDetails += $"  ·  Passerelle : {net.Gateway}";
                if (!string.IsNullOrEmpty(net.DnsServers)) netDetails += $"  ·  DNS : {net.DnsServers}";
                row.Children.Add(new TextBlock { Text = netDetails, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });

                string netInfo = $"MAC : {net.MacAddress}  ·  Débit : {net.LinkSpeed}  ·  {net.ConnectionType}";
                row.Children.Add(new TextBlock { Text = netInfo, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });

                PanelNetwork.Children.Add(row);
            }
        }
        else PanelNetwork.Children.Add(new TextBlock { Text = "Aucun adaptateur réseau actif.", Margin = new Thickness(16), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

        // ── EXPANDER : BATTERIE ──────────────────────────────────────────────

        if (report.Battery != null && !string.IsNullOrEmpty(report.Battery.Name))
        {
            ExpanderBattery.Visibility = Visibility.Visible;
            var bat = report.Battery;
            DetailBatHealth.Text = $"{bat.HealthPercent} %";
            DetailBatHealth.Foreground = bat.HealthPercent >= 80 ? OkBrush : bat.HealthPercent >= 60 ? WarnBrush : AlertBrush;
            DetailBatDesign.Text = $"{bat.DesignCapacityMwh:N0} mWh";
            DetailBatFull.Text = $"{bat.FullChargeCapacityMwh:N0} mWh";
            DetailBatCycles.Text = bat.CycleCount > 0 ? bat.CycleCount.ToString() : "Non disponible";
            DetailBatStatus.Text = bat.ChargeStatus;
            DetailBatRuntime.Text = bat.EstimatedRuntimeMin > 0 && bat.EstimatedRuntimeMin < 1000 ? $"{bat.EstimatedRuntimeMin} min" : "—";
        }

        // ── EXPANDER : APPLICATIONS ──────────────────────────────────────────

        PanelApps.Children.Clear();
        if (report.InstalledApps != null && report.InstalledApps.Count > 0)
        {
            TxtAppsCount.Text = $"{report.InstalledApps.Count} applications détectées";
            bool first = true;
            foreach (var app in report.InstalledApps)
            {
                if (!first) PanelApps.Children.Add(new MenuFlyoutSeparator());
                first = false;

                var row = new Grid { Padding = new Thickness(16, 8, 16, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

                var nameStack = new StackPanel { Spacing = 1 };
                nameStack.Children.Add(new TextBlock { Text = app.Name, Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
                if (!string.IsNullOrEmpty(app.Publisher))
                    nameStack.Children.Add(new TextBlock { Text = app.Publisher, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
                Grid.SetColumn(nameStack, 0); row.Children.Add(nameStack);

                var verTb = new TextBlock { Text = app.Version, FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(verTb, 1); row.Children.Add(verTb);

                var dateTb = new TextBlock { Text = app.InstallDate, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(dateTb, 2); row.Children.Add(dateTb);

                PanelApps.Children.Add(row);
            }
        }
        else
        {
            TxtAppsCount.Text = "";
            PanelApps.Children.Add(new TextBlock { Text = "Aucune application détectée.", Margin = new Thickness(16), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        }
    }

    private async void BtnExportPdf_Click(object sender, RoutedEventArgs e)
    {
        if (_report == null) return;

        try
        {
            BtnExportPdf.IsEnabled = false;

            string dossierRapports = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Assistools", "Rapports");

            string nomFichier = $"Rapport_Systeme_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.html";
            string cheminFichier = Path.Combine(dossierRapports, nomFichier);

            await TaskManager.RunAsync("Export du rapport système", (log, ct) =>
            {
                log("Préparation du dossier d'export…");
                Directory.CreateDirectory(dossierRapports);
                log($"Dossier : {dossierRapports}");

                log("Génération du document HTML…");
                string html = RapportService.GenererHtml(_report);
                File.WriteAllText(cheminFichier, html, System.Text.Encoding.UTF8);
                log($"[OK] Rapport enregistré : {nomFichier}");

                log("Ouverture du rapport dans le navigateur…");
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = cheminFichier,
                        UseShellExecute = true,
                    });
                    log("[OK] Rapport ouvert dans le navigateur par défaut.");
                    log("Astuce technicien : Ctrl+P puis « Enregistrer au format PDF » pour générer un PDF.");
                }
                catch (Exception exOpen) { log($"[AVERTISSEMENT] Impossible d'ouvrir le rapport : {exOpen.Message}"); }

                log("Ouverture du dossier dans l'Explorateur…");
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{cheminFichier}\"",
                        UseShellExecute = true,
                    });
                }
                catch (Exception exExp) { log($"[AVERTISSEMENT] Impossible d'ouvrir l'Explorateur : {exExp.Message}"); }
            });
        }
        catch (Exception ex)
        {
            LogStore.Append($"[ERREUR export rapport] {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            BtnExportPdf.IsEnabled = true;
        }
    }
}
