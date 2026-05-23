; ─────────────────────────────────────────────────────────────────────────────
; Assistools — Script Inno Setup 7
; Génère : Assistools-Setup-3.0.0.exe
; Prérequis : publier d'abord avec
;   dotnet publish Assistools.csproj -c Release -r win-x64 --self-contained true
;              -o publish\win-x64 -p:Platform=x64
; ─────────────────────────────────────────────────────────────────────────────

#define AppName      "Assistools"
#define AppVersion   "3.0.0"
#define AppPublisher "Assistouest Informatique"
#define AppURL       "https://assistouest.fr"
#define AppExeName   "Assistools.exe"
#define SourceDir    "publish\win-x64"
#define TaskName     "AssistoolsStartup"

; ── Paramètres généraux ──────────────────────────────────────────────────────
[Setup]
AppId={{8D7E4B1F-3C9A-4A2E-B7D1-E5F2A8B3C4D5}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppCopyright=© 2026 {#AppPublisher}

; Installation dans Program Files (64-bit)
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; Droits admin requis (l'app en a besoin pour ses optimisations)
PrivilegesRequired=admin

; Sortie
OutputDir=installer
OutputBaseFilename=Assistools-Setup-{#AppVersion}
SetupIconFile=Assets\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}

; Compression maximale
Compression=lzma2/ultra64
SolidCompression=yes

; Style moderne Inno Setup 7
WizardStyle=modern

; Restrictions plateforme
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; ── Langues ──────────────────────────────────────────────────────────────────
[Languages]
Name: "french";  MessagesFile: "compiler:Languages\French.isl"

; ── Options de l'assistant ───────────────────────────────────────────────────
[Tasks]
; Icône Bureau (décochée par défaut)
Name: "desktopicon"; \
  Description: "Créer un raccourci sur le Bureau"; \
  GroupDescription: "Options supplémentaires :"; \
  Flags: unchecked

; Lancer au démarrage de Windows (décochée par défaut)
; Implémenté via une tâche planifiée avec élévation → pas de popup UAC au boot
Name: "startup"; \
  Description: "Lancer Assistools automatiquement au démarrage de Windows"; \
  GroupDescription: "Options supplémentaires :"; \
  Flags: unchecked

; ── Fichiers ─────────────────────────────────────────────────────────────────
[Files]
; Exécutable principal (en premier pour l'affichage de progression)
Source: "{#SourceDir}\{#AppExeName}"; \
  DestDir: "{app}"; Flags: ignoreversion

; Tous les autres fichiers (DLLs, Assets, ressources WinUI)
Source: "{#SourceDir}\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs; \
  Excludes: "*.pdb,*.xml,createdump.exe"

; ── Raccourcis ───────────────────────────────────────────────────────────────
[Icons]
; Menu Démarrer
Name: "{group}\{#AppName}";             Filename: "{app}\{#AppExeName}"
Name: "{group}\Désinstaller {#AppName}"; Filename: "{uninstallexe}"

; Bureau (optionnel)
Name: "{autodesktop}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  Tasks: desktopicon

; ── Démarrage Windows via Tâche planifiée ────────────────────────────────────
; Avantage vs clé Run : s'exécute avec les droits admin sans popup UAC
[Run]
; Créer la tâche planifiée si "startup" est coché
Filename: "schtasks.exe"; \
  Parameters: "/Create /F /TN ""{#TaskName}"" /TR """"""{app}\{#AppExeName}"""""" /SC ONLOGON /RL HIGHEST /DELAY 0000:30"; \
  Flags: runhidden waituntilterminated; \
  Tasks: startup

; Proposer de lancer l'application à la fin de l'installation
Filename: "{app}\{#AppExeName}"; \
  Description: "Lancer {#AppName}"; \
  Flags: nowait postinstall skipifsilent runascurrentuser

; ── Nettoyage à la désinstallation ───────────────────────────────────────────
[UninstallRun]
; Supprimer la tâche planifiée (si elle existe)
Filename: "schtasks.exe"; \
  Parameters: "/Delete /TN ""{#TaskName}"" /F"; \
  Flags: runhidden waituntilterminated; \
  RunOnceId: "DeleteStartupTask"

; ── Nettoyage fichiers résiduels ─────────────────────────────────────────────
[UninstallDelete]
; Dossier complet (logs, configs générés à l'exécution)
Type: filesandordirs; Name: "{app}"

; ── Code Pascal — vérifications préalables ───────────────────────────────────
[Code]
// Vérifie que Windows 10 1809 ou supérieur est installé
function InitializeSetup(): Boolean;
var
  Ver: TWindowsVersion;
begin
  GetWindowsVersionEx(Ver);
  if (Ver.Major < 10) or ((Ver.Major = 10) and (Ver.Build < 17763)) then
  begin
    MsgBox(
      'Assistools nécessite Windows 10 version 1809 (build 17763) ou supérieur.' + #13#10 +
      'Veuillez mettre à jour Windows avant de continuer.',
      mbError, MB_OK
    );
    Result := False;
  end
  else
    Result := True;
end;
