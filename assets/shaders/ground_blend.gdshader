shader_type canvas_item;

// 区域 mask（R=森林，G=岩石，B=水域），小分辨率由 GPU 双线性放大，自然柔边。
uniform sampler2D mask_tex : filter_linear;
// 三区色调（与障碍视觉呼应）与混合强度。
uniform vec4 forest_tint : source_color = vec4(0.35, 0.42, 0.30, 1.0);
uniform vec4 rock_tint : source_color = vec4(0.45, 0.43, 0.40, 1.0);
uniform vec4 water_tint : source_color = vec4(0.25, 0.32, 0.38, 1.0);
uniform float strength : hint_range(0.0, 1.0) = 0.55;

void fragment() {
	vec4 base = texture(TEXTURE, UV);
	vec3 m = texture(mask_tex, UV).rgb;
	// 三区权重归一（mask 外区域权重为 0，保持底图原色）
	float wsum = m.r + m.g + m.b;
	vec3 region = forest_tint.rgb * m.r + rock_tint.rgb * m.g + water_tint.rgb * m.b;
	vec3 tinted = base.rgb * mix(vec3(1.0), region / max(wsum, 0.001), clamp(wsum, 0.0, 1.0));
	COLOR = vec4(mix(base.rgb, tinted, strength * clamp(wsum, 0.0, 1.0)), base.a);
}
