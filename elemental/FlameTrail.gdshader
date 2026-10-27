shader_type canvas_item;
render_mode blend_add;

uniform sampler2D noise_map : repeat_enable, filter_linear;
uniform float age = 0.0;
uniform float intensity = 1.0;
varying vec4 vertex_tint;
void vertex() { vertex_tint = COLOR; }
void fragment() {
    vec2 uv = UV;
    float n1 = texture(noise_map,vec2(uv.x*2.6-age*2.1,uv.y*1.5)).r;
    float n2 = texture(noise_map,vec2(uv.x*5.2-age*3.4,uv.y*3.0+age*.25)).r;
    float center = uv.y-.5 + (n1-.5)*.27;
    float envelope = pow(max(0.0,sin(uv.x*3.14159265)),.42);
    float body = smoothstep(.45,.035,abs(center)/max(.02,envelope));
    body *= smoothstep(.12,.7,n1*.6+n2*.4+envelope*.35);
    vec3 hot = mix(vec3(.75,.08,.008),vec3(1.0,.40,.035),smoothstep(.15,.65,body));
    hot = mix(hot,vec3(1.0,.86,.45),smoothstep(.7,.98,body));
    COLOR = vec4(hot,body*intensity) * vertex_tint;
}
