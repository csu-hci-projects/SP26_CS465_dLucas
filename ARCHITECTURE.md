# Architecture Documentation
**v1.0.3**

## Overview

This document delineates the architectural composition of the CS 465 Capstone project, encompassing the directory structure, data flow paradigm, technology stack, and scene hierarchies. The architecture prioritizes modularity, enabling the Base Scene to serve as a foundational template from which locomotion-specific scenes inherit core functionality.

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
│   │   ├── BaseScene.unity             # Foundational scene with tiling and XR Origin
│   │   ├── ViltrumScene.unity          # Flight locomotion implementation
│   │   └── MagicScene.unity            # Placeholder for future locomotion methods
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
│   ├── EditorBuildSettings.asset       # Scene build list (all 3 scenes registered)
│   ├── EditorSettings.asset
│   ├── GraphicsSettings.asset
│   ├── InputManager.asset
│   ├── MemorySettings.asset
│   ├── ProjectSettings.asset           # Core Unity project settings (Android/Quest target)
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
| Hand Tracking | XR Hands Subsystem | Skeletal hand tracking and gesture recognition |
| Geospatial Streaming | Cesium for Unity | 3D Tiles streaming, georeference management, and globe anchoring |
| Tile Source | Google Photorealistic 3D Tiles | Photogrammetric reconstruction of real-world environments |
| Target Platform | Android (Quest 2/Quest 3) | Standalone VR deployment |

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
|                       Unity Editor (Development)                        |
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
                                     |  Android Build (APK)
                                     v
+-------------------------------------------------------------------------+
|                            Meta Quest 2/3                               |
|                                                                         |
|    +---------------------------------------------------------------+    |
|    |                    Runtime Tile Streaming                     |    |
|    |                                                               |    |
|    |    * WiFi-based tile requests to Cesium Ion                  |    |
|    |    * Dynamic LOD adjustment based on device performance      |    |
|    |    * Hand tracking via XR Hands Subsystem                    |    |
|    +---------------------------------------------------------------+    |
|                                                                         |
+-------------------------------------------------------------------------+
```

## Scene Composition

### Base Scene

The Base Scene establishes the foundational architecture upon which all locomotion-specific scenes are constructed. It contains no locomotion scripts, serving purely as an environmental and XR infrastructure template.

**Scene Hierarchy:**

```
BaseScene
├── CesiumGeoreference
│   ├── Google Photorealistic 3D Tiles (Cesium3DTileset)
│   └── XR Origin (VR)
│       └── Camera Offset
│           ├── Main Camera
│           ├── Right Hand Tracking (Prefab)
│           └── Left Hand Tracking (Prefab)
└── Directional Light
```

**Key Components:**

| Component | Configuration | Purpose |
|-----------|---------------|---------|
| CesiumGeoreference | Origin: 39.7392°N, 104.99°W, 1600m | Establishes geographic coordinate system with Denver as reference |
| CesiumCameraManager | useMainCamera: true | Directs tile loading toward the main camera frustum |
| CesiumGlobeAnchor | adjustOrientationForGlobeWhenMoving: true | Maintains proper orientation relative to Earth's curvature |
| CesiumOriginShift | distance: 10000m | Mitigates floating-point precision degradation at distance |
| Cesium3DTileset | maximumScreenSpaceError: 1, maximumSimultaneousTileLoads: 80 | High-fidelity tile streaming for development |
| Directional Light | Color: warm (1.0, 0.76, 0.33), Intensity: 1.0 | Simulates late-afternoon solar illumination |

**Spawn Point Configuration:**

The XR Origin is positioned at a local offset of approximately (−10.85, 1.46, −10.05) relative to the georeference origin, placing the user at street level within the Denver metropolitan area.

### Viltrum Scene

The Viltrum Scene extends the Base Scene architecture with the Viltrumite flight locomotion system. The geographic origin is shifted to Fort Collins (40.5764°N, 105.0841°W) to provide a distinct testbed from the Base Scene.

**Scene Hierarchy Additions:**

```
ViltrumScene
├── CesiumGeoreference
│   ├── Google Photorealistic 3D Tiles (Cesium3DTileset)
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
| FistDetector | Monitors XRHandSubsystem for fist gestures by measuring fingertip-to-wrist proximity. Exposes `IsRightFist` and `IsLeftFist` boolean states. Visual feedback is provided via hand mesh color modulation. |
| ViltrumiteController | Consumes FistDetector state to drive locomotion. When a right-hand fist is detected, the user accelerates in the direction of the right wrist's forward vector. Arm extension (distance from wrist to head) modulates target velocity. Altitude-tiered speed limits prevent excessive velocity at low altitudes. Exponential smoothing (τ = 0.85s) provides cinematic acceleration curves. Terrain floor enforcement via raycasting prevents subterranean traversal. |

**Tileset Optimization for Locomotion:**

The Viltrum Scene employs adjusted tileset parameters to accommodate high-velocity traversal:

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| maximumScreenSpaceError | 16 | Reduced fidelity during rapid movement to prioritize frame rate |
| maximumSimultaneousTileLoads | 5 | Throttled concurrent loads to prevent bandwidth saturation |
| maximumCachedBytes | 1 GB | Constrained cache for Quest 2 memory limitations |
| loadingDescendantLimit | 120 | Expanded descendant loading for smoother LOD transitions at speed |
| culledScreenSpaceError | 64 | Aggressive culling for off-screen tiles |

### Magic Scene

The Magic Scene is currently a structural duplicate of the Base Scene, reserved for future locomotion method implementation. Its existence ensures the scene selection infrastructure is prepared for comparative locomotion studies.

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
