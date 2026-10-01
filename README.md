# beatsaber-frame-fixes

Two fixes for Beat Saber (Steam version) on the **Steam Frame**:

- **No more random pauses.** The Frame's "is the headset on my face" sensor sometimes reports for a split second that you took the headset off. Beat Saber pauses every time. With this fix it only pauses if the headset really is off (or the system menu is open) for longer than a quarter of a second.
- **Softer, shorter rumble.** Controller rumble is toned down, with note hits kept stronger than everything else. See the [table below](#rumble-settings) for the exact values. You can change them.

It works by patching two of Beat Saber's game files on your device. Your original files are backed up and can be put back with one command.

> Unofficial and not affiliated with Beat Games or Valve. This repository contains no game files. Use at your own risk.

## Install on the Steam Frame

1. Close Beat Saber if it's running.
2. Press the **+** button on the menu tray and choose **Desktop**.
3. Open the application launcher (bottom-left corner), search for **Konsole** and open it.
4. Copy this line, paste it into Konsole with **Ctrl+Shift+V** (or right-click > Paste), and press **Enter**:

   ```bash
   curl -fsSL https://raw.githubusercontent.com/VapidLinus/beatsaber-frame-fixes/main/beatsaber-frame-fixes.sh | bash
   ```

5. After a few seconds you should see something like this:

   ```
   Downloading the patcher...
   Found Beat Saber 1.45.2 at /home/steamos/.local/share/Steam/steamapps/common/Beat Saber
   Backed up the original game files to /home/steamos/.local/share/beatsaber-frame-fixes/backup
   Rumble fix applied: hits 60%, other rumble 30%, duration 30%
   Pause fix applied: pauses only after focus or presence is lost for 250 ms
   Done! You can start Beat Saber now.
   ```

6. Close Konsole and go back to the normal headset view (double-click **Return to Gaming Mode** on the desktop). Start Beat Saber and play.

If something goes wrong, the message tells you what to do. For example, *"Beat Saber is running. Close it and run this again."* Nothing is changed when an error is shown.

### After a Beat Saber update

Steam updates replace the patched files, so the fixes disappear. Do the same steps again.

If an update changed the game code these fixes rely on, the script says so and changes nothing. Check this page for a newer version.

### Undo

Do the same steps, but use this line:

```bash
curl -fsSL https://raw.githubusercontent.com/VapidLinus/beatsaber-frame-fixes/main/beatsaber-frame-fixes.sh | bash -s -- --restore
```

You can also let Steam restore the files: Beat Saber > **Properties** > **Installed Files** > **Verify integrity of game files**.

## Rumble settings

Strength is the rumble intensity the game sends to the controller, from 0 to 1. With the default settings:

| Event | Strength (original) | Strength (patched) | Length (original) | Length (patched) |
|---|---|---|---|---|
| Note hit | 1.0 | **0.6** (60%) | 0.13 s | **0.039 s** (30%) |
| Bad cut | 1.0 | **0.6** (60%) | 0.13 s | **0.039 s** (30%) |
| Bomb hit | 1.0 | **0.6** (60%) | 0.13 s | **0.039 s** (30%) |
| Chain head and links | 1.0 | **0.6** (60%) | 0.13 s | **0.039 s** (30%) |
| Saber touching an arc or wall | 0.75 | **0.225** (30%) | while touching | while touching |
| Sabers touching each other | 0.75 | **0.225** (30%) | while touching | while touching |
| Menu click | 1.0 | **0.3** (30%) | 0.01 s | **0.003 s** (30%) |
| Head inside a wall (headset rumble) | 10 | 3 | while inside | while inside |

Notes:

- The game caps strength at 1.0. The original values for hits are already at the cap, so they can be made weaker but not stronger. The head-in-wall value stays capped either way.
- On Valve-style controllers (Frame, Index) the game rumbles for twice the listed length. A note hit lasts about 0.26 s originally and about 0.08 s patched.
- Plain misses have no rumble in Beat Saber.

### Changing the settings

Add options after `bash -s --`. All values are percentages of the original, except `--pause-debounce`, which is in milliseconds:

```bash
curl -fsSL https://raw.githubusercontent.com/VapidLinus/beatsaber-frame-fixes/main/beatsaber-frame-fixes.sh | bash -s -- --hit-strength 80 --duration 50
```

| Option | Default | What it does |
|---|---|---|
| `--hit-strength <percent>` | 60 | Strength for note hits, bad cuts, bombs and chains |
| `--other-strength <percent>` | 30 | Strength for arcs, walls, saber clashes and menu clicks |
| `--duration <percent>` | 30 | Length of all rumble |
| `--pause-debounce <ms>` | 250 | How long the headset must report "off" (or the system menu must be open) before the game pauses |
| `--no-haptics` | | Leave rumble as the game has it |
| `--no-pause-fix` | | Leave pausing as the game has it |
| `--restore` | | Put the original game files back |
| `--game-dir <folder>` | | Beat Saber folder, if it isn't found automatically |

Running it again with different options always starts from the original files, so changes never stack. Options you leave out go back to their defaults.

## Other Linux devices

The same command works on a Steam Deck or a Linux PC running Beat Saber through Proton (x86-64 and ARM64). Whether you need the pause fix there depends on your headset.

## How it works

The script downloads the patcher for your device from the [latest release](https://github.com/VapidLinus/beatsaber-frame-fixes/releases/latest), checks it against the release's `SHA256SUMS` and runs it. The patcher is a small .NET program built from this repository's source by GitHub Actions. It uses [Mono.Cecil](https://github.com/jbevain/cecil) to edit the game's compiled code in `Beat Saber_Data/Managed`:

- **Rumble** (`BeatSaber.Haptics.dll`): all rumble goes through `RumbleHapticFeedbackPlayer.PlayHapticFeedback`. The patch multiplies each preset's strength and duration. Presets whose name starts with `Hit` (note, bad cut, bomb, chains) get the hit strength.
- **Pauses** (`Main.dll`): `PauseController` pauses as soon as OpenXR reports lost input focus or that the user isn't present. On the Frame this happens for about 20 ms at random during play. The patch starts a timer instead and pauses only if focus or presence is still lost after the debounce time. The pause button and all other pause reasons are unchanged. Ignored blips are logged to Beat Saber's `Player.log` as `[beatsaber-frame-fixes] ... Ignored focus/presence blip of N ms`.

Before changing anything, the patcher checks that the game code looks exactly as expected. Both files are patched in memory, and the game files are only replaced if every patch succeeded. Each patched file records a marker with the settings and a hash of the original. The originals are kept in `~/.local/share/beatsaber-frame-fixes/backup`, named by that hash.

## Building from source

Requires the .NET 10 SDK.

```bash
dotnet test
dotnet publish src/BeatSaberFrameFixes -c Release -r linux-arm64   # or linux-x64
```

To try a local build through the script, set `BSFF_BINARY` to the patcher's path:

```bash
BSFF_BINARY=./beatsaber-frame-fixes ./beatsaber-frame-fixes.sh
```

The tests patch small stand-in assemblies that copy the shape of the game's classes, so no game files are needed.

## License

[MIT](LICENSE). The release binaries include Mono.Cecil and the .NET runtime, see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).
