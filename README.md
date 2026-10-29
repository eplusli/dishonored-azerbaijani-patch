# Dishonored — Azərbaycan dili yaması

Azerbaijani text, dialogue and font patch for the original Windows PC **Dishonored**, including supported installed DLC.

**0.1.0-beta.1 is a test release.** All 6,361 extracted written values and 10,285 unique dialogue lines have translation drafts. Full contextual language review and complete base-game/DLC playtesting are still pending. Artwork and text baked into artwork remain unchanged.

## Quraşdırma / Installation

1. Back up your saves and close Dishonored.
2. Set the game's language to **English** before installing; this patch replaces the English/INT text resources.
3. Download `Dishonored-AZ-Setup-0.1.0-beta.1.exe` from [Releases](https://github.com/eplusli/dishonored-azerbaijani-patch/releases).
4. Open it, click **Browse**, and select the game root containing `Binaries` and `DishonoredGame`.
5. Click **Install / Quraşdır**. If the game is in a protected folder, use **Run as administrator**.
6. Wait for the success message, then launch Dishonored normally. Enable subtitles in the game's settings to see translated dialogue.

The installer works offline. It does not install the game, DLC, a game executable, or a launcher. It does not change saves. Absent DLC package groups are skipped; incomplete groups are rejected.

## Compatibility

- Windows 10/11, 64-bit, with the Windows .NET Framework 4.8/4.8.1 runtime.
- The file set tested is the original PC **Dishonored RHCP** build with DLC05, DLC06 and DLC07.
- Compatibility is checked against the exact original SHA-256 hashes for every selected file. Other editions, languages, game revisions or mods may be rejected. Do not rename files or bypass these checks.
- At least **5 GB free space** is recommended on the game drive; the installer calculates the actual requirement before installing.
- The executable is **unsigned**. No code-signing certificate was supplied. Check its hash against `SHA256SUMS.txt` in the release.

## English dilinə qaytarmaq / Uninstall

Close the game. Reopen the installer, select the same game folder and click **Restore English**. You can also run `Dishonored-AZ-Patch/Restore-English.exe` inside the game folder, then click **Restore English**.

**Keep the `Dishonored-AZ-Patch` folder.** It holds your original files and recovery state. If an installation is interrupted, reopen the installer and use **Restore English** before playing. Unknown edits made after installing are protected: restoration stops instead of overwriting them.

If this exact patch was already installed by a different method, the installer reports that it is already present. Use that method's original restore tool; this installer cannot invent missing English backups.

## Validation and feedback

See `installer-validation.json` in the release for checks of full installation, restoration, repeated installation, Unicode paths, incompatible files, optional DLC and simulated write-failure rollback. These checks validate file operations and reconstruction, not complete linguistic quality or every PC configuration.

Report translation mistakes with the displayed text, scene/mission, and a screenshot when possible. Include your game edition for installer compatibility reports. Do not attach whole game archives or saves containing personal information.

## Distribution

The release carries **binary differences**, translated text changes and font changes, not complete replacement game archives. You must own a compatible game. This is an unofficial community patch, unaffiliated with Arkane or Bethesda. Game content and trademarks belong to their respective owners.

Installer source: [installer/](installer/). MIT applies to installer code; bundled tools retain their own licenses. No rights to the original game are granted.
