# CS 465 Capstone: Flight Locomotion in World-Scale VR Environments
**v1.0.8**

## Introduction

**Course:** CS 465: Multimodal Interaction for 3D User Interfaces (Spring 2026)  
**Instructor:** Dr. Francisco R. Ortega  
**Student:** Devin Lucas  
**Last Updated:** April 14, 2026

This repository contains the source code and documentation for a capstone research project investigating flight-based locomotion techniques within world-scale virtual reality environments. The project leverages Cesium for Unity to stream Google Photorealistic 3D Tiles, reconstructing Fort Collins, Colorado as a photogrammetric testbed for locomotion experimentation. A rudimentary "Viltrumite" flight locomotion method, inspired by the aerial mechanics depicted in the *Invincible* comic and television series, has been implemented as the initial locomotion paradigm. Future development will introduce additional locomotion techniques to facilitate comparative analysis of intuitiveness, user comfort, and navigational efficacy across varying flight modalities.

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
| VR Headset | Meta Quest 2 (Quest 3 compatible with minimal configuration adjustments; ensure Android build target and OpenXR settings are preserved) |
| Development Machine | Windows 10 (version 1903 or later) or Windows 11 |
| Mobile Device | iOS or Android device with Meta Horizon app installed |
| Link Cable | USB-C to USB-C (or USB-C to USB-A) cable for tethered development |

### Software

| Component | Version |
|-----------|---------|
| Unity Hub | Latest stable release |
| Unity Editor | v6000.0.31f1 or higher |
| Meta Horizon App | Latest stable release |
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

### Hardware Setup and Connection

1. Power on the Meta Quest 2 and connect it to your Windows PC via the Link cable.
2. Ensure the Quest 2 is connected to a WiFi network.
3. Open the Meta Horizon mobile app and connect it to your Quest 2.
4. Navigate to device settings within the Meta Horizon app and enable **Developer Mode**.
5. Adjust the interpupillary distance (IPD) slider and head straps on the Quest 2 for optimal fit.

### Building and Deploying to Quest 2

1. In the Unity Editor, navigate to **File** → **Build Profiles**.
2. Under **Platforms**, select **Android**.
3. Add the desired scene to the build (options: `BaseScene`, `ViltrumScene`, or `MagicScene`).
4. Set **Texture Compression** to **ASTC**.
5. Set **Run Device** to your connected Quest 2.
6. Enable the **Development Build** checkbox.
7. Click **Switch Platform** and wait for asset reimporting to complete.
8. Close Build Profiles, then navigate to **File** → **Build and Run**.
9. Wait for the build process to complete, don the headset, and explore the environment.

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
