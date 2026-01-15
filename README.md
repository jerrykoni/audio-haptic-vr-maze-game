# Audio-Haptic VR Navigation for Non-Visual Environments
### 🎓 Master's Thesis | National and Kapodistrian University of Athens

[![Thesis Database](https://img.shields.io/badge/official_publication-University_Repository-blue?style=for-the-badge&logo=googlescholar)](https://pergamos.lib.uoa.gr/item/uoadl:5311945)

<!-- PASTE YOUR VIDEO LINK BELOW THIS LINE -->
https://github.com/user-attachments/assets/1f364ca7-a482-4fa7-a31c-58cbce52ba46

> 🔊 **Sound On:** This experience relies on spatial audio. Please unmute.
>
> 👁️ **Visual Note:** The actual user experience is **non-visual** (black screen). The visuals in this video are enabled strictly for demonstration purposes.

## 📖 Project Overview
This project is a **VR accessibility research tool** developed for **Meta Quest 2/3**. It investigates how blind and low-vision users can navigate complex 3D environments without visual cues.

The experience functions as a VR game where the player must escape a maze relying entirely on **spatial audio** and **haptic feedback**. Unlike traditional VR games, the visual layer is removed to simulate blindness or low vision.

> **Research Goal:** To propose audio-haptic navigation and spatial awareness systems for accessible navigation of BLV (Blind or Low Vision) users in virtual reality, validating their efficiency and the impact on the player's level of connection with the virtual world.

## 🎨 Design Approach
*   **Gap Analysis:** Researched and analyzed relevant games and experiences to identify gaps in current solutions and derive inspiration.
*   **Iterative Design:** Development was based on insights gathered from the first prototype.
    *   *See the initial prototype here:* [Link to First Prototype Repo](https://github.com/jerrykoni/vr-accessibility-lab)

## ⚙️ Key Systems & Mechanics

### 1. "Rotational Radio" Audio Guidance
An orientation system inspired by radio tuning.
*   **Function:** Audio sources crossfade based on the player's head and controller rotation.
*   **Implementation:** Uses a mix of spatial audio blending and music volume crossfading to static noise to help users align themselves with the correct path purely by ear. The path is determined by the line connecting the player with different anchor points.

### 2. The Cone Scanner
A custom physics-based scanning system that detects objects in a virtual space.
*   **Mechanic:** Emits a conical beam from the controller to detect objects on specific physics layers.
*   **Feedback:** 
    *   *Audio:* Object names or properties are dictated via TTS on detection; hover sounds scale with distance.
    *   *Haptics:* Sharp impulse on detection, followed by soft pulses (continuous feedback) while hovering, synced with hover sounds.

### 3. Proximity-Based Haptic Walls
A continuous haptic feedback loop using the **Meta XR Haptics SDK**.
*   **Function:**
    *   *Haptics:* Vibration intensity increases smoothly in the respective controller as the user gets closer to walls.
    *   *Audio:* Velocity-based scraping sounds are emitted when the player is "touching" a wall while moving their hand.

## 🧪 Research & Validation
To validate the effectiveness of the audio-haptic systems, a user study was conducted with **16 participants**.
*   **Methodology:** Participants, including both blind and sighted groups, navigated the environment using Audio-Haptic cues without visual input.
*   **Data Collection:** 
    *   Post-experiment questionnaires and semi-structured interviews to assess subjective user impact (confidence, cognitive load, and spatial awareness).
    *   Success rates and researcher observations were recorded to identify navigation strategies and behavioral patterns.

## 📈 Outcome
The study provided insights into how these feedback combinations influence navigation, spatial awareness success, and player engagement, directly contributing to future BLV accessibility standards in XR.

**Key Findings:**
*   **Success Rate:** In general, all participants successfully escaped the maze.
*   **Mechanic Preference:** Distinct patterns were observed in mechanic efficacy (e.g., most players favored "Rotational Radio").
*   **User Metrics:** Differences were noted across groups (e.g., higher "Presence" scores in sighted vs. blind participants).

## 🛠 Technical Implementation
*   **Engine:** Unity 6
*   **Language:** C#
*   **Hardware:** Meta Quest 2 / Quest 3
*   **SDKs:** Meta XR All-in-One SDK (Interaction, Spatial Audio, Haptics)

## 📄 Thesis Details
**Title:** Design and Development of an Audio and Haptic Approach for Non-Visual Navigation in 3D Virtual Environments

**Institution:** National and Kapodistrian University of Athens (MSc in ICT)

**Link:** [Click here to view the official publication](https://pergamos.lib.uoa.gr/item/uoadl:5311945)

---

### ⚖️ License & Attribution

#### Code (C# Scripts)
The software source code developed for this project (scripts, logic systems) is licensed under the **MIT License**. You are free to use, modify, and distribute the code in your own projects.

#### Thesis & Research Content
The academic text and research data are licensed under the **Creative Commons Attribution-NonCommercial 4.0 International License (CC BY-NC 4.0)**.

#### Third-Party Dependencies
This project utilizes third-party software and assets:
*   **Unity Engine:** © Unity Technologies
*   **Meta XR SDK:** © Meta Platforms, Inc.
*   **Assets:** Unity Store, Freesound.org
