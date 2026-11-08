# In-game UI

Preserve the painted teal setting and the blue/red hero/enemy power colors. Interface surfaces use deep green ink, warm parchment text and muted brass. Keep the playfield dominant.

Typography uses Alegreya SC Bold for the title and result headings, Alegreya Sans Medium for stage metadata and hints, and Alegreya Sans Bold for actions and power. Use the actual font weights without simulated bold. Display sizes are 58/64, action labels 36, instructions 40, body 32/34, metadata 30, and power numerals 44 on the 1080x1920 reference canvas. Numbers can reduce to 34 for larger values.

Retry is a secondary dark plate with a brass edge, drawn restart icon and a 224x104 reference target. The result action is a primary brass plate, 640x112, with a restart or forward icon appropriate to its action. Use short natural-case action labels. Button edges brighten for hover and keyboard selection; presses darken the face and move the contents down slightly; disabled controls retain readable muted text. Child decoration never intercepts pointer input.

UI plates and icons use native uGUI geometry. Font atlases are baked assets; typography and interface geometry work offline without downloaded images or fonts.
