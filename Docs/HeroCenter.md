# Hero Center

Open `Assets/_Game/Scenes/HeroCenter.unity` and press Play. It is the first enabled scene in Build Settings. The layout scales from a 1080 x 1920 portrait reference and respects the device safe area.

The bottom navigation opens Hero Center, Customize and Shop. Gold, gems, level, settings and the daily gift are available from the menu. Drag the hero to rotate the portrait. Adventure continues the existing saved tower run; Home returns from gameplay. The equipped hero, armor and weapon also appear in the tower game. Collection power describes cosmetic equipment; the existing tower puzzle's power and combat rules remain unchanged.

The starter hero is Grove Warden from OlyPoly's FREE Hero Pack. Frost Sentinel and Stone Guardian are Rare chest unlocks; Ember Sovereign is Legendary. Starter gear includes Oakguard Shield and Wayfarer Sword. Customize has four skins, five armor choices (the packs' shields and cloaks), and nine swords, axes and maces. Locked equipment can be previewed; only owned equipment can be equipped.

| Rarity | Color | Chest | Cost | Common / Rare / Epic / Legendary |
| --- | --- | --- | --- | --- |
| Common | Light gray `#CFD6E1` | Wayfarer | 400 gold | 70% / 25% / 4% / 1% |
| Rare | Blue `#479FFF` | Sentinel | 1,200 gold | 0% / 80% / 17% / 3% |
| Epic | Purple `#B86CFF` | Arcane | 50 gems | 0% / 0% / 90% / 10% |
| Legendary | Gold `#FFC743` | Sovereign | 120 gems | 0% / 0% / 0% / 100% |

Each chest awards one random non-starter item in its rarity pool. Duplicates credit 100 / 250 / 600 / 1,200 gold according to rarity. A fresh profile begins with 2,500 gold and 150 gems. The daily gift grants 300 gold and 15 gems once per UTC day. These are local prototype currencies with no purchase or account integration. Inventory, equipped gear and currency persist in `HeroWars.HeroProfile.v1`; the campaign save is separate.

The source packs are imported under `Assets/300Mind`, `Assets/Pack_Heros` and `Assets/GameLair`. Original model geometry, prefabs and texture files are preserved. The UI atlases' import cap is lifted from 2048 to 4096 with compression disabled, retaining all pixels in the supplied 3118 x 1754 source files. Nine-slice button sprites reference those atlases. URP weapon and two-sided cloak materials are separate assets under `Assets/_Game/HeroCenter/Materials`. Equipment thumbnails are new 512 x 512 camera renders; the source textures are unaffected. The hero portrait renders at its displayed pixel dimensions, with four-sample antialiasing and a 2048-pixel edge limit.

Edit rewards, rarities, weights, prices and asset references in `Assets/_Game/Resources/HeroCenter/Catalog.asset`. `Tower Trials > Set Up Hero Center` recreates the supplied starter catalog and refreshes the menu scene; keep custom balancing edits in mind before running it again.

Validation commands:

- `Tower Trials > Verify Hero Center`: asset references, starter/unlock rules, 1,600 seeded chest rolls, exact transactions, duplicate rewards, equip gates, save repair and daily gifts. Does not touch the player's save.
- `Tower Trials > Verify Hero Center (Play Mode)`: live raycasts and clicks through customization, chest opening, equip, persistence, the daily gift and the adventure/Home round trip. Uses a temporary profile and restores the player's inventory and campaign save. Run in HeroCenter during Play mode.
- `Tower Trials > Verify Game Rules`: the existing campaign's exhaustive rules and save checks.

Reports and screenshots are written to `Artifacts`.
