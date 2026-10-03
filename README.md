# beatsaber-frame-fixes

Three fixes for Beat Saber on the Steam Frame:

- **No more random pauses.** The Frame sometimes reports the headset as off for a split second, and Beat Saber pauses. Now the game only pauses if the headset stays off.
- **Gentler rumble.** Shorter and softer, with note hits still a bit stronger than everything else.
- **Lighter bloom.** The glow effect costs less: it skips a full-screen copy every frame, and during songs it uses a coarser glow, coarsest while you're busy hitting notes. Menus and pauses keep full quality.

Your original game files are backed up, and one command undoes everything.

*Unofficial, not affiliated with Beat Games or Valve. No game files are included here.*

## Install

1. Close Beat Saber.
2. Press **+** on the menu tray and pick **Desktop**.
3. Open **Konsole** from the app launcher.
4. Type this and press **Enter**:

   ```bash
   curl -L vapidlinus.github.io/beatsaber-frame-fixes/i | bash
   ```

5. When it says **Done!**, go back to the headset view and play.

Beat Saber updates undo the fixes, so run the same line again after an update.

### Undo

```bash
curl -L vapidlinus.github.io/beatsaber-frame-fixes/i | bash -s -- --restore
```

## What changes

### Rumble

| Event | Strength | Length |
|---|---|---|
| Note hit, bad cut, bomb, chains | 1.0 → **0.6** | 0.13 s → **0.078 s** |
| Saber on an arc or wall, sabers touching | 0.75 → **0.23** | while touching |
| Menu click | 1.0 → **0.3** | 0.01 s → **0.078 s** |

### Pausing

When the headset reports "off", the game now waits 250 ms and only pauses if it's still off. The blips it skips are logged to Beat Saber's `Player.log`.

### Bloom

The game makes bright things glow by blurring them on a 928 px wide image. A narrower image costs less but makes the glow coarser, so the fix narrows it only when you're unlikely to look closely. `--bloom-mode` picks how:

| Mode | Menus and pauses | Song playing | Right after a note hit |
|---|---|---|---|
| `full` | 928 | 928 | 928 |
| `song` | 928 | 464 | 464 |
| `aggressive` (default) | 928 | 464 | 256 |

"Right after a note hit" lasts until 1.5 s after your last hit: coarse while you're busy, back to 464 in calmer parts of a song. In a 90 s test song on the Frame (resolution scale 1.2), `aggressive` used about 17.9 W, against about 19.1 W with the game's own bloom. Every width change is logged to `Player.log`.

In every mode, the bloom also skips a full-screen copy the game makes each frame.

## Options

Add options after `bash -s --`, for example:

```bash
curl -L vapidlinus.github.io/beatsaber-frame-fixes/i | bash -s -- --bloom-mode song --hit-strength 80
```

| Option | Default | What it does |
|---|---|---|
| `--rumble-tweaks` | on | `off` keeps the game's own rumble |
| `--hit-strength` | 60 | % strength for hits, bad cuts, bombs and chains |
| `--other-strength` | 30 | % strength for everything else |
| `--hit-duration` | 60 | % length for hits, bad cuts, bombs and chains |
| `--other-duration` | 780 | % length for menu clicks (the rest lasts while touching) |
| `--pause-fix` | on | `off` keeps the game's own pausing |
| `--pause-debounce` | 250 | ms the headset has to be "off" before the game pauses |
| `--bloom-skip-copy` | on | `off` keeps the bloom's full-screen copy |
| `--bloom-mode` | aggressive | `full`, `song` or `aggressive`, see [Bloom](#bloom) |
| `--bloom-width` | 928 (the game's) | px width of the glow image in menus and pauses |
| `--bloom-song-width` | 464 | px width while a song plays |
| `--bloom-busy-width` | 256 | px width right after a note hit |
| `--bloom-busy-delay` | 1.5 | seconds after your last hit until the song width comes back |
| `--restore` | | puts the original game files back |

- Switches take `on` or `off` (or `true`/`false`, `yes`/`no`, `1`/`0`).
- With a switch off, its settings are ignored, so you can keep them in your command and just flip the switch. Bloom settings the chosen mode doesn't use are ignored the same way.
- Rumble strength can't go above the game's 1.0, so hits can only get weaker.
- Running it again always starts from the original files, so settings don't stack.

## How it works

The install line runs [beatsaber-frame-fixes.sh](beatsaber-frame-fixes.sh), which downloads a small patcher from the latest release (built from this repo by GitHub Actions), checks its checksum and runs it. The patcher edits three of the game's files with [Mono.Cecil](https://github.com/jbevain/cecil):

- `BeatSaber.Haptics.dll`: scales strength and length in the one place all rumble goes through.
- `Main.dll`: delays pausing as described above, and tells the bloom when a song is playing and when a note was last hit.
- `Rendering.dll`: blends the bloom straight into a spare image instead of copying the whole image first, like Unity's own effects do, and sets the width of the glow image. Below 512 px it uses a finer sampling filter so thin sabers don't flicker.

It checks that the game code looks as expected before touching anything, and backs up the originals to `~/.local/share/beatsaber-frame-fixes/backup`. It should also work on a Steam Deck or a Linux PC.

## Building

```bash
dotnet test
dotnet publish src/BeatSaberFrameFixes -c Release -r linux-arm64
```

Run a local build through the script with `BSFF_BINARY=./beatsaber-frame-fixes ./beatsaber-frame-fixes.sh`.

MIT licensed. Release binaries include Mono.Cecil and the .NET runtime, see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).
