# Buttons and typography validation

- Replaced the default font with bundled Alegreya SC Bold headings and Alegreya Sans Medium/Bold interface text. Three static TMP font atlases include the game copy; bold numerals have equal advances.
- Added larger ink-and-brass buttons, chamfered plates, drawn action icons, pointer/keyboard focus, pressed feedback, and readable disabled states. Blue/red power semantics remain intact.
- Inspected gameplay and result layouts at 540x960 and 450x975, plus failure, pressed, and keyboard-focus states. Captures are the `UI_*.png` files. All plate/icon meshes have CanvasRenderers and nonempty geometry.
- Every active text role uses a `TT_` font asset with no overflow. `2,717` fits at the full 44-reference-pixel numeral size. Details: `UI_FontChecks.json`.
- Verified physical GraphicRaycaster targeting and routed Retry clicks, disabled-click rejection, keyboard Submit on Try again, and Next stage advancement. Details: `UI_ButtonChecks.txt`.
- The existing pointer/combat playtest passes nine attacks in Stage 2 and reaches power 646, including a stronger-enemy loss, retry, input locking, floor/tower collapse, bounded combat effects, and stage completion. Details: `UI_Playtest.txt`.
- Final compilation passes. The asset/script audit checks 237 files with zero errors or warnings. The current Unity Console has zero errors and warnings. The web-oriented Impeccable detector reports no findings; native UI was verified in the Unity Editor.
- Original player save restored byte-for-byte. Updated the saved scene's generated Editor preview to match the runtime UI. Editor left in Edit mode after it was stopped during the session.

These checks used the Unity Editor, not a mobile player build or physical phone.
