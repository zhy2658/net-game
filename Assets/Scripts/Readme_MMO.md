# MMO Refactoring Guide

## 1. Package Installation
The `com.unity.netcode.gameobjects` package has been added to `manifest.json`. Unity will install it automatically when you return to the editor.

## 2. Scene Setup (Manual Steps Required)
Since I cannot edit the scene file directly without potentially breaking it, please perform the following steps in the Unity Editor:

1.  **Create NetworkManager**:
    *   Right-click in the Hierarchy -> Create Empty. Name it `NetworkManager`.
    *   Add Component -> `NetworkManager`.
    *   In the `NetworkManager` component, locate the `Transport` field. Click "Select Transport" -> choose `UnityTransport`.

2.  **Setup Player Prefab**:
    *   Select your player prefab (e.g., the one with `SimpleThirdPersonController`).
    *   Add Component -> `NetworkObject`.
    *   Add Component -> `NetworkTransform` (to sync position/rotation).
    *   **Important**: Drag this prefab into the `NetworkManager`'s `Player Prefab` slot.

3.  **Setup Connection UI**:
    *   Create a Canvas in your scene.
    *   Add two Buttons: rename them to "HostButton" and "ClientButton".
    *   Create an empty GameObject in the Canvas, name it `NetworkUI`.
    *   Add the `NetworkConnectUI` script to `NetworkUI`.
    *   Drag the buttons to the script's slots (or let it find them by name).

4.  **Test**:
    *   Build the project (File -> Build Settings).
    *   Run the built executable (as Host).
    *   Run the Editor (as Client).
    *   You should see two players moving independently!

## 3. Code Changes
*   `SimpleThirdPersonController.cs` has been modified to inherit from `NetworkBehaviour`.
*   Input logic is now restricted to `IsOwner`.
*   Added `NetworkConnectUI.cs` for basic connection handling.
