# Plugin icon generator

`generate_icon.py` draws the plugin icon (variant 1b "Orbit": three light dots on a ring, on a
dark rounded tile) as SVG. It was created by the designer; the icon is original artwork, no
third-party source. The OBS Studio logo is deliberately not used or imitated.

```bash
python tools/icon/generate_icon.py -o tools/icon/out/icon.svg --png 256
cp tools/icon/out/icon_256.png icon.png
```

The PNG export needs `pip install cairosvg`, which in turn needs the native Cairo library
(`libcairo-2.dll` on Windows, e.g. from the GTK runtime). Without it, write the SVG only and
render it with any SVG renderer at 256 px with a transparent background; the current `icon.png`
was rendered by headless Edge at 1024 px and downscaled with Lanczos.

Only the 256 px file is used, as `icon.png` in the repo root. The `out/` folder is not committed.
