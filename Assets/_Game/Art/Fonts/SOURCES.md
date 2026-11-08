# Game UI fonts

- Display headings: [Alegreya SC Bold](https://github.com/google/fonts/tree/main/ofl/alegreyasc).
- Body, metadata and hints: [Alegreya Sans Medium](https://github.com/google/fonts/tree/main/ofl/alegreyasans).
- Actions and power numerals: Alegreya Sans Bold, from the same family.

The original font files are bundled locally. Their SIL Open Font License notices are included alongside them as `AlegreyaSC-OFL.txt` and `AlegreyaSans-OFL.txt`.

`Tower Trials > Prepare UI Fonts` creates the three static TextMesh Pro font assets in `Assets/_Game/Resources/Fonts`. The bold asset uses equal numeral advances so rolling power values stay stable. The game requires no font downloads at runtime.
