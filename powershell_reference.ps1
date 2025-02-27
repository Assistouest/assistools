# ==================================================================================
# Ce fichier est une référence pour l'utilisation de PowerShell dans ce projet Rust.
# ==================================================================================
#
# GitHub Linguist analyse automatiquement les langages présents dans un dépôt
# pour afficher les statistiques de répartition des langages utilisés. Toutefois,
# dans ce projet, les scripts PowerShell sont directement imbriqués dans le code Rust,
# ce qui empêche leur détection automatique.
#
# Ce fichier a été ajouté afin de :
# - Rendre explicite l'utilisation de PowerShell dans le projet.
# - Aider GitHub à détecter PowerShell comme un langage utilisé dans ce repo.
# - Servir de documentation sur la manière dont PowerShell est employé ici.
#
# 🔹 Comment PowerShell est-il utilisé dans ce projet ?
# - Il est exécuté dynamiquement via Rust, par exemple en générant et exécutant 
#   des commandes PowerShell depuis du code Rust.
# - Il peut être utilisé pour automatiser certaines tâches comme les mises à jour,
#   la configuration du système ou l'exécution de scripts administratifs sous Windows.
#
# Ce fichier n'est pas destiné à être exécuté directement. Il est présent uniquement 
# pour assurer la reconnaissance de PowerShell par GitHub Linguist et servir de 
# documentation pour les développeurs.
#
# ==================================================================================

Write-Host "Ce script sert uniquement de référence pour la détection de PowerShell dans ce projet Rust."
