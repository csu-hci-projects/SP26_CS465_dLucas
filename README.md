# CS 465 Capstone: Gesture-Driven Locomotion in World-Scale VR Environments
**v1.1.0**

## Introduction

**Course:** CS 465: Multimodal Interaction for 3D User Interfaces (Spring 2026)
**Instructor:** Dr. Francisco R. Ortega
**Student:** Devin Lucas
**Last Updated:** April 22, 2026

This repository contains the source code and documentation for a capstone research project investigating gesture-driven locomotion techniques within world-scale virtual reality environments. The project leverages Cesium for Unity to stream Google Photorealistic 3D Tiles, reconstructing Fort Collins, Colorado as a photogrammetric testbed for locomotion experimentation. Four distinct locomotion paradigms are under development: "Viltrumite" flight (inspired by the aerial mechanics depicted in the *Invincible* comic and television series), stroke-based pinch locomotion, biomimetic bird-flight flapping, and traditional thumbstick-based controller locomotion. Viltrumite flight is the primary implementation and is under active refinement. The remaining three methods are in varying stages of design and implementation. The completed set of locomotion methods will facilitate comparative analysis of intuitiveness, user comfort, and navigational efficacy across modalities.

## Table of Contents

1. [Introduction](#introduction)
2. [Software and Hardware Requirements](#software-and-hardware-requirements)
3. [Installation Instructions](#installation-instructions)
4. [Related Links](#related-links)
5. [References](#references)

## Software and Hardware Requirements

### Hardware

| Component | Specification |
|-----------|---------------|
| VR Headset | Meta Quest 2 (powered on, Developer Mode enabled) |
| Development Machine | Windows 10 (version 1903 or later) or Windows 11 |
| Link Cable | USB-C to USB-C or USB-C to USB-A, USB 3.0 recommended |

### Software

| Component | Version |
|-----------|---------|
| Unity Hub | Latest stable release |
| Unity Editor | v6000.0.31f1 or higher |
| Meta Horizon Link (PC) | Latest stable release (installed and signed in) |
| Git | Latest stable release |

## Installation Instructions

### Software Setup

1. Open a terminal and navigate to the directory where you wish to house the project.
2. Clone the repository:
   ```bash
   git clone https://github.com/[repository-url]/SP26_CS465_dLucas.git
   ```
3. Open Unity Hub.
4. Click **Add** → **Add project from disk**.
5. Navigate to and select the cloned `SP26_CS465_dLucas` directory.
6. Ensure Unity Editor v6000.0.31f1 (or higher) is installed; if not, install it via Unity Hub.
7. Open the project in the Unity Editor.

### Running Scenes via PC Link (Required)

> **Important:** Building and running these scenes directly on the Meta Quest 2 as a standalone Android application will result in severe performance degradation due to the computational demands of real-time 3D tile streaming. Standalone deployment is expressly disadvised. All scenes must be run via Meta Horizon PC Link, which renders on PC hardware while retaining Quest 2 I/O (head tracking, hand tracking, display).

#### 1. Initiate Link

1. Open **Meta Horizon Link** on the PC.
2. Connect the Quest 2 to the PC via the Link cable.
3. Don the headset and accept the Link prompt, or navigate to **Settings > System > Quest Link** from inside the headset and toggle it on.
4. Confirm you are in the grey grid Link home environment.

#### 2. Meta Horizon Link Settings

1. In the Meta Horizon Link PC app, navigate to **Settings > General**.
2. Enable **Developer Runtime Features**.

#### 3. Unity Build Profiles

1. In the Unity Editor, navigate to **File > Build Profiles**.
2. Select **Windows** from the platform list.
3. Click **Switch Platform** and wait for asset reimporting to complete.
4. Confirm **Windows** is marked as `Active`.

#### 4. XR Plug-in Management

1. Navigate to **Edit > Project Settings > XR Plug-in Management**.
2. On the **Windows** tab (monitor icon), check **OpenXR**.
3. Navigate to **XR Plug-in Management > OpenXR** (Windows tab) and configure the following:
   - Set **Play Mode OpenXR Runtime** to `Oculus OpenXR`.
   - Set **Render Mode** to `Single Pass Instanced`.
4. Under **Enabled Interaction Profiles**, click **+** and add `Oculus Touch Controller Profile`.
5. Under **OpenXR Feature Groups**, enable:
   - Hand Tracking Subsystem
   - Hand Interaction Poses
6. Return to the main **XR Plug-in Management** page and confirm **Initialize XR on Startup** is checked.

#### 5. Launch

1. Close all Project Settings windows.
2. Press **Play** in the Unity Editor.
3. The scene will render in the headset, driven by PC hardware.

## Related Links

- [Checkpoint 1 Code Discussion and Project Demonstration](https://youtu.be/YAuNIUb0uPI)
- [Repository](https://github.com/csu-hci-projects/SP26_CS465_dLucas.git)
- [Overleaf](https://www.overleaf.com/read/kcccfsqhmnkw#d56e0b)
- [ARCHITECTURE.md](ARCHITECTURE.md)
- [VISION.md](VISION.md)

## References

1. Loco Motion Devs. *Superfly*. Steam, 2020, store.steampowered.com/app/1413020/Superfly/. Accessed 21 Feb. 2026.

2. Andrei, Stoian (Boe). *WorldLens: All-in-One Virtual Travel*. Meta Quest Store, 2024, www.meta.com/experiences/worldlens-all-in-one-virtual-travel/6320120764784270/. Accessed 3 Mar. 2026.

3. Plabst, Lucas, et al. "Order Up! Multimodal Interaction Techniques for Notifications in Augmented Reality." *IEEE Transactions on Visualization and Computer Graphics*, vol. 31, no. 5, 2025, pp. 2258–2267, doi:10.1109/TVCG.2025.3549186. Accessed 28 Mar. 2026.

4. Satriadi, Kadek Ananta, et al. "Augmented Reality Map Navigation with Freehand Gestures." *2019 IEEE Conference on Virtual Reality and 3D User Interfaces (VR)*, IEEE, 2019, pp. 593–603, doi:10.1109/VR.2019.8798340. Accessed 14 Apr. 2026.

5. Weissker, Tim, et al. "Try This for Size: Multi-Scale Teleportation in Immersive Virtual Reality." *IEEE Transactions on Visualization and Computer Graphics*, vol. 30, no. 5, 2024, pp. 2298–2308, doi:10.1109/TVCG.2024.3372043. Accessed 7 Apr. 2026.

6. Williams, Adam S., et al. "Understanding Multimodal User Gesture and Speech Behavior for Object Manipulation in Augmented Reality Using Elicitation." *IEEE Transactions on Visualization and Computer Graphics*, vol. 26, no. 12, 2020, pp. 3479–3489, doi:10.1109/TVCG.2020.3023566. Accessed 10 Apr. 2026.

*Note: Additional peer-reviewed literature will be cited in Checkpoint 2 and the final capstone release.*
