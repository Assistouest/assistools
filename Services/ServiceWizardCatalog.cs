using System.Collections.Generic;

namespace Assistools.Services;

public sealed record ServiceEntry(string Name, string Friendly, string Description);

public sealed record WizardCategory(
    string Key,
    string Icon,           // FontIcon glyph (Segoe MDL2)
    string Question,       // "Utilisez-vous une imprimante ?"
    string IfYesText,      // ce qui se passe si "Oui"
    string IfNoText,       // ce qui se passe si "Non"
    ServiceEntry[] Services);

// Catalogue centralisé des catégories du wizard.
// Convention de rédaction :
//   - IfYes = ce qu'il advient des SERVICES si l'utilisateur répond Oui (généralement : conservés)
//   - IfNo  = ce qu'il advient si l'utilisateur répond Non (généralement : désactivés + impact)
public static class ServiceWizardCatalog
{
    public static readonly WizardCategory[] All = new[]
    {
        new WizardCategory("printer",
            "",   // Print
            "Utilisez-vous une imprimante (locale, USB ou réseau) ?",
            "Les services d'impression sont conservés. Vous pourrez continuer à imprimer normalement.",
            "Les services d'impression sont désactivés. Vous ne pourrez plus imprimer tant que vous ne restaurez pas.",
            new[] {
                new ServiceEntry("Spooler",     "Spouleur d'impression",          "Gère la file d'attente d'impression."),
                new ServiceEntry("Fax",         "Fax",                            "Envoie et reçoit des fax (rarement utilisé)."),
                new ServiceEntry("PrintNotify", "Notifications d'imprimante",     "Affiche les notifications liées aux imprimantes."),
            }),

        new WizardCategory("bluetooth",
            "",   // Bluetooth
            "Utilisez-vous des appareils Bluetooth (casque, souris, clavier, manette) ?",
            "Les services Bluetooth sont conservés. Vos appareils continueront à se connecter normalement.",
            "Les services Bluetooth sont désactivés. Aucun appareil Bluetooth ne pourra se connecter à ce PC.",
            new[] {
                new ServiceEntry("bthserv",     "Service Bluetooth",              "Pilote principal Bluetooth."),
                new ServiceEntry("BTAGService", "Passerelle audio Bluetooth",     "Nécessaire pour les appels via casque BT."),
                new ServiceEntry("BthAvctpSvc", "AVCTP Bluetooth",                "Transport audio/vidéo Bluetooth (casques A2DP)."),
            }),

        new WizardCategory("xbox",
            "",   // Xbox logo / controller
            "Jouez-vous à des jeux Xbox / utilisez-vous le compte Xbox Live sur ce PC ?",
            "Les services Xbox Live sont conservés. Vos succès, sauvegardes cloud et multijoueur continueront à fonctionner.",
            "Les services Xbox Live sont désactivés. Le Game Pass, les succès et le multijoueur Xbox seront indisponibles.",
            new[] {
                new ServiceEntry("XblAuthManager", "Xbox Live - Authentification",  "Connexion au compte Xbox Live."),
                new ServiceEntry("XblGameSave",    "Xbox Live - Sauvegardes",       "Sauvegardes de parties dans le cloud Xbox."),
                new ServiceEntry("XboxNetApiSvc",  "Xbox Live - Mise en réseau",    "Multijoueur et chat Xbox Live."),
                new ServiceEntry("XboxGipSvc",     "Xbox - Accessoires",            "Pilotes pour manettes Xbox connectées."),
            }),

        new WizardCategory("biometric",
            "",   // Fingerprint
            "Utilisez-vous Windows Hello biométrique (empreinte digitale ou reconnaissance faciale) ?",
            "Le service biométrique est conservé. Votre connexion par empreinte ou par visage continuera à fonctionner.",
            "Le service biométrique est désactivé. Vous devrez vous connecter avec un code PIN ou un mot de passe.",
            new[] {
                new ServiceEntry("WbioSrvc", "Service biométrique Windows", "Lit les capteurs d'empreinte et de reconnaissance faciale."),
            }),

        new WizardCategory("touch",
            "",   // TouchPointer
            "Avez-vous un écran tactile, un PC 2-en-1, ou utilisez-vous un stylet ?",
            "Le service de saisie tactile est conservé. Le clavier tactile et la reconnaissance d'écriture restent disponibles.",
            "Le service de saisie tactile est désactivé. Pas d'impact si votre PC n'a pas d'écran tactile.",
            new[] {
                new ServiceEntry("TabletInputService", "Clavier tactile et stylet", "Affiche le clavier tactile et gère la saisie manuscrite."),
            }),

        new WizardCategory("search",
            "",   // Search
            "Utilisez-vous la recherche Windows (loupe pour trouver des fichiers et des apps) ?",
            "Le service de recherche est conservé. L'indexation continue, vos fichiers se trouvent instantanément.",
            "Le service de recherche est désactivé. Les recherches Windows seront beaucoup plus lentes, mais le menu Démarrer fonctionne toujours.",
            new[] {
                new ServiceEntry("WSearch", "Windows Search", "Indexe vos fichiers pour des recherches instantanées."),
            }),

        new WizardCategory("maps",
            "",   // MapPin
            "Utilisez-vous l'application Cartes Windows ou des fonctions de géolocalisation (météo locale, position) ?",
            "Les services de cartes et géolocalisation sont conservés. Les apps continueront à connaître votre position.",
            "Les services de cartes et géolocalisation sont désactivés. Les apps qui demandent votre position ne l'obtiendront plus.",
            new[] {
                new ServiceEntry("MapsBroker", "Téléchargement de cartes",    "Met à jour les cartes hors ligne."),
                new ServiceEntry("lfsvc",      "Service de géolocalisation",  "Fournit la position aux applications."),
            }),

        new WizardCategory("ics",
            "",   // MobileHotspot
            "Partagez-vous la connexion Internet de ce PC vers d'autres appareils (point d'accès Wi-Fi mobile) ?",
            "Le service de partage de connexion est conservé. Ce PC peut continuer à servir de point d'accès.",
            "Le service de partage de connexion est désactivé. Ce PC ne pourra plus partager sa connexion à d'autres appareils.",
            new[] {
                new ServiceEntry("SharedAccess", "Partage de connexion (ICS)", "Permet à ce PC de partager sa connexion."),
            }),

        new WizardCategory("smartcard",
            "",   // ContactCard / badge
            "Utilisez-vous une carte à puce (carte CPS pour la santé, eID, badge d'entreprise) ?",
            "Les services de carte à puce sont conservés. Votre lecteur de carte continuera à être détecté.",
            "Les services de carte à puce sont désactivés. Aucun lecteur de carte ne sera reconnu sur ce PC.",
            new[] {
                new ServiceEntry("SCardSvr",      "Carte à puce",                "Lit les cartes à puce insérées."),
                new ServiceEntry("ScDeviceEnum",  "Énumération cartes à puce",   "Détecte les lecteurs de cartes."),
                new ServiceEntry("SCPolicySvc",   "Stratégie carte à puce",      "Applique les règles de retrait de carte."),
            }),

        new WizardCategory("phone",
            "",   // Phone
            "Liez-vous votre smartphone à Windows (app Mobile connecté / Phone Link) ?",
            "Les services de téléphonie sont conservés. La liaison entre votre PC et votre téléphone reste active.",
            "Les services de téléphonie sont désactivés. Plus d'appels, SMS ni notifications du téléphone sur ce PC.",
            new[] {
                new ServiceEntry("PhoneSvc", "Service téléphonie",   "Gère les appels téléphoniques sur PC."),
                new ServiceEntry("TapiSrv",  "Téléphonie (TAPI)",    "API téléphonique historique de Windows."),
            }),

        new WizardCategory("rdp",
            "",   // Remote desktop screen
            "Vous connectez-vous à ce PC à distance via le Bureau à distance (RDP) ?",
            "Les services Bureau à distance sont conservés. Vous pourrez toujours vous connecter à ce PC depuis ailleurs.",
            "Les services Bureau à distance sont désactivés. Plus aucune connexion RDP entrante vers ce PC ne sera acceptée.",
            new[] {
                new ServiceEntry("TermService",     "Services Bureau à distance",        "Accepte les connexions RDP entrantes."),
                new ServiceEntry("SessionEnv",      "Configuration Bureau à distance",   "Configure l'environnement RDP."),
                new ServiceEntry("UmRdpService",    "Redirection RDP",                   "Redirige imprimantes et presse-papiers via RDP."),
            }),

        new WizardCategory("hyperv",
            "",   // Processing / chip
            "Utilisez-vous Hyper-V pour faire tourner des machines virtuelles, WSL2 ou Docker Desktop ?",
            "Les services Hyper-V sont conservés. Vos VM, WSL2 et conteneurs continueront à démarrer.",
            "Les services Hyper-V sont désactivés. Vous ne pourrez plus lancer de VM, ni utiliser WSL2 ou Docker Desktop.",
            new[] {
                new ServiceEntry("vmcompute", "Service VM Hyper-V",              "Moteur de calcul des VM Hyper-V."),
                new ServiceEntry("vmms",      "Gestion des machines virtuelles", "Crée et démarre les VM Hyper-V."),
                new ServiceEntry("HvHost",    "Hyper-V Host",                    "Service hôte de virtualisation."),
            }),

        new WizardCategory("connected",
            "",   // Sync
            "Synchronisez-vous votre activité entre ce PC et d'autres appareils Microsoft (Surface, autre PC, Edge) ?",
            "Les services de synchronisation inter-appareils sont conservés. Vos activités et timeline continueront à se synchroniser.",
            "Les services de synchronisation inter-appareils sont désactivés. Les apps Microsoft synchroniseront moins de choses entre vos appareils.",
            new[] {
                new ServiceEntry("CDPSvc",     "Plateforme appareils connectés", "Découverte et sync entre appareils Microsoft."),
                new ServiceEntry("NcbService", "Network Connection Broker",      "Notifications réseau pour apps Store."),
            }),

        new WizardCategory("insider",
            "",   // PreviewLink / flag
            "Êtes-vous inscrit au programme Windows Insider (versions de test de Windows) ?",
            "Le service Insider est conservé. Vous continuerez à recevoir les builds de test.",
            "Le service Insider est désactivé. Aucun impact si vous restez sur la version stable de Windows.",
            new[] {
                new ServiceEntry("wisvc", "Windows Insider Service", "Gère l'inscription Insider et les builds de test."),
            }),

        new WizardCategory("alljoyn",
            "",   // IOT / connected hub  (fallback: flag)
            "Utilisez-vous des objets connectés IoT compatibles AllJoyn (très rare en usage bureau ou domestique) ?",
            "Le routeur AllJoyn est conservé. La communication avec vos objets IoT reste possible.",
            "Le routeur AllJoyn est désactivé. Aucun impact sur 99 % des usages : ce protocole IoT est presque jamais utilisé.",
            new[] {
                new ServiceEntry("AJRouter", "Routeur AllJoyn", "Communication avec objets IoT en proximité."),
            }),

        new WizardCategory("retaildemo",
            "",   // Shop / bag
            "Ce PC est-il un modèle de démonstration installé en magasin (vitrine de revendeur) ?",
            "Le service de démonstration est conservé pour permettre la présentation en boutique.",
            "Le service de démonstration est désactivé. Aucun impact sur un PC personnel ou professionnel.",
            new[] {
                new ServiceEntry("RetailDemo", "Mode démo magasin", "Active la démonstration en boutique."),
            }),

        new WizardCategory("sysmain",
            "",   // HardDrive
            "Votre disque principal est-il un HDD (disque mécanique), et non un SSD ?",
            "SuperFetch est conservé. Recommandé sur HDD : précharge les apps fréquentes en RAM pour les ouvrir plus vite.",
            "SuperFetch est désactivé. Recommandé sur SSD : évite les écritures inutiles et libère de la RAM, sans ralentissement notable.",
            new[] {
                new ServiceEntry("SysMain", "SysMain (SuperFetch)", "Précharge les apps fréquentes en RAM."),
            }),
    };
}