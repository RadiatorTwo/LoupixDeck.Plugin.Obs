#!/usr/bin/env python3
"""Erzeugt das LoupixDeck-OBS-Plugin-Icon (Variante 1b "Orbit").

Nutzung:
    python generate_icon_1b.py                    # -> obs_icon_1b.svg
    python generate_icon_1b.py -o icon.svg --png 512 90
PNG-Export benötigt optional: pip install cairosvg
"""
import argparse
import math
from pathlib import Path

ACCENT = "#e6e6e6"
CX, CY = 256, 248
ORBIT_R = 108
DOT_R = 38


def build_svg(size: int = 512, dots: int = 3, rotation: float = -90.0) -> str:
    dot_svg = "\n    ".join(
        f'<circle cx="{CX + ORBIT_R * math.cos(math.radians(rotation + i * 360 / dots)):.1f}" '
        f'cy="{CY + ORBIT_R * math.sin(math.radians(rotation + i * 360 / dots)):.1f}" r="{DOT_R}" fill="url(#gl)"/>'
        for i in range(dots)
    )
    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="{size}" height="{size}">
  <defs>
    <linearGradient id="kb" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3a3d44"/><stop offset="1" stop-color="#16181c"/></linearGradient>
    <linearGradient id="kf" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2c2f35"/><stop offset="1" stop-color="#1f2125"/></linearGradient>
    <linearGradient id="kr" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffffff" stop-opacity=".22"/><stop offset=".5" stop-color="#ffffff" stop-opacity="0"/></linearGradient>
    <linearGradient id="gl" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#f7f7f7"/><stop offset="1" stop-color="#b9babd"/></linearGradient>
    <filter id="ds" x="-20%" y="-20%" width="140%" height="140%"><feDropShadow dx="0" dy="6" stdDeviation="6" flood-color="#000" flood-opacity=".45"/></filter>
  </defs>
  <rect x="24" y="30" width="464" height="464" rx="104" fill="#0a0b0c" opacity=".6"/>
  <rect x="24" y="20" width="464" height="464" rx="104" fill="url(#kb)"/>
  <rect x="52" y="44" width="408" height="408" rx="84" fill="url(#kf)"/>
  <rect x="25.5" y="21.5" width="461" height="461" rx="102.5" fill="none" stroke="url(#kr)" stroke-width="3"/>
  <g filter="url(#ds)">
    <circle cx="{CX}" cy="{CY}" r="{ORBIT_R}" fill="none" stroke="{ACCENT}" stroke-opacity=".35" stroke-width="16"/>
    {dot_svg}
  </g>
</svg>
'''


def main() -> None:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("-o", "--output", default="obs_icon_1b.svg")
    p.add_argument("--dots", type=int, default=3, help="Anzahl Punkte auf dem Orbit")
    p.add_argument("--rotation", type=float, default=-90.0, help="Startwinkel in Grad (-90 = oben)")
    p.add_argument("--png", type=int, nargs="*", metavar="PX", help="zusätzlich PNGs in diesen Größen")
    a = p.parse_args()

    out = Path(a.output)
    svg = build_svg(dots=a.dots, rotation=a.rotation)
    out.write_text(svg, encoding="utf-8")
    print(f"SVG: {out}")

    if a.png:
        try:
            import cairosvg
        except ImportError:
            raise SystemExit("PNG-Export braucht cairosvg: pip install cairosvg")
        for px in a.png:
            png = out.with_name(f"{out.stem}_{px}.png")
            cairosvg.svg2png(bytestring=svg.encode(), write_to=str(png), output_width=px, output_height=px)
            print(f"PNG: {png}")


if __name__ == "__main__":
    main()
