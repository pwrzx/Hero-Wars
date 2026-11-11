# Hero Wars mobile release

The release is an offline, portrait tower puzzle game for Android phones/tablets and iPhone/iPad. It uses the existing painted teal world, low-poly heroes, medieval weapons, Alegreya typography, parchment text and brass controls. The campaign contains 24 keeps across four chapters. Clear weaker enemies to absorb their power, earn first-clear gold and gems, unlock cosmetics through earned-currency chests, and equip four heroes, three shields and nine weapons. Cloaks stay on their original heroes.

## Build configuration

- Unity 6000.6.0f1, URP, Input System; version 1.0.0.
- Android: ARM64, IL2CPP, OpenGL ES 3, minimum API 26, target API 36. ASTC 4x4 overrides retain the source texture dimensions while reducing device memory.
- iOS: IL2CPP, Metal, deployment target iOS 15, iPhone and iPad. Xcode export is generated on Windows; native compilation, signing and installation require a Mac.
- The two shipping scenes are HeroCenter and TowerPrototype. Imported demo scenes are excluded.
- Package/bundle identifier: `com.herowars.towertrials`. Confirm that this is the publisher's intended identifier before creating store records. No developer account identity or signing secrets are embedded.
- Pipeline's release runtime define is excluded. Builds use release options, without script debugging or profiler connection.
- The 1024px opaque icon is rendered from the actual equipped hero in `Assets/_Game/Art/HeroWarsIcon.png`.

`Hero Wars > Release > Prepare mobile settings` applies this configuration, extends the authored opening campaign without replacing its edited values, creates the app icon, and sets mobile texture overrides. It does not recreate either scene or reset player progress.

## Repeatable builds

Exit Play mode, switch Unity's active platform, and use:

- `Hero Wars > Release > Build Android test APK`: `Builds/Android/HeroWars-QA.apk`, signed with the local Android debug identity for installation/testing.
- `Hero Wars > Release > Build Android store bundle`: `Builds/Android/HeroWars-unsigned.aab` without signing credentials, or `HeroWars-release.aab` when the publisher's signing environment is present.
- `Hero Wars > Release > Export iOS Xcode project`: `Builds/iOS`.

For a publisher-signed Android bundle, set `HERO_WARS_KEYSTORE`, `HERO_WARS_KEYSTORE_PASSWORD`, `HERO_WARS_KEY_ALIAS` and `HERO_WARS_KEY_PASSWORD` in the build process environment. Credentials are not printed or committed. Retain the upload key securely; the unsigned bundle is a preparation artifact and cannot be uploaded as-is.

For iOS, open the generated Xcode project on a Mac, select the publisher's Apple Developer team, confirm the bundle ID and capabilities, build to a connected device, then archive and validate in Xcode Organizer. Do not use an invented team ID or certificate.

## Verification

Editor commands:

- `Tower Trials > Verify Game Rules`: every reachable winning state in all 24 keeps, ties, invalid stages, overflow and save restoration.
- `Tower Trials > Verify Hero Center`: cosmetic references, chest pools/transactions, native cloaks, weapon grips and synchronized combat animations on all four heroes.
- `Tower Trials > Verify Hero Center (Play Mode)`: real raycasts through locked previews, chest opening, equip, daily gifts, first-launch instructions and the Adventure/Home round trip.
- `Tower Trials > Verify Tower Physics (Play Mode)`: 26 real attacks, every three-room kill order and an eight-floor collapse, with no support-recovery snaps.
- `Hero Wars > Release > Verify release data`: all 24 reward receipts, duplicate protection, progression, backup recovery and platform configuration.
- `Hero Wars > Release > Verify mobile loop (Play Mode)`: pause/resume, settings, background interruption during flight, stage-clear payment, next keep, and Journey selection. Uses isolated saves and restores the original player data.

Reports and visual captures are in `Artifacts`. A Unity Game view validates game logic/layout, not native OS behavior. Before store submission, test the signed player on an Android phone, an Android tablet, an iPhone and an iPad: cold launch, touch, safe areas, audio interruptions, background/resume, force-close/reopen, installation/update, all four heroes and the complete campaign. Check a 16KB Android device/emulator and a lower-memory device, and measure sustained frame time and thermals. Re-run native tests after adding any SDK.

After building, run `python Tools/verify_android_package.py Builds/Android/HeroWars-QA.apk` and repeat with the AAB path. The tool reads the actual ELF headers, checks ARM64 and 16KB segment alignment, checks APK alignment for any uncompressed native libraries, and verifies that the development Pipeline runtime type is absent from IL2CPP metadata. Google’s bundletool `validate --bundle=...` also validates the final AAB structure. File hashes and the last package results are recorded in `Artifacts/AndroidPackageValidation.json` and `Artifacts/AndroidBundleValidation.json`.

Run `python Tools/verify_ios_export.py Builds/iOS` to inspect the Xcode export's scene count, identifier, version, portrait/Metal configuration, all icon sizes, native haptics integration and privacy manifests. This is an export inspection; it does not run an Xcode compile, sign an application or install it on a device.

## Store handoff

The store description and privacy text are in `Docs/StoreListing.md` and `Docs/Privacy.md`. The publisher must supply the final account identity, support contact, a hosted privacy URL, signing credentials, content-rating answers and asset-license entitlement evidence. Store availability of the title/identifier is not established by a local build. No store account has been created and no binary has been uploaded.

Requirements checked on 11 November 2026:

- New Google Play submissions target Android 16 / API 36 or later: [Google Play target API requirements](https://support.google.com/googleplay/android-developer/answer/11926878).
- Native Android libraries must support 16KB memory pages: [Android page-size compatibility](https://developer.android.com/guide/practices/page-sizes).
- App Store uploads currently require Xcode 26+ and the iOS 26+ SDK: [Apple's SDK requirement](https://developer.apple.com/news/upcoming-requirements/?id=04282026a). Recheck before upload; Apple has announced the iOS 27 SDK requirement for April 2027 on its [submission page](https://developer.apple.com/app-store/submitting/).

See the final readiness report for actual build results and remaining device/account work. A successful export is not an App Store signing or review result.
