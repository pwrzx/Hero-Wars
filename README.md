# Tower Trials / Hero Wars Unity prototype

Unity version: **6000.6.0f1**, URP, Input System.

Open `Assets/_Game/Scenes/TowerPrototype.unity`, choose a **9:16 portrait** Game view, and press **Play**. Swipe right toward the room you want to attack. In the Editor, click and drag right using the mouse. Start with weaker enemies and build power before attacking stronger ones. Retry resets the current stage; clearing all towers unlocks the next authored stage. The current run is saved locally.

The editable Blender file is `ArtSource/Blender/HeroWars_AssetLibrary.blend`. The five exported starter models are already imported into Unity. See `Docs/DevelopmentPlan.md` for the confirmed design and remaining work.

Validation: use **Tower Trials > Verify Game Rules** for an exhaustive traversal of the authored stages plus save and invalid-data checks. The Editor-only `PrototypeChecks.StartPlaytest()` verifies the pointer/combat/animation flow during Play mode. Reports are written to `Artifacts`.

The initial verification passed 3,186 rule assertions, the pointer/animation playtest, a fresh-session save restore, and the Next Stage button. The asset GUID/script audit reports zero errors and warnings. Physical-phone performance and Android/iOS builds have not been tested.

The template's default volume profile contained orphaned subassets with missing scripts. They were removed through Unity's asset API; its original file is preserved in `Artifacts/BaselineBackup`.

The Unity Pipeline package enables live local Editor operations through the Unity CLI. No cloud services, account system, ads, purchases, or procedural stage generator are included yet.

Combat now includes a procedural shoulder/weapon rig, breathing and cape motion, jump anticipation and tucked legs, sword swings and trails, impact sparks, recoil and defeat poses, power wisps and rolling numbers, landing bursts, collapse effects, victory animation and short original synthesized sound cues. The generated Ancient Chasm painting is integrated behind the 3D scene.

On **TowerGame**, **Feedback Intensity** adjusts camera shake (zero turns shake off), **Feedback Volume** controls combat sound, and **Attack Duration** controls the jump timing. Combat VFX use a 24-effect pool plus at most 160 burst particles and 32 ambient motes. These starter meshes use procedural pivots rather than a humanoid skeleton.

The rules check reads `StarterCampaign.asset`, so it follows the balancing values used by the game. `CreateStarterStages()` remains the factory default for newly created campaign assets. Existing custom edits to those defaults have been preserved.

Tower floors and crowns now fall under gravity when a defeated room is removed. Compound colliders on the room floors and walls support the falling stack, while each surviving enemy, power badge, and target rim follows its room. The lowest survivor reaches the hero's level, and saved runs rebuild with the same compacted layout. The hero and goblins turn slightly toward each other. **Tower Fall Gravity** and **Character Facing Angle** on TowerGame adjust the fall weight and facing.

In Play mode, **Tower Trials > Verify Tower Physics (Play Mode)** checks all six three-room attack orders and an eight-floor bottom-up collapse through real attacks and collision contacts, including partial-save reloads and next-tower advancement. It uses temporary campaign fixtures and restores the player's campaign and save. The report is `Artifacts/TowerPhysicsValidation.txt`.

The in-game UI uses bundled Alegreya SC and Alegreya Sans fonts, with static TextMesh Pro atlases, tabular power numerals, and drawn ink-and-brass buttons. Retry and result actions have larger touch targets, distinct pointer/keyboard states and pressed feedback. Font sources and licenses are in `Assets/_Game/Art/Fonts`; **Tower Trials > Prepare UI Fonts** recreates missing font assets. See `Docs/UIStyle.md` for the UI conventions.
