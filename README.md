# NAS Backup Manager

Application WPF .NET 8 de scan et sauvegarde de fichiers vidéo depuis deux sources NAS vers une destination de sauvegarde.

## Prérequis

- Windows 10 ou Windows 11
- SDK .NET 8

## Lancer dans Visual Studio

1. Créer un dossier `NasBackupManager`
2. Ajouter les 11 fichiers du projet
3. Ouvrir `NasBackupManager.csproj`
4. Restaurer les packages NuGet
5. Compiler avec `Ctrl + Shift + B`
6. Démarrer avec `F5`

## Ligne de commande

```powershell
dotnet restore
dotnet build
dotnet run
```

## Publier une version portable

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish
```

Le dossier `publish` contient l'application distribuable.

## Fonctions incluses

- Sélection de NAS 1, NAS 2 et dossier de sauvegarde
- Scan récursif résilient aux erreurs d'accès
- Extensions vidéo : mkv, mp4, avi, mov, m4v, wmv et ts
- Parsing de titres de type FileBot : titre, année, résolution, codec, groupe
- Groupement de doublons par titre normalisé + année
- Identification des médias absents de la sauvegarde
- File de copie préparée automatiquement
- Dry Run par défaut
- Copie atomique via fichier `.partial`
- Vérification de la taille après copie
- Relance des opérations en échec
- Onglet diagnostic détaillé