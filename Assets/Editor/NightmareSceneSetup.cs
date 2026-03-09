using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI; // For UI creation
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEngine.Rendering.Universal; // For URP Settings
using UnityEditor.Animations; // Required for AnimatorController

public class NightmareSceneSetup : EditorWindow
{
    [MenuItem("TraeTools/Setup Nightmare Scene")]
    public static void SetupScene()
    {
        // 1. Scene Management - Use the SimpleNaturePack Demo Scene
        string targetScenePath = "Assets/SimpleNaturePack/Scenes/SimpleNaturePack_Demo.unity";
        
        if (EditorSceneManager.GetActiveScene().path != targetScenePath)
        {
            if (System.IO.File.Exists(targetScenePath))
            {
                if (EditorUtility.DisplayDialog("Switch Scene", $"Open {System.IO.Path.GetFileName(targetScenePath)} to proceed?", "Yes", "No"))
                {
                    EditorSceneManager.OpenScene(targetScenePath);
                }
                else return;
            }
            else
            {
                Debug.LogError($"Could not find target scene: {targetScenePath}");
                return;
            }
        }

        // 2. Fix Materials
        FixMaterials();

        // 2.1 Ensure EventSystem is clean before setup
        var es = GameObject.FindObjectOfType<EventSystem>();
        if (es != null) DestroyImmediate(es.gameObject);

        // 3. Cleanup existing Player/Camera and Legacy Controllers
        string[] targets = new string[] { "Akane_Player", "Player_Casual1", "FPSController", "FpsController", "FirstPersonController", "Main Camera", "MainCamera", "MobileControlsCanvas", "EventSystem" }; 
        foreach (string t in targets)
        {
            GameObject obj = GameObject.Find(t);
            if (obj != null) DestroyImmediate(obj);
        }

        // 4. Setup Lighting (Darker)
        RenderSettings.ambientIntensity = 0.2f;
        RenderSettings.reflectionIntensity = 0.2f;

        // 4.1 Resize World (Make SimpleNaturePack Bigger)
        ResizeWorld();

        // 5. Instantiate Player (Casual1 Anime Girl)
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnimeGirls/Casual1/Casual1.prefab");
        if (playerPrefab != null)
        {
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player_Casual1";
            
            // Set Position - Default for SimpleNaturePack (Scaled x20)
            Vector3 spawnPos = new Vector3(82f, 15f, -50f); 
            bool foundSpot = false;

            // Try to find a good spawn point (Disabled for manual override)
            /* 
            string[] landmarks = new string[] { "Ground_01", "Ground", "Terrain" };
            foreach (string mark in landmarks)
            {
                GameObject obj = GameObject.Find(mark);
                if (obj != null)
                {
                    Renderer r = obj.GetComponentInChildren<Renderer>();
                    if (r != null)
                    {
                        spawnPos = r.bounds.center + Vector3.up * 2f;
                        foundSpot = true;
                        break;
                    }
                }
            }
            */

            // Raycast check to ensure we don't spawn in void
            if (Physics.Raycast(spawnPos + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 50f))
            {
                spawnPos = hit.point + Vector3.up * 1f;
                foundSpot = true;
            }

            if (!foundSpot)
            {
                Debug.LogWarning("Could not find ground at default spawn pos. Using raw coordinates.");
            }
            
            player.transform.position = spawnPos;
            player.transform.rotation = Quaternion.Euler(0, 180, 0);
            
            // Fix Player Materials (URP)
            Renderer[] charRenderers = player.GetComponentsInChildren<Renderer>();
            foreach(var r in charRenderers)
            {
                foreach(var mat in r.sharedMaterials)
                {
                    UpgradeMaterialToURP(mat);
                }
            }

            // Ensure Components
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc == null) cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0, 0.75f, 0);
            cc.height = 1.5f;
            cc.radius = 0.3f;

            if (player.GetComponent<SimpleThirdPersonController>() == null)
                player.AddComponent<SimpleThirdPersonController>();
                
            // 8. Setup Animator Controller with New Animations
            Animator anim = player.GetComponent<Animator>();
            if (anim == null) anim = player.AddComponent<Animator>();

            // Ensure we have a valid avatar, otherwise animations won't play correctly
            if (anim.avatar == null || !anim.avatar.isValid)
            {
                 Avatar mixamoAvatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Animations/player/idle.fbx");
                 if (mixamoAvatar != null) anim.avatar = mixamoAvatar;
            }
            
            // Create a fresh Animator Controller for the Player
            string controllerPath = "Assets/Animations/player/PlayerController.controller";
            // Ensure directory exists
            if (!System.IO.Directory.Exists("Assets/Animations/player"))
            {
                System.IO.Directory.CreateDirectory("Assets/Animations/player");
            }
            
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            
            // Add Parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Trigger); // New Dead Trigger
            controller.AddParameter("Dance", AnimatorControllerParameterType.Trigger); // New Dance Trigger
            controller.AddParameter("isWalk", AnimatorControllerParameterType.Bool); // Keep for compatibility

            // Get States (Root State Machine)
            var rootStateMachine = controller.layers[0].stateMachine;

            // Add States
            var idleState = rootStateMachine.AddState("Idle");
            var runState = rootStateMachine.AddState("Run");
            var jumpState = rootStateMachine.AddState("Jump");
            var attackState = rootStateMachine.AddState("Attack");
            var deadState = rootStateMachine.AddState("Dead"); // New Dead State
            var danceState = rootStateMachine.AddState("Dance"); // New Dance State

            // Fix Loop Settings using ModelImporter (More reliable for FBX)
            SetFBXLoopTime("Assets/Animations/player/idle.fbx", true);
            SetFBXLoopTime("Assets/Animations/player/run.fbx", true); // Re-enable Loop for Run
            SetFBXLoopTime("Assets/Animations/player/dance1.fbx", true); // Dance should loop
            SetFBXLoopTime("Assets/Animations/player/dead0.fbx", false); // Dead should NOT loop

            // Load clips after setting import settings
            AnimationClip idleClip = LoadAnimationClip("Assets/Animations/player/idle.fbx", "mixamo.com"); 
            AnimationClip runClip = LoadAnimationClip("Assets/Animations/player/run.fbx", "mixamo.com");
            AnimationClip jumpClip = LoadAnimationClip("Assets/Animations/player/jump.fbx", "mixamo.com");
            AnimationClip attackClip = LoadAnimationClip("Assets/Animations/player/attack.fbx", "mixamo.com");
            AnimationClip deadClip = LoadAnimationClip("Assets/Animations/player/dead0.fbx", "mixamo.com");
            AnimationClip danceClip = LoadAnimationClip("Assets/Animations/player/dance1.fbx", "mixamo.com");

            if (idleClip != null) idleState.motion = idleClip;
            if (runClip != null)
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(runClip);
                settings.loopTime = true; // Loop is required for Run
                settings.loopBlend = true;
                settings.loopBlendOrientation = true;
                settings.loopBlendPositionY = true;
                settings.loopBlendPositionXZ = true;
                
                // Bake Into Pose to prevent "drifting" (Root Motion issue)
                settings.keepOriginalOrientation = true;
                settings.keepOriginalPositionY = true;
                settings.keepOriginalPositionXZ = true;
                
                AnimationUtility.SetAnimationClipSettings(runClip, settings);
                runState.motion = runClip;
            }
            if (jumpClip != null) jumpState.motion = jumpClip;
            if (attackClip != null) attackState.motion = attackClip;
            if (deadClip != null) deadState.motion = deadClip; // New Dead Motion
            if (danceClip != null) danceState.motion = danceClip; // New Dance Motion

            // Create Transitions
            // Idle <-> Run
            var toRun = idleState.AddTransition(runState);
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            toRun.hasExitTime = false; // Immediate transition
            toRun.duration = 0.1f;
            
            var toIdle = runState.AddTransition(idleState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            toIdle.hasExitTime = false; // Immediate transition
            toIdle.duration = 0.1f;

            // Any State -> Jump
            var toJump = rootStateMachine.AddAnyStateTransition(jumpState);
            toJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            toJump.hasExitTime = false;
            toJump.duration = 0.1f;
            
            var jumpExit = jumpState.AddTransition(idleState);
            jumpExit.hasExitTime = true; // Auto exit after jump
            jumpExit.duration = 0.2f;

            // Any State -> Attack
            var toAttack = rootStateMachine.AddAnyStateTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            toAttack.hasExitTime = false; // Immediate trigger
            toAttack.duration = 0.05f; // Faster transition
            toAttack.canTransitionToSelf = true; // Allow rapid attacks?
            
            var attackExit = attackState.AddTransition(idleState);
            attackExit.hasExitTime = true; // Auto exit after attack
            attackExit.exitTime = 0.8f; // Exit at 80% of animation
            attackExit.duration = 0.2f;

            // Any State -> Dead
            var toDead = rootStateMachine.AddAnyStateTransition(deadState);
            toDead.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            toDead.hasExitTime = false;
            toDead.duration = 0.1f;
            // Dead has no exit (stay dead) or manual reset

            // Any State -> Dance
            var toDance = rootStateMachine.AddAnyStateTransition(danceState);
            toDance.AddCondition(AnimatorConditionMode.If, 0, "Dance");
            toDance.hasExitTime = false;
            toDance.duration = 0.1f;
            
            var danceExit = danceState.AddTransition(idleState);
            danceExit.hasExitTime = true; // Auto exit
            danceExit.exitTime = 0.9f;
            danceExit.duration = 0.2f;

            // Assign Controller
            anim.runtimeAnimatorController = controller;
            
            // Ensure Avatar (Try to load from idle.fbx if Casual1 fails)
            if (anim.avatar == null || !anim.avatar.isValid)
            {
                 Avatar fallbackAvatar = AssetDatabase.LoadAssetAtPath<Avatar>("Assets/Animations/player/idle.fbx");
                 if (fallbackAvatar != null) anim.avatar = fallbackAvatar;
            }

            // Setup Attack Script
            PlayerAttack playerAttack = player.GetComponent<PlayerAttack>();
            if (playerAttack == null) playerAttack = player.AddComponent<PlayerAttack>();
            player.tag = "Player";

            // 7. Setup Camera
            GameObject mainCamera = new GameObject("Main Camera");
            mainCamera.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            mainCamera.AddComponent<AudioListener>();
            
            ThirdPersonCamera camScript = mainCamera.AddComponent<ThirdPersonCamera>();
            camScript.target = player.transform;
            camScript.distance = 3.5f;
            camScript.height = 1.6f;
            camScript.damping = 5.0f;
            camScript.rotationSpeed = 0.2f;
            
            Debug.Log("Casual1 Player & Camera Setup Complete.");
            
            // 10. Setup Weapon (Rusty Knife)
            GameObject knifePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dnk_Dev/KitchenRustyKnife/P_KitchenRustyKnife.prefab");
            if (knifePrefab != null)
            {
                playerAttack.weaponPrefab = knifePrefab;

                // Spawn Pickup Knife near player
                GameObject existingPickup = GameObject.Find("Rusty_Knife_Pickup");
                if (existingPickup != null) DestroyImmediate(existingPickup);

                GameObject pickupKnife = (GameObject)PrefabUtility.InstantiatePrefab(knifePrefab);
                pickupKnife.name = "Rusty_Knife_Pickup";
                
                // Fix Scale
                pickupKnife.transform.localScale = Vector3.one * 0.02f; 

                // Fix Knife Materials (URP)
                Renderer[] knifeRenderers = pickupKnife.GetComponentsInChildren<Renderer>();
                foreach(var r in knifeRenderers)
                {
                    foreach(var mat in r.sharedMaterials)
                    {
                        if (mat == null) continue;
                        UpgradeMaterialToURP(mat);
                    }
                }
                
                // Position it 2 meters in front of player
                Vector3 spawnForward = player.transform.forward;
                if (spawnForward == Vector3.zero) spawnForward = Vector3.forward;
                
                Vector3 knifePos = player.transform.position + spawnForward * 2.0f + Vector3.up * 1.0f;
                
                if (Physics.Raycast(knifePos + Vector3.up * 50.0f, Vector3.down, out RaycastHit knifeHit, 100f, ~0, QueryTriggerInteraction.Ignore))
                {
                    pickupKnife.transform.position = knifeHit.point + Vector3.up * 0.1f;
                    pickupKnife.transform.rotation = Quaternion.FromToRotation(Vector3.up, knifeHit.normal) * Quaternion.Euler(90, 0, 0);
                }
                else
                {
                    pickupKnife.transform.position = new Vector3(knifePos.x, player.transform.position.y + 0.1f, knifePos.z);
                    pickupKnife.transform.rotation = Quaternion.Euler(90, 0, 0);
                }

                Collider col = pickupKnife.GetComponent<Collider>();
                if (col == null) 
                {
                    BoxCollider box = pickupKnife.AddComponent<BoxCollider>();
                    box.isTrigger = true;
                    box.size = new Vector3(0.5f, 0.5f, 1.5f); 
                }
                else
                {
                    col.isTrigger = true;
                }

                WeaponPickup pickupScript = pickupKnife.GetComponent<WeaponPickup>();
                if (pickupScript == null) pickupScript = pickupKnife.AddComponent<WeaponPickup>();
                pickupScript.weaponPrefab = knifePrefab;
                
                Debug.Log("Weapon Setup Complete.");
            }
            
            // 11. Setup Enemy (Pumpkin King)
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/True_Horror/True_Pumpkin/pumpkin_king.fbx");
            if (enemyPrefab != null)
            {
                GameObject existingEnemy = GameObject.Find("Pumpkin_King");
                if (existingEnemy != null) DestroyImmediate(existingEnemy);

                GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                enemy.name = "Pumpkin_King";
                enemy.transform.localScale = Vector3.one * 0.5f; 
                
                Vector3 spawnForward = player.transform.forward;
                if (spawnForward == Vector3.zero) spawnForward = Vector3.forward;
                
                Vector3 enemyPos = player.transform.position + spawnForward * 10.0f;
                
                if (Physics.Raycast(enemyPos + Vector3.up * 50f, Vector3.down, out RaycastHit enemyHit, 100f, ~0, QueryTriggerInteraction.Ignore))
                {
                    enemy.transform.position = enemyHit.point;
                }
                else
                {
                    enemy.transform.position = enemyPos;
                }
                
                enemy.transform.LookAt(player.transform);
                
                if (enemy.GetComponent<CapsuleCollider>() == null)
                {
                    CapsuleCollider col = enemy.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0, 1f, 0);
                    col.height = 2f;
                    col.radius = 0.5f;
                }
                
                if (enemy.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
                {
                    UnityEngine.AI.NavMeshAgent agent = enemy.AddComponent<UnityEngine.AI.NavMeshAgent>();
                    agent.speed = 3.5f;
                    agent.acceleration = 8f;
                    agent.angularSpeed = 120f;
                }
                
                if (enemy.GetComponent<EnemyAI>() == null)
                {
                    enemy.AddComponent<EnemyAI>();
                }
                
                Animator enemyAnim = enemy.GetComponent<Animator>();
                if (enemyAnim == null) enemyAnim = enemy.AddComponent<Animator>();
                
                RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/True_Horror/True_Pumpkin/Demo Animations.controller");
                if (animCtrl != null)
                {
                    enemyAnim.runtimeAnimatorController = animCtrl;
                }
                
                Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                {
                    foreach (Material mat in r.sharedMaterials)
                    {
                        if (mat == null) continue;
                        UpgradeMaterialToURP(mat);
                    }
                }
                
                Debug.Log("Enemy Setup Complete.");
            }
            
            // 12. Setup Network (Create Prefab & NetworkManager)
            // Pass the calculated spawnPos to SetupNetwork
            SetupNetwork(player, spawnPos);
        }
        else
        {
            Debug.LogError("Could not find Casual1 Prefab!");
        }

        // 13. Setup Mobile Controls (Android)
        SetupMobileControls();
        
        // 14. Optimize Graphics (Fix blurry screen)
        // OptimizeGraphics(); // Disabled per user request
        
        // Cleanup Audio Listeners
        AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>();
        foreach (var l in listeners)
        {
            if (l.gameObject.name != "Main Camera") DestroyImmediate(l);
        }
        
        Debug.Log("Scene Setup & Repairs Complete.");
        
        // Add to Build Settings & Save
        string currentScenePath = EditorSceneManager.GetActiveScene().path;
        AddSceneToBuildSettings(currentScenePath);
        
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log($"Scene saved and added to Build Settings: {currentScenePath}");
    }

    private static void SetupMobileControls()
    {
        Debug.Log("Initializing Mobile Controls (Version 2.1 - Font Fix)...");

        // 1. Create EventSystem if missing (Required for UI)
        if (GameObject.FindObjectOfType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            
            // Add InputSystemUIInputModule via Reflection
            System.Type inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
            {
                esObj.AddComponent(inputModuleType);
            }
            else
            {
                // Fallback to StandaloneInputModule (Legacy)
                esObj.AddComponent<StandaloneInputModule>();
                Debug.LogWarning("InputSystemUIInputModule not found. Using StandaloneInputModule. Mobile controls might not work if using New Input System.");
            }
        }

        GameObject canvasObj = GameObject.Find("MobileControlsCanvas");
        if (canvasObj != null) DestroyImmediate(canvasObj);
        
        canvasObj = new GameObject("MobileControlsCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create Joystick Area (Left Bottom)
        GameObject joystickObj = new GameObject("OnScreenStick");
        joystickObj.transform.SetParent(canvasObj.transform);
        Image jsBg = joystickObj.AddComponent<Image>();
        jsBg.color = new Color(1, 1, 1, 0.3f); // Transparent White
        RectTransform jsRect = joystickObj.GetComponent<RectTransform>();
        jsRect.anchorMin = new Vector2(0, 0);
        jsRect.anchorMax = new Vector2(0, 0);
        jsRect.pivot = new Vector2(0, 0);
        jsRect.anchoredPosition = new Vector2(100, 100);
        jsRect.sizeDelta = new Vector2(200, 200);
        
        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(joystickObj.transform);
        Image handleImg = handleObj.AddComponent<Image>();
        handleImg.color = new Color(1, 1, 1, 0.8f);
        RectTransform handleRect = handleObj.GetComponent<RectTransform>();
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(100, 100);
        
        // Use Reflection to add OnScreenStick to avoid compile error if package/assembly missing in Editor
        System.Type stickType = System.Type.GetType("UnityEngine.InputSystem.OnScreen.OnScreenStick, Unity.InputSystem");
        if (stickType != null)
        {
            var stick = joystickObj.AddComponent(stickType);
            SerializedObject so = new SerializedObject(stick);
            // Try explicit path with angle brackets
            SerializedProperty controlPathProp = so.FindProperty("controlPath");
            if (controlPathProp == null) controlPathProp = so.FindProperty("m_ControlPath");
            
            if (controlPathProp != null)
            {
                controlPathProp.stringValue = "<Gamepad>/leftStick";
                SerializedProperty rangeProp = so.FindProperty("movementRange");
                if (rangeProp != null) rangeProp.floatValue = 50;
                so.ApplyModifiedProperties();
            }
        }
        else
        {
            Debug.LogWarning("OnScreenStick type not found. Please add it manually to 'OnScreenStick' GameObject.");
        }

        // Create Touchpad Area (Right Side, Behind Buttons) for Camera Rotation
        GameObject touchpadObj = new GameObject("OnScreenTouchpad");
        touchpadObj.transform.SetParent(canvasObj.transform);
        touchpadObj.transform.SetAsFirstSibling(); // Ensure it's behind buttons
        Image tpImg = touchpadObj.AddComponent<Image>();
        tpImg.color = new Color(0, 0, 0, 0.05f); // Very faint black to visualize area (or 0 for invisible)
        RectTransform tpRect = touchpadObj.GetComponent<RectTransform>();
        tpRect.anchorMin = new Vector2(0.5f, 0); // Right Half
        tpRect.anchorMax = new Vector2(1, 1);
        tpRect.pivot = new Vector2(0.5f, 0.5f);
        tpRect.anchoredPosition = Vector2.zero;
        tpRect.sizeDelta = Vector2.zero; // Fill right half
        
        // Use Reflection for OnScreenTouchpad
        System.Type touchpadType = System.Type.GetType("UnityEngine.InputSystem.OnScreen.OnScreenTouchpad, Unity.InputSystem");
        if (touchpadType != null)
        {
            var touchpad = touchpadObj.AddComponent(touchpadType);
            SerializedObject so = new SerializedObject(touchpad);
            SerializedProperty controlPathProp = so.FindProperty("controlPath");
            if (controlPathProp == null) controlPathProp = so.FindProperty("m_ControlPath");

            if (controlPathProp != null)
            {
                controlPathProp.stringValue = "<Gamepad>/rightStick";
                so.ApplyModifiedProperties();
            }
        }
        else
        {
             Debug.LogWarning("OnScreenTouchpad type not found. Please add it manually.");
        }
        
        // Create Attack Button (Right Bottom Big)
        CreateOnScreenButton(canvasObj.transform, "AttackButton", new Vector2(-250, 150), new Vector2(180, 180), "<Mouse>/leftButton", Color.red);

        // Create Jump Button (Right Bottom Small)
        CreateOnScreenButton(canvasObj.transform, "JumpButton", new Vector2(-100, 250), new Vector2(120, 120), "<Keyboard>/space", Color.green);
        
        // Create Run Button (Right Bottom Small)
        CreateOnScreenButton(canvasObj.transform, "RunButton", new Vector2(-400, 100), new Vector2(100, 100), "<Keyboard>/leftShift", Color.blue);
        
        // Add MobileUIHandler to manage visibility (Hide on PC, Show on Mobile)
        if (canvasObj.GetComponent<MobileUIHandler>() == null)
        {
            canvasObj.AddComponent<MobileUIHandler>();
        }
        
        Debug.Log("Mobile Controls Created.");
    }

    private static void CreateOnScreenButton(Transform parent, string name, Vector2 pos, Vector2 size, string path, Color color)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent);
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(color.r, color.g, color.b, 0.5f);
        
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0); // Bottom Right
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        
        // Use Reflection for OnScreenButton
        System.Type btnType = System.Type.GetType("UnityEngine.InputSystem.OnScreen.OnScreenButton, Unity.InputSystem");
        if (btnType != null)
        {
            var btn = btnObj.AddComponent(btnType);
            SerializedObject so = new SerializedObject(btn);
            SerializedProperty controlPathProp = so.FindProperty("controlPath");
            if (controlPathProp == null) controlPathProp = so.FindProperty("m_ControlPath");

            if (controlPathProp != null)
            {
                controlPathProp.stringValue = path;
                so.ApplyModifiedProperties();
            }
        }
        else
        {
             Debug.LogWarning($"OnScreenButton type not found. Please add it manually to '{name}' GameObject.");
        }
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] originalScenes = EditorBuildSettings.scenes;
        
        foreach (var scene in originalScenes)
        {
            if (scene.path == scenePath) return;
        }

        List<EditorBuildSettingsScene> newScenes = new List<EditorBuildSettingsScene>(originalScenes);
        newScenes.Add(new EditorBuildSettingsScene(scenePath, true));
        
        if (newScenes.Count > 1 && newScenes[0].path.Contains("SampleScene"))
        {
            var current = newScenes[newScenes.Count - 1];
            newScenes.RemoveAt(newScenes.Count - 1);
            newScenes.Insert(0, current);
        }

        EditorBuildSettings.scenes = newScenes.ToArray();
    }

    private static void FixMaterials()
    {
        Renderer[] renderers = Object.FindObjectsOfType<Renderer>();
        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                UpgradeMaterialToURP(mat);
            }
        }
    }

    private static bool UpgradeMaterialToURP(Material mat)
    {
        if (mat.shader.name == "Universal Render Pipeline/Lit") return false;

        string shaderName = mat.shader.name;
        // Check for Low-Poly Nature pack keywords
        bool isNature = shaderName.Contains("Nature") || shaderName.Contains("Tree") || shaderName.Contains("Vegetation") || shaderName.Contains("Leaves") || shaderName.Contains("Bark") || mat.name.Contains("Leaf") || mat.name.Contains("Grass");
        bool isStandard = shaderName == "Standard" || shaderName.Contains("Diffuse") || shaderName.Contains("Bumped");
        
        if (isStandard || isNature || shaderName == "Hidden/InternalErrorShader" || shaderName.Contains("SpeedTree") || shaderName.Contains("Legacy Shaders") || mat.name.Contains("Knife")) 
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return false;

            Texture mainTex = null;
            if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
            else if (mat.HasProperty("_layer1Tex")) mainTex = mat.GetTexture("_layer1Tex");
            else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

            Texture bumpMap = null;
            if (mat.HasProperty("_BumpMap")) bumpMap = mat.GetTexture("_BumpMap");
            else if (mat.HasProperty("_DetailBump")) bumpMap = mat.GetTexture("_DetailBump");

            Color color = Color.white;
            if (mat.HasProperty("_Color")) color = mat.GetColor("_Color");
            else if (mat.HasProperty("_MainColor")) color = mat.GetColor("_MainColor");

            mat.shader = urpLit;

            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
            if (bumpMap != null) mat.SetTexture("_BumpMap", bumpMap);
            mat.SetColor("_BaseColor", color);
            
            string lowerName = mat.name.ToLower();
            string lowerShader = shaderName.ToLower();
            
            if (lowerName.Contains("leaf") || lowerName.Contains("grass") || lowerName.Contains("fern") || lowerName.Contains("branch") || 
                lowerName.Contains("tree") || lowerName.Contains("bush") || lowerName.Contains("plant") ||
                lowerShader.Contains("transparent") || lowerShader.Contains("cutout"))
            {
                mat.SetFloat("_Surface", 0);
                mat.SetFloat("_AlphaClip", 1);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetFloat("_Cull", 0);
                mat.SetInt("_Cull", 0); 
            }
            else
            {
                mat.SetFloat("_Surface", 0);
                mat.SetFloat("_AlphaClip", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
            }
            
            return true;
        }
        return false;
    }

    private static AnimationClip LoadAnimationClip(string path, string clipNameFilter)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip)
            {
                if (clip.name.Contains("__preview__")) continue;
                if (string.IsNullOrEmpty(clipNameFilter) || clip.name.Contains(clipNameFilter)) 
                    return clip;
            }
        }
        return null;
    }

    private static void SetupNetwork(GameObject player, Vector3 spawnPos)
    {
        // Add Network Components to Player
        if (player.GetComponent<NetworkObject>() == null)
            player.AddComponent<NetworkObject>();
            
        // Use ClientNetworkTransform for client-authoritative movement
        // We use Reflection to find it since we just created the file
        System.Type clientTransformType = System.Type.GetType("Unity.Multiplayer.Samples.Utilities.ClientAuthority.ClientNetworkTransform, Assembly-CSharp");
        if (clientTransformType == null) clientTransformType = System.Type.GetType("Unity.Multiplayer.Samples.Utilities.ClientAuthority.ClientNetworkTransform");
        
        if (clientTransformType != null)
        {
            if (player.GetComponent(clientTransformType) == null)
                player.AddComponent(clientTransformType);
        }
        else
        {
            // Fallback to standard NetworkTransform if custom script not found yet
            if (player.GetComponent<NetworkTransform>() == null)
                player.AddComponent<NetworkTransform>();
            Debug.LogWarning("ClientNetworkTransform not found! Using standard NetworkTransform (might cause movement jitter if not server auth).");
        }

        // Create Prefab
        string prefabDir = "Assets/Prefabs";
        if (!System.IO.Directory.Exists(prefabDir))
            System.IO.Directory.CreateDirectory(prefabDir);
            
        string prefabPath = prefabDir + "/NetworkPlayer.prefab";
        GameObject playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, prefabPath);
        Debug.Log($"Created Network Player Prefab at: {prefabPath}");
        
        // Destroy Scene Instance (NetworkManager will spawn it)
        Object.DestroyImmediate(player);

        // Setup NetworkManager
        GameObject netManagerObj = GameObject.Find("NetworkManager");
        if (netManagerObj != null) Object.DestroyImmediate(netManagerObj);
        
        netManagerObj = new GameObject("NetworkManager");
        NetworkManager netManager = netManagerObj.AddComponent<NetworkManager>();
        
        // Add Transport
        UnityTransport transport = netManagerObj.AddComponent<UnityTransport>();
        // netManager.NetworkConfig.NetworkTransport = transport; // Set via SerializedObject below

        // Use SerializedObject to set NetworkConfig properties
        SerializedObject so = new SerializedObject(netManager);
        SerializedProperty networkConfig = so.FindProperty("NetworkConfig");
        if (networkConfig != null)
        {
            // Set Player Prefab
            SerializedProperty playerPrefabProp = networkConfig.FindPropertyRelative("PlayerPrefab");
            if (playerPrefabProp != null)
                playerPrefabProp.objectReferenceValue = playerPrefab;
                
            // Set NetworkTransport (Important: UnityTransport must be assigned!)
            // Note: In newer Netcode versions, this is a serialized field on NetworkManager itself or inside config.
            // We can try to set the NetworkTransport field directly if it exists.
            SerializedProperty transportProp = networkConfig.FindPropertyRelative("NetworkTransport");
            if (transportProp != null)
                transportProp.objectReferenceValue = transport;
        }
        so.ApplyModifiedProperties();
        
        // Ensure UnityTransport is assigned via Reflection as a fallback (some versions need this)
        try 
        {
            netManager.NetworkConfig.NetworkTransport = transport;
        }
        catch {}

        // Set Position to Spawn Point if possible
        // Note: NetworkManager spawns player at (0,0,0) by default or random points if configured.
        // We can't easily change default spawn pos without a custom NetworkManager script or a spawner.
        // But we can create a simple spawner script.
        
        GameObject spawnerObj = GameObject.Find("PlayerSpawner");
        if (spawnerObj != null) Object.DestroyImmediate(spawnerObj);
        spawnerObj = new GameObject("PlayerSpawner");
        // We will add a simple script via Reflection or just rely on manual move for now.
        // Actually, let's create a simple NetworkSpawner script to handle positioning.
        
        CreateSpawnerScript(spawnPos);
        if (System.Type.GetType("NetworkPlayerSpawner") != null)
        {
             spawnerObj.AddComponent(System.Type.GetType("NetworkPlayerSpawner"));
        }

        // Create UI
        GameObject uiObj = GameObject.Find("NetworkUI");
        if (uiObj != null) Object.DestroyImmediate(uiObj);
        
        uiObj = new GameObject("NetworkUI");
        Canvas canvas = uiObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // Ensure on top
        CanvasScaler scaler = uiObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        uiObj.AddComponent<GraphicRaycaster>();
        
        // Create NanoKcpClient Script (KCP/UDP)
        CreateNanoKcpClientScript();

        // Add NanoClient
        GameObject nanoObj = new GameObject("NanoClient");
        nanoObj.transform.SetParent(uiObj.transform);
        
        // Use reflection to find NanoKcpClient
        // Since we just created it, we might need to rely on next compile, but we try anyway
        System.Type clientType = System.Type.GetType("NanoKcpClient, Assembly-CSharp");
        if (clientType == null) clientType = System.Type.GetType("NanoKcpClient");

        if (clientType != null)
        {
            nanoObj.AddComponent(clientType);
        }
        else
        {
            Debug.LogWarning("NanoKcpClient script created. Please wait for compilation and run setup again if not attached.");
        }
        
        // Ensure EventSystem exists here for the Network UI
        if (GameObject.FindObjectOfType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            // Try add new Input System module
            System.Type inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null) esObj.AddComponent(inputModuleType);
            else esObj.AddComponent<StandaloneInputModule>();
        }

        NetworkConnectUI uiScript = uiObj.AddComponent<NetworkConnectUI>();
        
        // Create Buttons
        GameObject hostBtn = CreateButton(uiObj.transform, "HostButton", new Vector2(-200, 0), "Host\n(创建房间)", Color.green);
        GameObject clientBtn = CreateButton(uiObj.transform, "ClientButton", new Vector2(200, 0), "Client\n(加入房间)", Color.cyan);
        
        // Assign to script
        SerializedObject uiSo = new SerializedObject(uiScript);
        uiSo.FindProperty("hostButton").objectReferenceValue = hostBtn.GetComponent<Button>();
        uiSo.FindProperty("clientButton").objectReferenceValue = clientBtn.GetComponent<Button>();
        uiSo.ApplyModifiedProperties();
        
        Debug.Log("Network Setup Complete.");
    }
    
    private static void CreateSpawnerScript(Vector3 spawnPos)
    {
        string path = "Assets/Scripts/NetworkPlayerSpawner.cs";
        // Force update the spawn position in the script content
        // Note: The spawnPos passed to this function is already updated (82, 15, -50)
        string content = @"using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSpawner : NetworkBehaviour
{
    public Vector3 spawnPosition = new Vector3(" + spawnPos.x + "f, " + spawnPos.y + "f, " + spawnPos.z + @"f);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            var playerObject = client.PlayerObject;
            if (playerObject != null)
            {
                var cc = playerObject.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                
                playerObject.transform.position = spawnPosition;
                
                if (cc != null) cc.enabled = true;
            }
        }
    }
    
    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
             NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
        base.OnDestroy();
    }
}
";
        // Always overwrite to ensure updated coordinates
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
             
        System.IO.File.WriteAllText(path, content);
        AssetDatabase.Refresh();
    }

    private static void CreateNanoKcpClientScript()
    {
        string path = "Assets/Scripts/NanoKcpClient.cs";
        string content = @"using System;
using System.IO;
using System.Text;
using UnityEngine;
using KcpProject; 
using Google.Protobuf; 
using Protocol; 

public class NanoKcpClient : MonoBehaviour
{
    public string host = ""127.0.0.1"";
    public int port = 3250;

    private UDPSession session;
    private byte[] recvBuffer = new byte[8192]; 
    private MemoryStream streamBuffer = new MemoryStream(); 
    private bool isConnected = false;
    private int reqId = 1;
    private float _lastHeartbeatTime;

    public bool IsConnected => isConnected;

    const int HEADER_LENGTH = 4;
    const int MSG_TYPE_HANDSHAKE = 1;
    const int MSG_TYPE_HANDSHAKE_ACK = 2;
    const int MSG_TYPE_HEARTBEAT = 3;
    const int MSG_TYPE_DATA = 4;
    const int MSG_TYPE_KICK = 5;

    public Action<JoinResponse> OnJoinResponse;
    public Action<ChatMessage> OnChatMessage;
    public Action OnConnected;

    void Start()
    {
        Connect();
    }

    void OnGUI()
    {
        GUI.color = Color.white;
        string status = isConnected ? ""Connected (KCP)"" : ""Disconnected"";
        GUI.Label(new Rect(10, 10, 300, 20), $""Status: {status}"");

        if (isConnected)
        {
            if (GUI.Button(new Rect(10, 40, 120, 30), ""Join Room""))
            {
                SendRequest(""room.join"", new JoinRequest { Name = ""UnityKcpUser"" });
            }

            if (GUI.Button(new Rect(140, 40, 120, 30), ""Send Chat""))
            {
                SendRequest(""room.message"", new ChatMessage { SenderId = ""User1"", Content = ""Hello via KCP!"" });
            }
        }
        else
        {
            if (GUI.Button(new Rect(10, 40, 100, 30), ""Connect""))
            {
                Connect();
            }
        }
    }

    public void Connect()
    {
        if (session != null && isConnected) return;

        try
        {
            session = new UDPSession();
            // Pitaya KCP Settings
            session.Connect(host, port);
            session.mKCP.NoDelay(1, 10, 2, 1);
            session.mKCP.WndSize(128, 128);
            session.mKCP.SetStreamMode(true); // Enable Stream Mode!
            session.AckNoDelay = true; // Enable AckNoDelay

            Debug.Log($""Connecting to {host}:{port}..."");
            SendHandshake();
        }
        catch (Exception e)
        {
            Debug.LogError($""Connect Error: {e.Message}"");
        }
    }

    void Update()
    {
        if (session != null)
        {
            session.Update();
            // Heartbeat Logic (Only when connected)
            if (isConnected && Time.time - _lastHeartbeatTime > 5f)
            {
                _lastHeartbeatTime = Time.time;
                SendRaw(MSG_TYPE_HEARTBEAT, new byte[0]);
            }

            while (true)
            {
                int n = session.Recv(recvBuffer, 0, recvBuffer.Length);
                if (n <= 0) break;
                
                long oldPos = streamBuffer.Position;
                streamBuffer.Seek(0, SeekOrigin.End);
                streamBuffer.Write(recvBuffer, 0, n);
                streamBuffer.Position = oldPos;
            }
            ProcessStream();
        }
    }

    private void ProcessStream()
    {
        long length = streamBuffer.Length;
        while (length - streamBuffer.Position >= HEADER_LENGTH)
        {
            long packetStart = streamBuffer.Position;
            byte[] header = new byte[4];
            streamBuffer.Read(header, 0, 4);
            int type = header[0];
            int bodyLen = ((header[1] << 16) | (header[2] << 8) | header[3]);

            if (length - packetStart < HEADER_LENGTH + bodyLen)
            {
                streamBuffer.Position = packetStart;
                break;
            }

            byte[] body = new byte[bodyLen];
            streamBuffer.Read(body, 0, bodyLen);

            try { HandlePacket(type, body); }
            catch (Exception e) { Debug.LogError($""Packet Error: {e}""); }
        }

        if (streamBuffer.Position >= streamBuffer.Length)
        {
            streamBuffer.SetLength(0);
        }
        else if (streamBuffer.Position > 4096)
        {
            byte[] rest = new byte[streamBuffer.Length - streamBuffer.Position];
            streamBuffer.Read(rest, 0, rest.Length);
            streamBuffer = new MemoryStream();
            streamBuffer.Write(rest, 0, rest.Length);
            streamBuffer.Position = 0;
        }
    }

    private void HandlePacket(int type, byte[] body)
    {
        switch (type)
        {
            case MSG_TYPE_HANDSHAKE:
                Debug.Log(""Handshake received."");
                SendHandshakeAck();
                isConnected = true;
                OnConnected?.Invoke();
                break;
            case MSG_TYPE_HEARTBEAT:
                // Auto reply heartbeat if needed, or just ignore
                break;
            case MSG_TYPE_DATA:
                HandleDataPacket(body);
                break;
        }
    }

    private void HandleDataPacket(byte[] body)
    {
        // Pitaya Data Packet: [Flag(1) | ReqID(VarInt) | Body] (For Response)
        // Or [Flag(1) | Route(Str) | Body] (For Push)
        // This is complex, for now we try to parse directly if it fails we might need to skip header
        // Assuming simple response for now:
        
        // Skip header heuristic
        int offset = 0;
        if (body.Length > 0)
        {
             // Try to skip Flag and ID
             // This is a simplification. Real implementation needs a proper Pitaya Message Decoder.
             // For this demo, we just try to parse the body directly or with offset.
        }

        try {
            var joinRes = JoinResponse.Parser.ParseFrom(body);
            if (!string.IsNullOrEmpty(joinRes.RoomId) || joinRes.Code != 0) {
                 Debug.Log($""Join Response: {joinRes}"");
                 OnJoinResponse?.Invoke(joinRes);
                 return;
            }
        } catch {}

        try {
             var chatMsg = ChatMessage.Parser.ParseFrom(body);
             if (!string.IsNullOrEmpty(chatMsg.Content)) {
                 Debug.Log($""Chat Msg: {chatMsg.Content}"");
                 OnChatMessage?.Invoke(chatMsg);
                 return;
             }
        } catch {}
    }

    private void SendHandshake()
    {
        string json = ""{\""sys\"":{\""type\"":\""unity\"",\""version\"":\""1.0.0\""},\""user\"":{}}"";
        SendRaw(MSG_TYPE_HANDSHAKE, Encoding.UTF8.GetBytes(json));
    }

    private void SendHandshakeAck()
    {
        SendRaw(MSG_TYPE_HANDSHAKE_ACK, new byte[0]);
    }

    public void SendRequest(string route, IMessage message)
    {
        // Pitaya Message Format: [Flag(1) | ReqID(VarInt) | RouteLen(1) | Route(Str) | Body]
        using (var ms = new MemoryStream())
        {
            // 1. Flag (0x00 = Request, 0x02 = Notify)
            ms.WriteByte(0x00); // Request

            // 2. ReqID
            WriteVarInt(ms, reqId++);

            // 3. Route
            byte[] routeBytes = Encoding.UTF8.GetBytes(route);
            ms.WriteByte((byte)routeBytes.Length);
            ms.Write(routeBytes, 0, routeBytes.Length);

            // 4. Body
            message.WriteTo(ms);

            SendRaw(MSG_TYPE_DATA, ms.ToArray());
        }
    }

    public void SendNotify(string route, IMessage message)
    {
        // Pitaya Notify Format: [Flag(1) | RouteLen(1) | Route(Str) | Body]
        using (var ms = new MemoryStream())
        {
            // 1. Flag (0x02 = Notify)
            ms.WriteByte(0x02); 

            // 2. Route
            byte[] routeBytes = Encoding.UTF8.GetBytes(route);
            ms.WriteByte((byte)routeBytes.Length);
            ms.Write(routeBytes, 0, routeBytes.Length);

            // 3. Body
            message.WriteTo(ms);

            SendRaw(MSG_TYPE_DATA, ms.ToArray());
        }
    }

    private void WriteVarInt(Stream stream, int value)
    {
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) b |= 0x80;
            stream.WriteByte(b);
        } while (value != 0);
    }

    private void SendRaw(int type, byte[] body)
    {
        if (session == null) return;
        int len = body.Length;
        byte[] header = new byte[4];
        header[0] = (byte)type;
        header[1] = (byte)((len >> 16) & 0xFF);
        header[2] = (byte)((len >> 8) & 0xFF);
        header[3] = (byte)(len & 0xFF);
        byte[] packet = new byte[4 + len];
        Array.Copy(header, 0, packet, 0, 4);
        Array.Copy(body, 0, packet, 4, len);
        session.Send(packet, 0, packet.Length);
    }

    void OnDestroy()
    {
        if (session != null) session.Close();
    }
}
";
        string dir = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(path, content);
        Debug.Log("Created NanoKcpClient.cs");
        AssetDatabase.Refresh();
    }

    private static GameObject CreateButton(Transform parent, string name, Vector2 pos, string text, Color color)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent);
        
        Image img = btnObj.AddComponent<Image>();
        img.color = color;
        
        Button btn = btnObj.AddComponent<Button>();
        
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(180, 80);
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform);
        Text txt = textObj.AddComponent<Text>();
        txt.text = text;
        txt.color = Color.black;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 20;
        
        // Try to find any loaded font (safest approach)
        if (txt.font == null)
        {
            var allFonts = Resources.FindObjectsOfTypeAll<Font>();
            if (allFonts != null && allFonts.Length > 0)
            {
                // Prefer Arial or LegacyRuntime if available
                foreach (var f in allFonts)
                {
                    if (f.name == "Arial" || f.name == "LegacyRuntime")
                    {
                        txt.font = f;
                        break;
                    }
                }
                
                // Fallback to first available
                if (txt.font == null) txt.font = allFonts[0];
            }
        }
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero; // Reset position to center of parent
        
        return btnObj;
    }

    private static void ResizeWorld()
    {
        GameObject worldRoot = GameObject.Find("WorldRoot");
        if (worldRoot == null)
        {
            worldRoot = new GameObject("WorldRoot");
            
            // Get all root objects
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                // Ignore our system objects
                if (r.name == "WorldRoot") continue;
                if (r.name == "NetworkManager") continue;
                if (r.name == "NetworkUI") continue;
                if (r.name == "EventSystem") continue;
                if (r.name == "Main Camera") continue;
                if (r.name == "Directional Light") continue; 
                if (r.name.Contains("Canvas")) continue;
                if (r.name.Contains("Player")) continue;
                
                // Parent to WorldRoot
                r.transform.SetParent(worldRoot.transform);
            }
            
            // Scale up
            worldRoot.transform.localScale = Vector3.one * 20f;
            Debug.Log("World Resized x20");
        }
        else
        {
            // Already resized?
            if (worldRoot.transform.localScale.x < 15f)
                worldRoot.transform.localScale = Vector3.one * 20f;
        }
    }

    private static void OptimizeGraphics()
    {
        Debug.Log("Optimizing Graphics for PC...");
        
        // 1. Try to access current URP Asset
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null)
        {
            pipeline.renderScale = 1.0f;
            pipeline.msaaSampleCount = 4; // 4x MSAA
            pipeline.shadowDistance = 150f;
            Debug.Log("URP Settings Updated: RenderScale=1.0, MSAA=4x");
        }
        else
        {
            // Try to find ANY URP Asset in project and set it? No, risky.
            Debug.LogWarning("Current Render Pipeline is not URP or not set! Cannot optimize URP settings.");
        }

        // 2. Set Quality Settings
        QualitySettings.vSyncCount = 0; // Disable VSync for smoother testing
        Application.targetFrameRate = 60; // Correct API for target frame rate
        QualitySettings.globalTextureMipmapLimit = 0; // Full Resolution
        
        Debug.Log("Quality Settings Updated.");
    }

    private static void SetFBXLoopTime(string path, bool loop)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer != null)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            // If clips array is empty, it might be using default clip which is read-only via clipAnimations?
            // Actually defaultClipAnimations returns the imported clips.
            // If we want to override, we set clipAnimations.
            
            if (clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            bool changed = false;
            foreach (var clip in clips)
            {
                if (clip.loopTime != loop)
                {
                    clip.loopTime = loop;
                    changed = true;
                    
                    // Also fix root motion settings if looping
                    if (loop)
                    {
                        clip.lockRootRotation = true;
                        clip.lockRootHeightY = true;
                        clip.lockRootPositionXZ = true;
                        clip.keepOriginalOrientation = true;
                        clip.keepOriginalPositionY = true;
                        clip.keepOriginalPositionXZ = true;
                    }
                }
            }
            
            if (changed)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                Debug.Log($"Updated Loop Time for {path} to {loop}");
            }
        }
    }
}
