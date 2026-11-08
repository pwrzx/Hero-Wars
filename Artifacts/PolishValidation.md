# Combat polish validation

- Final game-rule check: 3,186 assertions pass against the saved campaign, including reachable power totals, no ties, solvable towers, invalid states and save restoration.
- Final pointer/animation playtests pass on all three stages: 9, 9 and 12 successful attacks, with final powers 374, 646 and 2,717. See `Polish_Stage1.txt`, `Polish_Stage2.txt` and `Polish_Stage3.txt`.
- Playtests cover a stronger-enemy loss, retry, short-gesture rejection, extra-swipe rejection during combat, one actual weapon-rig swing and one impact/slash per attack, room disappearance, hero returning home, tower collapse/advance, stage completion and one EventSystem.
- Combat pulses remain within the 24-item pool and combat particles within 160. Ambient motes are separately capped at 32.
- Both URP shaders compile without messages. Final Play-mode Console reports zero errors and warnings.
- Final asset audit checks 205 assets with zero errors or warnings.
- Five synthesized audio clips contain non-silent, unclipped samples; see `AudioValidation.txt`.
- Portrait captures inspected at 540x960 and 450x975. `Polished_Prototype.png` shows the stage; `Polished_Impact.png` captures the impact frame using a server-side condition; `Polished_TallPhone.png` checks the taller phone layout.
- Fixed a scene-loading regression found during the final pass: native MaterialPropertyBlock creation now occurs in CharacterMotion.Configure on the main thread, rather than a MonoBehaviour field initializer. All three stage playtests were rerun after that fix.

These are Unity Editor checks, not physical-phone performance measurements. The characters still use original starter mesh pieces with procedural pivots; production skeletal rigs and final character art remain future work. Pre-polish player progress is restored after testing, with a backup in the ignored UserSettings folder.
