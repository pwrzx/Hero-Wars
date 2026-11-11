# Hero Wars

A portrait, offline mobile tower puzzle game built with Unity **6000.6.0f1**, URP and the Input System.

Swipe right toward a room with less power than your hero. Defeat its enemy, absorb that power, and find the winning route through each tower. Clear 24 keeps across four chapters, earn gold and gems, and unlock four hero skins, three shields and nine weapons. The heroes retain their original cloaks; cloaks are not standalone armor rewards.

The game includes a journey selector, first-clear rewards with duplicate protection, first-launch instructions, pause/resume, background interruption handling, independent music/sound preferences, optional haptics, reduced motion, battery saver, local backup saves, daily gifts, randomized earned-currency chests, and saved customization. There are no accounts, ads or in-app purchases.

## Play

Open `Assets/_Game/Scenes/HeroCenter.unity` and enter Play mode with a portrait Game view. Choose Adventure to read the rules and continue your run. Journey selects unlocked keeps. Home returns from gameplay; Pause provides settings and restart. Mouse drag works in the Editor, and the same pointer flow handles mobile touch.

The authored opening stages live in `Assets/_Game/Data/StarterCampaign.asset`. Later stages are deterministically generated and validated by `CampaignConfig`; all current 24 stages are saved in the asset. The cosmetic catalog is `Assets/_Game/Resources/HeroCenter/Catalog.asset`.

## Mobile release

Android is configured for ARM64/IL2CPP, minimum API 26 and target API 36. iOS uses IL2CPP/Metal, iOS 15+, iPhone and iPad. Both ship only the two game scenes. Mobile textures use ASTC overrides and retain the source dimensions.

Use `Hero Wars > Release` for settings, Android test APK, Android store bundle and iOS Xcode export. Build outputs stay in the ignored `Builds` directory. Store signing, the publisher's final identity/contact details, native device testing and store submission remain publisher steps; a local Unity build does not establish store approval.

- [Release/build instructions](Docs/MobileRelease.md)
- [Final readiness report](Docs/ReleaseReadiness.md)
- [Store listing copy](Docs/StoreListing.md)
- [Privacy policy text](Docs/Privacy.md)
- [Asset credits](Docs/Credits.md)
- [Hero Center/catalog notes](Docs/HeroCenter.md)
- [UI style](Docs/UIStyle.md)

## Validation

`Tower Trials > Verify Game Rules` explores all reachable winning states and save round trips across the campaign. Hero Center checks cover cosmetic references, rarity pools, exact transactions, save repair, native cloaks, animation timing and equipment grips. Play-mode checks exercise real pointer events and scene transitions. Tower physics checks cover all three-room orders and an eight-floor collapse. The release commands also verify rewards, backup recovery, pause/settings, background interruption and progression.

Validation reports and screenshots are in `Artifacts`. The checks isolate and restore the player's saves. Source models, textures and fonts are retained; gameplay has no runtime download dependency.
