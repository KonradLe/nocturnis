# Nocturnis 0.8.4 — Polish / English

Choose POLSKI or ENGLISH before the opening cinematic on first launch. Change it later under Settings → Language (Ustawienia → Język); the choice persists independently of character saves.

- English menu, character selection, HUD, dialogue, quests, talents, item descriptions, shops, settings and credits.
- 1236 catalog entries, including dynamic text fragments. English display uses decimal points without changing save serialization.
- Separate English intro subtitles and Act I title cards. Both intro variants retain the same repaired English narration and audio mix.
- English README, Polish instructions and PLAY.cmd / GRAJ.cmd launchers.

## Download

- Nocturnis-0.8.4-Windows-x64.zip — portable Windows game. Extract the entire folder, then run PLAY.cmd or GRAJ.cmd.
- Nocturnis-0.8.4-Unity-project.zip — complete Unity 6000.3.26f1 project, including bulk assets.
- Nocturnis-0.8.4-PL-EN.pdf — actual game screenshots of the new language options and English UI.
- SHA256SUMS.txt — download checksums.

[Trailer](https://zautomatyzowani.ai/nocturis) — existing 0.8.2 footage, English narration and Polish subtitles.

## Verification

- 2487 editor localization assertions; existing development/progression and all 12 interior-floor checks passed.
- Actual Windows player: first-launch chooser, live PL/EN switch and persisted preference; five English character previews and settings.
- English RPG UI: 21 captures, including five talent/inventory pairs, quest journal, market, forge, stash and spellbook; 1280×720 plus selected 960×540 layouts. Save/load and fixture-state checks passed.
- English intro decoded successfully. Returning to the menu, continuing and beginning a journey did not replay the intro within the same session.
- Production saves/settings and prior QA preferences were preserved. The 0.8.3 source and local release were retained.

These are automated and visual checks of controlled fixtures, not a complete campaign playthrough or an FPS benchmark. The repository remains private; original third-party attribution is preserved.

## Po polsku

Uruchom GRAJ.cmd i wybierz POLSKI albo ENGLISH. Później zmienisz język w Ustawienia → Język. Zapisy postaci pozostają zgodne z 0.8.3. Lektor intro jest angielski, a napisy i plansze zgodne z wybranym językiem.
