# Vision Document
**v1.1.0**

## Overview

This document articulates the developmental trajectory for the CS 465 Capstone project, outlining planned refinements to the Viltrumite locomotion system, the design and implementation status of the PinchToMove system, and the roadmap for the remaining locomotion methods required for comparative analysis.

## Research Motivation

Existing locomotion techniques for virtual reality environments are adequate for basic navigation, but they often lack the intuitiveness and satisfaction that users expect from embodied interaction. World-scale VR environments, characterized by photorealistic reconstructions of real-world geography spanning kilometers of traversable terrain, present an opportunity to explore whether gesture-driven locomotion can provide a more engaging and natural navigational experience compared to conventional methods. This project investigates four locomotion paradigms — Viltrumite flight, stroke-based pinch locomotion, biomimetic bird-flight, and thumbstick-based controller locomotion — to assess their comparative intuitiveness, comfort, and navigational efficacy.

---

## Viltrumite Locomotion Refinements

The Viltrumite locomotion method, named for the superhuman flight style depicted in the *Invincible* comic and television series, is functional and under active refinement. The following items constitute the current development backlog:

### Hand Rendering Stability

Both hand meshes currently exhibit rapid oscillation that blurs the visual representation during normal use. Resolving this is a prerequisite for a polished user experience and for producing clean demonstration footage. The investigation and fix are tracked in ARCHITECTURE.md under *Known Issues and Active Investigations*.

### Arm Extension Calibration

The velocity modulation curve is parameterized by `minExtension` and `maxExtension` — wrist-to-head distances defining the dead zone and full-speed threshold respectively. These values must be empirically calibrated via A/B testing against the developer's physical arm dimensions before the locomotion feel can be considered representative.

### Hover Behavior

When the user holds a closed fist at chest level (below `minExtension`), the intent is to maintain a stationary hover. The current implementation returns early from the thrust calculation without actively zeroing velocity, causing the user to coast indefinitely. Active deceleration-to-zero when the fist is held close to the chest is a pending implementation task.

### Dual Fist Speed Boost

A speed multiplier activates when both fists are simultaneously and fully extended. This mechanic is implemented but not reliably triggering during testing. The root cause is under investigation.

### Acceleration and Deceleration Tuning

The exponential smoothing parameters governing acceleration and deceleration ramp require further tuning to achieve the target feel: weighty, cinematic build-up and a natural, momentum-preserving coast on release.

### Terrain Collision

The current terrain floor enforcement uses a single vertical raycast. Expanding this to provide more comprehensive collision avoidance — particularly against building geometry at low altitude — is a longer-horizon refinement.

---

## PinchToMove Locomotion

**Status: In Progress**

PinchToMove is a stroke-based locomotion method drawing conceptual inspiration from the scroll-wheel interaction paradigm. Discrete, chainable pinch strokes propel the user through the environment, compounding in velocity with successive repetitions. The mechanic is designed to be bidirectional and self-explanatory — no instruction should be required for a user to infer the relationship between hand motion and locomotion direction.

### Designed Mechanic

**Stroke lifecycle.** A single locomotion unit is a *stroke*:

1. The user makes a pinch with their dominant hand. The hand's aim forward vector at this moment is latched as the travel direction for the stroke. No movement begins at this point — the system is primed but not yet active.
2. While pinched, the user sweeps their arm (a come-hither motion toward the body, or a push-away motion away from the body). Displacement is accumulated from the position at pinch onset.
3. On pinch release, the stroke is committed: a velocity impulse is applied in the latched travel direction (come-hither) or its inverse (push-away). The user must fully release the pinch before initiating the next stroke.

**Bidirectionality.** The come-hither motion (pulling the pinched hand toward the body) propels the user forward in the aimed direction. The reverse motion (extending the pinched hand away from the body) propels the user in the opposite direction. This maps intuitively onto the scroll-wheel analogy: the direction of the physical gesture mirrors the direction of traversal in the virtual environment.

**Stroke power.** Impulse magnitude is a function of two inputs:
- *Stroke arc* — the world-space displacement magnitude from the wrist position at pinch onset to the wrist position at release. An elbow-driven stroke (large arc) yields substantially more power than a wrist-flick (small arc).
- *Stroke velocity* — arc divided by stroke duration. A fast stroke amplifies the impulse; a slow stroke dampens it.

**Chain multiplier.** Successive strokes committed within a configurable time window (`chainWindowSeconds`) accumulate a multiplier applied to stroke impulse. This produces the scroll-wheel acceleration effect: slow, deliberate strokes navigate precisely; rapid, rhythmic strokes build into high-speed traversal. The multiplier is capped at `chainMultiplierCap` and decays toward 1.0 during idle. All chaining parameters (`chainWindowSeconds`, `chainMultiplierIncrement`, `chainMultiplierCap`, `chainMultiplierDecayRate`) are exposed as serialized fields for in-editor A/B tuning.

**User precision tolerance.** The system does not require the user to return their hand to any specific spatial origin between strokes. Each pinch onset latches a fresh `strokeOrigin`, so chaining is gated purely on elapsed time between strokes — not on spatial hand positioning. Additionally, because the hand may already be in motion at pinch onset, the wrist velocity at onset is sampled and factored into the stroke arc computation to prevent near-zero arc reads from fast pinches.

### Open Tuning Items

The following parameters require empirical A/B testing during implementation and will be updated as calibration data is gathered:

- `chainWindowSeconds` — the time budget between strokes that sustains a chain
- `chainMultiplierIncrement` — per-stroke multiplier growth rate
- `chainMultiplierCap` — maximum achievable multiplier
- `chainMultiplierDecayRate` — idle bleed rate
- Base impulse scaling relative to stroke arc and velocity
- Deceleration tau after the final stroke in a chain

---

## Environmental Consistency

All locomotion scenes are configured with identical environmental parameters (tileset settings, lighting, camera configuration) as documented in ARCHITECTURE.md. This ensures that observed differences in user experience across locomotion methods are attributable to the interaction design rather than environmental variables.

---

## Remaining Locomotion Methods

Two additional locomotion methods are planned for implementation following the completion of PinchToMove. Each will occupy its own scene and will be built against the same environmental template as all prior scenes. Detailed behavioral and mechanical specifications will be documented when implementation begins.

### Bird Flight

A biomimetic locomotion technique wherein the user flaps their arms to generate propulsion, mimicking the wing mechanics of bird flight. Further details to follow.

### Controller Locomotion

A traditional thumbstick-based continuous locomotion method, included as a control condition for comparison against gesture-driven alternatives. Further details to follow.

---

## Performance Optimization

### Geographic Bounding

A spatial boundary constraining tile loading to the Fort Collins metropolitan area is planned. This will reduce tile request overhead and allow higher sustained fidelity within the target environment. Implementation is deferred until the locomotion systems are stabilized.
