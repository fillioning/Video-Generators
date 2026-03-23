/*{
    "CATEGORIES": [
        "Generator"
    ],
    "CREDIT": "fillioning",
    "DESCRIPTION": "Asymmetric Rorschach — organic ink blot generator with controllable symmetry breaking. Domain-warped FBM builds classic Rorschach shapes, while the Asymmetry parameter blends from perfect mirror symmetry to fully independent halves.",
    "INPUTS": [
        {
            "DEFAULT": 0.1,
            "LABEL": "Speed",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "speed",
            "TYPE": "float"
        },
        {
            "DEFAULT": 4,
            "LABEL": "Complexity",
            "MAX": 8,
            "MIN": 1,
            "NAME": "complexity",
            "TYPE": "long"
        },
        {
            "DEFAULT": 0.5,
            "LABEL": "Ink Threshold",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "threshold",
            "TYPE": "float"
        },
        {
            "DEFAULT": 0.15,
            "LABEL": "Edge Softness",
            "MAX": 0.5,
            "MIN": 0.01,
            "NAME": "edgeSoftness",
            "TYPE": "float"
        },
        {
            "DEFAULT": 0.5,
            "LABEL": "Asymmetry",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "asymmetry",
            "TYPE": "float"
        },
        {
            "DEFAULT": 2.0,
            "LABEL": "Scale",
            "MAX": 8.0,
            "MIN": 0.5,
            "NAME": "scale",
            "TYPE": "float"
        },
        {
            "DEFAULT": [0.05, 0.05, 0.08, 1.0],
            "LABEL": "Ink Color",
            "NAME": "inkColor",
            "TYPE": "color"
        },
        {
            "DEFAULT": [0.92, 0.90, 0.85, 1.0],
            "LABEL": "Paper Color",
            "NAME": "paperColor",
            "TYPE": "color"
        },
        {
            "DEFAULT": 0.6,
            "LABEL": "Distortion",
            "MAX": 2.0,
            "MIN": 0.0,
            "NAME": "distortion",
            "TYPE": "float"
        },
        {
            "DEFAULT": 0.3,
            "LABEL": "Vignette",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "vignette",
            "TYPE": "float"
        },
        {
            "DEFAULT": false,
            "LABEL": "Invert",
            "NAME": "invert",
            "TYPE": "bool"
        },
        {
            "DEFAULT": 2,
            "LABEL": "Layers",
            "MAX": 5,
            "MIN": 1,
            "NAME": "layers",
            "TYPE": "long"
        }
    ],
    "ISFVSN": "2"
}*/

// ─── Hash & noise primitives ───────────────────────────────────────

float hash21(vec2 p) {
    p = fract(p * vec2(443.897, 441.423));
    p += dot(p, p + 19.19);
    return fract(p.x * p.y);
}

float hash11(float p) {
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

// Value noise with smooth interpolation
float valueNoise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));

    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// FBM with variable octaves
float fbm(vec2 p, int octaves) {
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;

    for (int i = 0; i < 8; i++) {
        if (i >= octaves) break;
        value += amplitude * valueNoise(p * frequency);
        frequency *= 2.0;
        amplitude *= 0.5;
    }
    return value;
}

// ─── Domain-warped blot shape ──────────────────────────────────────

float blotField(vec2 p, float t, int oct, float dist, vec2 seed) {
    // First warp layer
    vec2 q = vec2(
        fbm(p + seed + t * 0.3, oct),
        fbm(p + seed + vec2(5.2, 1.3) + t * 0.2, oct)
    );

    // Second warp layer — controlled by distortion
    vec2 r = vec2(
        fbm(p + dist * q + vec2(1.7, 9.2) + t * 0.15, oct),
        fbm(p + dist * q + vec2(8.3, 2.8) + t * 0.12, oct)
    );

    return fbm(p + dist * r, oct);
}

// ─── Single blot layer ─────────────────────────────────────────────

float blotLayer(vec2 uv, float t, float layerIdx) {
    vec2 layerSeed = vec2(layerIdx * 17.31, layerIdx * 11.57);

    // Symmetric component: mirror x via abs (classic Rorschach)
    vec2 symUV = vec2(abs(uv.x), uv.y);
    float sym = blotField(symUV * scale, t, complexity, distortion, layerSeed);

    // Asymmetric component: original uv — each side gets independent noise
    vec2 asymSeed = layerSeed + vec2(100.0, 200.0);
    float asym = blotField(uv * scale, t, complexity, distortion, asymSeed);

    // Blend between symmetric and asymmetric
    float field = mix(sym, asym, asymmetry);

    // Threshold into ink with soft edges
    float lo = threshold - edgeSoftness * 0.5;
    float hi = threshold + edgeSoftness * 0.5;
    float ink = smoothstep(lo, hi, field);

    return ink;
}

// ─── Main ──────────────────────────────────────────────────────────

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    vec2 centered = (uv - 0.5) * vec2(aspect, 1.0);

    float t = TIME * speed * 10.0;

    // Accumulate blot layers
    float inkAcc = 0.0;
    for (int i = 0; i < 5; i++) {
        if (i >= layers) break;
        float fi = float(i);
        vec2 offset = vec2(
            hash11(fi * 3.17) - 0.5,
            hash11(fi * 5.23) - 0.5
        ) * 0.3;
        float timeOffset = hash11(fi * 7.91) * 10.0;
        float layer = blotLayer(centered + offset, t + timeOffset, fi);
        float weight = 1.0 / float(i + 1);
        inkAcc += layer * weight;
    }

    // Normalize
    float totalWeight = 0.0;
    for (int i = 0; i < 5; i++) {
        if (i >= layers) break;
        totalWeight += 1.0 / float(i + 1);
    }
    float ink = clamp(inkAcc / totalWeight, 0.0, 1.0);

    // Invert if requested
    if (invert) {
        ink = 1.0 - ink;
    }

    // Mix ink and paper colors
    vec3 color = mix(paperColor.rgb, inkColor.rgb, ink);

    // Vignette
    float vig = 1.0 - vignette * length(uv - 0.5) * 1.4;
    color *= vig;

    gl_FragColor = vec4(color, 1.0);
}
