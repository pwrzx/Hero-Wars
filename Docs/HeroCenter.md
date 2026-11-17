# Hero Center

Open `Assets/_Game/Scenes/HeroCenter.unity` and press Play. It is the first enabled scene in Build Settings. The layout scales from a 1080 x 1920 portrait reference and respects the device safe area.

The bottom navigation opens Hero Center, Customize and Shop. Gold, gems, level, settings and the daily gift are available from the menu. Drag the hero to rotate the portrait. Adventure continues the saved tower run; Journey selects unlocked keeps, and Home returns from gameplay. The equipped hero, shield and weapon also appear in the tower game. Home shows total starting power, equipment bonus, training rank and campaign progress. Hero skins remain cosmetic.

Chest weapons and shields grant starting-power bonuses when equipped: Common +5%, Rare +10%, Epic +20%, Legendary +35%. Add the weapon and shield percentages, apply that total to the trained base power, then round up once. Starter gear grants no bonus, preserving the 12-power opening. Owning or previewing an item grants no power; duplicates do not stack. Customize and chest reveals show each item's bonus. Gear changes apply on a fresh attempt without changing enemy powers.

The Unity Editor's Home screen includes **DEV: Reset to zero** beneath Train power. It resets the hero to level one, power 12, rank zero, starter gear, zero gold and gems, no completed stages, no daily receipt and a fresh tutorial. It clears both campaign checkpoints and both profile copies, then saves a fresh profile and reloads Hero Center. Sound and other preferences are preserved. The button and reset implementation are compiled out of all player builds.

Power training opens from Home. Each rank permanently increases starting power by 20%, rounded up: 12, 15, 18, 22, 27. Training costs 100 gold plus 50 per existing rank. Kills award 20 gold plus 4 per level index, including failed attempts. Checkpoint reloads and ad revives cannot award the same kill twice; fresh attempts earn gold again. Gold and training persist after defeat. After training, Adventure restarts the saved keep with the new starting power. Restarting resets kills and power earned within that attempt. The enemies retain exactly the same powers.

Gold training opens from Home beneath Daily gift. Each rank adds 10 percentage points to gold income: +10%, +20%, +30%, up to +1,000%. It costs 200 gold plus 100 per existing gold rank. The bonus applies to enemy-kill and first-clear gold, rounded up per reward. It applies to new rewards immediately without retroactively paying old kills. Daily gifts and duplicate-item compensation retain their published fixed amounts. Gold training persists with the profile and is cleared by the developer reset.

All 24 keeps have five towers of three enemies. Every fresh attempt shuffles the three enemies independently within each tower. Surviving rooms keep their order as the tower settles, including after a resume or ad revive. Keep one starts at 12 versus 7, 13, 29. Clearing the first tower gives 61; tower two contains 54, 74, 134, rounded from 0.88x, 1.22x, 2.2x. Towers three through five introduce progressively larger guard gaps. Each later keep scales the fixed enemy reference by 1.35. Enemies never scale against the player's actual upgrades. Being outmatched does not display a menu or end the run: the player can attack a stronger enemy and lose. The defeat screen offers a rewarded ad for +20% current power or Main menu. Confirmed ad completion revives the same run, preserving cleared rooms; skipped or failed ads grant nothing. The boost is temporary for that attempt and rounds up. Main menu queues a fresh attempt at the same keep for the next Adventure launch. The project currently supplies the provider hook only; real ads require a registered SDK adapter. Version-four checkpoints save the exact floor order and each reward's cleared-room snapshot. Older checkpoints resume with their original floor order. Rebalanced layouts invalidate only the active checkpoint; inventory, currency, permanent training and completed keeps remain intact.

The starter hero is Grove Warden from OlyPoly's FREE Hero Pack. Frost Sentinel and Stone Guardian are Rare chest unlocks; Ember Sovereign is Legendary. Starter gear includes Oakguard Shield and Wayfarer Sword. Customize has four skins, three armor choices (shields), and nine swords, axes and maces. Grove Mantle and Stoneweave Cloak remain part of their original heroes and are not separate inventory items or chest rewards. Old cloak loadouts fall back to an owned shield when loaded. Locked equipment can be previewed; only owned equipment can be equipped.

| Rarity | Color | Chest | Cost | Common / Rare / Epic / Legendary |
| --- | --- | --- | --- | --- |
| Common | Light gray `#CFD6E1` | Wayfarer | 400 gold | 70% / 25% / 4% / 1% |
| Rare | Blue `#479FFF` | Sentinel | 1,200 gold | 0% / 80% / 17% / 3% |
| Epic | Purple `#B86CFF` | Arcane | 50 gems | 0% / 0% / 90% / 10% |
| Legendary | Gold `#FFC743` | Sovereign | 120 gems | 0% / 0% / 0% / 100% |

Each chest awards one random non-starter item in its rarity pool. Duplicates credit 100 / 250 / 600 / 1,200 gold according to rarity. A fresh profile begins with 2,500 gold and 150 gems. The daily gift grants 300 gold and 15 gems once per UTC day. These are local prototype currencies with no purchase or account integration. Inventory, equipped gear and currency persist in `HeroWars.HeroProfile.v1`; the campaign save is separate.

The source packs are imported under `Assets/300Mind`, `Assets/Pack_Heros` and `Assets/GameLair`. Original model geometry, prefabs and texture files are preserved. The UI atlases retain the supplied source dimensions; Android and iOS use ASTC 4x4 overrides. Shared buttons use brass-edged uGUI geometry. URP weapon and cloak materials remain separate assets under `Assets/_Game/HeroCenter/Materials`. Equipment thumbnails are 512 x 512 camera renders; source textures are unaffected. The hero portrait renders at its displayed pixel dimensions, with four-sample antialiasing and a 1024-pixel edge limit.

Equipped heroes retarget the Fire hero's armed humanoid animation controller, so every skin closes its hands and uses a weapon-ready stance. Weapons attach to the right palm; shields attach to the left palm with a rear handle and clearance for gauntlets. Each item's `gripPoint` in the catalog identifies the grasp in prefab coordinates. Equipment length uses model-local bounds, keeping sizes stable when the hero or preview rotates. The same setup is used in the portrait and tower gameplay.

Edit rewards, rarities, weights, prices, grip points and asset references in `Assets/_Game/Resources/HeroCenter/Catalog.asset`. `Tower Trials > Set Up Hero Center` recreates the supplied starter catalog and refreshes the menu scene; keep custom balancing edits in mind before running it again.

Validation commands:

- `Tower Trials > Verify Hero Center`: asset references, starter/unlock rules, 1,600 seeded chest rolls, exact transactions, duplicate rewards, equip gates, save repair and daily gifts. Also checks all hero/weapon pairs and shields at portrait/gameplay sizes, rotated parents, closed hands and grip alignment throughout all armed animation clips. Does not touch the player's save.
- `Tower Trials > Verify Hero Center (Play Mode)`: live raycasts and clicks through customization, chest opening, equip, persistence, the daily gift and the adventure/Home round trip. Uses a temporary profile and restores the player's inventory and campaign save. Run in HeroCenter during Play mode.
- `Tower Trials > Verify Game Rules`: the existing campaign's exhaustive rules and save checks.
- `Tower Trials > Verify Power Progression`: all 24 fixed layouts, exact example values, blocked and upgraded attempts, minimum clear thresholds, percentage training, gold transactions and checkpoint compatibility.
- `Tower Trials > Verify Power Progression (Play Mode)`: isolated pointer combat through a real power wall, four menu upgrades and a five-tower clear. Restores the player's original saves.
- `Tower Trials > Verify Equipment Power & Dev Reset`: rarity bonuses, all weapon/shield combinations, trained power, actual chest loot, save restore and a full reset. Restores the player's original saves in a synchronous `finally` block.
- `Tower Trials > Verify Defeat Rewards & Gold Training`: attack-required defeat, temporary rewarded boosts, repeated rewards and exact checkpoint timing, old-save migration, overflow, gold-training costs, all-level income bonuses and duplicate reward protection.

Reports and screenshots are written to `Artifacts`.
