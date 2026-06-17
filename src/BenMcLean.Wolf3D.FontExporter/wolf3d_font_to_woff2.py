"""
Converts Wolf3D chunk font JSON (from FontExporter) to WOFF2.
Usage: python wolf3d_font_to_woff2.py <font.json> [output.woff2]

Dependencies (Python 3 packages):
  pip install fonttools brotli

Pixel aspect ratio: 320x200 at 4:3 gives pixels that are 6/5 taller than wide.
Each pixel becomes a 5-wide x 6-tall rectangle in font units (not a square).
UPM = font_height * SCALE_Y, advance widths = pixel_width * SCALE_X.

Extended character mappings (not in ASCII 32-126):
  U+2018 U+2019 left/right single quote  -> apostrophe (U+0027)
  U+201C U+201D left/right double quote  -> double quote (U+0022)
  U+2013 en dash  )  game glyph 139 (horizontal rule) if present, else hyphen (U+002D)
  U+2014 em dash  )
  U+00A9 copyright   game glyph 140 (circle/ring symbol)
  U+2022 bullet      period (U+002E) shifted to vertical center (both fonts)

Game-internal codepoints 128-138 (vertical bar, bold digits 0-9) are excluded entirely:
they have no correct Unicode mapping and are not useful on a web page.
"""

import json
import sys
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

# 320x200 at 4:3: pixel aspect ratio = (4/3) * (200/320) = 5:6
# Pixels are 6/5 taller than wide.
SCALE_X = 5  # font units per pixel column
SCALE_Y = 6  # font units per pixel row


def draw_pixel(pen, col, row, height):
	# Clockwise winding for TrueType outer contour (Y-up coordinates):
	# bottom-left -> top-left -> top-right -> bottom-right
	x0 = col * SCALE_X
	y0 = (height - 1 - row) * SCALE_Y
	pen.moveTo((x0, y0))
	pen.lineTo((x0, y0 + SCALE_Y))
	pen.lineTo((x0 + SCALE_X, y0 + SCALE_Y))
	pen.lineTo((x0 + SCALE_X, y0))
	pen.closePath()


def glyph_from_pixels(pixels, width, height):
	"""Build a TTGlyph from a flat pixel array (1=on, 0=off), row-major top-to-bottom."""
	pen = TTGlyphPen(None)
	for row in range(height):
		for col in range(width):
			if pixels[row * width + col]:
				draw_pixel(pen, col, row, height)
	return pen.glyph()


def shift_pixels_to_vertical_center(pixels, width, height):
	"""Return new pixel array with on-pixels shifted to vertical center of the em square."""
	on_rows = [r for r in range(height) if any(pixels[r * width + c] for c in range(width))]
	if not on_rows:
		return pixels
	glyph_height = max(on_rows) - min(on_rows) + 1
	shift = (height - glyph_height) // 2 - min(on_rows)
	new_pixels = [0] * (width * height)
	for r in on_rows:
		new_r = r + shift
		if 0 <= new_r < height:
			for c in range(width):
				new_pixels[new_r * width + c] = pixels[r * width + c]
	return new_pixels


def build_woff2(font_json_path, output_path):
	with open(font_json_path, 'r', encoding='utf-8') as f:
		data = json.load(f)

	font_name = data['Name']
	height = data['Height']
	# UPM = height * SCALE_Y so each pixel row = SCALE_Y font units
	upm = height * SCALE_Y

	glyphs_by_cp = {g['Codepoint']: g for g in data['Glyphs']}

	glyph_order = ['.notdef']
	cmap = {}
	glyph_objects = {}
	metrics = {}

	# .notdef: thin rectangle outline, proportioned for the 4:3 pixel grid
	notdef_pen = TTGlyphPen(None)
	notdef_w = height * SCALE_X  # as wide as a square character would be tall
	notdef_pen.moveTo((SCALE_X, 0))
	notdef_pen.lineTo((SCALE_X, upm - SCALE_Y))
	notdef_pen.lineTo((notdef_w - SCALE_X, upm - SCALE_Y))
	notdef_pen.lineTo((notdef_w - SCALE_X, 0))
	notdef_pen.closePath()
	glyph_objects['.notdef'] = notdef_pen.glyph()
	metrics['.notdef'] = (notdef_w, 0)

	# Game-internal codepoints with no correct Unicode mapping — handled selectively below.
	GAME_INTERNAL = set(range(128, 141))

	# Build glyphs for all standard codepoints, skipping game-internal ones.
	for cp in sorted(glyphs_by_cp.keys()):
		if cp in GAME_INTERNAL:
			continue
		g = glyphs_by_cp[cp]
		name = f'uni{cp:04X}'
		glyph_order.append(name)
		cmap[cp] = name
		glyph_objects[name] = glyph_from_pixels(g['Pixels'], g['Width'], height)
		metrics[name] = (g['Width'] * SCALE_X, 0)

	# Codepoint 139: horizontal rule -> em dash (U+2014) and en dash (U+2013).
	# Only present in SMALL; BIG falls back to the hyphen alias below.
	if 139 in glyphs_by_cp:
		g = glyphs_by_cp[139]
		dash_name = 'wolf3d_dash'
		glyph_order.append(dash_name)
		glyph_objects[dash_name] = glyph_from_pixels(g['Pixels'], g['Width'], height)
		metrics[dash_name] = (g['Width'] * SCALE_X, 0)
		cmap[0x2013] = dash_name  # en dash
		cmap[0x2014] = dash_name  # em dash

	# Codepoint 140: circle/ring -> copyright symbol (U+00A9).
	# Only present in SMALL.
	if 140 in glyphs_by_cp:
		g = glyphs_by_cp[140]
		copyright_name = 'wolf3d_copyright'
		glyph_order.append(copyright_name)
		glyph_objects[copyright_name] = glyph_from_pixels(g['Pixels'], g['Width'], height)
		metrics[copyright_name] = (g['Width'] * SCALE_X, 0)
		cmap[0x00A9] = copyright_name

	# Aliases: map Unicode characters to existing ASCII glyphs via cmap only.
	# Each entry is skipped if the target was already assigned above.
	aliases = [
		(0x2018, 0x0027),  # left single quotation mark  -> apostrophe
		(0x2019, 0x0027),  # right single quotation mark -> apostrophe
		(0x201C, 0x0022),  # left double quotation mark  -> double quote
		(0x201D, 0x0022),  # right double quotation mark -> double quote
		(0x2013, 0x002D),  # en dash fallback (BIG)      -> hyphen
		(0x2014, 0x002D),  # em dash fallback (BIG)      -> hyphen
	]
	for target_cp, source_cp in aliases:
		if target_cp not in cmap and source_cp in cmap:
			cmap[target_cp] = cmap[source_cp]

	# Bullet: period shifted to vertical center (both fonts).
	if 0x002E in glyphs_by_cp:
		period = glyphs_by_cp[0x002E]
		bullet_pixels = shift_pixels_to_vertical_center(period['Pixels'], period['Width'], height)
		bullet_name = 'wolf3d_bullet'
		glyph_order.append(bullet_name)
		cmap[0x2022] = bullet_name
		glyph_objects[bullet_name] = glyph_from_pixels(bullet_pixels, period['Width'], height)
		metrics[bullet_name] = (period['Width'] * SCALE_X, 0)

	fb = FontBuilder(upm, isTTF=True)
	fb.setupGlyphOrder(glyph_order)
	fb.setupCharacterMap(cmap)
	fb.setupGlyf(glyph_objects)
	fb.setupHorizontalMetrics(metrics)
	fb.setupHorizontalHeader(ascent=upm, descent=0)
	fb.setupNameTable({
		'familyName': f'Wolf3D {font_name}',
		'styleName': 'Regular',
	})
	fb.setupOS2(
		sTypoAscender=upm,
		sTypoDescender=0,
		sTypoLineGap=SCALE_Y,
		usWinAscent=upm,
		usWinDescent=0,
		sxHeight=upm * 2 // 3,
		sCapHeight=upm,
		fsType=0,
	)
	fb.setupPost()
	fb.setupHead(unitsPerEm=upm)

	fb.font.flavor = 'woff2'
	fb.font.save(output_path)
	print(f'Saved {output_path}  (height={height}px, use CSS font-size: {height * SCALE_Y}px or multiples of {SCALE_Y}px)')


if __name__ == '__main__':
	if len(sys.argv) < 2:
		print('Usage: python wolf3d_font_to_woff2.py <font.json> [output.woff2]')
		sys.exit(1)
	json_path = sys.argv[1]
	out_path = sys.argv[2] if len(sys.argv) > 2 else json_path.replace('.json', '.woff2')
	build_woff2(json_path, out_path)
