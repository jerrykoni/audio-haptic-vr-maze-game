# Audio-Haptic VR Navigation for Non-Visual Environments
### 🎓 Master's Thesis | National and Kapodistrian University of Athens

[![Thesis Database](https://img.shields.io/badge/official_publication-University_Repository-blue?style=for-the-badge&logo=googlescholar)](https://pergamos.lib.uoa.gr/item/uoadl:5311945)

https://github.com/user-attachments/assets/1f364ca7-a482-4fa7-a31c-58cbce52ba46

> 🔊 **Sound On:** This experience relies on spatial audio. Please unmute.
>
> 👁️ **Visual Note:** The actual user experience is **non-visual** (black screen). The visuals in this video are enabled strictly for demonstration purposes.

## 📖 Project Overview
This project is a **VR accessibility research tool** developed for **Meta Quest 2/3**. It investigates how blind and low-vision users can navigate complex 3D environments without visual cues.

The experience functions as a VR game where the player must escape a maze relying entirely on **spatial audio** and **haptic feedback**. Unlike traditional VR games, the visual layer is removed to simulate blindness or low vision.

> **Research Goal:** To validate audio-haptic combinations for accessible navigation in virtual reality environments.

## ⚙️ Key Systems & Mechanics

### 1. The Cone Scanner
A custom physics-based scanning system that detects objects in a virtual space.
*   **Mechanic:** Emits a conical beam from the controller to detect objects on specific physics layers.
*   **Feedback:** 
    *   *Audio:* TTS (Text-to-Speech) names the object; hover sounds scale with distance.
    *   *Haptics:* Sharp impulse on detection, followed by soft pulses (continuous feedback).
*   **Optimization:** Configurable scan frequency and geometry to maintain performance on standalone Quest hardware.

### 2. Proximity-Based Haptic Walls
A continuous haptic feedback loop using the **Meta XR Haptics SDK**.
*   **Function:** Vibration intensity increases smoothly as the user gets closer to obstacles.
*   **Tech Stack:** Optimized loop logic to handle complex wall geometry and multiple collider tags without CPU spikes.

### 3. "Rotational Radio" Audio Guidance
An orientation system inspired by radio tuning.
*   **Function:** Audio sources ("anchors") crossfade based on the player's head and controller rotation.
*   **Implementation:** Uses spatial audio blending to help users align themselves with the correct path purely by ear.

## 🧪 Research & Validation
To validate the effectiveness of the audio-haptic systems, a user study was conducted with **16 participants**.
*   **Methodology:** Participants (both blind and sighted) navigated the environment using Audio-Haptic cues without visual input.
*   **Data Collection:** Post-experiment questionnaires and structured interviews to assess user confidence, cognitive load, and spatial awareness.
*   **Outcome:** The study provided insights into how different feedback combinations influence navigation success, directly contributing to future accessibility standards in XR.

## 🛠 Technical Implementation
*   **Engine:** Unity 6
*   **Language:** C#
*   **Hardware:** Meta Quest 2 / Quest 3
*   **SDKs:** Meta XR All-in-One SDK (Interaction, Spatial Audio, Haptics)

## 📄 Thesis Details
**Title:** Design and Development of an Audio and Haptic Approach for Non-Visual Navigation in 3D Virtual Environments

**Institution:** National and Kapodistrian University of Athens (MSc in ICT)

**Link:** [Click here to view the official publication](https://pergamos.lib.uoa.gr/item/uoadl:5311945)

## ⚖️ License & Attribution

### Code (C# Scripts)
The software source code developed for this project (scripts, logic systems) is licensed under the **MIT License**. You are free to use, modify, and distribute the code in your own projects.

### Thesis & Research Content
The academic text and research data are licensed under the **Creative Commons Attribution-NonCommercial 4.0 International License (CC BY-NC 4.0)**.

### Third-Party Dependencies
This project utilizes the following third-party software:
*   **Unity Engine:** © Unity Technologies
*   **Meta XR SDK:** © Meta Platforms, Inc.
