# Réparation et Maintenance Windows en un clic

**Assistools** est un logiciel gratuit, open-source conçu pour nettoyer, réparer et optimiser les systèmes d'exploitation Windows (10 et 11) en toute simplicité. Développé par [Assistouest Informatique](https://assistouest.fr/), cet utilitaire s'adresse aussi bien aux débutants souhaitant entretenir leur PC qu'aux professionnels et techniciens informatiques.

---

## Fonctionnalités Principales

Assistools repose sur une interface minimaliste proposant trois axes majeurs : le **Mode Auto**, les **Outils Avancés** et le **Booster**.

* **Nettoyage Profond** : Suppression des fichiers temporaires, vidage de la corbeille, purge des caches navigateurs et du cache *Windows Update* pour libérer de l'espace disque de manière sécurisée.
* **Réparation Système** : Lancement automatisé des outils de diagnostic officiels de Microsoft (`SFC` et `DISM`) pour vérifier l'intégrité de Windows, identifier et réparer automatiquement les fichiers systèmes corrompus.
* **Optimisation Matérielle** : Adaptation automatique selon le type de stockage détecté. Défragmentation ciblée pour les disques mécaniques (HDD) et exécution de la commande `ReTrim` pour préserver et accélérer les disques SSD. Ajustement de la mémoire virtuelle.
* **Sécurité et Mises à jour** : Relance et vérification des services *Windows Update*, forçage de la mise à jour des signatures de *Windows Defender* (ou *Avast*) et exécution d'une analyse de sécurité rapide.
* **Sécurité intégrée** : Création automatique d'un point de restauration système avant chaque action critique.

## Téléchargement et Installation

⚠️ **Note Importante :** Ce dépôt GitHub contient **uniquement le code source** du projet.

Pour télécharger la version installable (la plus récente) d'Assistools pour votre PC, rendez-vous sur notre site officiel :

👉 **[Télécharger Assistools (Site Officiel)](https://assistouest.fr/logiciel-maintenance-informatique/)**

1. Rendez-vous sur la page officielle via le lien ci-dessus.
2. Téléchargez le programme d'installation.
3. Exécutez l'installeur et suivez les instructions à l'écran.
4. Lancez Assistools en mode administrateur pour profiter du **Mode Auto** (maintenance complète en 1 clic) ou des actions spécifiques !

> ⚠️ **Note de sécurité :** Assistools modifie des clés de registre, exécute des scripts PowerShell et interagit profondément avec le système pour effectuer ses réparations. De plus, le code n'étant pas signé numériquement par un certificat annuel coûteux, il est probable que **Windows SmartScreen** ou votre antivirus (comme Windows Defender) affiche un avertissement préventif. Il s'agit d'un comportement standard pour les nouveaux outils de maintenance non signés. Le code source est entièrement ouvert et auditable ici-même.

## Développement (Code Source)

Assistools est principalement développé en **C# (WPF / .NET)**.

## Licence

Ce projet est distribué de manière **Open-Source** et 100% gratuite. 

## À propos du Projet

Créé et maintenu par **Adrien Piron** de [Assistouest Informatique](https://assistouest.fr/), service de dépannage et maintenance informatique situé à Nantes.

* 🌐 [Présentation officielle du logiciel Assistools](https://assistouest.fr/logiciel-maintenance-informatique/)
* 📰 [Tutoriels et actualités](https://assistouest.fr/blog/)
