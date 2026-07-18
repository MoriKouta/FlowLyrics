#!/usr/bin/env python3
"""Repair Flow Dots punctuation and add common accented Latin glyphs.

The generated font keeps the existing 5x7 dot vocabulary. It is intentionally
idempotent so a checkout can regenerate the embedded UI font before packaging.
"""

from __future__ import annotations

import argparse
import unicodedata
from pathlib import Path

from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont


DOT = 68
X = (30, 162, 294, 426, 558)
Y = (715, 603, 491, 379, 267, 155, 43)


def add_rect(pen: TTGlyphPen, x: int, y: int, size: int = DOT) -> None:
    pen.moveTo((x, y))
    pen.lineTo((x + size, y))
    pen.lineTo((x + size, y + size))
    pen.lineTo((x, y + size))
    pen.closePath()


def matrix_glyph(rows: tuple[str, ...], *, tail: tuple[int, int] | None = None):
    pen = TTGlyphPen(None)
    for row, pattern in enumerate(rows):
        for column, value in enumerate(pattern):
            if value == "#":
                add_rect(pen, X[column], Y[row])
    if tail is not None:
        add_rect(pen, X[tail[0]], tail[1])
    return pen.glyph()


PUNCTUATION = {
    "!": ("..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.."),
    "%": ("#...#", "...#.", "...#.", "..#..", ".#...", ".#...", "#...#"),
    "&": (".##..", "#..#.", "#..#.", ".##..", "#.#..", "#..#.", ".##.#"),
    "(": ("...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#."),
    ")": (".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..."),
    "?": (".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."),
}


ACCENTS = {
    "\u0300": ((1, 799), (2, 715)),  # grave
    "\u0301": ((2, 715), (3, 799)),  # acute
    "\u0302": ((1, 715), (2, 799), (3, 715)),  # circumflex
    "\u0303": ((1, 759), (2, 715), (3, 759)),  # tilde
    "\u0304": ((1, 799), (2, 799), (3, 799)),  # macron
    "\u0306": ((1, 799), (2, 715), (3, 799)),  # breve
    "\u0307": ((2, 799),),  # dot above
    "\u0308": ((1, 799), (3, 799)),  # diaeresis
    "\u030a": ((1, 715), (2, 799), (3, 715), (2, 715)),  # ring
    "\u030b": ((1, 715), (2, 799), (3, 715), (4, 799)),  # double acute
    "\u030c": ((1, 799), (2, 715), (3, 799)),  # caron
}


def accented_glyph(font: TTFont, base_name: str, combining: str):
    pen = TTGlyphPen(font.getGlyphSet())
    if combining in {"\u0327", "\u0328"}:  # cedilla / ogonek
        font.getGlyphSet()[base_name].draw(pen)
        add_rect(pen, X[2 if combining == "\u0327" else 3], -69, 52)
        if combining == "\u0328":
            add_rect(pen, X[2], 15, 52)
        return pen.glyph()

    transform = TransformPen(pen, (1, 0, 0, 1, 0, -112))
    font.getGlyphSet()[base_name].draw(transform)
    for column, y in ACCENTS[combining]:
        add_rect(pen, X[column] + 8, y, 52)
    return pen.glyph()


def install_glyph(font: TTFont, character: str, glyph) -> None:
    codepoint = ord(character)
    glyph_name = f"uni{codepoint:04X}"
    if glyph_name not in font.getGlyphOrder():
        font.setGlyphOrder(font.getGlyphOrder() + [glyph_name])
    font["glyf"][glyph_name] = glyph
    font["hmtx"].metrics[glyph_name] = (760, 30)
    for table in font["cmap"].tables:
        if table.isUnicode():
            table.cmap[codepoint] = glyph_name


def repair(source: Path, output: Path) -> None:
    font = TTFont(source)
    for character, rows in PUNCTUATION.items():
        install_glyph(font, character, matrix_glyph(rows))

    # Comma and semicolon extend below the baseline instead of rendering as '+'.
    install_glyph(font, ",", matrix_glyph((".....",) * 6 + ("..#..",), tail=(1, -69)))
    install_glyph(font, ";", matrix_glyph(("..#..", ".....", ".....", "..#..", ".....", ".....", "..#.."), tail=(1, -69)))

    cmap = font.getBestCmap()
    for codepoint in range(0x00C0, 0x0180):
        character = chr(codepoint)
        decomposition = unicodedata.normalize("NFD", character)
        if len(decomposition) != 2 or decomposition[0] not in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz":
            continue
        combining = decomposition[1]
        if combining not in ACCENTS and combining not in {"\u0327", "\u0328"}:
            continue
        base_name = cmap.get(ord(decomposition[0]))
        if base_name:
            install_glyph(font, character, accented_glyph(font, base_name, combining))

    font["head"].yMax = max(font["head"].yMax, 851)
    font["maxp"].numGlyphs = len(font.getGlyphOrder())
    output.parent.mkdir(parents=True, exist_ok=True)
    font.save(output)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("font", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    output = args.output or args.font
    temporary = output.with_suffix(output.suffix + ".tmp") if output == args.font else output
    repair(args.font, temporary)
    if temporary != output:
        temporary.replace(output)


if __name__ == "__main__":
    main()
