shader_type canvas_item;

varying vec4 modulate;

void vertex()
{
	modulate = COLOR;
}

void fragment()
{
	vec2 size = vec2(textureSize(TEXTURE, 0));
	vec2 tp = UV * size;
	vec2 fw = max(fwidth(tp), vec2(1e-5));
	vec2 base = floor(tp - 0.5) + 0.5;
	vec2 wt = clamp((tp - base - 0.5) / fw + 0.5, 0.0, 1.0);
	COLOR = texture(TEXTURE, (base + wt) / size) * modulate;
}
