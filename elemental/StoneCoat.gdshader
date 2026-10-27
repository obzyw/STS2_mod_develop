shader_type canvas_item;
render_mode blend_mix;

uniform sampler2D stone_surface : source_color, repeat_enable, filter_linear_mipmap;
uniform float edv_strength : hint_range(0.0,1.0) = 0.0;
uniform float edv_activation : hint_range(0.0,1.0) = 0.0;
uniform float pattern_pixels = 220.0;
varying vec4 vertex_tint;

void vertex() { vertex_tint = COLOR; }

void fragment() {
    vec4 original = texture(TEXTURE, UV);
    // Atlas UVs stay with the actual skinned mesh: the coat follows deformation and draw order.
    vec2 stone_uv = UV * vec2(textureSize(TEXTURE, 0)) / pattern_pixels;
    vec3 stone = texture(stone_surface, stone_uv).rgb;
    float volume = dot(original.rgb, vec3(0.25,0.55,0.20));
    float shade = mix(0.76,1.22,smoothstep(0.035,0.83,volume));
    vec3 armor = stone * shade;
    // Retain the character's drawn dark contours and local volume, rather than covering it with a decal.
    armor *= mix(0.74,1.0,smoothstep(0.015,0.12,volume));
    vec2 texel = TEXTURE_PIXEL_SIZE * 1.3;
    float nearby = min(min(texture(TEXTURE,UV+vec2(texel.x,0.0)).a,texture(TEXTURE,UV-vec2(texel.x,0.0)).a),
                       min(texture(TEXTURE,UV+vec2(0.0,texel.y)).a,texture(TEXTURE,UV-vec2(0.0,texel.y)).a));
    float edge = max(0.0,original.a-nearby);
    armor += vec3(0.65,0.15,0.42) * edge * edv_activation * 0.65;
    COLOR = vec4(mix(original.rgb,armor,edv_strength), original.a) * vertex_tint;
}
