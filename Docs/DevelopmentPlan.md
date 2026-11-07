# Tower Trials — working title

Portrait mobile tower puzzle using original fantasy artwork. References are in the sibling `pics` directory and are design references only.

## Confirmed rules

- Swipe toward a room in the front enemy tower to attack its occupant.
- Hero power must be strictly greater than enemy power. Defeat grants the enemy's entire power exactly once.
- The hero jumps from his home tower to the enemy. On victory, the enemy and its room disappear; the hero returns to his own tower after each fight.
- A stronger enemy defeats the hero. Retry resets the current stage.
- All rooms must be cleared before a tower collapses and disappears. The next tower then advances.
- Clear all enemy towers to complete the stage.
- No matching enemy power values within an authored stage; no reachable hero/enemy ties, including after power accumulation.
- Preserve run progress locally on the player's device.

## First implementation

Three authored stages; original starter Blender models; touch/mouse swipe input; attack animation; tower collapse and advance; stage completion; retry; local save/restore; portrait safe-area-aware HUD. The game and imported character meshes are an initial prototype, not final illustrated art or rigged production assets.

## Art workflow

`ArtSource/Blender/HeroWars_AssetLibrary.blend` is the editable master library. It contains named collections for characters, architecture and props. Store future authored/generated 3D assets in their own collections here; pack any external textures before sharing the file.

Unity uses FBX exports in `Assets/_Game/Resources/Models`. Export each model at its own origin with unit scale; use -Z forward and Y up. Add Unity materials under `Resources/Materials` matching the Blender material names.

`Tools/Blender/create_asset_library.py` is the original reproducible starter build. It overwrites the starter library if rerun; do not run it over hand-edited art. Blender source is outside `Assets`, so opening a Unity build does not depend on Blender being installed.

## Later phase

After core gameplay is tuned, implement an on-device seeded stage generator. It must generate a winning route, reject duplicates and every reachable equal-power encounter, respect bounded power growth, preserve generator version/seed and the current run locally, and vary the supplied art and tower modules. `StageDefinition.Validate()` is the existing rule gate. No production randomizer or backend is introduced in this prototype.

Android versus iOS distribution remains to be chosen. Device builds, final fantasy artwork, character rigging/animation, audio and release testing remain future work.
