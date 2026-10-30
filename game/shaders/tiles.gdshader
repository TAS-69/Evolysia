// Colours the baked world (TileBake): each texel holds palette-slot indices, and the colour of a
// slot comes from lut_nature for the texel's cell (biome x season, blended at season changes).
// Snow, sea ice, hillshade and culture tints are applied per cell, so nothing is rebaked as the
// year turns. Pixels stay crisp at any zoom: neighbouring texels blend only across the one
// screen pixel where they meet.
shader_type canvas_item;

uniform sampler2D world : filter_nearest;     // R ground, G flora in season, B flora off-season, A flora bits
uniform sampler2D lut : filter_nearest;       // lut_nature: 256 slots x (biome x season) rows
uniform sampler2D cell_a : filter_nearest;    // per cell: LUT row A, row B, blend A->B, snow cover
uniform sampler2D cell_b : filter_nearest;    // per cell: culture tint rgb x strength, strength
uniform sampler2D cell_c : filter_nearest;    // per cell: hillshade (x128), flags (1 off-season, 2 water)
uniform sampler2D snow_tile : filter_nearest; // slot indices of a snow tile (16 x 16)
uniform sampler2D ice_tile : filter_nearest;  // slot indices of an ice tile (16 x 16)
uniform bool desaturate = false;              // culture view: grey land, tinted territory
uniform float fade = 1.0;

const int TILE = 16;

int byte_of(float v) { return int(v * 255.0 + 0.5); }

int wrap_x(int x, int w) { return x - w * int(floor(float(x) / float(w))); }

ivec2 cell_of(ivec2 c, ivec2 cells) { return ivec2(wrap_x(c.x, cells.x), clamp(c.y, 0, cells.y - 1)); }

float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

float value_noise(vec2 p)
{
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// A per-cell value interpolated between cell centres, so snow drifts, hillshade and territory
// have soft edges instead of following the cell grid.
vec4 between_cells(sampler2D tex, ivec2 p, ivec2 cells)
{
	vec2 f = (vec2(p) + 0.5) / float(TILE) - 0.5;
	vec2 c0 = floor(f);
	vec2 t = f - c0;
	ivec2 a = ivec2(c0);
	vec4 v00 = texelFetch(tex, cell_of(a, cells), 0);
	vec4 v10 = texelFetch(tex, cell_of(a + ivec2(1, 0), cells), 0);
	vec4 v01 = texelFetch(tex, cell_of(a + ivec2(0, 1), cells), 0);
	vec4 v11 = texelFetch(tex, cell_of(a + ivec2(1, 1), cells), 0);
	return mix(mix(v00, v10, t.x), mix(v01, v11, t.x), t.y);
}

vec3 texel(ivec2 p)
{
	ivec2 size = textureSize(world, 0);
	ivec2 cells = size / TILE;
	p = ivec2(wrap_x(p.x, size.x), clamp(p.y, 0, size.y - 1));
	ivec2 cell = p / TILE;
	vec4 w = texelFetch(world, p, 0);
	vec4 a = texelFetch(cell_a, cell, 0);
	vec4 c = texelFetch(cell_c, cell, 0);
	int flags = byte_of(c.g);
	bool off = (flags & 1) != 0;
	bool flora = (byte_of(w.a) & (off ? 2 : 1)) != 0;
	int slot;
	if (flora)
		slot = byte_of(off ? w.b : w.g);
	else
	{
		slot = byte_of(w.r);
		float snow = between_cells(cell_a, p, cells).a;
		if (snow > 0.01 && snow > 0.7 * value_noise(vec2(p) / 5.0) + 0.3 * hash(vec2(p)))
		{
			ivec2 tp = ivec2(p.x % TILE, p.y % TILE);
			slot = byte_of((flags & 2) != 0 ? texelFetch(ice_tile, tp, 0).r : texelFetch(snow_tile, tp, 0).r);
		}
	}
	vec3 col = mix(texelFetch(lut, ivec2(slot, byte_of(a.r)), 0).rgb, texelFetch(lut, ivec2(slot, byte_of(a.g)), 0).rgb, a.b);
	if (!flora) col *= between_cells(cell_c, p, cells).r * 255.0 / 128.0;
	if (desaturate)
	{
		col = mix(col, vec3(dot(col, vec3(0.299, 0.587, 0.114))), 0.55);
		vec4 tint = between_cells(cell_b, p, cells);   // rgb premultiplied by strength
		if (tint.a > 0.0) col = mix(col, tint.rgb / tint.a, tint.a * 0.45);
	}
	return col;
}

void fragment()
{
	vec2 size = vec2(textureSize(world, 0));
	vec2 tp = UV * size;
	vec2 fw = max(fwidth(tp), vec2(1e-5));
	vec2 base = floor(tp - 0.5);
	vec2 wt = clamp((tp - 0.5 - base - 0.5) / fw + 0.5, 0.0, 1.0);
	ivec2 ib = ivec2(base);
	vec3 col;
	if ((wt.x == 0.0 || wt.x == 1.0) && (wt.y == 0.0 || wt.y == 1.0))
		col = texel(ib + ivec2(wt));
	else
		col = mix(mix(texel(ib), texel(ib + ivec2(1, 0)), wt.x), mix(texel(ib + ivec2(0, 1)), texel(ib + ivec2(1, 1)), wt.x), wt.y);
	COLOR = vec4(col, fade);
}
