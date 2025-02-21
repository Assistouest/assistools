#![windows_subsystem = "windows"]  // Empêche l'affichage de la fenêtre de commande


use eframe::egui::{self, CentralPanel, Context, SidePanel}; // Ajout de TextEdit ici

use std::process::Command;
use std::{thread, time::Duration};
use std::sync::{Arc, Mutex};
use std::process::Command as ProcessCommand;
use std::str;
use std::process::Stdio;
use std::path::Path;
use encoding_rs::WINDOWS_1252;
use eframe::IconData;
use image::GenericImageView;






fn main() {
    // Chargez votre icône en tant qu'image PNG
    let icon = match image::open("app.png") {
        Ok(img) => {
            let rgba = img.to_rgba8();
            let (width, height) = img.dimensions();
            Some(IconData {
                rgba: rgba.into_raw(),
                width,
                height,
            })
        }
        Err(e) => {
            eprintln!("Erreur lors du chargement de l'icône PNG : {:?}", e);
            None
        }
    };

    // Configuration des options de la fenêtre de l'application
    let options = eframe::NativeOptions {
        resizable: false,
        initial_window_size: Some(egui::vec2(800.0, 600.0)),
        icon_data: icon, // Appliquez l'icône ici
        ..Default::default()
    };

    // Démarrage de l'application avec gestion des erreurs
    if let Err(e) = eframe::run_native(
        "Assistools",
        options,
        Box::new(|_| Box::new(AppState::default())),
    ) {
        eprintln!("Échec du démarrage de l'application : {:?}", e);
    }
}

// Cette structure gère l'état global de l'application
struct AppState {
    main_app: ApplicationOptimisation, // Interface principale de l'application
}

impl Default for AppState {
    fn default() -> Self {
        Self {
            main_app: ApplicationOptimisation::default(),
        }
    }
}

impl eframe::App for AppState {
    fn update(&mut self, ctx: &Context, frame: &mut eframe::Frame) {
        // Afficher l'interface principale
        self.main_app.update(ctx, frame);
    }
}


 

// Structure pour gérer l'application principale
struct ApplicationOptimisation {
    onglet_selectionne: usize,
    en_execution: Arc<Mutex<bool>>,
    message_execution: Arc<Mutex<String>>,
}

impl Default for ApplicationOptimisation {
    fn default() -> Self {
        Self {
            onglet_selectionne: 0,
            en_execution: Arc::new(Mutex::new(false)),
            message_execution: Arc::new(Mutex::new(String::new())),
        }
    }
}


impl eframe::App for ApplicationOptimisation {
    fn update(&mut self, ctx: &Context, _frame: &mut eframe::Frame) {
        // Panneau latéral gauche avec boutons pour changer d'onglet
SidePanel::left("panneau_lateral")
    .resizable(false)
    .min_width(60.0)
    .max_width(60.0)
    .show(ctx, |ui| {
        ui.vertical_centered(|ui| {
            ui.add_space(20.0);

            // Bouton "Mode énergie" avec icône grande et texte petit
            if ui.add(egui::Button::new(
               egui::RichText::new("\u{1F504}").size(32.0).strong() // Icône grande

            ).frame(false)).clicked() {
                self.onglet_selectionne = 0;
            }
            ui.label(egui::RichText::new("Mode auto").size(12.0)); // Texte petit
            
            ui.add_space(50.0);

            // Bouton "Outils de maintenance" avec icône grande et texte petit
            if ui.add(egui::Button::new(
                egui::RichText::new("\u{1F527}").size(32.0).strong() // Icône grande
            ).frame(false)).clicked() {
                self.onglet_selectionne = 1;
            }
            ui.label(egui::RichText::new("Outils avancés").size(12.0)); // Texte petit

            ui.add_space(50.0);

            // Bouton "Booster" avec icône grande et texte petit
            if ui.add(egui::Button::new(
              egui::RichText::new("\u{23E9}").size(32.0).strong() // Icône grande

            ).frame(false)).clicked() {
                self.onglet_selectionne = 2;
            }
            ui.label(egui::RichText::new("Booster").size(12.0)); // Texte petit
            
            ui.add_space(50.0);

            // Bouton "Informations" avec icône grande et texte petit
            if ui.add(egui::Button::new(
                egui::RichText::new("\u{2139}").size(32.0).strong() // Icône grande
            ).frame(false)).clicked() {
                self.onglet_selectionne = 3; // Onglet "Information"
            }
            ui.label(egui::RichText::new("Infos").size(12.0)); // Texte petit
        });
    });


        // Panneau central avec contenu en fonction de l'onglet sélectionné
        CentralPanel::default().show(ctx, |ui| {
            ui.vertical_centered(|ui| {
                match self.onglet_selectionne {
                   0 => {
    // Spinner et gestion des tâches pour le premier onglet
    self.afficher_bouton_tout_en_un(ui);

    if *self.en_execution.lock().unwrap() {
        // Si une tâche est en cours d'exécution, afficher le spinner et le message
        ui.add(egui::Spinner::default().size(30.0));
        let message = self.message_execution.lock().unwrap();
        ui.add_space(20.0);
        ui.label(message.to_string());
    } else {
        // Si aucune tâche n'est en cours, afficher un texte incitatif sous le bouton tout en un
        ui.add_space(10.0);
        ui.label("Lancez l'exécution de toutes vos tâches de maintenance d'un simple clic.");
    }
},

                    1 => {
                        // Pas de spinner pour le second onglet
                        self.afficher_boutons_individuels(ui);
                    },
     2 => {
    // Contenu du troisième onglet
    ui.add_space(20.0);
    ui.label(egui::RichText::new("Booster les performances").size(24.0));
    ui.add_space(10.0);
    ui.label("Configurer votre système afin qu'il soit adapté à votre ordinateur.");
    ui.add_space(20.0);

    // Bouton pour activer le mode performance d'alimentation
    ui.with_layout(egui::Layout::left_to_right(egui::Align::Min), |ui| {
        if ui.button(egui::RichText::new("Activer le mode performance d'alimentation").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan(
                "Optimisation des performances...".to_string(),
                optimiser_performances_energie,
            );
        }
    });

     // Ajouter un espace avant les boutons "Mémoire virtuelle"
    ui.add_space(20.0);

    // Aligner les boutons de mémoire virtuelle côte à côte
    ui.with_layout(egui::Layout::left_to_right(egui::Align::Min), |ui| {
        if ui.button(egui::RichText::new("Trimmer un SSD").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan(
                "Trim en cours...".to_string(),
                optimize_ssd,
            );
        }

        ui.add_space(10.0); // Espacement entre les deux boutons

        if ui.button(egui::RichText::new("Defragmenter un HDD").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan(
                "Defragmentation en cours...".to_string(),
                                optimize_hdd,

            );
        }
    });





  

    // Ajouter un espace avant les boutons "Mémoire virtuelle"
    ui.add_space(20.0);

    // Aligner les boutons de mémoire virtuelle côte à côte
    ui.with_layout(egui::Layout::left_to_right(egui::Align::Min), |ui| {
        if ui.button(egui::RichText::new("Ajuster la mémoire virtuelle").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan(
                "Ajustement de la mémoire virtuelle...".to_string(),
                ajuster_mem_virtuelle,
            );
        }

        ui.add_space(10.0); // Espacement entre les deux boutons

        if ui.button(egui::RichText::new("Mémoire virtuelle par défaut").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan(
                "Ajustement de la mémoire virtuelle...".to_string(),
                mem_virtuelle_par_default,
            );
        }
    });

    ui.add_space(20.0); // Espacement entre les groupes de boutons

    // Boutons pour activer et désactiver le mode jeu côte à côte
    ui.horizontal(|ui| {
        if ui.button(egui::RichText::new("Activer le mode jeu").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan(
                "Activation du mode jeu...".to_string(),
                activer_mode_jeu,
            );
        }

        ui.add_space(20.0); // Espacement entre les deux boutons

        if ui.button(egui::RichText::new("Désactiver le mode jeu").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan(
                "Désactivation du mode jeu...".to_string(),
                desactiver_mode_jeu,
            );
        }
    });
}



  

                 3 => {
    // Contenu du quatrième onglet "Information"
    
    
    ui.add_space(20.0);
    
    // Première section : Présentation de l'outil sans titre de description
    ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
        // Contenu détaillé et professionnel, bien aligné à gauche
        ui.label("Assistools est un utilitaire avancé conçu pour optimiser et réparer les systèmes Windows (7, 8, 10, 11) en un seul clic.");
        ui.add_space(5.0);
        ui.label("Avec une approche automatisée, il offre des fonctionnalités puissantes pour la maintenance de votre PC. Il simplifie les tâches complexes de votre système.");
    });
    
    ui.add_space(20.0);
    
    // Séparateur pour une meilleure lisibilité
    ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
        ui.add(egui::Separator::default().spacing(20.0));
    });
    
    // Deuxième section : Fonctionnalités clés, sans bordure et bien alignées à gauche
    ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
        ui.label(egui::RichText::new("Fonctionnalités")
            .size(22.0)
            .strong() // Texte en gras
        );
        ui.add_space(10.0);

        // Liste des fonctionnalités détaillées, alignées à gauche
        ui.label("• Réparation des systèmes Windows 7, 8, 10 et 11 en un seul clic.");
        ui.add_space(5.0);
        ui.label("• Nettoyage automatisé des fichiers inutiles pour libérer de l'espace disque.");
        ui.add_space(5.0);
        ui.label("• Maintenance en un clic avec une automatisation complète du processus de nettoyage.");
        ui.add_space(5.0);
        ui.label("• Réinstallation des fichiers système critiques via Windows Update.");
        ui.add_space(5.0);
        ui.label("• Mise à jour de Windows Defender suivie d'une analyse rapide des menaces.");
        ui.add_space(5.0);
        ui.label("• Gestion proactive de la sécurité et des performances via un processus optimisé.");
    });
    
    ui.add_space(20.0);
    
    // Séparateur pour une meilleure lisibilité
    ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
        ui.add(egui::Separator::default().spacing(20.0));
    });
    
    // Troisième section : Informations sur l'éditeur, sans bordure et bien alignées à gauche
    ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
        ui.label(egui::RichText::new("Informations sur l'éditeur")
            .size(22.0)
            .strong() // Texte en gras
        );
        ui.add_space(10.0);
        
        // Informations sur l'éditeur, alignées à gauche
        ui.label("Éditeur : Assistouest Informatique");
        ui.add_space(5.0);
        ui.label("Numéro de build : 1.0.0");
        ui.add_space(5.0);
        ui.label("Support technique : support@assistouest.fr");
            ui.add_space(5.0);

        // Lien vers le site web de l'éditeur
        ui.hyperlink("https://assistouest.fr").on_hover_text("Visitez notre site web");

    });
}

                    _ => (),
                }
            });

            // Supprimez cette ligne pour retirer le message d'administration
            // self.afficher_message_admin(ui);
        });

        // Requête de rafraîchissement continu si une tâche est en cours d'exécution
        if *self.en_execution.lock().unwrap() {
            ctx.request_repaint();
        }
    }
}


fn optimize_hdd() {
    let powershell_script = r#"
    try {
        # Récupérer la liste des volumes NTFS
        $drives = Get-WmiObject -Class Win32_Volume | Where-Object { $_.DriveType -eq 3 -and $_.FileSystem -eq 'NTFS' }

        foreach ($drive in $drives) {
            # Récupérer les informations sur le disque physique associé
            $disk = Get-PhysicalDisk | Where-Object { $_.DeviceID -eq $drive.DeviceID }

            # Vérifier si le disque est un HDD
            if ($disk.MediaType -eq 'HDD') {
                Write-Host "Défragmentation de la partition $($drive.DriveLetter)..."
                # Lancer la défragmentation
                Optimize-Volume -DriveLetter $drive.DriveLetter -Defrag -Verbose
            } else {
                Write-Host "Le disque $($drive.DriveLetter) n'est pas un HDD, défragmentation ignorée."
            }
        }
    } catch {
        Write-Output "Erreur lors de la défragmentation des disques : $_"
    }
    "#;

    let output = Command::new("powershell")
        .arg("-Command")
        .arg(powershell_script)
        .output();

    match output {
        Ok(output) => {
            // Essayer d'abord de décoder en UTF-8
            if let Ok(stdout) = str::from_utf8(&output.stdout) {
                println!("{}", stdout);
            } else {
                // Si UTF-8 échoue, essayer avec Windows-1252
                let (stdout, _, _) = WINDOWS_1252.decode(&output.stdout);
                println!("{}", stdout);
            }

            if let Ok(stderr) = str::from_utf8(&output.stderr) {
                eprintln!("{}", stderr);
            } else {
                let (stderr, _, _) = WINDOWS_1252.decode(&output.stderr);
                eprintln!("{}", stderr);
            }
        }
        Err(e) => {
            eprintln!("Erreur lors de l'exécution du script PowerShell : {}", e);
        }
    }
}


fn desactiver_mode_jeu() {
    // Désactiver le mode jeu de Windows
    let desactiver_mode_jeu_script = r#"
    # Désactiver le mode jeu dans Windows (Windows 10 et 11)
    $reg_key_path = "HKCU:\Software\Microsoft\GameBar"
    Set-ItemProperty -Path $reg_key_path -Name "AutoGameModeEnabled" -Value 0
    "#;

    // Exécuter le script PowerShell pour désactiver le mode jeu
    let status = Command::new("powershell")
        .arg("-Command")
        .arg(desactiver_mode_jeu_script)
        .status();

    match status {
        Ok(statut) if statut.success() => println!("Le mode jeu a été désactivé avec succès."),
        Ok(statut) => eprintln!("Erreur lors de la désactivation du mode jeu : Code {:?}", statut.code()),
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell pour désactiver le mode jeu : {:?}", e),
    }
}


fn activer_mode_jeu() {
    // Activer le mode jeu de Windows
    let activer_mode_jeu_script = r#"
    # Activer le mode jeu dans Windows (Windows 10 et 11)
    $reg_key_path = "HKCU:\Software\Microsoft\GameBar"
    Set-ItemProperty -Path $reg_key_path -Name "AutoGameModeEnabled" -Value 1
    "#;

    // Exécuter le script PowerShell pour activer le mode jeu
    let status = Command::new("powershell")
        .arg("-Command")
        .arg(activer_mode_jeu_script)
        .status();

    match status {
        Ok(statut) if statut.success() => println!("Le mode jeu a été activé avec succès."),
        Ok(statut) => eprintln!("Erreur lors de l'activation du mode jeu : Code {:?}", statut.code()),
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell pour activer le mode jeu : {:?}", e),
    }

    // Liste des processus à fermer pendant le mode jeu (sans extension .exe pour une meilleure détection)
    let processus_a_fermer = vec![
    
    "SearchUI",        // Cortana (assistant vocal)
    "msedge",          // Microsoft Edge
    "chrome",          // Google Chrome
    "ccleaner",        // CCleaner (outil de nettoyage)
    "Dropbox",         // Dropbox client (cloud)
    "googledrivesync", // Google Drive client (cloud)
    "AdobeARM",        // Adobe Updater (mise à jour d'Adobe)
    "Teams",           // Microsoft Teams (collaboration professionnelle)
    "Slack",           // Slack (messagerie professionnelle)
    "Zoom",            // Zoom (vidéoconférence)
    "Outlook",         // Microsoft Outlook (email professionnel)
    "Thunderbird",     // Mozilla Thunderbird (email)
    "MicrosoftWord",   // Microsoft Word (traitement de texte)
    "MicrosoftExcel",  // Microsoft Excel (tableurs)
    "MicrosoftPowerPoint", // Microsoft PowerPoint (présentations)
    "Notepad++",       // Notepad++ (éditeur de texte)
    "AnyDesk",         // AnyDesk (contrôle à distance)
    "TeamViewer",      // TeamViewer (contrôle à distance)
    "BitTorrent",      // BitTorrent (client torrent)
    "uTorrent",        // uTorrent (client torrent)
    "DropboxUpdate",   // Dropbox Updater
    "GoogleUpdate",    // Google Updater
    "WacomHost",       // Logiciel pour tablettes graphiques Wacom
    "AutodeskDesktopApp", // Autodesk Desktop App (mise à jour AutoCAD, etc.)
    "IntelDriverSupport", // Support de mise à jour des pilotes Intel
    "JavaUpdate",      // Mise à jour Java
    ];

    for processus in processus_a_fermer {
        let verifier_processus_script = format!(
            r#"
            # Vérifier si le processus {} est en cours d'exécution
            Get-Process -Name {} -ErrorAction SilentlyContinue
            "#,
            processus, processus
        );

        // Vérifier si le processus existe avant de le fermer
        let verification = Command::new("powershell")
            .arg("-Command")
            .arg(verifier_processus_script)
            .output();

        if let Ok(output) = verification {
            if !output.stdout.is_empty() {
                // Si le processus existe, le fermer
                let kill_process_script = format!(
                    r#"
                    # Fermer le processus {}
                    Stop-Process -Name {} -Force
                    "#,
                    processus, processus
                );

                let status = Command::new("powershell")
                    .arg("-Command")
                    .arg(kill_process_script)
                    .status();

                match status {
                    Ok(statut) if statut.success() => println!("Processus {} fermé avec succès.", processus),
                    Ok(statut) => eprintln!("Erreur lors de la fermeture du processus {} : Code {:?}", processus, statut.code()),
                    Err(e) => eprintln!("Échec de l'exécution du script PowerShell pour fermer le processus {} : {:?}", processus, e),
                }
            } else {
                println!("Processus {} non trouvé, pas besoin de fermer.", processus);
            }
        }
    }

    // Liste des services à arrêter pendant le mode jeu
    let services_a_arreter = vec![
          "OneSyncSvc",
    "spooler", // Service d'impression
    "wuauserv", // Windows Update
    "fdPHost", // Service de partage réseau et d'accès aux dossiers
    "WSearch", // Service de recherche Windows
    "DiagTrack", // Services de diagnostic (Connected User Experiences and Telemetry)

    "TabletInputService", // Service d'entrée de tablette
    "WerSvc", // Windows Error Reporting Service
    "RemoteRegistry", // Remote Registry
    "PrintWorkflowUserSvc", // Service de gestion des travaux d'impression
    ];

    for service in services_a_arreter {
        let stop_service_script = format!(
            r#"
            # Arrêter le service {}
            Stop-Service -Name {} -Force
            "#,
            service, service
        );

        // Exécuter le script PowerShell pour arrêter chaque service
        let status = Command::new("powershell")
            .arg("-Command")
            .arg(stop_service_script)
            .status();

        match status {
            Ok(statut) if statut.success() => println!("Service {} arrêté avec succès.", service),
            Ok(statut) => eprintln!("Erreur lors de l'arrêt du service {} : Code {:?}", service, statut.code()),
            Err(e) => eprintln!("Échec de l'exécution du script PowerShell pour arrêter le service {} : {:?}", service, e),
        }
    }

    println!("Mode jeu activé, services et processus inutiles désactivés.");
}


fn ajuster_mem_virtuelle() {
    // Récupérer la quantité de RAM installée en Mo via PowerShell
    let script_powershell_get_ram = r#"
    # Récupérer la quantité de RAM installée en Mo
    (Get-WmiObject -Class Win32_ComputerSystem).TotalPhysicalMemory / 1MB
    "#;

    // Exécuter le script PowerShell pour récupérer la RAM installée
    let output = Command::new("powershell")
        .arg("-Command")
        .arg(script_powershell_get_ram)
        .output()
        .expect("Échec lors de l'exécution de la commande PowerShell");

    // Convertir la sortie en chaîne et la nettoyer
    let output_str = String::from_utf8_lossy(&output.stdout);
    let output_str = output_str.trim().replace(",", "."); // Remplacer la virgule par un point

    // Affichage de la sortie pour le débogage
    println!("RAM détectée (brute): {}", output_str);

    // Conversion en f64 (flottant), gestion des erreurs si conversion échoue
    let ram_mo: f64 = match output_str.parse::<f64>() {
        Ok(valeur) => valeur,
        Err(_) => {
            eprintln!("Échec lors de la conversion de la RAM en flottant.");
            return;
        }
    };

    // Conversion en entier en arrondissant vers le bas
    let ram_mo: u64 = ram_mo.floor() as u64;

    // Calculer la taille initiale et maximale du fichier d'échange (2.5x la RAM pour l'initiale)
    let initial_size = (ram_mo as f64 * 2.5).floor() as u64;
let max_size = (ram_mo as f64 * 3.0).floor() as u64;


    // Affichage des valeurs calculées pour la taille minimale et maximale du fichier d'échange
    println!("Taille minimale du fichier d'échange: {} Mo", initial_size);
    println!("Taille maximale du fichier d'échange: {} Mo", max_size);

    // Créer le script PowerShell pour ajuster la mémoire virtuelle avec les tailles dynamiques
    let script_powershell_adjust_mem = format!(r#"
    # Chemin de la clé de registre pour la mémoire virtuelle
    $reg_path = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'

    # Désactiver la gestion automatique du fichier de pagination
    Set-ItemProperty -Path $reg_path -Name 'PagingFiles' -Value 'C:\\pagefile.sys {initial_size} {max_size}'

    # Assurez-vous que la clé 'PagingFiles' est définie pour une taille personnalisée
    $automatic_managed = Get-ItemProperty -Path $reg_path -Name 'PagingFiles'
    if ($automatic_managed -ne 'C:\\pagefile.sys {initial_size} {max_size}') {{
        Set-ItemProperty -Path $reg_path -Name 'PagingFiles' -Value 'C:\\pagefile.sys {initial_size} {max_size}'
    }}

    # Appliquer les modifications avec les nouvelles tailles
    $paging_file_value = 'C:\\pagefile.sys ' + {initial_size} + ' ' + {max_size}
    
    # Appliquer les modifications
    Set-ItemProperty -Path $reg_path -Name 'PagingFiles' -Value $paging_file_value

    Write-Output 'Mémoire virtuelle ajustée avec succès.'
    "#, initial_size=initial_size, max_size=max_size);

    // Exécuter le script PowerShell pour ajuster la mémoire virtuelle
    let status = Command::new("powershell")
        .arg("-Command")
        .arg(script_powershell_adjust_mem)
        .status();

    // Gérer les résultats de l'exécution du script
    match status {
        Ok(statut) if statut.success() => println!("Mémoire virtuelle ajustée avec succès."),
        Ok(statut) => eprintln!("Erreur lors de l'ajustement de la mémoire virtuelle : Code {:?}", statut.code()),
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell : {:?}", e),
    }

    // Ajout pour vider le fichier d'échange lors de l'arrêt
    let script_powershell_clear_pagefile = r#"
    # Chemin de la clé de registre pour vider le fichier de pagination à l'arrêt
    $reg_path_shutdown = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'

    # Configurer ClearPageFileAtShutdown à 1 pour vider la mémoire virtuelle à l'arrêt
    Set-ItemProperty -Path $reg_path_shutdown -Name 'ClearPageFileAtShutdown' -Value 1

    Write-Output 'ClearPageFileAtShutdown activé.'
    "#;

    // Exécuter le script PowerShell pour activer ClearPageFileAtShutdown
    let status_clear_pagefile = Command::new("powershell")
        .arg("-Command")
        .arg(script_powershell_clear_pagefile)
        .status();

    // Gérer les résultats de l'exécution du script pour vider le fichier de pagination
    match status_clear_pagefile {
        Ok(statut) if statut.success() => println!("ClearPageFileAtShutdown activé avec succès."),
        Ok(statut) => eprintln!("Erreur lors de l'activation de ClearPageFileAtShutdown : Code {:?}", statut.code()),
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell : {:?}", e),
    }
}



fn mem_virtuelle_par_default() {
    // Script PowerShell pour réactiver la gestion automatique de la mémoire virtuelle
    let script_powershell_default_mem = r#"
    $computerinfo = Get-WmiObject Win32_ComputerSystem -EnableAllPrivileges
    $computerinfo.AutomaticManagedPagefile = $True
    $computerinfo.Put()
    "#;

    // Exécuter le script PowerShell pour réactiver la gestion automatique de la mémoire virtuelle
    let status = Command::new("powershell")
        .arg("-Command")
        .arg(script_powershell_default_mem)
        .status();

    // Gérer les résultats de l'exécution du script
    match status {
        Ok(statut) if statut.success() => println!("Gestion automatique de la mémoire virtuelle réactivée avec succès."),
        Ok(statut) => eprintln!("Erreur lors de la réactivation de la gestion automatique de la mémoire virtuelle : Code {:?}", statut.code()),
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell : {:?}", e),
    }
}





fn optimiser_performances_energie() {
    let powershell_script = r#"
    # Rechercher l'ID du mode 'Meilleures performances' sur les systèmes où il est disponible
    $guid_best_performance = (powercfg -L | Select-String -Pattern "Ultimate Performance")

    # Vérifier si le mode 'Meilleures performances' est disponible
    if ($guid_best_performance) {
        $guid_best_performance_id = $guid_best_performance.ToString().Split()[3]
        powercfg -S $guid_best_performance_id
        Write-Host "Le mode 'Meilleures performances' a été activé."
    } else {
        # Activer le mode 'Performance élevée' si 'Meilleures performances' n'est pas disponible
        powercfg -S SCHEME_MIN
        Write-Host "Le mode 'Performance élevée' a été activé."
    }
    "#;

    let status = Command::new("powershell")
        .arg("-Command")
        .arg(powershell_script)
        .status();

    match status {
        Ok(statut) if statut.success() => println!("Le mode haute performance a été activé sur votre appareil."),
        Ok(statut) => eprintln!("Erreur lors de l'application des paramètres d'alimentation : Code {:?}", statut.code()),
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell : {:?}", e),
    }
}





impl ApplicationOptimisation {



   fn afficher_bouton_tout_en_un(&self, ui: &mut egui::Ui) {
    ui.vertical_centered(|ui| {
        ui.add_space(200.0);  // Ajuster selon les besoins

        // Ajouter de l'espace autour du bouton pour simuler du padding
        ui.add_space(10.0);  // Padding avant le bouton

        // Bouton principal pour lancer toutes les tâches de maintenance
        if ui.add(egui::Button::new(egui::RichText::new("Démarrer tout").size(22.0))
                    .rounding(5.0))  // Coins légèrement arrondis
                    .clicked() 
        {
            let en_execution = self.en_execution.clone();
            let message_execution = self.message_execution.clone();

            thread::spawn(move || {
                *en_execution.lock().unwrap() = true;

                

                *message_execution.lock().unwrap() = "Nettoyage du disque...".to_string();
                lancer_nettoyage_disque();
                thread::sleep(Duration::from_secs(2));

                *message_execution.lock().unwrap() = "Réparation du système...".to_string();
                lancer_reparation_systeme();
                thread::sleep(Duration::from_secs(2));

                *message_execution.lock().unwrap() = "Analyse des virus...".to_string();
                mise_a_jour_et_analyse_securite();
                thread::sleep(Duration::from_secs(2));

                 *message_execution.lock().unwrap() = "Reorganisation du SSD...".to_string();
                optimize_ssd();
                thread::sleep(Duration::from_secs(2));

                *message_execution.lock().unwrap() = "Reorganisation du HDD...".to_string();
                optimize_hdd();
                thread::sleep(Duration::from_secs(2));


                *message_execution.lock().unwrap() = "Votre ordinateur a bien été entretenu".to_string();
                *en_execution.lock().unwrap() = false;
            });
        }

        ui.add_space(10.0);  // Padding après le bouton

        // Afficher le message uniquement si l'exécution est terminée et que le message de fin est prêt
        if let Ok(en_execution) = self.en_execution.lock() {
            if !*en_execution {
                if let Ok(message) = self.message_execution.lock() {
                    if message.as_str() == "Votre ordinateur a bien été entretenu" {
                        ui.add_space(20.0);  // Ajouter un espace avant le message final
                        ui.label(egui::RichText::new(message.clone()).size(18.0));  // Message de fin
                    }
                }
            }
        }

        ui.add_space(20.0);
    });
}








 fn afficher_boutons_individuels(&self, ui: &mut egui::Ui) {
    ui.vertical(|ui| {
        ui.add_space(80.0);
        if ui.button(egui::RichText::new("Nettoyage des fichiers temporaires").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan("Nettoyage du disque...".to_string(), lancer_nettoyage_disque);
        }

        ui.add_space(20.0);
        if ui.button(egui::RichText::new("Réparation avancée du système").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan("Réparation du système...".to_string(), lancer_reparation_systeme);
        }

        ui.add_space(20.0);
        if ui.button(egui::RichText::new("Mise à jour et analyse antivirus").size(18.0)).clicked() {
            self.demarrer_tache_en_arriere_plan("Mise à jour et analyse sécuritaire...".to_string(), mise_a_jour_et_analyse_securite);
        }

        ui.add_space(20.0);

        // Les deux boutons sur la même ligne
        ui.horizontal(|ui| {
            if ui.button(egui::RichText::new("Activer le nettoyage automatique").size(18.0)).clicked() {
                configurer_nettoyage_automatique();
            }

            ui.add_space(20.0);

            if ui.button(egui::RichText::new("Désactiver le nettoyage automatique").size(18.0)).clicked() {
                reinitialiser_nettoyage_automatique();
            }
        });
    });
}


    fn demarrer_tache_en_arriere_plan<F>(&self, message: String, fonction: F)
    where
        F: FnOnce() + Send + 'static,
    {
        let en_execution = self.en_execution.clone();
        let message_execution = self.message_execution.clone();

        thread::spawn(move || {
            *en_execution.lock().unwrap() = true;
            *message_execution.lock().unwrap() = message;
            fonction();
            *en_execution.lock().unwrap() = false;
        });
    }
}




fn optimize_ssd() {
    let powershell_script = r#"
    try {
        $ssdDisks = Get-PhysicalDisk | Where-Object MediaType -eq 'SSD'

        if ($ssdDisks) {
            Write-Output "Optimisation des lecteurs SSD en cours..."
            foreach ($ssdDisk in $ssdDisks) {
                $partitions = Get-Partition | Where-Object -FilterScript { $_.DiskNumber -eq $ssdDisk.DeviceID }

                foreach ($partition in $partitions) {
                    if ($partition.DriveLetter) {
                        Write-Output "Optimisation du lecteur $($partition.DriveLetter)..."
                        Optimize-Volume -DriveLetter $partition.DriveLetter -ReTrim -Verbose
                    }
                }
            }
            Write-Output "Optimisation terminée."
        } else {
            Write-Output "Aucun SSD trouvé."
        }
    } catch {
        Write-Output "Erreur lors de l'optimisation des lecteurs : $_"
    }
    "#;

    let output = Command::new("powershell")
        .arg("-Command")
        .arg(powershell_script)
        .output();

    match output {
        Ok(output) => {
            // Essayer d'abord de décoder en UTF-8
            if let Ok(stdout) = str::from_utf8(&output.stdout) {
                println!("{}", stdout);
            } else {
                // Si UTF-8 échoue, essayer avec Windows-1252
                let (stdout, _, _) = WINDOWS_1252.decode(&output.stdout);
                println!("{}", stdout);
            }

            if let Ok(stderr) = str::from_utf8(&output.stderr) {
                eprintln!("{}", stderr);
            } else {
                let (stderr, _, _) = WINDOWS_1252.decode(&output.stderr);
                eprintln!("{}", stderr);
            }
        }
        Err(e) => {
            eprintln!("Erreur lors de l'exécution du script PowerShell : {}", e);
        }
    }
}




fn lancer_nettoyage_disque() {
    // Script PowerShell intégré sous forme de chaîne brute avec gestion des erreurs par bloc
    let ps_script = r#"
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
"#;



    // Exécuter le script PowerShell
    let output = Command::new("powershell")
        .arg("-Command")
        .arg(ps_script)
        .stdout(Stdio::piped())  // Capturer la sortie standard
        .stderr(Stdio::piped())  // Capturer la sortie d'erreur
        .output();

    match output {
        Ok(output) => {
            // Afficher la sortie standard et la sortie d'erreur
            let stdout = String::from_utf8_lossy(&output.stdout);
            let stderr = String::from_utf8_lossy(&output.stderr);

            println!("Sortie de PowerShell: {}", stdout);
            if !stderr.is_empty() {
                eprintln!("Erreurs de PowerShell: {}", stderr);
            }
        },
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell : {:?}", e),
    }
}





fn lancer_reparation_systeme() {
    let commandes_dism = [
        ["DISM", "/Online", "/Cleanup-Image", "/CheckHealth"],
        ["DISM", "/Online", "/Cleanup-Image", "/ScanHealth"],
        ["DISM", "/Online", "/Cleanup-Image", "/RestoreHealth"],
        ["DISM", "/Online", "/Cleanup-Image", "/StartComponentCleanup"],
    ];

    for args_cmd in &commandes_dism {
        let _ = executer_commande(args_cmd);
    }

    let _ = executer_commande(&["sfc", "/scannow"]);
}






// Fonction principale de mise à jour et analyse avec Windows Defender
fn mise_a_jour_et_analyse_securite() {
    let chemin_defender = r"C:\Program Files\Windows Defender\MpCmdRun.exe";

    // Vérification si Windows Defender est activé en utilisant un script PowerShell
    let ps_script = r"Get-MpPreference | Select -ExpandProperty DisableRealtimeMonitoring";
    let output = Command::new("powershell")
        .args(&["-Command", ps_script])
        .output()
        .expect("Échec de l'exécution de PowerShell");

    let is_defender_disabled = String::from_utf8_lossy(&output.stdout).trim() == "True";

    if is_defender_disabled {
        eprintln!("Windows Defender n'est pas actif. Tentative d'utilisation d'un autre antivirus.");
        utiliser_autre_antivirus(); // Basculer vers un autre antivirus si Defender est désactivé.
        return;
    }

    // Vérification si l'exécutable Windows Defender existe
    if !Path::new(chemin_defender).exists() {
        eprintln!("L'exécutable Windows Defender n'a pas été trouvé à : {}", chemin_defender);
        utiliser_autre_antivirus(); // Basculer vers un autre antivirus si Defender est manquant.
        return;
    }

    // Tentative de mise à jour des signatures de Defender
    if let Err(e) = executer_commande_defender(&[chemin_defender, "-SignatureUpdate"]) {
        eprintln!("Échec de la mise à jour de Windows Defender : {:?}", e);
        utiliser_autre_antivirus(); // Basculer vers un autre antivirus en cas d'échec.
        return;
    }

    // Tentative d'exécution d'une analyse rapide avec Defender
    if let Err(e) = executer_commande_defender(&[chemin_defender, "-Scan", "-ScanType", "1"]) {
        eprintln!("Échec de l'analyse rapide avec Windows Defender : {:?}", e);
        utiliser_autre_antivirus(); // Basculer vers un autre antivirus en cas d'échec.
    }
}

// Fonction pour basculer vers un autre antivirus
fn utiliser_autre_antivirus() {
    let ps_script = r"Get-CimInstance -Namespace root\SecurityCenter2 -Class AntiVirusProduct | Select-Object -ExpandProperty displayName";
    let output = Command::new("powershell")
        .args(&["-Command", ps_script])
        .output()
        .expect("Échec de l'exécution de PowerShell");

    let antivirus_name = String::from_utf8_lossy(&output.stdout).trim().to_string();

    if antivirus_name.contains("Avast") {
        utiliser_avast();
    } 
    
    else {
        eprintln!("Aucun autre antivirus reconnu n'est installé ou actif.");
    }
}





// Fonction spécifique pour Windows Defender avec gestion des erreurs
fn executer_commande_defender(args_cmd: &[&str]) -> Result<(), std::io::Error> {
    let statut = Command::new(args_cmd[0])
        .args(&args_cmd[1..])
        .status()?;

    match statut.code() {
        Some(0) => Ok(()), // Succès : pas de problème
        Some(2) => Err(std::io::Error::new(
            std::io::ErrorKind::Other,
            "Erreur : analyse incomplète ou fichier introuvable.",
        )),
        Some(code) => Err(std::io::Error::new(
            std::io::ErrorKind::Other,
            format!("Windows Defender a échoué avec le code de sortie : {}", code),
        )),
        None => Err(std::io::Error::new(
            std::io::ErrorKind::Other,
            "Windows Defender a échoué sans code de sortie.",
        )),
    }
}

// Fonction spécifique pour Avast avec gestion des erreurs
fn executer_commande_avast(args_cmd: &[&str]) -> Result<(), std::io::Error> {
    let statut = Command::new(args_cmd[0])
        .args(&args_cmd[1..])
        .status()?;

    match statut.code() {
        Some(0) => {
            println!("Analyse terminée avec succès : Aucun malware trouvé.");
            Ok(()) // Succès
        }
        Some(1) => {
            println!("Malware détecté et supprimé avec succès.");
            Ok(()) // Succès même si des malwares ont été trouvés
        }
        Some(code) => Err(std::io::Error::new(
            std::io::ErrorKind::Other,
            format!("Avast a échoué avec le code de sortie : {}", code),
        )),
        None => Err(std::io::Error::new(
            std::io::ErrorKind::Other,
            "Avast a échoué sans code de sortie.",
        )),
    }
}

// Fonction de mise à jour et analyse avec Avast
fn utiliser_avast() {
    let chemin_avast_quick_scan = r"C:\Program Files\AVAST Software\Avast\ashQuick.exe";
    let chemin_avast_update = r"C:\Program Files\AVAST Software\Avast\ashUpd.exe";

    // Vérification si l'exécutable pour la mise à jour existe
    if !Path::new(chemin_avast_update).exists() {
        eprintln!("Le chemin spécifié pour la mise à jour Avast n'existe pas : {}", chemin_avast_update);
        return;
    }

    // Mise à jour des définitions de virus (vps) d'Avast
    if let Err(e) = executer_commande_avast(&[chemin_avast_update, "vps"]) {
        eprintln!("Échec de la mise à jour des définitions de l'antivirus Avast : {:?}", e);
    }

    // Vérification si l'exécutable pour l'analyse rapide existe
    if !Path::new(chemin_avast_quick_scan).exists() {
        eprintln!("Le chemin spécifié pour l'analyse rapide Avast n'existe pas : {}", chemin_avast_quick_scan);
        return;
    }

    // Lancement de l'analyse rapide silencieuse avec suppression automatique des malwares
    if let Err(e) = executer_commande_avast(&[chemin_avast_quick_scan, "/silent", "/action:delete", "C:\\"]) {
        eprintln!("Échec de l'analyse rapide avec Avast : {:?}", e);
    }
}


fn executer_commande(args_cmd: &[&str]) -> Result<(), std::io::Error> {
    let statut = Command::new(args_cmd[0])
        .args(&args_cmd[1..])
        .status()?;

    if !statut.success() {
        return Err(std::io::Error::new(
            std::io::ErrorKind::Other,
            format!("La commande {:?} a échoué avec le code de sortie : {:?}", args_cmd, statut.code()),
        ));
    }

    Ok(())
}






fn configurer_nettoyage_automatique() {
    let status = ProcessCommand::new("reg")
        .args(&["add", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "AllowStorageSenseGlobal", "/t", "REG_DWORD", "/d", "1", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de l'activation du Nettoyage Automatique : {:?}", e);
    }

    let status = ProcessCommand::new("reg")
        .args(&["add", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "ConfigStorageSenseGlobalCadence", "/t", "REG_DWORD", "/d", "1", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de la configuration de l'exécution quotidienne pour le Nettoyage Automatique : {:?}", e);
    }

    let status = ProcessCommand::new("reg")
        .args(&["add", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "ConfigStorageSenseRecycleBinCleanupThreshold", "/t", "REG_DWORD", "/d", "14", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de la configuration de la suppression des fichiers de la corbeille après 14 jours : {:?}", e);
    }

    let status = ProcessCommand::new("reg")
        .args(&["add", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "ConfigStorageSenseDownloadsCleanupThreshold", "/t", "REG_DWORD", "/d", "0", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de la configuration de l'exclusion du dossier Téléchargements : {:?}", e);
    }

    let status = ProcessCommand::new("reg")
        .args(&["add", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "AllowStorageSenseTemporaryFilesCleanup", "/t", "REG_DWORD", "/d", "1", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de la configuration de la suppression des fichiers temporaires : {:?}", e);
    }

}

fn reinitialiser_nettoyage_automatique() {
    let status = ProcessCommand::new("reg")
        .args(&["delete", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "AllowStorageSenseGlobal", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de la suppression de AllowStorageSenseGlobal : {:?}", e);
    }

    let status = ProcessCommand::new("reg")
        .args(&["delete", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "ConfigStorageSenseGlobalCadence", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de la suppression de ConfigStorageSenseGlobalCadence : {:?}", e);
    }

    let status = ProcessCommand::new("reg")
        .args(&["delete", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "ConfigStorageSenseRecycleBinCleanupThreshold", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de la suppression de ConfigStorageSenseRecycleBinCleanupThreshold : {:?}", e);
    }

    let status = ProcessCommand::new("reg")
        .args(&["delete", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "ConfigStorageSenseDownloadsCleanupThreshold", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de la suppression de ConfigStorageSenseDownloadsCleanupThreshold : {:?}", e);
    }

    let status = ProcessCommand::new("reg")
        .args(&["delete", "HKLM\\Software\\Policies\\Microsoft\\Windows\\StorageSense", "/v", "AllowStorageSenseTemporaryFilesCleanup", "/f"])
        .status();
    if let Err(e) = status {
        eprintln!("Échec de la suppression de AllowStorageSenseTemporaryFilesCleanup : {:?}", e);
    }
}
