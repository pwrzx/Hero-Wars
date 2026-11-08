# Tower collapse and facing validation

- Unity Editor 6000.6.0f1 compiles the updated runtime and Editor checks without errors.
- `TowerPhysicsValidation.txt`: 26 actual attacks across all six three-room kill orders and an eight-floor bottom-up collapse. Falling rooms and crowns land on supporting colliders, with zero timeout recoveries. Checks cover stable floor spacing, enemies at hero height, inward facing, moved swipe targets, target rims, input locking, saved partial-run reloads, and next-tower advancement. Runtime errors cause the check to fail.
- Existing pointer/combat playtests pass on all three authored stages: 9, 9, and 12 attacks; final powers 374, 646, and 2,717. Reports: `Physics_Stage1.txt`, `Physics_Stage2.txt`, and `Physics_Stage3.txt`.
- Existing game rules: all 3,186 assertions pass against the saved campaign asset.
- Final Unity Console: zero current errors and warnings. Asset GUID/script audit: 209 files, zero errors and warnings.
- Inspected `Physics_Settled.png` (540x960) and `Physics_TallPhone.png` (450x975). Both show enemy 448 at hero 475's height with the crown resting on the surviving room and the characters angled toward each other.
- Original player save restored and verified byte-for-byte: Stage 3, power 475, cleared masks `[7,7,5,0]`. The temporary background-running override was removed. The Editor remains in Play mode.

Validation used the Unity Editor; no mobile player build or physical-device test was performed.
