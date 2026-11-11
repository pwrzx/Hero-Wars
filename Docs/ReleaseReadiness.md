# Mobile release candidate — 12 November 2026

The source is prepared for Android and iOS release builds. The Android installation package is built and inspected. An iOS Xcode export is part of this handoff; it still requires compilation and signing on a Mac. Store submission is not complete: no publisher signing credentials or physical devices were available in this workspace.

## Delivered game

- Responsive portrait menus with bounded safe areas, centered tablet content, readable inventory and chest odds, larger touch targets, consistent button states and exact power numbers.
- Attack poses, hit audio and effects resolve at the enemy tower before the hero returns. All four heroes retain their original cloaks; armor rewards contain only the three shields.
- Twenty-four solvable keeps across four chapters, Journey selection, first-clear rewards, duplicate reward protection and persistent progression.
- First-launch instructions, pause/restart, background interruption recovery, music and sound controls, optional haptics, reduced motion and battery saver.
- Local primary/backup saves, daily gifts, earned-currency cosmetic chests and persisted equipment. No accounts, ads, in-app purchases or gameplay downloads.
- Original hero launcher icon, mobile texture overrides, platform build menus, store copy, privacy text and asset credits.

## Validation evidence

| Check | Result | Report |
| --- | --- | --- |
| Campaign rules | 3,777 assertions across all reachable winning states in 24 stages | [RulesVerification.txt](../Artifacts/RulesVerification.txt) |
| Cosmetics, grips and character timing | 13,147 assertions | [HeroCenterValidation.txt](../Artifacts/HeroCenterValidation.txt) |
| Hero Center pointer flow | Passed, including previews, equip, chests, gifts and scene return | [HeroCenterPlayValidation.txt](../Artifacts/HeroCenterPlayValidation.txt) |
| Tower physics | 26,918 assertions, 26 real attacks, all six three-room orders and an eight-floor collapse; zero recovery snaps | [TowerPhysicsValidation.txt](../Artifacts/TowerPhysicsValidation.txt) |
| Release data and save recovery | 137 assertions, including exact rewards, repeat protection, locale-safe dates/numbers and full-value badge fit | [MobileReleaseValidation.txt](../Artifacts/MobileReleaseValidation.txt) |
| Mobile gameplay loop | 844 checks and nine real pointer attacks, including pause, background interruption, rewards and Journey selection | [MobilePlayValidation.txt](../Artifacts/MobilePlayValidation.txt) |
| Unity asset metadata | 562 files, zero errors and zero warnings | [UnityAssetValidation.json](../Artifacts/UnityAssetValidation.json) |
| Store text fields | Name, subtitle, short description, keywords and promotional text fit their platform limits | [StoreListingValidation.txt](../Artifacts/StoreListingValidation.txt) |

Editor layout checks covered 360×640, 428×926 and 768×1024 viewports. Visual captures in `Artifacts/Release-*.png` show Home, Customize, Shop, Journey, Settings, a stage clear and tablet layout. These are Unity Game view captures, not store screenshots or physical-device performance results. Play-mode checks preserve and restore the original player saves and preferences.

## Build outputs

Build products remain in the ignored `Builds` folder. The build scripts, configuration, reports and validation tools are versioned.

- `Builds/Android/HeroWars-QA.apk`: release player options with the local debug signing identity, intended for installation and QA. See [Android build report](../Artifacts/Build-Android-QA.json), [manifest inspection](../Artifacts/AndroidManifestValidation.txt) and [native package checks](../Artifacts/AndroidPackageValidation.json).
- `Builds/Android/HeroWars-unsigned.aab`: unsigned store preparation bundle. It needs the publisher's upload key before Google Play submission. See [bundle build report](../Artifacts/Build-Android-Bundle.json), [bundle package checks](../Artifacts/AndroidBundleValidation.json) and [bundletool validation](../Artifacts/AndroidBundletoolValidation.txt).
- `Builds/iOS`: Xcode project for iPhone and iPad, also packaged as `Builds/HeroWars-iOS-Xcode.zip` for transfer to a Mac. See [export build report](../Artifacts/Build-iOS.json), [export inspection](../Artifacts/IosExportValidation.json) and [archive validation](../Artifacts/IosArchiveValidation.json). Export success does not establish that the native project compiles, installs or passes App Store validation.

Android targets API 36, minimum API 26, ARM64/IL2CPP. Package inspection checks all six native libraries for 16 KB-compatible ELF segments and verifies that the Pipeline runtime server is absent. The APK has no internet permission and is not debuggable. iOS uses IL2CPP, Metal and deployment target iOS 15. Both platforms include only the two game scenes.

Both final Android builds completed with zero errors and zero warnings. The APK is 36,304,427 bytes; the unsigned AAB is 36,172,729 bytes. SHA-256 hashes are in their package-validation reports. The launcher foreground was extracted from the built APK and visually checked in [Android-AppIcon.png](../Artifacts/Android-AppIcon.png).

The iOS export completed with zero errors and four vendor warnings: one deprecated TextMeshPro shader pragma and three IL2CPP notices about splitting large TextMeshPro methods into separate C++ files. Inspection confirmed all 19 icon sizes, the opaque 1024px store icon, iPhone/iPad and portrait configuration, the native haptics source and generated call, two shipping scenes, and the Unity privacy manifests including the local UserDefaults reason. The release metadata contains no Pipeline runtime server. Xcode compilation, signing and device installation have not been performed on this Windows machine.

Package/bundle identifier `com.herowars.towertrials` and version `1.0.0` are configured for reproducibility. The publisher must confirm the final identifier before creating store records. No private key, password, Apple team or certificate is committed.

## Work that requires publisher accounts or devices

1. Confirm the title, bundle/package identifier, publisher identity, asset entitlements and backdrop provenance. Add the real support contact and host the [privacy policy](Privacy.md).
2. Sign the Android bundle with the publisher's upload key. On a Mac, open the iOS export with the currently required Xcode/iOS SDK, select the Apple Developer team, compile, archive and validate.
3. Test signed builds on an Android phone/tablet and iPhone/iPad: cold launch, touch, cutouts, audio interruptions, background/resume, force-close/reopen, updates, complete campaign and all equipment combinations. Include a 16 KB Android environment and a lower-memory device; measure sustained frame time, memory and thermals.
4. Capture screenshots from the final signed players, complete content-rating/privacy forms, select pricing/territories and submit through the publisher's store accounts.

See [MobileRelease.md](MobileRelease.md) for repeatable build commands, signing environment variables and links to current platform submission requirements.
