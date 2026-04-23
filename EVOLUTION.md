# PinchToMove: Evolution Log
**v1.0.0 — April 22, 2026**

## Overview

This document chronicles the design and implementation of the PinchToMove locomotion method for the CS 465 Capstone project. It covers ideation, architectural decisions, implementation iterations, dead ends, and the current development trajectory. It is written as a living record of the reasoning behind each decision, not just the decisions themselves.

---

## Ideation

With Viltrumite flight implemented and functional, the next locomotion method needed to offer meaningful contrast — both in feel and in interaction model. Viltrumite is continuous and momentum-heavy: you hold a gesture and accelerate. I wanted PinchToMove to be discrete and deliberate, something closer to how a user navigates a map or scrolls a document than how they fly a plane.

The core metaphor I landed on was the **scroll wheel**. A scroll wheel input is discrete — you don't hold it down and coast, you make a deliberate physical gesture that produces a proportional output. Crucially, chaining those gestures in rapid succession accelerates the effect, and stopping resets it. I wanted PinchToMove to feel exactly like that, but in three-dimensional space.

I had to verbalize my conception precisely: the user pinches with their dominant hand, performs a come-hither motion (pulling the pinched hand toward their body), releases the pinch, and locomotes in the direction their hand was pointed at onset. Chaining successive strokes — pinch, pull, release, repeat — would compound velocity. A push-away motion would reverse direction. The system had to be bidirectional and self-explanatory; no instruction required.

The scroll wheel analogy clarified the chaining mechanic. On a scroll wheel, you lift your finger, replace it at the top, and pull down again to continue scrolling. If you don't lift — if you just push your finger back up — you scroll in reverse. The same logic applies here: the user must release the pinch between strokes. Holding a pinch and returning the hand to its origin without releasing would produce a near-zero or inverted stroke, not a neutral one.

---

## Gesture Detection: PinchDetector

### Why Middle Finger

The natural first instinct was to use index-thumb pinch — the most common pinch gesture in XR. Meta's `MetaAimHand` API exposes `pinchStrengthIndex` directly, making it the obvious choice. The problem: Meta's OS intercepts index pinch at the system level. On the Quest 2, an index pinch recenters the viewport. This is a fundamental conflict with any locomotion system that relies on that gesture.

The solution was to switch to middle-thumb pinch (`pinchStrengthMiddle`). This has a useful biomechanical side effect: with the middle finger curled to meet the thumb, the index finger stays naturally extended. The user ends up pointing with their index finger while pinching with their middle — an intuitive "aim and grip" posture that emerged organically rather than by design.

### Tracking the Pinch Midpoint

The first implementation tracked wrist position as the primary spatial input for stroke computation. This worked for large, elbow-driven strokes but failed to register small, wrist-based strokes — the wrist simply doesn't move enough. The fix was to track the **midpoint between the MiddleTip and ThumbTip joints** instead.

This is the actual physical contact point of the pinch. It moves meaningfully even in small wrist-flick strokes, and averaging the two tips reduces per-tip jitter. Both joints are accessible via `XRHandJointID` through the same `XRHandSubsystem` already in use — no additional packages or OpenXR features required.

The detector accumulates midpoint positions in a rolling buffer each frame, which is used to estimate hand velocity at pinch onset. This onset velocity is exposed to the controller for stroke arc correction — if the hand is already in motion when the pinch fires, that pre-existing velocity is factored out of the arc computation to prevent inflated stroke reads.

### Hysteresis

Pinch onset and release use separate strength thresholds (`pinchOnsetThreshold` = 0.8, `pinchReleaseThreshold` = 0.5). The gap between them prevents the state machine from chattering on the boundary — a real problem with analog gesture inputs where strength oscillates around a single threshold value.

---

## Locomotion: PinchToMoveController

### First Iteration: Latched Direction Vector

The first working implementation latched the travel direction at pinch onset using `MetaAimHand`'s aim pose — a "laser pointer" ray computed from the hand's orientation at the moment of pinch. Stroke arc was computed at release and applied as an instantaneous velocity impulse.

This produced correct directional locomotion, but felt clunky. The aim ray is sensitive to hand orientation at a single frame, which is inherently noisy. More fundamentally, "where your knuckles are pointing" is not a natural thing for a human to reason about — the mental model doesn't match the physical intuition.

### Second Iteration: Head-to-Wrist Vector

The second approach replaced the aim ray with the vector from head position to wrist position at pinch onset. This is purely positional — it doesn't involve gaze direction or head rotation, only where the hand is in space relative to the head. If the arm is extended forward, travel is forward. If it's extended to the right, travel is to the right.

This felt more natural in testing, but introduced a new problem: the direction was still latched at a single moment (pinch onset), meaning the user was locked to an invisible directional line for the entire stroke. Turning while pinching had no effect on travel direction, which felt rigid and unintuitive.

### Third Iteration: Live Best-Fit Line (Current)

The insight that resolved both prior approaches: **the stroke vector itself is the travel direction**. Rather than latching a direction at onset and using the stroke for power only, the stroke path through space defines both direction and magnitude simultaneously.

The implementation accumulates pinch midpoint positions every frame while pinched. Each frame, a **line of best fit** is computed through all accumulated samples using Principal Component Analysis (PCA). PCA finds the single straight line that best represents the overall path of the hand through space, regardless of the natural arc that a wrist or elbow sweep produces. The dominant axis of that line becomes the travel direction; its magnitude relative to the stroke origin drives speed.

Locomotion begins as soon as a minimum displacement threshold is exceeded and a minimum number of samples have accumulated — it does not wait for pinch release. This makes the system feel live and responsive rather than commit-on-release.

**Why PCA and not just first-to-last sample direction?** A human arm sweep traces a circular arc through space, not a straight line. The first-to-last vector would be a chord of that arc — reasonable, but sensitive to where exactly the user starts and ends. PCA fits a line through the entire cloud of samples, which is more stable and more representative of the user's actual intent. Three iterations of power iteration on the covariance matrix of the sample cloud is computationally cheap and sufficient for the low sample counts a hand stroke generates.

**Sign determination.** The PCA axis is unsigned — it points in one direction or its opposite with equal mathematical validity. To determine which end is "forward," the net displacement vector from stroke origin to current midpoint is dotted against the fitted axis. A come-hither motion produces a displacement that opposes the initial reach direction, so the sign is negated to map come-hither to forward travel and push-away to reverse travel.

---

## What Didn't Make It

**Index finger direction vector.** The extended index finger during middle-thumb pinch creates a natural pointing gesture. I considered sampling the index fingertip-to-knuckle direction as a secondary travel direction input. I abandoned this for two reasons: the index finger curves upward and to the left at rest, making its resting direction unreliable; and the PCA approach already produces a cleaner direction signal from the stroke itself without requiring additional joint tracking.

**Head-to-wrist direction.** Described above. Directional locking to a single onset vector — regardless of how that vector is computed — produces unintuitive rigid locomotion. The live best-fit approach supersedes it entirely.

**Dead zone for origin drift suppression.** Early in the live-stroke design, I considered a spatial dead zone around the stroke origin to suppress micro-movements from hand tremor. I rejected this on precision grounds — a dead zone that's large enough to suppress tremor is also large enough to eat legitimate small strokes. The PCA regression handles tremor implicitly by fitting across many samples rather than reacting to any single noisy frame.

---

## Current State

Phase 1 is complete and functional. The user can locomote in three-dimensional space using middle-thumb pinch strokes. Travel direction is determined live by PCA over pinch midpoint samples. Stroke arc length drives speed linearly via `arcToSpeedScale`. The system is bidirectional. Chaining is not yet implemented.

**Tunable parameters exposed for A/B testing:**
- `arcToSpeedScale` — primary speed dial
- `maxSpeed` — hard cap
- `minimumArcThreshold` — tremor filter
- `minimumRegressionSamples` — regression stability floor

---

## Next Steps

### Phase 2 — Stroke Velocity

Factor stroke duration into the speed calculation. Arc divided by stroke duration produces a velocity scalar — a fast stroke should feel punchier than a slow one of equal arc length. The pre-pinch midpoint velocity already sampled at onset will be integrated here to correct for hand motion that predates the pinch.

### Phase 3 — Chain Multiplier

Implement the scroll-wheel acceleration mechanic. Successive strokes committed within a configurable time window (`chainWindowSeconds`) accumulate a multiplier applied to stroke impulse. The multiplier is capped and decays during idle. All chaining parameters are serialized for in-editor tuning:

- `chainWindowSeconds` — time budget between strokes to sustain a chain
- `chainMultiplierIncrement` — per-stroke multiplier growth
- `chainMultiplierCap` — maximum multiplier
- `chainMultiplierDecayRate` — idle bleed rate

### Stroke End Detection (Pending Design)

A currently open design question: locomotion continues as long as the pinch is held, even after the stroke arc has naturally ended. This is unintuitive — the user expects locomotion to stop when their hand stops, not when they release the pinch. The proposed solution is a **displacement delta window**: if total midpoint displacement over the last N frames falls below a threshold, the stroke is considered complete and velocity is frozen. This is more tremor-resistant than a per-frame velocity threshold and avoids introducing deceleration logic prematurely. The question of whether a new hand movement while still pinched re-initiates the stroke, or whether release-and-re-pinch is required, remains open pending testing.

### Deceleration

Exponential velocity decay after stroke completion, consistent with the Viltrumite implementation. Deferred until stroke end detection is resolved, since deceleration behavior is meaningless without a reliable stroke-end signal.
