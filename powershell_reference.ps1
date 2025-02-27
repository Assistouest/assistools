# ==============================================================================
# Référence pour l'utilisation de PowerShell dans ce projet Rust
# ==============================================================================
#
# GitHub Linguist analyse automatiquement les langages présents dans un dépôt
# pour afficher les statistiques de répartition. Dans ce projet, les scripts PowerShell
# sont directement imbriqués dans le code Rust, ce qui empêche leur détection automatique.
#
# Ce fichier a été ajouté pour :
# - Rendre explicite l'utilisation de PowerShell dans le projet.
# - Permettre à GitHub de reconnaître PowerShell comme langage utilisé.
# - Servir de documentation sur l'emploi des scripts PowerShell ici.
#
# 🔹 Comment PowerShell est-il utilisé dans ce projet ?
# - Exécution dynamique via Rust, par exemple en générant et en lançant des commandes
#   PowerShell depuis le code Rust.
# - Automatisation de tâches telles que les mises à jour, la configuration système
#   ou l'exécution de scripts administratifs sous Windows.
#
# Ce fichier n'est pas destiné à être exécuté directement ; il sert uniquement à
# assurer la reconnaissance de PowerShell par GitHub Linguist et à informer les développeurs.
#
# ==============================================================================
Write-Host "Ce script sert uniquement de référence pour la détection de PowerShell dans ce projet Rust."


# ------------------------------------------------------------------------------
# 1. Dans la fonction optimize_hdd()
# ------------------------------------------------------------------------------
try {
    $drives = Get-WmiObject -Class Win32_Volume | Where-Object { $_.DriveType -eq 3 -and $_.FileSystem -eq 'NTFS' }
    foreach ($drive in $drives) {
        $disk = Get-PhysicalDisk | Where-Object { $_.DeviceID -eq $drive.DeviceID }
        if ($disk.MediaType -eq 'HDD') {
            Write-Host "Défragmentation de la partition $($drive.DriveLetter)..."
            Optimize-Volume -DriveLetter $drive.DriveLetter -Defrag -Verbose
        } else {
            Write-Host "Le disque $($drive.DriveLetter) n'est pas un HDD, défragmentation ignorée."
        }
    }
} catch {
    Write-Output "Erreur lors de la défragmentation des disques : $_"
}


# ------------------------------------------------------------------------------
# 2. Dans la fonction ajuster_mem_virtuelle()
# ------------------------------------------------------------------------------
# a. Script pour obtenir la RAM
(Get-WmiObject -Class Win32_ComputerSystem).TotalPhysicalMemory / 1MB

# b. Script pour ajuster la mémoire virtuelle
# Ce script est construit via format! en Rust (les valeurs {initial_size} et {max_size}
# seront remplacées lors de l'exécution).
$reg_path = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'
Set-ItemProperty -Path $reg_path -Name 'PagingFiles' -Value 'C:\pagefile.sys {initial_size} {max_size}'
$automatic_managed = Get-ItemProperty -Path $reg_path -Name 'PagingFiles'
if ($automatic_managed -ne 'C:\pagefile.sys {initial_size} {max_size}') {
    Set-ItemProperty -Path $reg_path -Name 'PagingFiles' -Value 'C:\pagefile.sys {initial_size} {max_size}'
}
$paging_file_value = 'C:\pagefile.sys ' + {initial_size} + ' ' + {max_size}
Set-ItemProperty -Path $reg_path -Name 'PagingFiles' -Value $paging_file_value
Write-Output 'Mémoire virtuelle ajustée avec succès.'

# c. Script pour activer le vidage du fichier d'échange au shutdown
$reg_path_shutdown = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'
Set-ItemProperty -Path $reg_path_shutdown -Name 'ClearPageFileAtShutdown' -Value 1
Write-Output 'ClearPageFileAtShutdown activé.'


# ------------------------------------------------------------------------------
# 3. Dans la fonction optimize_ssd()
# ------------------------------------------------------------------------------
try {
    fsutil behavior set DisableDeleteNotify 0
    fsutil behavior set disablelastaccess 1
    fsutil behavior set disable8dot3 1

    $ssdDisks = Get-PhysicalDisk | Where-Object MediaType -eq 'SSD'
    if ($ssdDisks) {
        foreach ($ssdDisk in $ssdDisks) {
            $partitions = Get-Partition | Where-Object { $_.DiskNumber -eq $ssdDisk.DeviceID }
            foreach ($partition in $partitions) {
                if ($partition.DriveLetter) {
                    Optimize-Volume -DriveLetter $partition.DriveLetter -ReTrim -Verbose
                }
            }
        }
    } else {
        Write-Output "Aucun SSD trouvé."
    }
} catch {
    Write-Output "Erreur lors de l'optimisation des lecteurs : $_"
}


# ------------------------------------------------------------------------------
# 4. Dans la fonction lancer_nettoyage_disque()
# ------------------------------------------------------------------------------
# Ce script regroupe plusieurs blocs qui effectuent divers nettoyages.
try {
    Write-Output "Nettoyage des fichiers temporaires..."
    Get-ChildItem -Path "$env:TEMP" -Recurse -Verbose | Remove-Item -Force -Recurse -Verbose -ErrorAction Stop
} catch {
    Write-Output "Erreur lors du nettoyage des fichiers temporaires: $_"
}
try {
    Write-Output "Nettoyage des fichiers Internet temporaires..."
    $inetCache = "$env:LOCALAPPDATA\Microsoft\Windows\INetCache\*"
    Get-ChildItem -Path $inetCache -Recurse -Verbose | Remove-Item -Force -Recurse -Verbose -ErrorAction Stop
} catch {
    Write-Output "Erreur lors du nettoyage des fichiers Internet temporaires: $_"
}
try {
    Write-Output "Vidage de la corbeille..."
    Clear-RecycleBin -Force -Confirm:$false 
} catch {
    Write-Output "Erreur lors du vidage de la corbeille: $_"
}
try {
    Write-Output "Suppression des anciens points de restauration système..."
    $command = "vssadmin Delete Shadows /For=C: /All /Quiet"
    Write-Output "Exécution de la commande: $command"
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c $command" -NoNewWindow -Wait -Verbose
    Write-Output "Suppression des anciens points de restauration terminée."
} catch {
    Write-Output "Erreur lors de la suppression des points de restauration: $_"
}
try {
    Write-Output "Nettoyage des fichiers de préfetch..."
    $prefetchPath = "$env:windir\Prefetch\*"
    Get-ChildItem -Path $prefetchPath -Recurse -Verbose | Remove-Item -Force -Recurse -Verbose -ErrorAction Stop
} catch {
    Write-Output "Erreur lors du nettoyage des fichiers de préfetch: $_"
}
try {
    Write-Output "Suppression des anciens profils utilisateur..."
    Get-WmiObject -Class Win32_UserProfile | Where-Object { !$_.Special -and $_.LastUseTime -lt (Get-Date).AddDays(-180) } | Remove-WmiObject -Verbose
} catch {
    Write-Output "Erreur lors de la suppression des profils utilisateur: $_"
}
try {
    Write-Output "Nettoyage du cache du système de notification..."
    $notifyCache = "$env:LOCALAPPDATA\Microsoft\Windows\Notifications\*"
    Get-ChildItem -Path $notifyCache -Recurse -Verbose | Remove-Item -Force -Recurse -Verbose -ErrorAction Stop
} catch {
    Write-Output "Erreur lors du nettoyage du cache des notifications: $_"
}
try {
    Write-Output "Nettoyage des fichiers de cache de Windows Defender..."
    $defenderCache = "$env:ProgramData\Microsoft\Windows Defender\Scans\History\Service\*"
    Get-ChildItem -Path $defenderCache -Recurse -Verbose | Remove-Item -Force -Recurse -Verbose -ErrorAction Stop
} catch {
    Write-Output "Erreur lors du nettoyage du cache de Windows Defender: $_"
}
try {
    Write-Output "Suppression des fichiers temporaires de l'installation de Windows..."
    $windowsTempInstall = "$env:windir\Temp\*"
    Get-ChildItem -Path $windowsTempInstall -Recurse -Verbose | Remove-Item -Force -Recurse -Verbose -ErrorAction Stop
} catch {
    Write-Output "Erreur lors de la suppression des fichiers temporaires d'installation: $_"
}
try {
    Write-Output "Nettoyage des fichiers temporaires d'installation d'applications..."
    $appTempFiles = "$env:LOCALAPPDATA\Temp\*"
    Get-ChildItem -Path $appTempFiles -Recurse -Verbose | Remove-Item -Force -Recurse -Verbose -ErrorAction Stop
} catch {
    Write-Output "Erreur lors du nettoyage des fichiers temporaires d'applications: $_"
}
try {
    Write-Output "Nettoyage des fichiers de mise à jour de Windows..."
    $windowsUpdate = "$env:windir\SoftwareDistribution\Download\*"
    Get-ChildItem -Path $windowsUpdate -Recurse -Verbose | Remove-Item -Force -Recurse -Verbose -ErrorAction Stop
} catch {
    Write-Output "Erreur lors du nettoyage des fichiers de mise à jour: $_"
}
try {
    Write-Output "Vidage du cache des vignettes..."
    $thumbCache = "$env:LOCALAPPDATA\Microsoft\Windows\Explorer\ThumbCache_*.db"
    Get-ChildItem -Path $thumbCache -Force -Verbose | Remove-Item -Force -Verbose -ErrorAction Stop
} catch {
    Write-Output "Erreur lors du vidage du cache des vignettes: $_"
}
Write-Output "Nettoyage du disque terminé avec succès."


# ------------------------------------------------------------------------------
# 5. Dans la fonction lancer_mise_a_jour_windows()
# ------------------------------------------------------------------------------
# Sauvegarder la politique d'exécution actuelle
$originalExecutionPolicy = Get-ExecutionPolicy
Set-ExecutionPolicy Bypass -Scope Process -Force

if (-not (Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue)) {
    Write-Output "NuGet n'est pas installé. Installation en cours..."
    Install-PackageProvider -Name NuGet -ForceBootstrap -Force -Scope CurrentUser
}

Write-Output "Installation du module PSWindowsUpdate..."
Install-Module -Name PSWindowsUpdate -Force -AllowClobber -Scope CurrentUser
Import-Module PSWindowsUpdate

Write-Output "Recherche des mises à jour disponibles..."
$updates = Get-WindowsUpdate
if ($updates) {
    Write-Output "Des mises à jour sont disponibles. Installation en cours..."
    Install-WindowsUpdate -AcceptAll -ForceInstall -AutoReboot
} else {
    Write-Output "Aucune mise à jour disponible via PSWindowsUpdate."
}

# Exécution de usoclient pour lancer une autre méthode de mise à jour
Write-Output "Exécution de usoclient ScanInstallWait..."
Start-Process -FilePath "$env:windir\system32\usoclient.exe" -ArgumentList "ScanInstallWait" -NoNewWindow -Wait

Set-ExecutionPolicy $originalExecutionPolicy -Scope Process -Force
Write-Output "Politique d'exécution restaurée à : $originalExecutionPolicy"
