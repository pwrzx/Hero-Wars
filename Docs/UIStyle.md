# In-game UI

Preserve the painted teal setting and the blue/red hero/enemy power colors. Interface surfaces use deep green ink, warm parchment text and muted brass. Keep the playfield dominant.

Typography uses Alegreya SC Bold for the title and result headings, Alegreya Sans Medium for stage metadata and hints, and Alegreya Sans Bold for actions and power. Use the actual font weights without simulated bold. Display sizes are 58/64, action labels 36, instructions 40, body 32/34, metadata 30, and power numerals 44 on the 1080x1920 reference canvas. Numbers can reduce to 34 for larger values.

Home and Pause are secondary dark plates with brass edges and 224x144 reference targets. Restart is available in Pause. The result action is a primary brass plate, 640x112, with a restart or forward icon appropriate to its action. Shared menu buttons use the same chamfered geometry, typography and state colors. Button edges brighten for hover and keyboard selection; presses darken the face and move the contents down slightly; disabled controls retain readable muted text. Child decoration never intercepts pointer input.

UI plates and icons use native uGUI geometry. Font atlases are baked assets; typography and interface geometry work offline without downloaded images or fonts.

Safe areas must be bounded by the current viewport, including transient stale simulator/orientation bounds. Tablet content is centered at a maximum 1120 reference units; the surrounding art fills the screen. Inventory has a visible scroll thumb and readable, autosizing chest odds. Power badges expand for exact values, using invariant number formatting instead of rounding combat-critical power. Reduced motion disables camera shake and scales on modal/reward reveals; battery saver targets 30 fps instead of 60.

The defeat card reserves a 740×144 rewarded-continue button above a separate 640×144 Retry action. It explains that continuing retains power and cleared rooms. When no rewarded ad is ready, its button is disabled and Retry takes the primary brass style. The stage-clear card retains its existing size and progression action. No inline ad banner is displayed in the playfield.
