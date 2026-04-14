# Vision Document

## Overview

This document articulates the developmental trajectory for the CS 465 Capstone project, outlining planned refinements to the existing Viltrumite locomotion system and the proposed additional locomotion methods for comparative analysis.

## Research Motivation

Existing locomotion techniques for virtual reality environments are adequate for basic navigation, but they often lack the intuitiveness and satisfaction that users expect from embodied interaction. World-scale VR environments, characterized by photorealistic reconstructions of real-world geography spanning kilometers of traversable terrain, present an opportunity to explore whether gesture-driven flight locomotion can provide a more engaging and natural navigational experience compared to conventional methods.

## Viltrumite Locomotion Refinements

The Viltrumite locomotion method, named for the superhuman flight style depicted in the *Invincible* comic and television series, currently exists in a functional prototype state. The following refinements are planned:

### Speed Limits and Altitude Settings

The current implementation uses discrete altitude tiers to cap maximum velocity. Planned refinements include optimizing the relationship between altitude and speed limits to provide smoother transitions and more intuitive velocity envelopes at varying heights above terrain.

### Acceleration and Deceleration Mechanics

The acceleration and deceleration curves require tuning to achieve a balance between responsiveness and cinematic smoothness. Optimization efforts will focus on refining the exponential smoothing parameters and deceleration rates.

### Terrain Collision

Implementing robust terrain collision detection to prevent the user from passing through buildings and terrain geometry. The current vertical raycast approach will be expanded to provide more comprehensive collision avoidance.

### Resolution and Fidelity

Increasing the visual resolution and fidelity of the environment during flight, potentially through dynamic LOD adjustments that respond to user velocity and maintain visual quality at rest.

## Environmental Consistency

Future locomotion scenes will mirror the Viltrumite environment in terms of graphics settings and configuration decisions. This ensures that observed differences in user experience are attributable to the locomotion method itself rather than environmental variables.

## Proposed Locomotion Methods

### Pinch-to-Move

A precision navigation technique wherein the user employs a pinch gesture to initiate and control movement through the environment.

### Bird Flight

A biomimetic locomotion technique wherein the user flaps their arms to generate propulsion, mimicking bird wing mechanics.

### Controller Locomotion

A traditional thumbstick-based continuous locomotion method, included as a baseline for comparison against gesture-driven alternatives.

## Performance Optimization

### Geographic Bounding

Implementing a box boundary constraining the renderable environment to the Fort Collins area. This constraint will improve graphics performance by reducing the tile loading overhead and enabling higher visual fidelity within the bounded region.
