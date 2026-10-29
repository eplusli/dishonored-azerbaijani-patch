# Installer source

The Windows Forms installer targets the Windows .NET Framework 4.x runtime included with Windows 10 and 11. It is compiled as a 64-bit Windows executable with the Windows framework C# compiler. No Python, Git, xdelta installation, or network connection is required on the destination PC.

The embedded ZIP contains 746 VCDIFF deltas, a hash manifest, xdelta 3.2.2 (Apache 2.0), and third-party notices. Original packages are decompressed locally using a bounds-checked managed decoder adapted from LZOkay (MIT). The installer verifies source, decompressed source, delta, reconstructed output and backup SHA-256 hashes.

Build with the private translation workspace available:

1. Run `python installer/build_payload.py`. It reads the existing installation manifest, verified English backups, and staged translations; every generated delta is decoded and checked before packaging.
2. Run `installer/build.ps1` from PowerShell. The script compiles the executable into `release/`.
3. Run `python installer/test_installer.py` to test the actual executable against disposable fixtures. This requires enough disk space for a full patched game file set. It never changes the installed game.

The public repository intentionally excludes game archives, English backups, user profiles and private working files. The release executable contains the built deltas. A release build cannot be regenerated from the installer source alone; the original translation workspace is also required.

Installer code is MIT licensed; see `LICENSE`. The LZO decoder retains the upstream MIT notice under `licenses/`. xdelta is separately Apache-2.0 licensed. The supplied executable is unsigned.
