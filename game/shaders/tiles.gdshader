shader_type canvas_item;

uniform sampler2D world : filter_nearest;
uniform sampler2D roots : filter_nearest;
uniform sampler2D lut : filter_nearest;
uniform sampler2D cell_a : filter_nearest;
uniform sampler2D cell_b : filter_nearest;
uniform sampler2D cell_c : filter_nearest;
uniform sampler2D snow_tile : filter_nearest;
uniform sampler2D ice_tile : filter_nearest;
uniform sampler2D anim : filter_nearest;
uniform sampler2D anim_slots : filter_nearest;
uniform int tile = 16;
uniform ivec2 origin = ivec2(0);
uniform ivec2 cells = ivec2(1);
uniform bool wrap = false;
uniform bool desaturate = false;
uniform float fade = 1.0;

int byte_of(float v) { return int(v * 255.0 + 0.5); }

int wrap_x(int x, int w) { return x - w * int(floor(float(x) / float(w))); }

ivec2 cell_of(ivec2 c) { return ivec2(wrap_x(c.x, cells.x), clamp(c.y, 0, cells.y - 1)); }

float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }

float value_noise(vec2 p)
{
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

ivec2 root_cell(ivec2 cell, int code)
{
	if (code <= 0) return cell;
	code -= 1;
	return cell_of(cell + ivec2(code % 5 - 2, code / 5));
}

vec4 between_cells(sampler2D tex, ivec2 g)
{
	vec2 f = (vec2(g) + 0.5) / float(tile) - 0.5;
	vec2 c0 = floor(f);
	vec2 t = f - c0;
	ivec2 a = ivec2(c0);
	vec4 v00 = texelFetch(tex, cell_of(a), 0);
	vec4 v10 = texelFetch(tex, cell_of(a + ivec2(1, 0)), 0);
	vec4 v01 = texelFetch(tex, cell_of(a + ivec2(0, 1)), 0);
	vec4 v11 = texelFetch(tex, cell_of(a + ivec2(1, 1)), 0);
	return mix(mix(v00, v10, t.x), mix(v01, v11, t.x), t.y);
}

vec3 texel(ivec2 p)
{
	ivec2 size = textureSize(world, 0);
	p = ivec2(wrap ? wrap_x(p.x, size.x) : clamp(p.x, 0, size.x - 1), clamp(p.y, 0, size.y - 1));
	ivec2 g = p + origin * tile;
	ivec2 cell = cell_of(g / tile);
	ivec2 in_cell = g - (g / tile) * tile;
	vec4 w = texelFetch(world, p, 0);
	vec4 c = texelFetch(cell_c, cell, 0);
	int flags = byte_of(c.g);

	int bits = byte_of(w.a);
	vec4 r = texelFetch(roots, p, 0);
	ivec2 in_season = root_cell(cell, byte_of(r.r)), off_season = root_cell(cell, byte_of(r.g));
	bool off = (byte_of(texelFetch(cell_c, (bits & 1) != 0 ? in_season : (bits & 2) != 0 ? off_season : cell, 0).g) & 1) != 0;
	bool flora = (bits & (off ? 2 : 1)) != 0;
	vec4 a = texelFetch(cell_a, flora ? (off ? off_season : in_season) : cell, 0);
	int slot;
	if (flora)
		slot = byte_of(off ? w.b : w.g);
	else
	{
		slot = byte_of(w.r);
		int kind = byte_of(c.b);
		if (kind > 0 && (byte_of(texelFetch(anim_slots, ivec2(slot, 0), 0).r) & (1 << (kind - 1))) != 0)
		{
			int frame = int(TIME * 3.0) % 4;
			int variant = int(hash(vec2(cell)) * 4.0);
			slot = byte_of(texelFetch(anim, ivec2(frame * tile + in_cell.x, ((kind - 1) * 4 + variant) * tile + in_cell.y), 0).r);
		}
		float snow = between_cells(cell_a, g).a;
		if (snow > 0.01 && snow > 0.7 * value_noise(vec2(g) * 16.0 / float(tile) / 5.0) + 0.3 * hash(vec2(g)))
			slot = byte_of((flags & 2) != 0 ? texelFetch(ice_tile, in_cell, 0).r : texelFetch(snow_tile, in_cell, 0).r);
	}
	vec3 col = mix(texelFetch(lut, ivec2(slot, byte_of(a.r)), 0).rgb, texelFetch(lut, ivec2(slot, byte_of(a.g)), 0).rgb, a.b);
	if (!flora) col *= between_cells(cell_c, g).r * 255.0 / 128.0;
	if (desaturate)
	{
		col = mix(col, vec3(dot(col, vec3(0.299, 0.587, 0.114))), 0.55);
		vec4 tint = between_cells(cell_b, g);
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
