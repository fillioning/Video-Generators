/*{
    "CATEGORIES": [
        "Generator"
    ],
    "CREDIT": "fillioning",
    "DESCRIPTION": "Resonance Generator — dark organic fluid forms with noise dithering, circular wavefronts, and streak artifacts. Inspired by acoustic resonance patterns and video feedback decay.",
    "INPUTS": [
        {
            "DEFAULT": 0.5,
            "LABEL": "Turbulence",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "turbulence",
            "TYPE": "float"
        },
        {
            "DEFAULT": 0.4,
            "LABEL": "Density",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "density",
            "TYPE": "float"
        },
        {
            "DEFAULT": 0.6,
            "LABEL": "Grain",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "grain",
            "TYPE": "float"
        },
        {
            "DEFAULT": 0.3,
            "LABEL": "Rings",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "rings",
            "TYPE": "float"
        },
        {
            "DEFAULT": 0.2,
            "LABEL": "Streaks",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "streaks",
            "TYPE": "float"
        },
        {
            "DEFAULT": 0.15,
            "LABEL": "Warmth",
            "MAX": 1.0,
            "MIN": 0.0,
            "NAME": "warmth",
            "TYPE": "float"
        },
        {
            "DEFAULT": 0.05,
            "LABEL": "Speed",
            "MAX": 0.5,
            "MIN": 0.0,
            "NAME": "speed",
            "TYPE": "float"
        },
        {
            "DEFAULT": 3.0,
            "LABEL": "Scale",
            "MAX": 8.0,
            "MIN": 0.5,
            "NAME": "noiseScale",
            "TYPE": "float"
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

// FBM — fractal Brownian motion
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

// Domain-warped FBM for organic fluid shapes
float warpedFbm(vec2 p, float t) {
    vec2 q = vec2(
        fbm(p + vec2(0.0, 0.0) + t * 0.3, 5),
        fbm(p + vec2(5.2, 1.3) + t * 0.2, 5)
    );

    vec2 r = vec2(
        fbm(p + noiseScale * q + vec2(1.7, 9.2) + t * 0.15, 5),
        fbm(p + noiseScale * q + vec2(8.3, 2.8) + t * 0.12, 5)
    );

    return fbm(p + noiseScale * r * turbulence, 6);
}

// ─── Dithering / stipple noise ─────────────────────────────────────

float stipple(vec2 uv, float intensity) {
    // High-frequency hash noise to simulate stipple/halftone grain
    float n = hash21(uv * RENDERSIZE * 1.5 + TIME * 0.1);
    // Threshold modulated by intensity
    float threshold = intensity * (1.0 - grain) + grain * 0.5;
    // Blend between smooth and hard-stippled
    float soft = intensity;
    float hard = step(n, intensity) * intensity;
    return mix(soft, hard, grain * 0.8);
}

// ─── Circular ring wavefronts ──────────────────────────────────────

float ringWave(vec2 uv, vec2 center, float time, float idx) {
    float dist = length(uv - center);
    // Expanding ring
    float ringRadius = fract(time * 0.08 + idx * 0.37) * 1.5;
    float ringWidth = 0.02 + 0.03 * rings;
    float ring = smoothstep(ringWidth, 0.0, abs(dist - ringRadius));
    // Fade as ring expands
    ring *= smoothstep(1.5, 0.0, ringRadius);
    // Thin dark outline
    ring *= 0.6 + 0.4 * sin(dist * 40.0 - time * 2.0);
    return ring;
}

// ─── Diagonal streak artifacts ─────────────────────────────────────

float diagonalStreaks(vec2 uv, float t) {
    float acc = 0.0;
    for (int i = 0; i < 6; i++) {
        float fi = float(i);
        float seed = hash11(fi * 7.31 + 0.5);
        // Position along diagonal
        float phase = fract(t * 0.05 * (0.5 + seed) + seed * 6.28);
        vec2 origin = vec2(
            mix(-0.3, 1.3, hash11(fi * 3.17)),
            mix(-0.3, 1.3, hash11(fi * 5.23 + 2.0))
        );
        // Streak direction — mostly diagonal
        float angle = 1.1 + seed * 0.6;
        vec2 dir = vec2(cos(angle), sin(angle));
        // Distance from line
        vec2 d = uv - origin;
        float along = dot(d, dir);
        float perp = abs(dot(d, vec2(-dir.y, dir.x)));
        // Thin bright line
        float lineWidth = 0.001 + 0.003 * seed;
        float streak = smoothstep(lineWidth, 0.0, perp);
        // Only visible along certain length
        float len = 0.3 + 0.5 * seed;
        streak *= smoothstep(0.0, 0.05, along) * smoothstep(len, len - 0.05, along);
        // Fade in/out with time
        streak *= smoothstep(0.0, 0.1, phase) * smoothstep(1.0, 0.7, phase);
        acc += streak;
    }
    return acc;
}

// ─── Main ──────────────────────────────────────────────────────────

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    vec2 uvScaled = (uv - 0.5) * vec2(aspect, 1.0);

    float t = TIME * speed * 20.0;

    // ── Base organic fluid field ──
    vec2 noiseUV = uvScaled * noiseScale * 0.5;
    float fluid = warpedFbm(noiseUV, t);

    // Shape into dark clouds: remap so most of the image is dark
    float darkBias = 1.0 - density;
    float shaped = smoothstep(darkBias, darkBias + 0.35, fluid);

    // Secondary turbulence layer for detail
    float detail = fbm(uvScaled * noiseScale * 2.0 + t * 0.4, 4);
    shaped *= 0.7 + 0.3 * detail;

    // ── Dithered stipple texture ──
    float dithered = stipple(uv, shaped);

    // ── Circular rings ──
    float ringAcc = 0.0;
    for (int i = 0; i < 4; i++) {
        float fi = float(i);
        vec2 center = vec2(
            0.3 + 0.4 * hash11(fi * 1.23 + floor(t * 0.02)),
            0.3 + 0.4 * hash11(fi * 2.71 + floor(t * 0.02) + 5.0)
        );
        vec2 centerScaled = (center - 0.5) * vec2(aspect, 1.0);
        ringAcc += ringWave(uvScaled, centerScaled, t, fi);
    }
    ringAcc = clamp(ringAcc * rings, 0.0, 1.0);

    // Apply ring as subtle dark outline overlaid on fluid
    float ringMask = stipple(uv + 0.01, ringAcc * 0.4);

    // ── Diagonal streaks ──
    float streakVal = diagonalStreaks(uv, t) * streaks;
    float streakDithered = stipple(uv + 0.1, streakVal * 0.5);

    // ── Scattered bright particles ──
    float particles = 0.0;
    for (int i = 0; i < 12; i++) {
        float fi = float(i);
        float seed = hash11(fi * 3.77 + 1.0);
        vec2 pos = vec2(
            hash11(fi * 1.31 + floor(t * 0.03 + seed * 10.0)),
            hash11(fi * 2.47 + floor(t * 0.03 + seed * 10.0) + 3.0)
        );
        pos = (pos - 0.5) * vec2(aspect, 1.0);
        float dist = length(uvScaled - pos);
        float p = smoothstep(0.015, 0.0, dist);
        // Flicker
        p *= step(0.4, hash21(vec2(fi, floor(t * 0.5))));
        particles += p;
    }
    particles = clamp(particles, 0.0, 1.0) * density;

    // ── Composite ──
    float luminance = dithered + ringMask + streakDithered + particles * 0.3;
    luminance = clamp(luminance, 0.0, 1.0);

    // Apply warm sepia/beige tint to bright areas, pure black for dark
    vec3 warmLight = vec3(0.78, 0.72, 0.65); // warm beige
    vec3 coolLight = vec3(0.7, 0.7, 0.72);   // neutral grey
    vec3 tint = mix(coolLight, warmLight, warmth);

    vec3 color = luminance * tint;

    // Subtle vignette to push edges darker
    float vig = 1.0 - 0.4 * length(uv - 0.5);
    color *= vig;

    // Final gamma — keep dark mood
    color = pow(color, vec3(1.3));

    gl_FragColor = vec4(color, 1.0);
}
