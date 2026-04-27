# Vision Document
**v1.1.1**

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

1. The user pinches with their dominant hand (middle finger to thumb). The pinch midpoint — the average of MiddleTip and ThumbTip joint positions — is recorded as the stroke origin.
2. While pinched, the user sweeps their arm in a come-hither (toward the body) or push-away (away from the body) motion. Each frame, the midpoint position is sampled and added to the stroke sample set.
3. A line of best fit (PCA) is continuously computed through all accumulated samples. The principal axis is the live travel direction; sign is derived from the net displacement from stroke origin — come-hither maps to forward, push-away maps to reverse.
4. Locomotion begins as soon as minimum displacement and minimum sample count thresholds are exceeded. The system does not wait for pinch release.
5. The stroke ends when the hand settles (cumulative midpoint displacement across the last `settlementFrameWindow` frames falls below `settlementDisplacementThreshold`) or when the pinch is released. On stroke end, target velocity is set to zero and exponential deceleration begins — the user glides into stillness.
6. If the hand settles while still pinched and then begins moving again, a new stroke initiates automatically from a fresh origin. No re-pinch is required.

**Bidirectionality.** The come-hither motion propels the user forward along the stroke direction; the push-away motion propels the user in the opposite direction. The physical direction of the arm sweep directly mirrors the direction of locomotion.

**Stroke power.** Speed is proportional to stroke arc — the world-space displacement magnitude from stroke origin to the current midpoint at peak PCA sample. An elbow-driven stroke yields more power than a wrist flick.

**Chain multiplier.** Each successive stroke completed within `chainWindowSeconds` of the previous stroke end multiplies the current multiplier by `chainMultiplierGrowthFactor` (1.32 by default). This produces exponential growth: four chained strokes reach approximately 3× unchained speed. The multiplier is capped at `chainMultiplierCap` (10.0) and decays toward 1.0 at `chainMultiplierDecayRate` per second during idle.

**Y suppression.** Biomechanically arc-y strokes can introduce unintentional vertical drift. The PCA residual — mean perpendicular distance of samples from the fitted line — serves as a curvature proxy. High-residual strokes have their Y travel component dampened proportionally; low-residual strokes pass Y through unmodified, preserving intentional vertical locomotion (e.g., an overhead lat pulldown for ascent).

**User precision.** Chaining is gated on elapsed time only — not on spatial hand positioning. Each pinch onset records a fresh stroke origin, so the user is free to re-pinch anywhere in their range of motion.

### Open Tuning Items

The following parameters require empirical calibration against actual stroke residual values from the test population:

- `maxResidualForFullSuppression` — residual at which Y is fully zeroed (currently 0.04m). Enable `enableDebugLogging` and observe residual values across stroke types before finalizing.
- `minResidualForNoSuppression` — residual below which Y is untouched (currently 0.01m). Same calibration procedure.

---

## Environmental Consistency

All locomotion scenes are configured with identical environmental parameters (tileset settings, lighting, camera configuration) as documented in ARCHITECTURE.md. This ensures that observed differences in user experience across locomotion methods are attributable to the interaction design rather than environmental variables.

---

## Remaining Locomotion Methods

The bird-flight locomotion concept was evaluated and dropped from the design space. The fourth locomotion method has not yet been selected. Candidates include a hybrid Viltrumite/PinchToMove system, traditional thumbstick-based controller locomotion, and novel gesture concepts not yet prototyped. The comparative study may also proceed with three locomotion methods if no fourth candidate proves suitable. A decision is pending.

---

## Performance Optimization

### Geographic Bounding

A spatial boundary constraining tile loading to the Fort Collins metropolitan area is planned. This will reduce tile request overhead and allow higher sustained fidelity within the target environment. Implementation is deferred until the locomotion systems are stabilized.
