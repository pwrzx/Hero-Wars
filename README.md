# Tower Trials / Hero Wars Unity prototype

Unity version: **6000.6.0f1**, URP, Input System.

Open `Assets/_Game/Scenes/TowerPrototype.unity`, choose a **9:16 portrait** Game view, and press **Play**. Swipe right toward the room you want to attack. In the Editor, click and drag right using the mouse. Start with weaker enemies and build power before attacking stronger ones. Retry resets the current stage; clearing all towers unlocks the next authored stage. The current run is saved locally.

The editable Blender file is `ArtSource/Blender/HeroWars_AssetLibrary.blend`. The five exported starter models are already imported into Unity. See `Docs/DevelopmentPlan.md` for the confirmed design and remaining work.

Validation: use **Tower Trials > Verify Game Rules** for an exhaustive traversal of the authored stages plus save and invalid-data checks. The Editor-only `PrototypeChecks.StartPlaytest()` verifies the pointer/combat/animation flow during Play mode. Reports are written to `Artifacts`.

The initial verification passed 3,186 rule assertions, the pointer/animation playtest, a fresh-session save restore, and the Next Stage button. The asset GUID/script audit reports zero errors and warnings. Physical-phone performance and Android/iOS builds have not been tested.

The template's default volume profile contained orphaned subassets with missing scripts. They were removed through Unity's asset API; its original file is preserved in `Artifacts/BaselineBackup`.

The Unity Pipeline package enables live local Editor operations through the Unity CLI. No cloud services, account system, ads, purchases, or procedural stage generator are included yet.
