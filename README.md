# beatsaber-frame-fixes

Three small fixes for Beat Saber on the Steam Frame:

- **No more random pauses.** The Frame sometimes thinks you took the headset off for a split second, and Beat Saber pauses. Now it only pauses if the headset is actually off.
- **Gentler rumble.** Shorter and softer, with note hits still a bit stronger than the rest.
- **Lighter bloom.** The glow effect skips one full-screen copy every frame. It looks exactly the same.

Your original game files are backed up, and you can undo everything with one command.

*Unofficial, not affiliated with Beat Games or Valve. No game files are included here.*

## Install

1. Close Beat Saber.
2. Press **+** on the menu tray and pick **Desktop**.
3. Open **Konsole** from the app launcher.
4. Paste this in (**Ctrl+Shift+V**) and press **Enter**:

   ```bash
   curl -fsSL https://raw.githubusercontent.com/VapidLinus/beatsaber-frame-fixes/main/beatsaber-frame-fixes.sh | bash
   ```

5. When it says **Done!**, head back to the headset view and play.

Beat Saber updates undo the fixes, so just run it again after an update.

**To undo**, run the same line with `-s -- --restore` on the end:

```bash
curl -fsSL https://raw.githubusercontent.com/VapidLinus/beatsaber-frame-fixes/main/beatsaber-frame-fixes.sh | bash -s -- --restore
```

## Rumble

| Event | Strength | Length |
|---|---|---|
| Note hit, bad cut, bomb, chains | 1.0 → **0.6** | 0.13 s → **0.078 s** |
| Saber on an arc or wall, sabers touching | 0.75 → **0.23** | while touching |
| Menu click | 1.0 → **0.3** | 0.01 s → **0.078 s** |

Want it different? Add options after `bash -s --`, for example:

```bash
curl -fsSL https://raw.githubusercontent.com/VapidLinus/beatsaber-frame-fixes/main/beatsaber-frame-fixes.sh | bash -s -- --hit-strength 80 --hit-duration 100
```

| Option | Default | |
|---|---|---|
| `--hit-strength` | 60 | % strength for hits, bad cuts, bombs and chains |
| `--other-strength` | 30 | % strength for everything else |
| `--hit-duration` | 60 | % length for hits, bad cuts, bombs and chains |
| `--other-duration` | 780 | % length for menu clicks (the rest lasts while touching) |
| `--pause-debounce` | 250 | ms the headset has to be "off" before the game pauses |
| `--bloom-width` | 512 | px width of the image the glow is blurred on. Lower is cheaper but the glow gets blocky |
| `--no-haptics`, `--no-pause-fix`, `--no-bloom-fix` | | skip one of the fixes |
| `--restore` | | undo everything |

Strength can't go above 1.0, so hits can only get weaker. Running it again always starts from the original files, so settings don't stack.

## How it works

The script downloads a small patcher (built from this repo by GitHub Actions), checks its checksum and runs it. The patcher edits three of the game's files with [Mono.Cecil](https://github.com/jbevain/cecil):

- `BeatSaber.Haptics.dll`: scales strength and length in the one place all rumble goes through.
- `Main.dll`: instead of pausing right away when the headset reports "off", waits 250 ms and only pauses if it's still off. Skipped blips are logged to Beat Saber's `Player.log`.
- `Rendering.dll`: the bloom used to copy the whole image and blend the glow from that copy back. Now it blends straight into the spare image and uses that as the new one, like Unity's own effects do.

It checks the game code looks as expected before touching anything, and backs up the originals to `~/.local/share/beatsaber-frame-fixes/backup`. It should also work on a Steam Deck or Linux PC.

## Building

```bash
dotnet test
dotnet publish src/BeatSaberFrameFixes -c Release -r linux-arm64
```

Run a local build through the script with `BSFF_BINARY=./beatsaber-frame-fixes ./beatsaber-frame-fixes.sh`.

MIT licensed. Release binaries include Mono.Cecil and the .NET runtime, see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).
