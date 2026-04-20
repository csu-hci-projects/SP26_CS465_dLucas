# Vision Document
**v1.0.4**

## Overview

This document articulates the developmental trajectory for the CS 465 Capstone project, outlining planned refinements to the Viltrumite locomotion system and the roadmap for implementing the remaining locomotion methods required for comparative analysis.

## Research Motivation

Existing locomotion techniques for virtual reality environments are adequate for basic navigation, but they often lack the intuitiveness and satisfaction that users expect from embodied interaction. World-scale VR environments, characterized by photorealistic reconstructions of real-world geography spanning kilometers of traversable terrain, present an opportunity to explore whether gesture-driven flight locomotion can provide a more engaging and natural navigational experience compared to conventional methods.

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

## Environmental Consistency

All locomotion scenes are configured with identical environmental parameters (tileset settings, lighting, camera configuration) as documented in ARCHITECTURE.md. This ensures that observed differences in user experience across locomotion methods are attributable to the interaction design rather than environmental variables.

## Remaining Locomotion Methods

Three additional locomotion methods are planned for implementation. Each will occupy its own scene (`Controller.unity`, `FlapLikeABird.unity`, `PinchToMove.unity`) and will be built against the same environmental template as the Viltrum scene. Detailed behavioral and mechanical specifications for each method will be documented when implementation begins.

### Pinch-to-Move

A precision navigation technique wherein the user employs a pinch gesture to initiate and control movement through the environment. Further details to follow.

### Bird Flight

A biomimetic locomotion technique wherein the user flaps their arms to generate propulsion, mimicking the wing mechanics of bird flight. Further details to follow.

### Controller Locomotion

A traditional thumbstick-based continuous locomotion method, included as a control condition for comparison against gesture-driven alternatives. Further details to follow.

## Performance Optimization

### Geographic Bounding

A spatial boundary constraining tile loading to the Fort Collins metropolitan area is planned. This will reduce tile request overhead and allow higher sustained fidelity within the target environment. Implementation is deferred until the locomotion systems are stabilized.
