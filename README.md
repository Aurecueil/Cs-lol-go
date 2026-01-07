# cslol-go
`LtMAO-hai` is my lol custom skin manager.

<img src="https://github.com/Aurecueil/Temp/blob/main/images/app1.png?raw=true" width="800">

# Installation
1. Download: [0.2.1.zip](https://github.com/Aurecueil/Cs-lol-go/releases/download/0.2.1/0.2.1.zip)
2. Extract: `0.2.1.zip`
3. Run `ModLoader.exe`

<img src="https://github.com/Aurecueil/Temp/blob/main/images/exe1.png?raw=true" width="520">

# Updates
1. cslolgo always check for latest version when you open the app. If there is a newer version, the app will display message box.
2. To fully update: re-download the whole app from github and extract it to same location.

# Documentation

## cslolgo
Just `cslol-manager`, but more features and options.

<img src="https://github.com/Aurecueil/Temp/blob/main/images/manager.png?raw=true" width="800">


## modtiles

To enable the mod, check checkmark on the left.

Options on right are in order:
- Topaz mod fixer
- Export
- Edit
- Delete

![](https://github.com/Aurecueil/Temp/blob/main/images/modtile.png)


## Searchbar
In searchbar u can use following to better filter search results.

Following tree must be at the very start of search query:
- `-g` (global) to show ALL installed mods
- `-f` (flat) to show ALL mods for current mods (lol/tft)
- `-l` (local) to show all mods in current folder and subfolders (recursive)

Other search options: (keyword can be partial)
- `name:` eg `name:Spirit Blossom` or `nam:Spirit Blossom` or `na:Spirit Blossom` or `n:Spirit Blossom`
- `author:` eg `a:moga`
- `wad:` eg `w:aurora`

Logic Statements:
- `||` OR statement, eg. `Briar || Zoe`

## Cli
When app is running, u can call the exe with commands to execute actions.
This will bring app to front.
Actions are executed in preset order.
- `--start` to start loader
- `--sp` to Select (change) selected profile
- `--stop` to stop loader
- `--dbu` to prevent app from being brought to front

## Features
- Folders
- Random Mods
- Mod Fixer
- Modpkg Import/Export
- Mod Thumbnails
- Runeforge Protocol Downloads
- Instant Profiles
- Customizable Modtiles size and ratio

# Credits:
- [Divine skins (HUGE help)](https://divineskins.gg/)
- [League Toolkit](https://github.com/LeagueToolkit)
- [Jade's RitoBin](https://github.com/RitoShark/Jade-League-Bin-Editor)
- [Manifest Downloader](https://github.com/Morilli/ManifestDownloader)
