<p align="center">
  <img src="https://github.com/Aurecueil/Temp/blob/main/images/cslolgo_0.2.2.png?raw=true" width="800" >
</p>

# cslol-go
My LoL custom skin manager. Like [`cslol-manager`](https://github.com/LeagueToolkit/cslol-manager), but with more features and options.


## Features
- Customizable UI
- Mod Folders
- Random mod selection
- [Runeforge](https://www.runeforge.dev/) one-click downloads
- [Modpkg](https://github.com/LeagueToolkit/league-mod) import/export
- Mod fixer
- Mod thumbnails
- Instant profiles

# Installation

<table align="center">
  <tr>
    <td>
      <p> 
        
1. Download: [0.2.1.zip](https://github.com/Aurecueil/Cs-lol-go/releases/download/0.2.1/0.2.1.zip)
2. Extract: `0.2.1.zip`
3. Run `ModLoader.exe`

      </p>
    </td>
    <td><img src="https://github.com/Aurecueil/Temp/blob/main/images/exe1.png?raw=true" width="520"></td>
  </tr>
</table>

# Updates
cslol-go automatically checks for the latest version when you open the app. If there is a newer version, the app will display a message box.

To update: download the latest release from GitHub and extract it to the same location (overwriting existing files).

# Usage
## Mod Tiles
<p align="center">
<img src="https://github.com/Aurecueil/Temp/blob/main/images/modtile.png"/>
</p>

To enable a mod, check its checkmark on the left.

The buttons on a mod tile are as follows (left to right):
1. 🔧Topaz mod fixer
1. 📄Export
1. ✏️Edit
1. ❌Delete

## Topaz Mod Fixer
<div align="center" style="display: flex; justify-content: center; gap: 20px;">
  <img src="https://github.com/Aurecueil/Temp/blob/main/images/fixer_menu.png?raw=true"
       style="height: 600px; width: auto;" />

  <img src="https://github.com/Aurecueil/Temp/blob/main/images/fixer_log.png?raw=true"
       style="height: 600px; width: auto;" />
</div>


> [!IMPORTANT]
> Fixer will attempt to autofill settings, but remember to check if both character and skin number are correct

Current features of Topaz Mod Fixer:
- Repath
- Fix outdated path
- Fix HP bar style
- Delete Unused definitions and assets
- Fix VO events.bnk with newest version and correct language
- Creates Backup of the mod, in case things go south

Opotional features:
- Manifest (use older game files to fill in stuff missing in newer versions)
- No-skin (Apply to all skins)
- Binless (output mod without .bin files)
- Kill static materials (disabled static materials, as outdated ones can crash the game)

## Search
Filter your installed mods. You can use the following flags to better filter search results.

> [!IMPORTANT]
> These flags **must** be at the very start of the search query.

- `-g` (global) to show ALL installed mods
- `-f` (flat) to show ALL mods for current mods (lol/tft)
- `-l` (local) to show all mods in current folder and subfolders (recursive)

### Other search options

> [!TIP]
> These keywords can be *anywhere* in the search query, and can be partial (`author:` / `auth:` / `a:`)

- `name:` e.g. `n:Spirit Blossom`
- `author:` e.g. `a:moga`
- `wad:` e.g `w:aurora`

Logic Statements:
- `||` OR statement, e.g. `Briar || Zoe`

## CLI
While the app is running, you can call the executable again with flags, to execute specific actions.

> [!TIP]
> This will bring the app to front.

Actions are executed in a fixed order, the order of the flags do not affect execution order.
- `--start` to start the mod loader
- `--sp` to switch the currently selected profile
- `--stop` to stop the mod loader
- `--dbu` to prevent the app from being brought to front

# Credits:
- [Divine skins (HUGE help)](https://divineskins.gg/)
- [League Toolkit](https://github.com/LeagueToolkit)
- [Jade's RitoBin](https://github.com/RitoShark/Jade-League-Bin-Editor)
- [Manifest Downloader](https://github.com/Morilli/ManifestDownloader)
