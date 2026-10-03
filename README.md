# beatsaber-frame-fixes

Three fixes for Beat Saber on the Steam Frame:

- **No more random pauses.** The Frame sometimes reports the headset as off for a split second, and Beat Saber pauses. Now the game only pauses if the headset stays off.
- **Gentler rumble.** Shorter and softer, with note hits still a bit stronger than everything else.
- **Lighter bloom.** The glow effect costs less: it skips a full-screen copy the game makes every frame, and during songs it uses a coarser glow, coarsest while you're busy hitting notes. Menus and pauses keep full quality.

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

Each section compares the unmodded game with the fix's defaults. All values can be changed with [options](#options).

### Rumble

| Event | Strength: game → fix | Length: game → fix |
|---|---|---|
| Note hit, bad cut, bomb, chains | 1.0 → **0.4** | 0.13 s → **0.085 s** |
| Saber on an arc or wall, sabers touching | 0.75 → **0.15** | while touching (unchanged) |
| Menu click | 1.0 → **0.2** | 0.01 s (unchanged) |

### Pausing

- **Game:** pauses the moment the headset reports "off", even for a split second.
- **Fix:** waits 250 ms and only pauses if the headset is still "off". The blips it skips are logged to Beat Saber's `Player.log`.

### Bloom

The game makes bright things glow by blurring them on a 928 px wide image, everywhere and all the time. A narrower image costs less but makes the glow coarser, so the fix narrows it only when you're unlikely to look closely. `--bloom-mode` picks how:

| `--bloom-mode` | Menus and pauses | Song playing | Right after a note hit |
|---|---|---|---|
| `off` (vanilla) | 928 | 928 | 928 |
| `full` | 928, `--bloom-width` | 928, `--bloom-width` | 928, `--bloom-width` |
| `song` | 928, `--bloom-width` | 464, `--bloom-song-width` | 464, `--bloom-song-width` |
| `aggressive` (default) | 928, `--bloom-width` | 464, `--bloom-song-width` | 256, `--bloom-busy-width` |

The numbers are the defaults; change them with the option named next to each. `off` always keeps the game's width, while `full` uses `--bloom-width` everywhere, so `--bloom-mode full --bloom-width 464` gives 464 all the time.

"Right after a note hit" lasts until 1.5 s (`--bloom-busy-delay`) after your last hit: coarse while you're busy, back to the song width in calmer parts of a song. Below 512 px, the fix also swaps the game's 4-tap filter for a finer 13-tap one in the first blur step, so thin sabers don't flicker. Every width change is logged to `Player.log`.

Separately from the width, the game copies the whole screen image every frame before adding the glow. The fix skips that copy in every mode, including `off`, by adding the glow directly into a spare image instead. It looks the same.

In a 90 s test song on the Frame (resolution scale 1.2), the fix's defaults used about 17.9 W, against about 19.1 W for the unmodded game.

## Options

Add options after `bash -s --`, for example:

```bash
curl -L vapidlinus.github.io/beatsaber-frame-fixes/i | bash -s -- --bloom-mode song --hit-strength 80
```

"Fix default" is what you get without the option. "Game" is how the unmodded game behaves, so setting an option to that value leaves that part as the game has it.

| Option | Fix default | Game | What it does |
|---|---|---|---|
| `--rumble-tweaks` | on | off | `off` keeps the game's own rumble |
| `--hit-strength` | 40 | 100 | % of the game's strength for hits, bad cuts, bombs and chains |
| `--other-strength` | 20 | 100 | % of the game's strength for everything else |
| `--hit-duration` | 65 | 100 | % of the game's length for hits, bad cuts, bombs and chains |
| `--other-duration` | 100 | 100 | % of the game's length for menu clicks (the rest lasts while touching) |
| `--pause-fix` | on | off | `off` keeps the game's own pausing |
| `--pause-debounce` | 250 | 0 | ms the headset has to be "off" before the game pauses |
| `--bloom-skip-copy` | on | off | `off` keeps the game's full-screen copy |
| `--bloom-mode` | aggressive | off | `off`, `full`, `song` or `aggressive`, see [Bloom](#bloom) |
| `--bloom-width` | 928 | 928 | px width of the glow image in menus and pauses, and everywhere in `full` mode |
| `--bloom-song-width` | 464 | 928 | px width while a song plays |
| `--bloom-busy-width` | 256 | 928 | px width right after a note hit |
| `--bloom-busy-delay` | 1.5 | | seconds after your last hit until the song width comes back |
| `--restore` | | | puts the original game files back |

- Switches take `on` or `off` (or `true`/`false`, `yes`/`no`, `1`/`0`).
- With a switch off, its settings are ignored, so you can keep them in your command and just flip the switch. Bloom settings the chosen mode doesn't use are ignored the same way.
- Rumble strength can't go above the game's 1.0, so hits can only get weaker.
- Running it again always starts from the original files, so settings don't stack.

## How it works

The install line runs [beatsaber-frame-fixes.sh](beatsaber-frame-fixes.sh), which downloads a small patcher from the latest release (built from this repo by GitHub Actions), checks its checksum and runs it. The patcher edits three of the game's files with [Mono.Cecil](https://github.com/jbevain/cecil):

- `BeatSaber.Haptics.dll`: scales strength and length in the one place all rumble goes through.
- `Main.dll`: delays pausing as described above, and tells the bloom when a song is playing and when a note was last hit.
- `Rendering.dll`: blends the bloom straight into a spare image instead of copying the whole image first, like Unity's own effects do, and sets the width of the glow image. Below 512 px it swaps the game's 4-tap sampling filter for the shader's finer 13-tap one.

It checks that the game code looks as expected before touching anything, and backs up the originals to `~/.local/share/beatsaber-frame-fixes/backup`. It should also work on a Steam Deck or a Linux PC.

## Building

```bash
dotnet test
dotnet publish src/BeatSaberFrameFixes -c Release -r linux-arm64
```

Run a local build through the script with `BSFF_BINARY=./beatsaber-frame-fixes ./beatsaber-frame-fixes.sh`.

MIT licensed. Release binaries include Mono.Cecil and the .NET runtime, see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).
