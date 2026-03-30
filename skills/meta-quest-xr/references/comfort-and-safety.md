# Comfort And Safety

Quest comfort rules for this project:

- Prioritize readability over spectacle.
- Keep distortion subtle and predictable.
- Prefer continuous interpolation instead of surprise transitions.
- Never build behavior that depends on flicker or fast pulses.
- Provide an immediate user reset path.

Recommended clamps:
- Distortion UV offset: `<= 0.05`
- Fade in/out: `>= 1.5s`
- Chromatic aberration: mild and score-driven only
- Peripheral blur: stronger than center blur, never full-screen smear

Review questions:
- Could a sudden head turn spike the effect uncomfortably?
- Does the effect decay gracefully when motion stops?
- Can a user test and tune values in Editor before putting on the headset?
- Is the zero-intensity baseline calm and stable?
