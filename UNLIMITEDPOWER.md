# PC Link Workflow: Full Reproduction Guide

## Prerequisites

- Meta Horizon Link app installed on PC
- Link cable connected (USB 3)
- Quest 2 powered on

---

## 1. Initiate Link

1. Open Meta Horizon Link on PC.
2. Don the headset and accept the Link prompt, or navigate to **Settings > System > Quest Link** and toggle it on from inside the headset.
3. Confirm you are in the grey grid Link home environment.

---

## 2. Meta Horizon Link Settings

1. In the Link app on PC, navigate to **Settings > General**.
2. Enable **Developer Runtime Features**.

---

## 3. Unity Build Profiles

1. Navigate to **File > Build Profiles**.
2. Select **Windows** from the platform list.
3. Click **Switch Platform** and wait for asset reimporting to complete.
4. Confirm **Windows** is marked as `Active`.

---

## 4. XR Plug-in Management

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

---

## 5. Launch

1. Close all Project Settings windows.
2. Hit **Play** in the Unity Editor.
3. The scene will render in the headset, driven by PC hardware.
