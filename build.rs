extern crate winres;

fn main() {
    if cfg!(target_os = "windows") {
        let mut res = winres::WindowsResource::new();
        // Ajout du manifeste pour les droits d'administrateur
        res.set_manifest(r#"
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
                <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
                    <security>
                        <requestedPrivileges>
                            <requestedExecutionLevel level="requireAdministrator" uiAccess="false"/>
                        </requestedPrivileges>
                    </security>
                </trustInfo>
            </assembly>
        "#);
        // Définition de l'icône de l'application
        res.set_icon("app.ico");  // Remplacez "path/to/your/app.ico" par le chemin réel de votre fichier icône

        // Compilation des ressources
        res.compile().expect("Erreur lors de la compilation des ressources!");
    }
}
