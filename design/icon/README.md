# App icon

An Android adaptive icon with three layers, written as vector drawables so it is sharp at every density:

* `vn_icon_background.xml`: emerald with a lighter centre and a darker rim, the plain concave glass of the app.
* `vn_icon_foreground.xml`: a paper sheet with a folded corner (notes), an emerald V (Visary) and two lines of text (reading).
* `vn_icon_monochrome.xml`: the same sheet with the V and lines cut out, for themed icons on Android 13 and later.

All of the mark sits inside the 66dp safe circle of the 108dp canvas, so no launcher mask (circle, squircle, rounded square, teardrop, square) cuts it. `preview.png` shows it under those masks at 140 and 48 pixels, and as a themed icon on light and dark wallpapers.

`visary-icon.svg` is the source drawing. Change it and the three XML files together.
