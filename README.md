# beatsaber-frame-fixes

Three small fixes for Beat Saber on the Steam Frame:

- **No more random pauses.** The Frame sometimes thinks you took the headset off for a split second, and Beat Saber pauses. Now it only pauses if the headset is actually off.
- **Gentler rumble.** Shorter and softer, with note hits still a bit stronger than the rest.
- **Lighter bloom.** The glow effect skips a full-screen copy every frame, and while a song is playing it uses a slightly coarser glow that's hard to notice in action, and a coarser one still while you're busy hitting notes. Menus and pauses keep full quality.

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

## Bloom

The game makes bright things glow by blurring them on a 928 px wide image. A narrower image is cheaper but the glow gets coarser, so the patcher narrows it only when you're unlikely to look closely:

| `--bloom-mode` | Menus and pauses | Song playing | Right after a note hit |
|---|---|---|---|
| `full` | 928 | 928 | 928 |
| `song` | 928 | 464 | 464 |
| `aggressive` (default) | 928 | 464 | 256 |

"Right after a note hit" lasts until 1.5 s after your last hit, so it stays coarse while you're busy and goes back to 464 in calmer parts of a song. In a 90 s test song on the Frame, `aggressive` drew about 17.9 W against about 19.1 W at full quality. Every width change is logged to Beat Saber's `Player.log`.

## Options

Want it different? Add options after `bash -s --`, for example:

```bash
curl -fsSL https://raw.githubusercontent.com/VapidLinus/beatsaber-frame-fixes/main/beatsaber-frame-fixes.sh | bash -s -- --hit-strength 80 --hit-duration 100
```

| Option | Default | |
|---|---|---|
| `--rumble-tweaks` | on | `off` keeps the game's own rumble |
| `--hit-strength` | 60 | % strength for hits, bad cuts, bombs and chains |
| `--other-strength` | 30 | % strength for everything else |
| `--hit-duration` | 60 | % length for hits, bad cuts, bombs and chains |
| `--other-duration` | 780 | % length for menu clicks (the rest lasts while touching) |
| `--pause-fix` | on | `off` keeps the game's own pausing |
| `--pause-debounce` | 250 | ms the headset has to be "off" before the game pauses |
| `--bloom-skip-copy` | on | `off` keeps the game's full-screen copy |
| `--bloom-mode` | aggressive | `full`, `song` or `aggressive`, see [Bloom](#bloom) |
| `--bloom-width` | 928 (the game's) | px width of the glow image in menus and pauses |
| `--bloom-song-width` | 464 | px width while a song plays |
| `--bloom-busy-width` | 256 | px width right after a note hit |
| `--bloom-busy-delay` | 1.5 | seconds after your last hit until the song width comes back |
| `--restore` | | undo everything |

Switches take `on` or `off`. With a switch off, its settings are ignored, so you can keep them in your command and just flip the switch. The same goes for bloom settings the chosen mode doesn't use. Strength can't go above 1.0, so hits can only get weaker. Running it again always starts from the original files, so settings don't stack.

## How it works

The script downloads a small patcher (built from this repo by GitHub Actions), checks its checksum and runs it. The patcher edits three of the game's files with [Mono.Cecil](https://github.com/jbevain/cecil):

- `BeatSaber.Haptics.dll`: scales strength and length in the one place all rumble goes through.
- `Main.dll`: instead of pausing right away when the headset reports "off", waits 250 ms and only pauses if it's still off. Skipped blips are logged to Beat Saber's `Player.log`. It also tells the bloom when a song is playing and when a note was last hit.
- `Rendering.dll`: the bloom used to copy the whole image and blend the glow from that copy back. Now it blends straight into the spare image and uses that as the new one, like Unity's own effects do. During songs it blurs a narrower image than the game's 928 px one (see [Bloom](#bloom)), with a finer sampling filter below 512 px so thin sabers don't flicker. Each width change is logged to `Player.log`.

It checks the game code looks as expected before touching anything, and backs up the originals to `~/.local/share/beatsaber-frame-fixes/backup`. It should also work on a Steam Deck or Linux PC.

## Building

```bash
dotnet test
dotnet publish src/BeatSaberFrameFixes -c Release -r linux-arm64
```

Run a local build through the script with `BSFF_BINARY=./beatsaber-frame-fixes ./beatsaber-frame-fixes.sh`.

MIT licensed. Release binaries include Mono.Cecil and the .NET runtime, see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).
