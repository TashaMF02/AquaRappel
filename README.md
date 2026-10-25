# AquaRappel

Application mobile de bien-être : **hydratation, sommeil et pauses écran**, avec rappels en notifications, compte sécurisé et synchronisation avec un serveur.

| | |
|---|---|
| **Étudiante** | MOMO FOUMTHIM GÉORGIE ANGELA NATACHA |
| **Classe / matricule** | GIT-GLO5 · 22G00264 |
| **Contact** | natachamomo02@gmail.com |
| **Cours** | Atelier de développement web mobile |
| **Technologies** | .NET MAUI (net9.0-android) · ASP.NET Core 9 · SQLite |

> **À l'attention de l'enseignant.** Ce document explique comment installer, lancer et tester AquaRappel, puis résume l'architecture, la sécurité et les limites. Le rapport PDF de 2 pages complète ce dossier.

---

## 1. Ce que fait l'application

- **Eau** : objectif quotidien réglable, saisie rapide (+150 / +250 / +500 ml, saisie libre), jauge de verres (goutte pleine quand on a bu), conseil météo selon la température de Douala (API Open-Meteo).
- **Rappels** (boire, pause écran, coucher) : on touche la carte pour régler **librement** la fréquence (de 1 minute à 24 h), la **plage horaire** et les **jours actifs** de chaque rappel. Ce sont de vraies **notifications**, reçues même application fermée.
- **Sommeil** : heures de coucher et de réveil, durée calculée, qualité (Mal / Bof / Top).
- **Pauses écran** : règle 20-20-20, compteur de pauses du jour.
- **Statistiques** : 7 derniers jours, moyenne, jours où l'objectif est atteint.
- **Compte** : inscription / connexion, session mémorisée, déconnexion.
- **Mode sombre** (interrupteur sur l'accueil), police Bookman, icônes dessinées.
- **Synchronisation** automatique avec le serveur (aucun bouton : elle se fait après un changement et à l'ouverture).

## 2. Contenu du dépôt

```
AquaRappel/                  Application .NET MAUI (Android)
  Models/                    données (comptes, eau, sommeil, pauses, réglages)
  Views/                     pages XAML (aucune logique métier)
  ViewModels/                logique des écrans (MVVM)
  Services/                  données chiffrées, sécurité, API météo, API serveur,
                             synchronisation, thèmes, dialogues, rappels
  Helpers/Mvvm.cs            ObservableObject, commandes (sans bibliothèque)
  Platforms/Android/         notifications en arrière-plan (AlarmManager), manifeste
AquaRappel_Server/           Serveur ASP.NET Core + base SQLite
  Program.cs                 API REST
  BASE_DE_DONNEES.md         schéma de la base
```

## 3. Prérequis

| Pour tester… | Il faut |
|---|---|
| L'application | Visual Studio 2022/2026 avec la charge **« Développement d'applications mobiles avec .NET »**, SDK **.NET 9**, un émulateur Android (Pixel 7, API 35 conseillé) **ou** un téléphone Android avec le débogage USB |
| Le serveur | SDK **.NET 9** (déjà inclus avec Visual Studio). **Internet requis une seule fois** pour télécharger le paquet `Microsoft.Data.Sqlite` |

## 4. Lancer le serveur

Dans PowerShell, depuis le dossier `AquaRappel_Server` :

```powershell
dotnet run --urls http://0.0.0.0:5080
```

Vérification : ouvrir `http://localhost:5080/api/health` → `{"status":"ok", ...}`.
La base de données `data/aquarappel.db` est créée automatiquement au premier lancement (ouvrable avec *DB Browser for SQLite*). **Laisser cette fenêtre ouverte** pendant les tests.

## 5. Lancer l'application

### Option A : émulateur Android (le plus simple)
1. Ouvrir `AquaRappel.sln` dans Visual Studio, choisir un **émulateur Android** comme cible, puis F5.
2. Le serveur est joint par défaut à l'adresse `http://10.0.2.2:5080` (c'est l'ordinateur vu depuis l'émulateur) : **rien à régler**.

### Option B : téléphone Android (même Wi-Fi que l'ordinateur)
1. Sur l'ordinateur : `ipconfig`, noter l'**adresse IPv4 de la carte Wi-Fi** (ex. `192.168.1.142`).
2. Dans `Platforms/Android/Resources/xml/network_security_config.xml`, remplacer `192.168.1.10` par cette adresse, puis recompiler. (HTTP n'est autorisé que pour les adresses de test listées dans ce fichier ; tout le reste exige HTTPS.)
3. Autoriser le port dans le pare-feu Windows (PowerShell en administrateur) :
   `New-NetFirewallRule -DisplayName "AquaRappel" -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow -Profile Private,Public`
4. Dans l'appli, sur la page de connexion, **toucher 5 fois rapidement le logo** (mode développeur) : le champ *Adresse du serveur* apparaît. Saisir `http://192.168.1.142:5080`.

> Si l'appli ne démarre pas : désinstaller l'ancienne version, supprimer les dossiers `bin` et `obj`, nettoyer, régénérer. La cible de lancement doit être Android (pas Windows).

## 6. Parcours de test conseillé (≈ 5 minutes)

1. **Inscription** : créer un compte (prénom, email, mot de passe de **8 caractères minimum avec au moins une lettre et un chiffre**). Au premier lancement, **accepter l'autorisation « Notifications »**.
2. **Accueil** : ajouter de l'eau (+250 ml) → les gouttes se remplissent. Basculer le **mode sombre**.
3. **Rappels** : toucher « Boire de l'eau » → régler par exemple **toutes les 1 min**, plage **00:00 → 23:59**, tous les jours → Enregistrer. **Quitter l'application** (sans la fermer de force) : une notification arrive.
   *Les alarmes Android sont « non exactes » : avec l'écran éteint, un retard de quelques minutes est normal. Sur Xiaomi/HyperOS, activer « Démarrage automatique » et « Aucune restriction » de batterie pour l'application.*
4. **Sommeil / Pauses / Stats** : enregistrer une nuit, une pause, consulter les statistiques.
5. **Synchronisation** : ouvrir la base `aquarappel.db` → les tables `water`, `sleep`, `breaks`, `settings` contiennent les données de l'utilisateur. Se connecter avec le même compte depuis un autre appareil : les données sont récupérées.
6. **Sécurité** : 5 mots de passe erronés → blocage de 5 minutes ; serveur arrêté → l'appli reste utilisable hors ligne.

## 7. Architecture

- **MVVM sans bibliothèque externe** : les vues ne contiennent que du *binding* et des commandes ; chaque écran a son ViewModel ; les services (données, sécurité, météo, API, synchronisation, dialogues, navigation, rappels) sont injectés par `MauiProgram.cs`.
- **Hors ligne d'abord** : les données sont stockées localement (fichier chiffré) ; le serveur fusionne sans jamais supprimer de données (réglages : le plus récent l'emporte).
- **Serveur** : API REST (inscription, connexion, déconnexion, synchronisation, suppression de compte) et base SQLite relationnelle (6 tables, clés étrangères, contraintes `CHECK`) — voir `AquaRappel_Server/BASE_DE_DONNEES.md`.

| Méthode | Route | Rôle |
|---|---|---|
| GET | `/api/health` | état du serveur |
| POST | `/api/auth/register`, `/api/auth/login` | création de compte, connexion → jeton |
| POST | `/api/auth/logout` | invalide le jeton |
| POST / GET | `/api/sync` | synchronisation des données (Bearer) |
| DELETE | `/api/account` | supprime le compte et toutes ses données (Bearer) |

## 8. Sécurité

- Mots de passe : **PBKDF2-SHA256, 100 000 itérations, sel aléatoire**, comparaison à temps constant (téléphone **et** serveur) ; jamais stockés en clair.
- Données locales chiffrées en **AES-GCM**, clé dans le stockage sécurisé Android ; session dans le coffre sécurisé ; sauvegarde automatique Android désactivée.
- Jetons de session aléatoires de 256 bits (le serveur ne garde que leur SHA-256), 30 jours, 5 appareils maximum.
- Blocage temporaire après 5 échecs (téléphone et serveur), message d'erreur identique pour un email inconnu et un mauvais mot de passe.
- Serveur : limitation à 120 requêtes/min/IP, corps limité à 1 Mo, validation et plafonnement de toutes les entrées, requêtes SQL paramétrées.
- Réseau : HTTPS obligatoire côté Android, sauf les adresses de test déclarées pour le serveur local.

## 9. Éco-conception

Mode sombre (écrans OLED) · alarmes du système plutôt qu'un service permanent · synchronisation uniquement après un changement, limitée aux 120 derniers jours · un appel météo au maximum toutes les 30 minutes (délai 6 s, repli hors ligne) · polices Bookman réduites aux caractères latins (30 Ko chacune) · aucune bibliothèque tierce côté application, aucun pistage ni publicité.

## 10. Limites connues 

- Le serveur de démonstration tourne sur l'ordinateur en **HTTP local** ; une mise en production demanderait un hébergement **HTTPS**.
- Les notifications sont planifiées par l'application (alarmes locales) : elles ne sont pas « poussées » par le serveur (pas de Firebase).
- Les alarmes Android étant non exactes, les rappels peuvent être retardés quand le téléphone est en veille profonde.
- Version Android uniquement.
- Stockage SQLite sur un seul serveur (PostgreSQL à prévoir pour de nombreux utilisateurs).

## 11 Appareils de support de test

J'ai testé l'application sur un émulateur Android (Pixel 7, API 35) et sur un téléphone réel (Xiaomi, Android 15), adapté les écrans.

## 12. Licences et sources

- Police **URW Bookman** : GNU AGPL v3 avec exception police (ArtifexSoftware/urw-base35-fonts).
- Données météo : **Open-Meteo** (https://open-meteo.com), licence CC BY 4.0.
- Paquet serveur : Microsoft.Data.Sqlite (licence MIT).
- Maquettes : Figma (8 écrans).
