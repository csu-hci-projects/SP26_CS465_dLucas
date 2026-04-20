# Architecture Documentation
**v1.0.4**

## Overview

This document delineates the architectural composition of the CS 465 Capstone project, encompassing the directory structure, data flow paradigm, technology stack, and scene hierarchies. The architecture prioritizes modularity, with `Viltrum.unity` serving as the primary locomotion implementation scene and the remaining scenes structured as parallel development targets.

## Directory Structure

```
SP26_CS465_dLucas/
├── .gitignore
├── .vscode/
│   ├── extensions.json
│   ├── launch.json
│   └── settings.json
├── Assets/
│   ├── CesiumSettings/
│   │   └── Resources/
│   │       ├── CesiumIonServers/       # Cesium Ion server configuration assets
│   │       └── CesiumRuntimeSettings.asset  # Cesium runtime token/config
│   ├── InputSystem_Actions.inputactions  # Default Unity Input System action map
│   ├── Samples/
│   │   └── XR Hands/
│   │       └── 1.7.3/                  # XR Hands package sample assets (v1.7.3)
│   ├── Scenes/
│   │   ├── Viltrum.unity               # Viltrumite flight locomotion (primary implementation)
│   │   ├── Controller.unity            # Controller locomotion (pending implementation)
│   │   ├── FlapLikeABird.unity         # Bird-flight locomotion (pending implementation)
│   │   └── PinchToMove.unity           # Pinch-to-move locomotion (pending implementation)
│   ├── Scripts/
│   │   ├── Gesture/
│   │   │   └── FistDetector.cs         # Hand tracking fist gesture recognition
│   │   └── Navigation/
│   │       └── ViltrumiteController.cs # Viltrumite flight locomotion logic
│   ├── TextMesh Pro/                   # TextMeshPro package assets (Unity-managed)
│   ├── XR/
│   │   ├── Loaders/
│   │   │   └── OpenXRLoader.asset      # OpenXR loader configuration
│   │   ├── Settings/
│   │   │   ├── OpenXR Editor Settings.asset   # OpenXR editor-time settings
│   │   │   └── OpenXR Package Settings.asset  # OpenXR feature/extension settings
│   │   └── XRGeneralSettingsPerBuildTarget.asset  # XR build target config
│   └── XRI/
│       └── Settings/
│           ├── Resources/              # XRI runtime resources
│           └── XRInteractionEditorSettings.asset  # XR Interaction Toolkit editor config
├── Packages/
│   ├── manifest.json                   # Unity package dependencies
│   └── packages-lock.json              # Resolved package versions lockfile
├── ProjectSettings/
│   ├── Packages/
│   │   └── com.unity.testtools.codecoverage/  # Code coverage tool settings
│   ├── AudioManager.asset
│   ├── DynamicsManager.asset
│   ├── EditorBuildSettings.asset       # Scene build list (all scenes registered)
│   ├── EditorSettings.asset
│   ├── GraphicsSettings.asset
│   ├── InputManager.asset
│   ├── MemorySettings.asset
│   ├── ProjectSettings.asset           # Core Unity project settings
│   ├── ProjectVersion.txt              # Unity version pin (6000.0.31f1)
│   ├── QualitySettings.asset
│   ├── ShaderGraphSettings.asset
│   ├── TagManager.asset
│   ├── TimeManager.asset
│   ├── XRSettings.asset
│   └── [additional Unity config assets]
├── SP26_CS465_dLucas.slnx              # Visual Studio solution file (Unity-generated)
├── README.md
├── ARCHITECTURE.md
└── VISION.md
```

*Note: This manifest represents the principal files relevant to the capstone. The repository contains additional Unity-generated directories (Library, Logs, Temp) which are excluded from version control.*

## Technology Stack

| Layer | Technology | Purpose |
|-------|------------|---------|
| Game Engine | Unity 6 (v6000.0.31f1) | Core development platform and scene management |
| XR Framework | Unity XR Interaction Toolkit | Cross-platform XR input handling and interaction abstractions |
| Hand Tracking | XR Hands Subsystem (v1.7.3) | Skeletal hand tracking and gesture recognition |
| Geospatial Streaming | Cesium for Unity | 3D Tiles streaming, georeference management, and globe anchoring |
| Tile Source | Google Photorealistic 3D Tiles | Photogrammetric reconstruction of real-world environments |
| Rendering Platform | PC via Meta Horizon Link | PC-rendered VR with Quest 2 I/O; see README for setup |

## Data Flow Architecture

```
+-------------------------------------------------------------------------+
|                            Cesium Ion Cloud                             |
|                                                                         |
|    +---------------------------------------------------------------+    |
|    |       Google Photorealistic 3D Tiles (Asset 2275207)          |    |
|    +---------------------------------------------------------------+    |
|                                    |                                    |
+-------------------------------------------------------------------------+
                                     |
                                     |  HTTPS Tile Requests
                                     v
+-------------------------------------------------------------------------+
|                    PC (Unity Editor via Meta Horizon Link)              |
|                                                                         |
|    +---------------------------------------------------------------+    |
|    |                   Cesium3DTileset Components                  |    |
|    |                                                               |    |
|    |    * Tile selection based on screen-space error              |    |
|    |    * LOD management and frustum culling                      |    |
|    |    * Physics mesh generation for terrain collision           |    |
|    +---------------------------------------------------------------+    |
|                                    |                                    |
|                                    v                                    |
|    +---------------------------------------------------------------+    |
|    |                     CesiumGeoreference                        |    |
|    |                                                               |    |
|    |    * WGS84 coordinate system management                      |    |
|    |    * Origin placement (Fort Collins: 40.5764°N, 105.0841°W)  |    |
|    |    * ECEF to Unity coordinate transformations                |    |
|    +---------------------------------------------------------------+    |
|                                    |                                    |
|                                    v                                    |
|    +---------------------------------------------------------------+    |
|    |                       XR Origin (VR)                          |    |
|    |                                                               |    |
|    |    * CesiumGlobeAnchor for world-space positioning           |    |
|    |    * CesiumOriginShift for floating-origin precision         |    |
|    |    * Hand tracking prefabs (Left/Right)                      |    |
|    |    * Main Camera with TrackedPoseDriver                      |    |
|    +---------------------------------------------------------------+    |
|                                                                         |
+-------------------------------------------------------------------------+
                                     |
                                     |  Meta Horizon Link (USB-C)
                                     v
+-------------------------------------------------------------------------+
|                            Meta Quest 2                                 |
|                                                                         |
|    +---------------------------------------------------------------+    |
|    |                    I/O (Display + Hand Tracking)              |    |
|    |                                                               |    |
|    |    * Stereoscopic display of PC-rendered frames              |    |
|    |    * Head pose and hand joint tracking via XR Hands          |    |
|    |    * No local tile computation — all rendering on PC         |    |
|    +---------------------------------------------------------------+    |
|                                                                         |
+-------------------------------------------------------------------------+
```

## Scene Composition

All locomotion scenes share a common structural template: a `CesiumGeoreference` rooted at Fort Collins (40.5764°N, 105.0841°W, 1590m), a `Google Photorealistic 3D Tiles` tileset child, an `XR Origin (VR)` with hand tracking prefabs, and a `Directional Light`. Locomotion-specific `Scripts` are attached to a `Locomotion` GameObject under `XR Origin (VR)`. Environmental settings (tileset parameters, lighting, camera configuration) are held consistent across all scenes to ensure observed differences in user experience are attributable to the locomotion method rather than environmental variables.

### Viltrum Scene

The Viltrum Scene houses the primary locomotion implementation. It is the only scene currently containing finalized locomotion logic.

**Scene Hierarchy:**

```
Viltrum
├── CesiumGeoreference
│   ├── Google Photorealistic 3D Tiles (Cesium3DTileset + TileQualityController)
│   └── XR Origin (VR)
│       ├── Camera Offset
│       │   ├── Main Camera
│       │   ├── Right Hand Tracking (Prefab)
│       │   └── Left Hand Tracking (Prefab)
│       └── Locomotion
│           ├── FistDetector (Script)
│           └── ViltrumiteController (Script)
└── Directional Light
```

**Locomotion System Architecture:**

| Component | Responsibilities |
|-----------|------------------|
| FistDetector | Monitors `XRHandSubsystem` for fist gestures by measuring fingertip-to-wrist proximity. Exposes `IsRightFist` and `IsLeftFist` boolean states. Visual feedback is provided via hand mesh color modulation. |
| ViltrumiteController | Consumes `FistDetector` state to drive locomotion. When a right-hand fist is detected, the user accelerates in the direction of the right wrist's forward vector. Arm extension (wrist-to-head distance) modulates target velocity up to a configurable speed cap. Exponential smoothing (configurable τ) produces cinematic acceleration and deceleration curves. Terrain floor enforcement via downward raycasting prevents subterranean traversal. A dual-fist full-extension boost multiplies the speed cap when both fists are simultaneously and fully extended. |

**Known Issues and Active Investigations:**

*Hand Shaking.* Both hands render with a rapid oscillation that blurs the mesh during normal use. The root cause has not yet been isolated, but investigation is focused on two hypotheses: (1) an update loop conflict between Unity's game loop and the XR tracking pose update, where `TrackedPoseDriver` update timing may be mismatched against the `XRHandSubsystem`'s pose write cycle; and (2) a conflict between the XR Hands subsystem joint pose updates and the `SkinnedMeshRenderer`'s `LateUpdate` skinning pass. Concrete diagnostic steps include verifying `TrackedPoseDriver` `m_UpdateType` on hand prefabs (switching from `UpdateAndBeforeRender` to `BeforeRender`), confirming that `XRHandSkeletonDriver` or equivalent update type matches the `TrackedPoseDriver`, and auditing whether `FistDetector` pose reads in `Update` conflict with pose writes occurring in `LateUpdate`.

*Arm Extension Calibration.* The extension range (`minExtension` / `maxExtension`) used to modulate speed is not yet calibrated to the developer's physical arm length. A/B testing is required to empirically determine the wrist-to-head distances that correspond to a relaxed arm position and a fully extended arm, and to update the serialized values accordingly.

*Hover at Chest.* When the fist is held close to the chest (below `minExtension`), the controller's `Fly()` method returns early without actively zeroing velocity, resulting in a coast rather than a true hover. Active deceleration to zero when the gesture falls below `minExtension` is a pending implementation task.

*Dual Fist Boost.* The dual-fist boost is not reliably firing during testing. The `IsDualFistBoostActive` method requires `rightExtensionNormalized >= 1f`, which may be excessively brittle given floating-point extension measurements — though this has not been confirmed as the definitive root cause and warrants further investigation.

**Tileset Configuration:**

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| maximumScreenSpaceError | 4 | High-fidelity tile selection, viable on PC hardware |
| preloadAncestors | true | Ensures parent tiles are resident before children stream in |
| preloadSiblings | false | Disabled to prevent asymmetric LOD artifacts |
| maximumSimultaneousTileLoads | 48 | Elevated concurrent load count leveraging PC bandwidth |
| maximumCachedBytes | 16 GB | Large cache budget for PC memory |
| loadingDescendantLimit | 48 | Balanced descendant loading depth |
| enforceCulledScreenSpaceError | true | Maintains LOD precision on culled tiles |
| culledScreenSpaceError | 512 | Aggressive culling threshold for off-screen tiles |
| createPhysicsMeshes | false | Disabled to reduce CPU overhead at high tile counts |

**TileQualityController** is attached alongside `Cesium3DTileset` and dynamically manages `maximumScreenSpaceError` and `culledScreenSpaceError` at runtime to approximate foveated loading behavior.

### Remaining Locomotion Scenes

`Controller.unity`, `FlapLikeABird.unity`, and `PinchToMove.unity` share the same scene structure and environmental configuration as `Viltrum.unity`. Their `Locomotion` GameObjects currently contain placeholder script references carried over from the initial scene duplication. All locomotion logic in these scenes will be replaced during their respective implementation phases. Behavioral and mechanical specifications for each will be documented when implementation begins.

## Rendering and Lighting

All scenes utilize Unity's default skybox material with a directional light positioned at a 50° altitude and −30° azimuth, producing elongated shadows characteristic of late-afternoon illumination. The warm color temperature (6570K) enhances the realism of the photogrammetric tile textures.

**Camera Configuration:**

| Parameter | Value |
|-----------|-------|
| Near Clip Plane | 0.01m |
| Far Clip Plane | 100,000m |
| Field of View | 60° |
| HDR | Enabled |
| MSAA | Enabled |

The extended far clip plane accommodates world-scale rendering, while the minimal near clip plane preserves close-range interaction fidelity.
