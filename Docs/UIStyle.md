# In-game UI

The battle takes place against the Dawn Valley painting. Limestone arches mark occupied rooms, and the hero starts on a blue-bannered balcony. Blue and red badges distinguish hero and enemy power.

Germania One sets keep names and result headings. Oswald Regular sets instructions, and Oswald SemiBold sets action labels and tabular power numerals. On the 1080×1920 reference canvas, the keep name fits at up to 54 units and power badges at up to 44 units.

The stage pennon leaves room for Home and Pause at the top of the playfield. A shield marks the route back to the Hero Center. Nine-sliced 300Mind sprites form gold primary buttons, teal beveled controls and quieter inventory tiles. Hover and keyboard selection brighten the edge. Pressing a button darkens its face and lowers the label.

The sprites and static font atlases ship with the game. Combat badges and gesture icons use uGUI geometry.

Safe areas must be bounded by the current viewport, including transient stale simulator/orientation bounds. Tablet content is centered at a maximum 1120 reference units; the surrounding art fills the screen. Inventory has a visible scroll thumb and readable, autosizing chest odds. Power badges expand for exact values, using invariant number formatting instead of rounding combat-critical power. Reduced motion disables camera shake and scales on modal/reward reveals; battery saver targets 30 fps instead of 60.

The defeat card reserves a 740×144 rewarded-continue button above a separate 640×144 Retry action. It explains that continuing retains power and cleared rooms. When no rewarded ad is ready, its button is disabled and Retry takes the primary brass style. The stage-clear card retains its existing size and progression action. No inline ad banner is displayed in the playfield.
