#![windows_subsystem = "windows"]

use eframe::egui::{self, CentralPanel, Context, SidePanel, vec2, IconData, FontData, FontDefinitions, FontFamily};
use std::process::Command;
use std::{thread};
use std::sync::{Arc, Mutex};
use std::process::Command as ProcessCommand;
use std::str;
use std::process::Stdio;
use std::path::Path;
use encoding_rs::WINDOWS_1252;
use image::GenericImageView;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Langue {
    Anglais,
    Francais,
    Espagnol,
    Chinois,
    Arabe,
    Hindi,
    Portugais,
    Russe,
    Allemand,
    Japonais,
    Swahili,
}

fn main() {
    // Tente de charger l'icône depuis le chemin spécifié
    let icon = load_icon("app.png");

    // Crée un ViewportBuilder en définissant la taille et la possibilité de redimensionnement
    let mut viewport = egui::ViewportBuilder::default()
        .with_inner_size(vec2(800.0, 600.0))
        .with_resizable(false);

    // Si l'icône a été chargée, on l'applique au viewport
    if let Some(icon) = icon {
        viewport = viewport.with_icon(Arc::new(icon));
    }

    // Configuration des options de la fenêtre via le viewport
    let options = eframe::NativeOptions {
        viewport,
        vsync: true,
        multisampling: 0,
        depth_buffer: 0,
        stencil_buffer: 0,
        ..Default::default()
    };

    // Démarrage de l'application
    if let Err(e) = eframe::run_native(
        "Assistools",
        options,
        // Ici, dans le callback de création, on configure les polices une seule fois.
        Box::new(|cc| {
            // Configuration des polices avant que l'interface ne soit rendue.
            configurer_polices(&cc.egui_ctx);
            Ok(Box::new(AppState::default()))
        }),
    ) {
        eprintln!("Échec du démarrage de l'application : {:?}", e);
    }
}


/// Tente de charger une icône à partir du chemin spécifié et retourne une Option<IconData>
fn load_icon(path: &str) -> Option<IconData> {
    match image::open(path) {
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
            eprintln!("Erreur lors du chargement de l'icône PNG depuis '{}': {:?}", path, e);
            None
        }
    }
}

impl Default for Langue {
    fn default() -> Self {
        Langue::Francais
    }
}

/// Structure contenant les labels traduits pour le panneau latéral
struct SidePanelLabels {
    pub mode_auto: &'static str,
    pub outils_avances: &'static str,
    pub booster: &'static str,
    pub infos: &'static str,
}

/// Traductions pour le panneau latéral en fonction de la langue sélectionnée
fn traductions_side_panel(langue: Langue) -> SidePanelLabels {
    match langue {
        Langue::Francais => SidePanelLabels {
            mode_auto: "Mode auto",
            outils_avances: "Outils avancés",
            booster: "Booster",
            infos: "Infos",
        },
        Langue::Anglais => SidePanelLabels {
            mode_auto: "Auto mode",
            outils_avances: "Advanced Tools",
            booster: "Booster",
            infos: "Info",
        },
        Langue::Espagnol => SidePanelLabels {
            mode_auto: "Modo auto",
            outils_avances: "Herramientas Avanzadas",
            booster: "Acelerar",
            infos: "Información",
        },
        Langue::Chinois => SidePanelLabels {
            mode_auto: "自动模式",
            outils_avances: "高级工具",
            booster: "加速",
            infos: "信息",
        },
        Langue::Arabe => SidePanelLabels {
            mode_auto: "الوضع التلقائي",
            outils_avances: "أدوات متقدمة",
            booster: "تعزيز",
            infos: "معلومات",
        },
        Langue::Hindi => SidePanelLabels {
            mode_auto: "स्वचालित मोड",
            outils_avances: "उन्नत उपकरण",
            booster: "बूस्टर",
            infos: "जानकारी",
        },
        Langue::Portugais => SidePanelLabels {
            mode_auto: "Modo automático",
            outils_avances: "Ferramentas avançadas",
            booster: "Impulsionar",
            infos: "Informações",
        },
       
        Langue::Russe => SidePanelLabels {
            mode_auto: "Автоматический режим",
            outils_avances: "Расширенные инструменты",
            booster: "Бустер",
            infos: "Информация",
        },
        Langue::Allemand => SidePanelLabels {
            mode_auto: "Automatikmodus",
            outils_avances: "Erweiterte Werkzeuge",
            booster: "Booster",
            infos: "Infos",
        },
        Langue::Japonais => SidePanelLabels {
            mode_auto: "オートモード",
            outils_avances: "高度なツール",
            booster: "ブースター",
            infos: "情報",
        },
        Langue::Swahili => SidePanelLabels {
            mode_auto: "Hali ya kiotomatiki",
            outils_avances: "Vifaa vya hali ya juu",
            booster: "Kuongeza kasi",
            infos: "Taarifa",
        },
    }
}


/// Structure regroupant l'ensemble des traductions pour l'interface
#[derive(Clone)]
struct Translations {
    // Onglet 0
    language_label: &'static str,
    default_message: &'static str,
    warning_message: &'static str,
    clean_and_repair_button: &'static str,
    // Messages d'exécution pour le bouton tout en un
    cleaning_disk_message: &'static str,
    repair_system_message: &'static str,
    update_windows_message: &'static str,
    antivirus_scan_message: &'static str,
    ssd_optimization_message: &'static str,
    hdd_optimization_message: &'static str,
    maintenance_complete_message: &'static str,
    // Onglet 1
    temp_files_cleanup: &'static str,
    advanced_system_repair: &'static str,
    update_and_antivirus: &'static str,
    enable_auto_cleanup: &'static str,
    disable_auto_cleanup: &'static str,
    // Onglet 2
    boost_performance_title: &'static str,
    boost_performance_description: &'static str,
    power_mode_button: &'static str,
    trim_ssd_button: &'static str,
    defrag_hdd_button: &'static str,
    adjust_virtual_memory_button: &'static str,
    default_virtual_memory_button: &'static str,
    // Onglet 3
    about_description: &'static str,
    features_title: &'static str,
    features_list: [&'static str; 7],
    publisher_title: &'static str,
    publisher_name: &'static str,
    build_number: &'static str,
    technical_support: &'static str,
    useful_links_title: &'static str,
    website_label: &'static str,
    github_label: &'static str,
}

/// Fonction renvoyant toutes les traductions en fonction de la langue sélectionnée
fn translations_all(langue: Langue) -> Translations {
    match langue {
        Langue::Francais => Translations {
            language_label: "Langue :",
            default_message: "Un seul clic pour nettoyer, réparer, mettre à jour et booster votre PC.",
            warning_message: "\u{26A0} Attention : la maintenance de votre PC peut durer plusieurs heures",
            clean_and_repair_button: "Nettoyer et réparer",
            cleaning_disk_message: "Nettoyage du disque...",
            repair_system_message: "Réparation du système...",
            update_windows_message: "Mise à jour de Windows...",
            antivirus_scan_message: "Analyse des virus...",
            ssd_optimization_message: "Reorganisation du SSD...",
            hdd_optimization_message: "Reorganisation du HDD...",
            maintenance_complete_message: "Votre ordinateur a bien été entretenu",
            temp_files_cleanup: "Nettoyage des fichiers temporaires",
            advanced_system_repair: "Réparation avancée du système",
            update_and_antivirus: "Mise à jour et analyse antivirus",
            enable_auto_cleanup: "Activer le nettoyage automatique",
            disable_auto_cleanup: "Désactiver le nettoyage automatique",
            boost_performance_title: "Booster les performances",
            boost_performance_description: "Configurer votre système afin qu'il soit adapté à votre ordinateur.",
            power_mode_button: "Activer le mode performance d'alimentation",
            trim_ssd_button: "Trimmer un SSD",
            defrag_hdd_button: "Défragmenter un HDD",
            adjust_virtual_memory_button: "Ajuster la mémoire virtuelle",
            default_virtual_memory_button: "Mémoire virtuelle par défaut",
            about_description: "Assistools est un utilitaire avancé conçu pour optimiser et réparer les systèmes Windows (7, 8, 10, 11) en un seul clic.",
            features_title: "Fonctionnalités",
            features_list: [
                "Réparation des systèmes Windows 7, 8, 10 et 11 en un seul clic.",
                "Nettoyage automatisé des fichiers inutiles pour libérer de l'espace disque.",
                "Mise à jour de Windows et des pilotes depuis WUpdate.",
                "Maintenance en un clic avec une automatisation complète du processus de nettoyage.",
                "Réinstallation des fichiers système critiques via Windows Update.",
                "Mise à jour de Windows Defender suivie d'une analyse rapide des menaces.",
                "Gestion proactive de la sécurité et des performances via un processus optimisé.",
            ],
            publisher_title: "Informations sur l'éditeur",
            publisher_name: "Éditeur : Assistouest Informatique",
            build_number: "Numéro de build : 1.0.0",
            technical_support: "Support technique : support@assistouest.fr",
            useful_links_title: "🔗 Liens utiles",
            website_label: "🌍 Site web :",
            github_label: "📦 Dépôt GitHub :",
        },
        Langue::Anglais => Translations {
            language_label: "Language:",
            default_message: "One click to clean, repair, update, and boost your PC.",
            warning_message: "\u{26A0} Warning: PC maintenance may take several hours",
            clean_and_repair_button: "Clean and Repair",
            cleaning_disk_message: "Cleaning the disk...",
            repair_system_message: "System repair...",
            update_windows_message: "Updating Windows...",
            antivirus_scan_message: "Virus scan...",
            ssd_optimization_message: "SSD optimization...",
            hdd_optimization_message: "HDD optimization...",
            maintenance_complete_message: "Your computer has been successfully maintained",
            temp_files_cleanup: "Temporary files cleanup",
            advanced_system_repair: "Advanced system repair",
            update_and_antivirus: "Update and antivirus scan",
            enable_auto_cleanup: "Enable automatic cleanup",
            disable_auto_cleanup: "Disable automatic cleanup",
            boost_performance_title: "Boost Performance",
            boost_performance_description: "Configure your system to be optimized for your computer.",
            power_mode_button: "Activate high performance power mode",
            trim_ssd_button: "Trim SSD",
            defrag_hdd_button: "Defragment HDD",
            adjust_virtual_memory_button: "Adjust virtual memory",
            default_virtual_memory_button: "Default virtual memory",
            about_description: "Assistools is an advanced utility designed to optimize and repair Windows systems (7, 8, 10, 11) with one click.",
            features_title: "Features",
            features_list: [
                "Repair Windows 7, 8, 10, and 11 systems with one click.",
                "Automated cleaning of unnecessary files to free up disk space.",
                "Update Windows and drivers via WUpdate.",
                "One-click maintenance with a fully automated cleaning process.",
                "Reinstall critical system files via Windows Update.",
                "Update Windows Defender followed by a quick threat scan.",
                "Proactive management of security and performance via an optimized process.",
            ],
            publisher_title: "Publisher Information",
            publisher_name: "Publisher: Assistouest Informatique",
            build_number: "Build Number: 1.0.0",
            technical_support: "Technical Support: support@assistouest.fr",
            useful_links_title: "🔗 Useful Links",
            website_label: "Website:",
            github_label: "GitHub Repository:",
        },
        Langue::Espagnol => Translations {
            language_label: "Idioma:",
            default_message: "Un solo clic para limpiar, reparar, actualizar y acelerar tu PC.",
            warning_message: "\u{26A0} Atención: el mantenimiento de tu PC puede durar varias horas",
            clean_and_repair_button: "Limpiar y reparar",
            cleaning_disk_message: "Limpieza del disco...",
            repair_system_message: "Reparación del sistema...",
            update_windows_message: "Actualización de Windows...",
            antivirus_scan_message: "Análisis de virus...",
            ssd_optimization_message: "Optimización del SSD...",
            hdd_optimization_message: "Optimización del HDD...",
            maintenance_complete_message: "Tu ordenador ha sido mantenido correctamente",
            temp_files_cleanup: "Limpieza de archivos temporales",
            advanced_system_repair: "Reparación avanzada del sistema",
            update_and_antivirus: "Actualización y análisis antivirus",
            enable_auto_cleanup: "Activar limpieza automática",
            disable_auto_cleanup: "Desactivar limpieza automática",
            boost_performance_title: "Acelerar el rendimiento",
            boost_performance_description: "Configura tu sistema para que se adapte a tu ordenador.",
            power_mode_button: "Activar modo de alto rendimiento",
            trim_ssd_button: "Recortar SSD",
            defrag_hdd_button: "Desfragmentar HDD",
            adjust_virtual_memory_button: "Ajustar la memoria virtual",
            default_virtual_memory_button: "Memoria virtual por defecto",
            about_description: "Assistools es una utilidad avanzada diseñada para optimizar y reparar sistemas Windows (7, 8, 10, 11) con un solo clic.",
            features_title: "Características",
            features_list: [
                "Reparación de sistemas Windows 7, 8, 10 y 11 con un solo clic.",
                "Limpieza automatizada de archivos innecesarios para liberar espacio en disco.",
                "Actualización de Windows y drivers a través de WUpdate.",
                "Mantenimiento con un solo clic y proceso de limpieza completamente automatizado.",
                "Reinstalación de archivos críticos del sistema mediante Windows Update.",
                "Actualización de Windows Defender seguida de un análisis rápido de amenazas.",
                "Gestión proactiva de la seguridad y el rendimiento a través de un proceso optimizado.",
            ],
            publisher_title: "Información del editor",
            publisher_name: "Editor: Assistouest Informatique",
            build_number: "Número de compilación: 1.0.0",
            technical_support: "Soporte técnico: support@assistouest.fr",
            useful_links_title: "🔗 Enlaces útiles",
            website_label: "Sitio web:",
            github_label: "Repositorio GitHub:",
        },
        Langue::Chinois => Translations {
            language_label: "语言：",
            default_message: "只需一键，即可清理、修复、更新并加速您的电脑。",
            warning_message: "\u{26A0} 警告：电脑维护可能需要数小时。",
            clean_and_repair_button: "清理并修复",
            cleaning_disk_message: "正在清理磁盘...",
            repair_system_message: "正在修复系统...",
            update_windows_message: "正在更新 Windows...",
            antivirus_scan_message: "正在扫描病毒...",
            ssd_optimization_message: "正在优化 SSD...",
            hdd_optimization_message: "正在优化 HDD...",
            maintenance_complete_message: "您的电脑已成功维护",
            temp_files_cleanup: "清理临时文件",
            advanced_system_repair: "高级系统修复",
            update_and_antivirus: "更新及病毒扫描",
            enable_auto_cleanup: "启用自动清理",
            disable_auto_cleanup: "禁用自动清理",
            boost_performance_title: "提升性能",
            boost_performance_description: "配置您的系统以达到最佳性能。",
            power_mode_button: "启用高性能模式",
            trim_ssd_button: "执行 SSD TRIM",
            defrag_hdd_button: "整理 HDD",
            adjust_virtual_memory_button: "调整虚拟内存",
            default_virtual_memory_button: "恢复默认虚拟内存",
            about_description: "Assistools 是一款先进工具，专为一键优化和修复 Windows 系统 (7, 8, 10, 11) 设计。",
            features_title: "功能",
            features_list: [
                "一键修复 Windows 7、8、10 和 11 系统。",
                "自动清理无用文件，释放磁盘空间。",
                "通过 WUpdate 更新 Windows 和驱动程序。",
                "一键维护，全程自动清理过程。",
                "通过 Windows Update 重新安装关键系统文件。",
                "更新 Windows Defender 并进行快速威胁扫描。",
                "主动优化安全与性能管理。",
            ],
            publisher_title: "发行信息",
            publisher_name: "发行者：Assistouest Informatique",
            build_number: "版本号：1.0.0",
            technical_support: "技术支持：support@assistouest.fr",
            useful_links_title: "🔗 有用链接",
            website_label: "🌍 网站：",
            github_label: "📦 GitHub 仓库：",
        },
        Langue::Arabe => Translations {
            language_label: "اللغة:",
            default_message: "بنقرة واحدة لتنظيف وإصلاح وتحديث وتسريع جهاز الكمبيوتر الخاص بك.",
            warning_message: "\u{26A0} تحذير: قد تستغرق صيانة الكمبيوتر عدة ساعات.",
            clean_and_repair_button: "تنظيف وإصلاح",
            cleaning_disk_message: "جارٍ تنظيف القرص...",
            repair_system_message: "جارٍ إصلاح النظام...",
            update_windows_message: "جارٍ تحديث Windows...",
            antivirus_scan_message: "جارٍ فحص الفيروسات...",
            ssd_optimization_message: "جارٍ تحسين SSD...",
            hdd_optimization_message: "جارٍ تحسين HDD...",
            maintenance_complete_message: "تم صيانة جهاز الكمبيوتر بنجاح",
            temp_files_cleanup: "تنظيف الملفات المؤقتة",
            advanced_system_repair: "إصلاح نظام متقدم",
            update_and_antivirus: "تحديث وفحص الفيروسات",
            enable_auto_cleanup: "تفعيل التنظيف التلقائي",
            disable_auto_cleanup: "تعطيل التنظيف التلقائي",
            boost_performance_title: "تعزيز الأداء",
            boost_performance_description: "قم بتكوين نظامك لتحقيق أفضل أداء.",
            power_mode_button: "تفعيل وضع الطاقة العالية",
            trim_ssd_button: "قص SSD",
            defrag_hdd_button: "إلغاء تجزئة HDD",
            adjust_virtual_memory_button: "ضبط الذاكرة الافتراضية",
            default_virtual_memory_button: "الذاكرة الافتراضية الافتراضية",
            about_description: "Assistools هي أداة متقدمة مصممة لتحسين وإصلاح أنظمة Windows (7, 8, 10, 11) بنقرة واحدة.",
            features_title: "الميزات",
            features_list: [
                "إصلاح أنظمة Windows 7، 8، 10 و 11 بنقرة واحدة.",
                "تنظيف الملفات غير الضرورية لتحرير مساحة القرص.",
                "تحديث Windows وبرامج التشغيل عبر WUpdate.",
                "صيانة بنقرة واحدة مع عملية تنظيف مؤتمتة بالكامل.",
                "إعادة تثبيت ملفات النظام الحيوية عبر Windows Update.",
                "تحديث Windows Defender مع فحص سريع للتهديدات.",
                "إدارة الأمان والأداء بشكل استباقي.",
            ],
            publisher_title: "معلومات الناشر",
            publisher_name: "الناشر: Assistouest Informatique",
            build_number: "رقم البناء: 1.0.0",
            technical_support: "الدعم الفني: support@assistouest.fr",
            useful_links_title: "🔗 روابط مفيدة",
            website_label: "🌍 الموقع:",
            github_label: "📦 مستودع GitHub:",
        },
        Langue::Hindi => Translations {
            language_label: "भाषा:",
            default_message: "केवल एक क्लिक में अपने पीसी को साफ़, मरम्मत, अपडेट और तेज़ करें।",
            warning_message: "\u{26A0} चेतावनी: पीसी रख-रखाव में कई घंटे लग सकते हैं।",
            clean_and_repair_button: "साफ़ करें और मरम्मत करें",
            cleaning_disk_message: "डिस्क की सफाई हो रही है...",
            repair_system_message: "सिस्टम की मरम्मत हो रही है...",
            update_windows_message: "Windows अपडेट हो रहा है...",
            antivirus_scan_message: "वायरस स्कैन हो रहा है...",
            ssd_optimization_message: "SSD अनुकूलन हो रहा है...",
            hdd_optimization_message: "HDD अनुकूलन हो रहा है...",
            maintenance_complete_message: "आपका कंप्यूटर सफलतापूर्वक मेंटेन किया गया है",
            temp_files_cleanup: "अस्थायी फ़ाइलें साफ़ करें",
            advanced_system_repair: "उन्नत सिस्टम मरम्मत",
            update_and_antivirus: "अपडेट और एंटीवायरस स्कैन",
            enable_auto_cleanup: "स्वचालित सफाई सक्षम करें",
            disable_auto_cleanup: "स्वचालित सफाई अक्षम करें",
            boost_performance_title: "प्रदर्शन बढ़ाएं",
            boost_performance_description: "अपने कंप्यूटर के लिए सिस्टम को अनुकूलित करें।",
            power_mode_button: "उच्च प्रदर्शन मोड सक्रिय करें",
            trim_ssd_button: "SSD ट्रिम करें",
            defrag_hdd_button: "HDD को डीफ्रैग करें",
            adjust_virtual_memory_button: "वर्चुअल मेमोरी समायोजित करें",
            default_virtual_memory_button: "डिफ़ॉल्ट वर्चुअल मेमोरी",
            about_description: "Assistools एक उन्नत यूटिलिटी है, जिसे Windows सिस्टम (7, 8, 10, 11) को एक क्लिक में अनुकूलित और मरम्मत करने के लिए डिज़ाइन किया गया है।",
            features_title: "विशेषताएँ",
            features_list: [
                "एक क्लिक में Windows 7, 8, 10 और 11 सिस्टम की मरम्मत।",
                "अनावश्यक फाइलों को हटाकर डिस्क स्पेस मुक्त करें।",
                "WUpdate के जरिए Windows और ड्राइवर अपडेट करें।",
                "पूरी तरह स्वचालित सफाई प्रक्रिया के साथ एक क्लिक में रखरखाव।",
                "Windows Update के जरिए महत्वपूर्ण सिस्टम फ़ाइलों को पुनर्स्थापित करें।",
                "Windows Defender को अपडेट करें और तेज़ खतरे की जांच करें।",
                "सुरक्षा और प्रदर्शन का सक्रिय प्रबंधन।",
            ],
            publisher_title: "प्रकाशक की जानकारी",
            publisher_name: "प्रकाशक: Assistouest Informatique",
            build_number: "बिल्ड नंबर: 1.0.0",
            technical_support: "तकनीकी सहायता: support@assistouest.fr",
            useful_links_title: "🔗 उपयोगी लिंक",
            website_label: "🌍 वेबसाइट:",
            github_label: "📦 GitHub रिपॉजिटरी:",
        },
        Langue::Portugais => Translations {
            language_label: "Idioma:",
            default_message: "Um clique para limpar, reparar, atualizar e acelerar seu PC.",
            warning_message: "\u{26A0} Atenção: a manutenção do PC pode levar várias horas.",
            clean_and_repair_button: "Limpar e Reparar",
            cleaning_disk_message: "Limpando o disco...",
            repair_system_message: "Reparando o sistema...",
            update_windows_message: "Atualizando o Windows...",
            antivirus_scan_message: "Verificando vírus...",
            ssd_optimization_message: "Otimização do SSD...",
            hdd_optimization_message: "Otimização do HDD...",
            maintenance_complete_message: "Seu computador foi mantido com sucesso",
            temp_files_cleanup: "Limpeza de arquivos temporários",
            advanced_system_repair: "Reparo avançado do sistema",
            update_and_antivirus: "Atualização e verificação antivírus",
            enable_auto_cleanup: "Ativar limpeza automática",
            disable_auto_cleanup: "Desativar limpeza automática",
            boost_performance_title: "Impulsionar o Desempenho",
            boost_performance_description: "Configure seu sistema para se adequar ao seu computador.",
            power_mode_button: "Ativar modo de alta performance",
            trim_ssd_button: "Executar TRIM no SSD",
            defrag_hdd_button: "Desfragmentar o HDD",
            adjust_virtual_memory_button: "Ajustar memória virtual",
            default_virtual_memory_button: "Memória virtual padrão",
            about_description: "Assistools é uma ferramenta avançada projetada para otimizar e reparar sistemas Windows (7, 8, 10, 11) com um clique.",
            features_title: "Recursos",
            features_list: [
                "Repare sistemas Windows 7, 8, 10 e 11 com um clique.",
                "Limpe arquivos desnecessários para liberar espaço em disco.",
                "Atualize Windows e drivers via WUpdate.",
                "Manutenção com um clique e processo de limpeza totalmente automatizado.",
                "Reinstale arquivos críticos do sistema via Windows Update.",
                "Atualize o Windows Defender seguido de uma varredura rápida.",
                "Gerencie proativamente a segurança e o desempenho.",
            ],
            publisher_title: "Informações do Editor",
            publisher_name: "Editor: Assistouest Informatique",
            build_number: "Número da Versão: 1.0.0",
            technical_support: "Suporte Técnico: support@assistouest.fr",
            useful_links_title: "🔗 Links Úteis",
            website_label: "🌍 Site:",
            github_label: "📦 Repositório GitHub:",
        },
     
        Langue::Russe => Translations {
            language_label: "Язык:",
            default_message: "Одним кликом очистите, исправьте, обновите и ускорьте свой ПК.",
            warning_message: "\u{26A0} Внимание: обслуживание ПК может занять несколько часов.",
            clean_and_repair_button: "Очистить и исправить",
            cleaning_disk_message: "Очистка диска...",
            repair_system_message: "Ремонт системы...",
            update_windows_message: "Обновление Windows...",
            antivirus_scan_message: "Сканирование на вирусы...",
            ssd_optimization_message: "Оптимизация SSD...",
            hdd_optimization_message: "Оптимизация HDD...",
            maintenance_complete_message: "Ваш компьютер успешно обслужен",
            temp_files_cleanup: "Очистка временных файлов",
            advanced_system_repair: "Расширенный ремонт системы",
            update_and_antivirus: "Обновление и антивирусная проверка",
            enable_auto_cleanup: "Включить автоматическую очистку",
            disable_auto_cleanup: "Отключить автоматическую очистку",
            boost_performance_title: "Ускорение производительности",
            boost_performance_description: "Настройте систему для оптимальной работы вашего ПК.",
            power_mode_button: "Включить режим высокой производительности",
            trim_ssd_button: "Выполнить TRIM для SSD",
            defrag_hdd_button: "Дефрагментация HDD",
            adjust_virtual_memory_button: "Настроить виртуальную память",
            default_virtual_memory_button: "Стандартная виртуальная память",
            about_description: "Assistools — это продвинутая утилита для оптимизации и ремонта Windows (7, 8, 10, 11) одним кликом.",
            features_title: "Функции",
            features_list: [
                "Однокликовый ремонт Windows 7, 8, 10 и 11.",
                "Автоматическая очистка ненужных файлов для освобождения места на диске.",
                "Обновление Windows и драйверов через WUpdate.",
                "Полностью автоматизированное обслуживание одним кликом.",
                "Переустановка критических системных файлов через Windows Update.",
                "Быстрое сканирование угроз с обновлением Windows Defender.",
                "Проактивное управление безопасностью и производительностью.",
            ],
            publisher_title: "Информация об издателе",
            publisher_name: "Издатель: Assistouest Informatique",
            build_number: "Номер сборки: 1.0.0",
            technical_support: "Техническая поддержка: support@assistouest.fr",
            useful_links_title: "🔗 Полезные ссылки",
            website_label: "🌍 Сайт:",
            github_label: "📦 GitHub Репозиторий:",
        },
        Langue::Allemand => Translations {
            language_label: "Sprache:",
            default_message: "Ein Klick, um deinen PC zu reinigen, zu reparieren, zu aktualisieren und zu beschleunigen.",
            warning_message: "\u{26A0} Warnung: Die PC-Wartung kann mehrere Stunden dauern.",
            clean_and_repair_button: "Reinigen und Reparieren",
            cleaning_disk_message: "Reinige die Festplatte...",
            repair_system_message: "Repariere das System...",
            update_windows_message: "Windows wird aktualisiert...",
            antivirus_scan_message: "Virenscan läuft...",
            ssd_optimization_message: "SSD-Optimierung läuft...",
            hdd_optimization_message: "HDD-Optimierung läuft...",
            maintenance_complete_message: "Dein PC wurde erfolgreich gewartet",
            temp_files_cleanup: "Temporäre Dateien bereinigen",
            advanced_system_repair: "Erweiterte Systemreparatur",
            update_and_antivirus: "Update und Virenscan",
            enable_auto_cleanup: "Automatische Reinigung aktivieren",
            disable_auto_cleanup: "Automatische Reinigung deaktivieren",
            boost_performance_title: "Leistung steigern",
            boost_performance_description: "Optimiere dein System für deinen PC.",
            power_mode_button: "Hochleistungsmodus aktivieren",
            trim_ssd_button: "SSD trimmen",
            defrag_hdd_button: "HDD defragmentieren",
            adjust_virtual_memory_button: "Virtuellen Speicher anpassen",
            default_virtual_memory_button: "Standard-Virtueller Speicher",
            about_description: "Assistools ist ein fortschrittliches Tool zur Optimierung und Reparatur von Windows-Systemen (7, 8, 10, 11) per Klick.",
            features_title: "Funktionen",
            features_list: [
                "Repariere Windows 7, 8, 10 und 11 Systeme mit einem Klick.",
                "Automatisches Reinigen unnötiger Dateien zur Freisetzung von Speicherplatz.",
                "Aktualisiere Windows und Treiber über WUpdate.",
                "Ein-Klick-Wartung mit vollautomatisiertem Reinigungsprozess.",
                "Neuinstallation kritischer Systemdateien via Windows Update.",
                "Schneller Virenscan mit Update von Windows Defender.",
                "Proaktive Verwaltung von Sicherheit und Leistung.",
            ],
            publisher_title: "Verlegerinformationen",
            publisher_name: "Verleger: Assistouest Informatique",
            build_number: "Build-Nummer: 1.0.0",
            technical_support: "Technischer Support: support@assistouest.fr",
            useful_links_title: "🔗 Nützliche Links",
            website_label: "🌍 Webseite:",
            github_label: "📦 GitHub Repository:",
        },
        Langue::Japonais => Translations {
            language_label: "言語：",
            default_message: "ワンクリックでPCをクリーン、修復、更新、加速します。",
            warning_message: "\u{26A0} 警告：PCのメンテナンスには数時間かかる場合があります。",
            clean_and_repair_button: "クリーン＆修復",
            cleaning_disk_message: "ディスクをクリーン中...",
            repair_system_message: "システム修復中...",
            update_windows_message: "Windowsを更新中...",
            antivirus_scan_message: "ウイルススキャン中...",
            ssd_optimization_message: "SSDを最適化中...",
            hdd_optimization_message: "HDDを最適化中...",
            maintenance_complete_message: "PCのメンテナンスが完了しました",
            temp_files_cleanup: "一時ファイルのクリーンアップ",
            advanced_system_repair: "高度なシステム修復",
            update_and_antivirus: "更新とアンチウイルススキャン",
            enable_auto_cleanup: "自動クリーンアップを有効化",
            disable_auto_cleanup: "自動クリーンアップを無効化",
            boost_performance_title: "パフォーマンス向上",
            boost_performance_description: "PCに最適なシステム設定を行います。",
            power_mode_button: "高パフォーマンスモードを有効化",
            trim_ssd_button: "SSDトリム実行",
            defrag_hdd_button: "HDDのデフラグ",
            adjust_virtual_memory_button: "仮想メモリを調整",
            default_virtual_memory_button: "デフォルト仮想メモリ",
            about_description: "Assistoolsは、Windowsシステム (7, 8, 10, 11) をワンクリックで最適化・修復する先進的なユーティリティです。",
            features_title: "機能",
            features_list: [
                "ワンクリックでWindows 7, 8, 10, 11の修復。",
                "不要なファイルを自動削除してディスク容量を確保。",
                "WUpdateでWindowsとドライバーを更新。",
                "完全自動のワンクリックメンテナンス。",
                "Windows Updateで重要なシステムファイルを再インストール。",
                "Windows Defenderを更新し迅速に脅威スキャン。",
                "セキュリティとパフォーマンスを積極的に管理。",
            ],
            publisher_title: "発行者情報",
            publisher_name: "発行者：Assistouest Informatique",
            build_number: "ビルド番号：1.0.0",
            technical_support: "テクニカルサポート：support@assistouest.fr",
            useful_links_title: "🔗 便利なリンク",
            website_label: "🌍 ウェブサイト：",
            github_label: "📦 GitHubリポジトリ：",
        },
        Langue::Swahili => Translations {
            language_label: "Lugha:",
            default_message: "Bonyeza mara moja kusafisha, kurekebisha, kusasisha na kuongeza kasi ya PC yako.",
            warning_message: "\u{26A0} Onyo: Matengenezo ya PC yanaweza kuchukua masaa kadhaa.",
            clean_and_repair_button: "Safisha na Rekebisha",
            cleaning_disk_message: "Inasafisha diski...",
            repair_system_message: "Inarekebisha mfumo...",
            update_windows_message: "Inasasisha Windows...",
            antivirus_scan_message: "Inachunguza virusi...",
            ssd_optimization_message: "Inaboresha SSD...",
            hdd_optimization_message: "Inaboresha HDD...",
            maintenance_complete_message: "PC yako imetengenezwa kikamilifu",
            temp_files_cleanup: "Safisha faili za muda",
            advanced_system_repair: "Matengenezo ya mfumo ya juu",
            update_and_antivirus: "Sasisha na skan virusi",
            enable_auto_cleanup: "Washa usafishaji wa kiotomatiki",
            disable_auto_cleanup: "Zima usafishaji wa kiotomatiki",
            boost_performance_title: "Ongeza Utendaji",
            boost_performance_description: "Panga mfumo wako ili ufanye kazi bora kwa PC yako.",
            power_mode_button: "Washa hali ya nguvu ya juu",
            trim_ssd_button: "Fanya TRIM kwenye SSD",
            defrag_hdd_button: "Fanya defragmentation ya HDD",
            adjust_virtual_memory_button: "Badilisha kumbukumbu ya mtandao",
            default_virtual_memory_button: "Kumbukumbu ya mtandao chaguomsingi",
            about_description: "Assistools ni zana ya hali ya juu iliyoundwa ili kuboresha na kurekebisha mifumo ya Windows (7, 8, 10, 11) kwa bonyeza moja.",
            features_title: "Vipengele",
            features_list: [
                "Sahihisha mifumo ya Windows 7, 8, 10, na 11 kwa bonyeza moja.",
                "Ondoa faili zisizohitajika ili kutoa nafasi ya diski.",
                "Sasisha Windows na madereva kupitia WUpdate.",
                "Matengenezo ya bonyeza moja na usafishaji wa kiotomatiki kabisa.",
                "Sakinisha tena faili muhimu za mfumo kupitia Windows Update.",
                "Sasisha Windows Defender pamoja na uchunguzi wa haraka wa tishio.",
                "Simamia usalama na utendaji kwa utaratibu ulioboreshwa.",
            ],
            publisher_title: "Taarifa za Mchapishaji",
            publisher_name: "Mchapishaji: Assistouest Informatique",
            build_number: "Nambari ya Ujenzi: 1.0.0",
            technical_support: "Msaada wa Kiufundi: support@assistouest.fr",
            useful_links_title: "🔗 Viungo vya Manufaa",
            website_label: "🌍 Tovuti:",
            github_label: "📦 Hazina ya GitHub:",
        },
    }
}
/// Structure gérant l'état global de l'application
struct AppState {
    main_app: ApplicationOptimisation,
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
        self.main_app.update(ctx, frame);
    }
}

/// Structure principale de l'application
struct ApplicationOptimisation {
    onglet_selectionne: usize,
    en_execution: Arc<Mutex<bool>>,
    message_execution: Arc<Mutex<String>>,
    langue_actuelle: Langue,
   
}

impl Default for ApplicationOptimisation {
    fn default() -> Self {
        Self {
            onglet_selectionne: 0,
            en_execution: Arc::new(Mutex::new(false)),
            message_execution: Arc::new(Mutex::new(String::new())),
            langue_actuelle: Langue::default(),
          
        }
    }
}

impl eframe::App for ApplicationOptimisation {
    fn update(&mut self, ctx: &Context, _frame: &mut eframe::Frame) {
        let side_labels = traductions_side_panel(self.langue_actuelle);
        let texts = translations_all(self.langue_actuelle);


        // Panneau latéral
        SidePanel::left("panneau_lateral")
            .resizable(true)
            .min_width(100.0)
            .max_width(120.0)
            .show(ctx, |ui| {
                ui.vertical_centered(|ui| {
                    ui.add_space(20.0);
                    if ui.add(egui::Button::new(
                        egui::RichText::new("\u{1F504}").size(32.0).strong()
                    ).frame(false)).clicked() {
                        self.onglet_selectionne = 0;
                    }
                    ui.label(egui::RichText::new(side_labels.mode_auto).size(12.0));
                    ui.add_space(50.0);
                    if ui.add(egui::Button::new(
                        egui::RichText::new("\u{1F527}").size(32.0).strong()
                    ).frame(false)).clicked() {
                        self.onglet_selectionne = 1;
                    }
                    ui.label(egui::RichText::new(side_labels.outils_avances).size(12.0));
                    ui.add_space(50.0);
                    if ui.add(egui::Button::new(
                        egui::RichText::new("\u{23E9}").size(32.0).strong()
                    ).frame(false)).clicked() {
                        self.onglet_selectionne = 2;
                    }
                    ui.label(egui::RichText::new(side_labels.booster).size(12.0));
                    ui.add_space(50.0);
                    if ui.add(egui::Button::new(
                        egui::RichText::new("\u{2139}").size(32.0).strong()
                    ).frame(false)).clicked() {
                        self.onglet_selectionne = 3;
                    }
                    ui.label(egui::RichText::new(side_labels.infos).size(12.0));
                });
            });

        // Panneau central
        CentralPanel::default().show(ctx, |ui| {
            ui.vertical_centered(|ui| {
                match self.onglet_selectionne {
                  0 => {
    // Onglet 0 : Interface "tout en un"
   
    ui.horizontal(|ui| {
        ui.label(texts.language_label);
        // Récupère la largeur disponible pour le ComboBox
        let available_width = ui.available_width();
        egui::ComboBox::from_id_salt("combo_langue")
            .selected_text(match self.langue_actuelle {
                Langue::Francais  => "Français",
                Langue::Anglais   => "English",
                Langue::Espagnol  => "Español",
                Langue::Chinois   => "中文",
                Langue::Arabe     => "العربية",
                Langue::Hindi     => "हिन्दी",
                Langue::Portugais => "Português",
                Langue::Russe     => "Русский",
                Langue::Allemand  => "Deutsch",
                Langue::Japonais  => "日本語",
                Langue::Swahili   => "Kiswahili",
            }.to_string())
            // Fixe la largeur du ComboBox à la largeur disponible
            .width(available_width)
            .show_ui(ui, |ui| {
                // Définit une hauteur minimale pour le contenu du dropdown
                ui.set_min_height(40.0);
                ui.selectable_value(&mut self.langue_actuelle, Langue::Francais,  "Français");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Anglais,   "English");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Espagnol,  "Español");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Chinois,   "中文");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Arabe,     "العربية");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Hindi,     "हिन्दी");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Portugais, "Português");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Russe,     "Русский");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Allemand,  "Deutsch");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Japonais,  "日本語");
                ui.selectable_value(&mut self.langue_actuelle, Langue::Swahili,   "Kiswahili");
            });
    });
    ui.add_space(110.0);
    // Bouton principal et gestion du spinner
    self.afficher_bouton_tout_en_un(ui);
    if *self.en_execution.lock().unwrap() {
        ui.add(egui::Spinner::default().size(30.0));
        let message = self.message_execution.lock().unwrap();
        ui.add_space(20.0);
        ui.label(message.to_string());
    } else {
        ui.label(texts.default_message);
    }
    let available = ui.available_height();
    let spacer = if available > 60.0 { available - 60.0 } else { 0.0 };
    ui.add_space(spacer);
    egui::Frame::new()
        .fill(egui::Color32::from_rgb(0, 105, 226))
        .inner_margin(egui::Margin { left: 10, right: 10, top: 10, bottom: 10 })
        .show(ui, |ui| {
            ui.label(egui::RichText::new(texts.warning_message)
                .color(egui::Color32::WHITE)
                .size(16.0),
            );
        });
},

                   1 => {
                        // Onglet 1 : Outils individuels
                        self.afficher_boutons_individuels(ui, &texts);
                    },
                   2 => {
                        // Onglet 2 : Booster les performances
                        ui.add_space(20.0);
                        ui.label(egui::RichText::new(texts.boost_performance_title).size(24.0));
                        ui.add_space(10.0);
                        ui.label(texts.boost_performance_description);
                        ui.add_space(20.0);
                        ui.with_layout(egui::Layout::left_to_right(egui::Align::Min), |ui| {
                            if ui.button(egui::RichText::new(texts.power_mode_button).size(18.0)).clicked() {
                                self.demarrer_tache_en_arriere_plan(
                                    "Optimisation des performances...".to_string(),
                                    optimiser_performances_energie,
                                );
                            }
                        });
                        ui.add_space(20.0);
                        ui.with_layout(egui::Layout::left_to_right(egui::Align::Min), |ui| {
                            if ui.button(egui::RichText::new(texts.trim_ssd_button).size(18.0)).clicked() {
                                self.demarrer_tache_en_arriere_plan(
                                    "Trim en cours...".to_string(),
                                    optimize_ssd,
                                );
                            }
                            ui.add_space(10.0);
                            if ui.button(egui::RichText::new(texts.defrag_hdd_button).size(18.0)).clicked() {
                                self.demarrer_tache_en_arriere_plan(
                                    "Défragmentation en cours...".to_string(),
                                    optimize_hdd,
                                );
                            }
                        });
                        ui.add_space(20.0);
                        ui.with_layout(egui::Layout::left_to_right(egui::Align::Min), |ui| {
                            if ui.button(egui::RichText::new(texts.adjust_virtual_memory_button).size(18.0)).clicked() {
                                self.demarrer_tache_en_arriere_plan(
                                    "Ajustement de la mémoire virtuelle...".to_string(),
                                    ajuster_mem_virtuelle,
                                );
                            }
                            ui.add_space(10.0);
                            if ui.button(egui::RichText::new(texts.default_virtual_memory_button).size(18.0)).clicked() {
                                self.demarrer_tache_en_arriere_plan(
                                    "Réinitialisation de la mémoire virtuelle...".to_string(),
                                    mem_virtuelle_par_default,
                                );
                            }
                        });
                    },
                   3 => {
                        // Onglet 3 : Informations
                        ui.add_space(20.0);
                        ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
                            ui.label(texts.about_description);
                        });
                        ui.add_space(20.0);
                        ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
                            ui.add(egui::Separator::default().spacing(20.0));
                        });
                        ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
                            ui.label(egui::RichText::new(texts.features_title).size(22.0).strong());
                            ui.add_space(10.0);
                            for feature in texts.features_list.iter() {
                                ui.label(format!("• {}", feature));
                                ui.add_space(5.0);
                            }
                        });
                        ui.add_space(20.0);
                        ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
                            ui.add(egui::Separator::default().spacing(20.0));
                        });
                        ui.with_layout(egui::Layout::top_down(egui::Align::Min), |ui| {
                            ui.label(egui::RichText::new(texts.publisher_title).size(22.0).strong());
                            ui.add_space(10.0);
                            ui.label(texts.publisher_name);
                            ui.add_space(5.0);
                            ui.label(texts.build_number);
                            ui.add_space(5.0);
                            ui.label(texts.technical_support);
                            ui.add_space(20.0);
                            ui.label(egui::RichText::new(texts.useful_links_title).size(22.0).strong());
                            ui.add_space(10.0);
                            ui.horizontal(|ui| {
                                ui.label(egui::RichText::new(texts.website_label).strong());
                                ui.hyperlink("https://assistouest.fr/logiciel-maintenance-informatique/")
                                    .on_hover_text("Visitez notre site web");
                            });
                            ui.add_space(5.0);
                            ui.horizontal(|ui| {
                                ui.label(egui::RichText::new(texts.github_label).strong());
                                ui.hyperlink("https://github.com/Assistouest/assistools/releases/")
                                    .on_hover_text("Consultez les mises à jour et téléchargements");
                            });
                        });
                    },
                   _ => (),
                }
            });
            if *self.en_execution.lock().unwrap() {
                ctx.request_repaint();
            }
        });
    }
}

impl ApplicationOptimisation {
    fn afficher_bouton_tout_en_un(&self, ui: &mut egui::Ui) {
        let texts = translations_all(self.langue_actuelle);
        ui.vertical_centered(|ui| {
            ui.add_space(100.0);
            let bouton_largeur = ui.available_width() * 0.5;
            let frame = egui::Frame::new()
                .inner_margin(egui::Margin {
                    left: 20_i8,
                    right: 20_i8,
                    top: 10_i8,
                    bottom: 10_i8,
                })
                .fill(egui::Color32::from_rgb(50, 50, 50))
                .stroke(egui::Stroke::new(0.0, egui::Color32::TRANSPARENT))
                .corner_radius(15);
            let frame_response = frame.show(ui, |ui| {
                ui.allocate_ui(egui::Vec2::new(bouton_largeur, 0.0), |ui| {
                    let button_response = ui.add(
                        egui::Button::new(
                            egui::RichText::new(texts.clean_and_repair_button)
                                .size(22.0)
                                .color(egui::Color32::WHITE)
                        )
                        .fill(egui::Color32::from_rgb(50, 50, 50))
                        .corner_radius(50)
                        .stroke(egui::Stroke::new(0.0, egui::Color32::TRANSPARENT))
                    );
                    if button_response.clicked() {
                        let en_execution = self.en_execution.clone();
                        let message_execution = self.message_execution.clone();
                        let texts = texts.clone();
                        std::thread::spawn(move || {
                            *en_execution.lock().unwrap() = true;
                            *message_execution.lock().unwrap() = texts.cleaning_disk_message.to_string();
                            lancer_nettoyage_disque();
                            std::thread::sleep(std::time::Duration::from_secs(2));
                            
                            *message_execution.lock().unwrap() = texts.repair_system_message.to_string();
                            lancer_reparation_systeme();
                            std::thread::sleep(std::time::Duration::from_secs(2));
                            
                            *message_execution.lock().unwrap() = texts.update_windows_message.to_string();
                            lancer_mise_a_jour_windows();
                            std::thread::sleep(std::time::Duration::from_secs(2));
                            
                            *message_execution.lock().unwrap() = texts.antivirus_scan_message.to_string();
                            mise_a_jour_et_analyse_securite();
                            std::thread::sleep(std::time::Duration::from_secs(2));
                            
                            *message_execution.lock().unwrap() = texts.ssd_optimization_message.to_string();
                            optimize_ssd();
                            std::thread::sleep(std::time::Duration::from_secs(2));
                            
                            *message_execution.lock().unwrap() = texts.hdd_optimization_message.to_string();
                            optimize_hdd();
                            std::thread::sleep(std::time::Duration::from_secs(2));
                            
                            *message_execution.lock().unwrap() = texts.maintenance_complete_message.to_string();
                            *en_execution.lock().unwrap() = false;
                        });
                    }
                });
            });
            if frame_response.response.hovered() {
                let rect = frame_response.response.rect;
                ui.painter().rect_stroke(
                    rect,
                    15.0,
                    egui::Stroke::new(2.0, egui::Color32::LIGHT_GRAY),
                    egui::StrokeKind::Inside
                );
            }
            ui.add_space(10.0);
            if let Ok(en_execution) = self.en_execution.lock() {
                if !*en_execution {
                    if let Ok(message) = self.message_execution.lock() {
                        if message.as_str() == texts.maintenance_complete_message {
                            ui.add_space(20.0);
                            ui.label(egui::RichText::new(message.clone()).size(18.0));
                        }
                    }
                }
            }
            ui.add_space(20.0);
        });
    }

    fn afficher_boutons_individuels(&self, ui: &mut egui::Ui, texts: &Translations) {
        ui.vertical(|ui| {
            ui.add_space(80.0);
            if ui.button(egui::RichText::new(texts.temp_files_cleanup).size(18.0)).clicked() {
                self.demarrer_tache_en_arriere_plan("Nettoyage du disque...".to_string(), lancer_nettoyage_disque);
            }
            ui.add_space(20.0);
            if ui.button(egui::RichText::new(texts.advanced_system_repair).size(18.0)).clicked() {
                self.demarrer_tache_en_arriere_plan("Réparation du système...".to_string(), lancer_reparation_systeme);
            }
            ui.add_space(20.0);
            if ui.button(egui::RichText::new(texts.update_and_antivirus).size(18.0)).clicked() {
                self.demarrer_tache_en_arriere_plan("Mise à jour et analyse sécuritaire...".to_string(), mise_a_jour_et_analyse_securite);
            }
            ui.add_space(20.0);
            ui.horizontal(|ui| {
                if ui.button(egui::RichText::new(texts.enable_auto_cleanup).size(18.0)).clicked() {
                    configurer_nettoyage_automatique();
                }
                ui.add_space(20.0);
                if ui.button(egui::RichText::new(texts.disable_auto_cleanup).size(18.0)).clicked() {
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

/// Configure et applique la police personnalisée pour egui
fn configurer_polices(ctx: &Context) {
    let mut fonts = FontDefinitions::default();
    
    // Police principale (Latine)
    fonts.font_data.insert(
        "NotoSans".to_owned(),
        FontData::from_static(include_bytes!("NotoSans-Regular.ttf")).into(),
    );

    // Police pour le chinois et japonais
    fonts.font_data.insert(
        "NotoSansCJK".to_owned(),
        FontData::from_static(include_bytes!("NotoSansCJK-Regular.ttc")).into(),
    );

    // Police pour l'arabe
    fonts.font_data.insert(
        "NotoNaskhArabic".to_owned(),
        FontData::from_static(include_bytes!("NotoNaskhArabic-Regular.ttf")).into(),
    );

    // Police pour l'hindi
    fonts.font_data.insert(
        "NotoSansDevanagari".to_owned(),
        FontData::from_static(include_bytes!("NotoSansDevanagari-Regular.ttf")).into(),
    );

   

    // Ajouter les polices de secours dans l'ordre de priorité
    if let Some(proportional) = fonts.families.get_mut(&FontFamily::Proportional) {
        proportional.insert(0, "NotoSans".to_owned());
        proportional.push("NotoSansCJK".to_owned());
        proportional.push("NotoNaskhArabic".to_owned());
        proportional.push("NotoSansDevanagari".to_owned());
 
    }

    ctx.set_fonts(fonts);
}


fn optimize_hdd() {
    let powershell_script = r#"
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
    "#;

    let output = Command::new("powershell")
        .arg("-Command")
        .arg(powershell_script)
        .output();

    match output {
        Ok(output) => {
            if let Ok(stdout) = str::from_utf8(&output.stdout) {
                println!("{}", stdout);
            } else {
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

fn ajuster_mem_virtuelle() {
    let script_powershell_get_ram = r#"
    (Get-WmiObject -Class Win32_ComputerSystem).TotalPhysicalMemory / 1MB
    "#;

    let output = Command::new("powershell")
        .arg("-Command")
        .arg(script_powershell_get_ram)
        .output()
        .expect("Échec lors de l'exécution de la commande PowerShell");

    let output_str = String::from_utf8_lossy(&output.stdout);
    let output_str = output_str.trim().replace(",", ".");
    println!("RAM détectée (brute): {}", output_str);

    let ram_mo: f64 = match output_str.parse::<f64>() {
        Ok(valeur) => valeur,
        Err(_) => {
            eprintln!("Échec lors de la conversion de la RAM en flottant.");
            return;
        }
    };

    let ram_mo: u64 = ram_mo.floor() as u64;
    let initial_size = (ram_mo as f64 * 2.5).floor() as u64;
    let max_size = (ram_mo as f64 * 3.0).floor() as u64;

    println!("Taille minimale du fichier d'échange: {} Mo", initial_size);
    println!("Taille maximale du fichier d'échange: {} Mo", max_size);

    let script_powershell_adjust_mem = format!(r#"
    $reg_path = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'
    Set-ItemProperty -Path $reg_path -Name 'PagingFiles' -Value 'C:\pagefile.sys {initial_size} {max_size}'
    $automatic_managed = Get-ItemProperty -Path $reg_path -Name 'PagingFiles'
    if ($automatic_managed -ne 'C:\pagefile.sys {initial_size} {max_size}') {{
        Set-ItemProperty -Path $reg_path -Name 'PagingFiles' -Value 'C:\pagefile.sys {initial_size} {max_size}'
    }}
    $paging_file_value = 'C:\pagefile.sys ' + {initial_size} + ' ' + {max_size}
    Set-ItemProperty -Path $reg_path -Name 'PagingFiles' -Value $paging_file_value
    Write-Output 'Mémoire virtuelle ajustée avec succès.'
    "#, initial_size=initial_size, max_size=max_size);

    let status = Command::new("powershell")
        .arg("-Command")
        .arg(script_powershell_adjust_mem)
        .status();

    match status {
        Ok(statut) if statut.success() => println!("Mémoire virtuelle ajustée avec succès."),
        Ok(statut) => eprintln!("Erreur lors de l'ajustement de la mémoire virtuelle : Code {:?}", statut.code()),
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell : {:?}", e),
    }

    let script_powershell_clear_pagefile = r#"
    $reg_path_shutdown = 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management'
    Set-ItemProperty -Path $reg_path_shutdown -Name 'ClearPageFileAtShutdown' -Value 1
    Write-Output 'ClearPageFileAtShutdown activé.'
    "#;

    let status_clear_pagefile = Command::new("powershell")
        .arg("-Command")
        .arg(script_powershell_clear_pagefile)
        .status();

    match status_clear_pagefile {
        Ok(statut) if statut.success() => println!("ClearPageFileAtShutdown activé avec succès."),
        Ok(statut) => eprintln!("Erreur lors de l'activation de ClearPageFileAtShutdown : Code {:?}", statut.code()),
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell : {:?}", e),
    }
}

fn mem_virtuelle_par_default() {
    let script_powershell_default_mem = r#"
    $computerinfo = Get-WmiObject Win32_ComputerSystem -EnableAllPrivileges
    $computerinfo.AutomaticManagedPagefile = $True
    $computerinfo.Put()
    "#;

    let status = Command::new("powershell")
        .arg("-Command")
        .arg(script_powershell_default_mem)
        .status();

    match status {
        Ok(statut) if statut.success() => println!("Gestion automatique de la mémoire virtuelle réactivée avec succès."),
        Ok(statut) => eprintln!("Erreur lors de la réactivation de la gestion automatique de la mémoire virtuelle : Code {:?}", statut.code()),
        Err(e) => eprintln!("Échec de l'exécution du script PowerShell : {:?}", e),
    }
}

fn optimiser_performances_energie() {
    let powershell_script = r#"
    $guid_best_performance = (powercfg -L | Select-String -Pattern "Ultimate Performance")
    if ($guid_best_performance) {
        $guid_best_performance_id = $guid_best_performance.ToString().Split()[3]
        powercfg -S $guid_best_performance_id
        Write-Host "Le mode 'Meilleures performances' a été activé."
    } else {
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



fn optimize_ssd() {
    let powershell_script = r#"
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
    "#;

    let status = Command::new("powershell")
        .arg("-Command")
        .arg(powershell_script)
        .stdout(Stdio::inherit())
        .stderr(Stdio::inherit())
        .status();

    match status {
        Ok(status) if status.success() => (),
        Ok(status) => eprintln!("Le script s'est terminé avec un code de sortie : {:?}", status.code()),
        Err(e) => eprintln!("Erreur lors de l'exécution du script PowerShell : {}", e),
    }
}



fn lancer_nettoyage_disque() {
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

    let status = Command::new("powershell")
        .arg("-Command")
        .arg(ps_script)
        .stdout(Stdio::inherit())
        .stderr(Stdio::inherit())
        .status();

    match status {
        Ok(status) if status.success() => (),
        Ok(status) => eprintln!("Le script s'est terminé avec un code de sortie : {:?}", status.code()),
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

fn mise_a_jour_et_analyse_securite() {
    let chemin_defender = r"C:\Program Files\Windows Defender\MpCmdRun.exe";
    let ps_script = r"Get-MpPreference | Select -ExpandProperty DisableRealtimeMonitoring";
    let output = Command::new("powershell")
        .args(&["-Command", ps_script])
        .output()
        .expect("Échec de l'exécution de PowerShell");

    let is_defender_disabled = String::from_utf8_lossy(&output.stdout).trim() == "True";

    if is_defender_disabled {
        eprintln!("Windows Defender n'est pas actif. Tentative d'utilisation d'un autre antivirus.");
        utiliser_autre_antivirus();
        return;
    }

    if !Path::new(chemin_defender).exists() {
        eprintln!("L'exécutable Windows Defender n'a pas été trouvé à : {}", chemin_defender);
        utiliser_autre_antivirus();
        return;
    }

    if let Err(e) = executer_commande_defender(&[chemin_defender, "-SignatureUpdate"]) {
        eprintln!("Échec de la mise à jour de Windows Defender : {:?}", e);
        utiliser_autre_antivirus();
        return;
    }

    if let Err(e) = executer_commande_defender(&[chemin_defender, "-Scan", "-ScanType", "1"]) {
        eprintln!("Échec de l'analyse rapide avec Windows Defender : {:?}", e);
        utiliser_autre_antivirus();
    }
}

fn utiliser_autre_antivirus() {
    let ps_script = r"Get-CimInstance -Namespace root\SecurityCenter2 -Class AntiVirusProduct | Select-Object -ExpandProperty displayName";
    let output = Command::new("powershell")
        .args(&["-Command", ps_script])
        .output()
        .expect("Échec de l'exécution de PowerShell");

    let antivirus_name = String::from_utf8_lossy(&output.stdout).trim().to_string();

    if antivirus_name.contains("Avast") {
        utiliser_avast();
    } else {
        eprintln!("Aucun autre antivirus reconnu n'est installé ou actif.");
    }
}

fn executer_commande_defender(args_cmd: &[&str]) -> Result<(), std::io::Error> {
    let statut = Command::new(args_cmd[0])
        .args(&args_cmd[1..])
        .status()?;

    match statut.code() {
        Some(0) => Ok(()),
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

fn executer_commande_avast(args_cmd: &[&str]) -> Result<(), std::io::Error> {
    let statut = Command::new(args_cmd[0])
        .args(&args_cmd[1..])
        .status()?;

    match statut.code() {
        Some(0) => {
            println!("Analyse terminée avec succès : Aucun malware trouvé.");
            Ok(())
        }
        Some(1) => {
            println!("Malware détecté et supprimé avec succès.");
            Ok(())
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

fn utiliser_avast() {
    let chemin_avast_quick_scan = r"C:\Program Files\AVAST Software\Avast\ashQuick.exe";
    let chemin_avast_update = r"C:\Program Files\AVAST Software\Avast\ashUpd.exe";

    if !Path::new(chemin_avast_update).exists() {
        eprintln!("Le chemin spécifié pour la mise à jour Avast n'existe pas : {}", chemin_avast_update);
        return;
    }

    if let Err(e) = executer_commande_avast(&[chemin_avast_update, "vps"]) {
        eprintln!("Échec de la mise à jour des définitions de l'antivirus Avast : {:?}", e);
    }

    if !Path::new(chemin_avast_quick_scan).exists() {
        eprintln!("Le chemin spécifié pour l'analyse rapide Avast n'existe pas : {}", chemin_avast_quick_scan);
        return;
    }

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




fn lancer_mise_a_jour_windows() {
    let ps_script = r#"
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
    "#;

    let status = Command::new("powershell")
        .arg("-Command")
        .arg(ps_script)
        .stdout(Stdio::inherit())
        .stderr(Stdio::inherit())
        .status();

    match status {
        Ok(status) if status.success() => (),
        Ok(status) => eprintln!("Le script s'est terminé avec un code de sortie : {:?}", status.code()),
        Err(e) => eprintln!("Erreur lors de l'exécution du script de mise à jour Windows : {:?}", e),
    }
}


