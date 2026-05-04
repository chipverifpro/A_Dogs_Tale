#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DogGame.EditorTools
{
    /// <summary>
    /// Builds a clean DogGame prefab from a selected imported model asset or scene instance.
    ///
    /// Suggested location:
    /// Assets/A_Dogs_Tale/Scripts/Editor/DogGamePrefabBuilder.cs
    ///
    /// Menu:
    /// Tools/DogGame/Prefab Builder/Build Prefab From Selected Model
    ///
    /// Generated hierarchy:
    ///
    /// PF_ModelName
    /// ├── VisualOffset
    /// │   └── ImportedModel
    /// ├── Colliders
    /// ├── InteractionPoints
    /// ├── RuntimePoints
    /// └── HiddenGuides
    ///
    /// This script intentionally avoids hard dependencies on your runtime DogGame classes.
    /// It creates the prefab structure and converted guide objects. You can then run your
    /// existing setup tools, or extend ApplyPresetSpecificComponents() to add your modules.
    /// </summary>
    public sealed class DogGamePrefabBuilderWindow : EditorWindow
    {
        private enum SetupPreset
        {
            StaticProp,
            InteractableProp,
            PickupObject,
            ScentSource,
            AnimalAgent,
            Gate,
            WaterSource,
            ProjectileLauncher
        }

        private enum GuideHandling
        {
            HideOriginalGuides,
            MoveOriginalGuidesToHiddenGuides,
            DeleteOriginalGuides
        }

        private const string MenuRoot = "Tools/DogGame/Prefab Builder/";

        private UnityEngine.Object selectedModelAssetOrInstance;

        private string prefabRootName = "";
        private string prefabFolder = "Assets/A_Dogs_Tale/Resources/Prefabs/Generated";

        private SetupPreset setupPreset = SetupPreset.InteractableProp;
        private GuideHandling guideHandling = GuideHandling.HideOriginalGuides;

        private Vector3 visualOffsetPosition = Vector3.zero;
        private Vector3 visualOffsetEulerRotation = Vector3.zero;
        private Vector3 visualOffsetScale = Vector3.one;

        private bool centerVisualOnRootXZ = true;
        private bool placeVisualBottomOnGround = true;

        private bool convertGuideColliders = true;
        private bool convertGuidePoints = true;
        private bool createApproxBodyColliderWhenMissing = false;

        private bool openPrefabAfterBuild = true;
        private bool selectPrefabAfterBuild = true;

        [MenuItem(MenuRoot + "Build Prefab From Selected Model")]
        private static void ShowWindow()
        {
            DogGamePrefabBuilderWindow window = GetWindow<DogGamePrefabBuilderWindow>("DogGame Prefab Builder");
            window.minSize = new Vector2(520, 560);
            window.selectedModelAssetOrInstance = Selection.activeObject;
            window.AutoFillNameFromSelection();
            window.Show();
        }

        [MenuItem(MenuRoot + "Quick Build Interactable Prop")]
        private static void QuickBuildInteractableProp()
        {
            BuildFromSelectionWithPreset(SetupPreset.InteractableProp);
        }

        [MenuItem(MenuRoot + "Quick Build Static Prop")]
        private static void QuickBuildStaticProp()
        {
            BuildFromSelectionWithPreset(SetupPreset.StaticProp);
        }

        [MenuItem(MenuRoot + "Quick Build Pickup Object")]
        private static void QuickBuildPickupObject()
        {
            BuildFromSelectionWithPreset(SetupPreset.PickupObject);
        }

        private static void BuildFromSelectionWithPreset(SetupPreset preset)
        {
            DogGamePrefabBuilderWindow window = CreateInstance<DogGamePrefabBuilderWindow>();
            window.selectedModelAssetOrInstance = Selection.activeObject;
            window.setupPreset = preset;
            window.AutoFillNameFromSelection();
            window.BuildPrefab();
            window.Close();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("DogGame Prefab Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select an imported OBJ/model asset or a scene instance, then build a prefab with a stable root and VisualOffset child.",
                MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                selectedModelAssetOrInstance = EditorGUILayout.ObjectField(
                    "Model Asset or Instance",
                    selectedModelAssetOrInstance,
                    typeof(UnityEngine.Object),
                    true);
                if (EditorGUI.EndChangeCheck())
                {
                    AutoFillNameFromSelection();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    prefabRootName = EditorGUILayout.TextField("Prefab Root Name", prefabRootName);
                    if (GUILayout.Button("Auto", GUILayout.Width(64)))
                    {
                        AutoFillNameFromSelection();
                    }
                }

                prefabFolder = EditorGUILayout.TextField("Prefab Folder", prefabFolder);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
                setupPreset = (SetupPreset)EditorGUILayout.EnumPopup("Setup Preset", setupPreset);
                guideHandling = (GuideHandling)EditorGUILayout.EnumPopup("Guide Handling", guideHandling);

                convertGuideColliders = EditorGUILayout.ToggleLeft("Convert Guide_Collider_* meshes to BoxColliders", convertGuideColliders);
                convertGuidePoints = EditorGUILayout.ToggleLeft("Convert Guide_* point markers to empty transforms", convertGuidePoints);
                createApproxBodyColliderWhenMissing = EditorGUILayout.ToggleLeft("Create approximate body collider if no guide collider exists", createApproxBodyColliderWhenMissing);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("VisualOffset", EditorStyles.boldLabel);
                visualOffsetPosition = EditorGUILayout.Vector3Field("Position", visualOffsetPosition);
                visualOffsetEulerRotation = EditorGUILayout.Vector3Field("Rotation", visualOffsetEulerRotation);
                visualOffsetScale = EditorGUILayout.Vector3Field("Scale", visualOffsetScale);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reset"))
                    {
                        visualOffsetPosition = Vector3.zero;
                        visualOffsetEulerRotation = Vector3.zero;
                        visualOffsetScale = Vector3.one;
                    }

                    if (GUILayout.Button("Rotate X +90")) visualOffsetEulerRotation += new Vector3(90f, 0f, 0f);
                    if (GUILayout.Button("Rotate Y +90")) visualOffsetEulerRotation += new Vector3(0f, 90f, 0f);
                    if (GUILayout.Button("Scale 0.1")) visualOffsetScale = Vector3.one * 0.1f;
                }

                centerVisualOnRootXZ = EditorGUILayout.ToggleLeft("Center visual bounds on root X/Z", centerVisualOnRootXZ);
                placeVisualBottomOnGround = EditorGUILayout.ToggleLeft("Place visual bounds bottom on Y=0", placeVisualBottomOnGround);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("After Build", EditorStyles.boldLabel);
                openPrefabAfterBuild = EditorGUILayout.ToggleLeft("Open prefab after build", openPrefabAfterBuild);
                selectPrefabAfterBuild = EditorGUILayout.ToggleLeft("Select prefab after build", selectPrefabAfterBuild);
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Current Selection", GUILayout.Height(32)))
                {
                    selectedModelAssetOrInstance = Selection.activeObject;
                    AutoFillNameFromSelection();
                }

                using (new EditorGUI.DisabledScope(selectedModelAssetOrInstance == null))
                {
                    if (GUILayout.Button("Build Prefab", GUILayout.Height(32)))
                    {
                        BuildPrefab();
                    }
                }
            }
        }

        private void AutoFillNameFromSelection()
        {
            if (selectedModelAssetOrInstance == null)
            {
                return;
            }

            string rawName = selectedModelAssetOrInstance.name;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return;
            }

            prefabRootName = "PF_" + SanitizeName(rawName);
        }

        private void BuildPrefab()
        {
            if (selectedModelAssetOrInstance == null)
            {
                EditorUtility.DisplayDialog("DogGame Prefab Builder", "Select a model asset or scene instance first.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(prefabRootName))
            {
                AutoFillNameFromSelection();
            }

            if (string.IsNullOrWhiteSpace(prefabRootName))
            {
                EditorUtility.DisplayDialog("DogGame Prefab Builder", "Prefab root name is empty.", "OK");
                return;
            }

            GameObject temporarySceneInstance = null;

            try
            {
                GameObject sourceInstance = ResolveSourceInstance(selectedModelAssetOrInstance, out temporarySceneInstance);
                if (sourceInstance == null)
                {
                    EditorUtility.DisplayDialog(
                        "DogGame Prefab Builder",
                        "Could not instantiate or resolve the selected object as a GameObject/model.",
                        "OK");
                    return;
                }

                Directory.CreateDirectory(prefabFolder);

                GameObject prefabRoot = new GameObject(prefabRootName);
                Undo.RegisterCreatedObjectUndo(prefabRoot, "Create DogGame prefab root");

                GameObject visualOffsetObject = new GameObject("VisualOffset");
                visualOffsetObject.transform.SetParent(prefabRoot.transform, false);
                visualOffsetObject.transform.localPosition = visualOffsetPosition;
                visualOffsetObject.transform.localRotation = Quaternion.Euler(visualOffsetEulerRotation);
                visualOffsetObject.transform.localScale = SanitizeScale(visualOffsetScale);

                GameObject collidersRoot = new GameObject("Colliders");
                collidersRoot.transform.SetParent(prefabRoot.transform, false);

                GameObject interactionPointsRoot = new GameObject("InteractionPoints");
                interactionPointsRoot.transform.SetParent(prefabRoot.transform, false);

                GameObject runtimePointsRoot = new GameObject("RuntimePoints");
                runtimePointsRoot.transform.SetParent(prefabRoot.transform, false);

                GameObject hiddenGuidesRoot = new GameObject("HiddenGuides");
                hiddenGuidesRoot.transform.SetParent(prefabRoot.transform, false);
                hiddenGuidesRoot.SetActive(false);

                GameObject importedModel = Instantiate(sourceInstance);
                importedModel.name = selectedModelAssetOrInstance.name;
                importedModel.transform.SetParent(visualOffsetObject.transform, false);
                importedModel.transform.localPosition = Vector3.zero;
                importedModel.transform.localRotation = Quaternion.identity;
                importedModel.transform.localScale = Vector3.one;

                ApplyBoundsAlignment(importedModel);

                List<Transform> guideTransforms = FindGuideTransforms(importedModel.transform);

                int convertedColliderCount = convertGuideColliders
                    ? ConvertGuideColliders(guideTransforms, collidersRoot.transform)
                    : 0;

                int convertedPointCount = convertGuidePoints
                    ? ConvertGuidePoints(guideTransforms, interactionPointsRoot.transform, runtimePointsRoot.transform)
                    : 0;

                if (createApproxBodyColliderWhenMissing && convertedColliderCount == 0)
                {
                    CreateApproximateBodyCollider(importedModel, collidersRoot.transform);
                    convertedColliderCount = 1;
                }

                HandleOriginalGuideMeshes(guideTransforms, hiddenGuidesRoot.transform);

                ApplyPresetSpecificStructure(prefabRoot.transform, setupPreset);
                ApplyPresetSpecificComponents(prefabRoot, setupPreset);

                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(prefabFolder, prefabRoot.name + ".prefab").Replace("\\", "/"));

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                DestroyImmediate(prefabRoot);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (selectPrefabAfterBuild && savedPrefab != null)
                {
                    Selection.activeObject = savedPrefab;
                    EditorGUIUtility.PingObject(savedPrefab);
                }

                if (openPrefabAfterBuild && savedPrefab != null)
                {
                    AssetDatabase.OpenAsset(savedPrefab);
                }

                Debug.Log(
                    $"[DogGamePrefabBuilder] Created prefab: {prefabPath}\n" +
                    $"Converted guide colliders: {convertedColliderCount}\n" +
                    $"Converted guide points: {convertedPointCount}",
                    savedPrefab);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("DogGame Prefab Builder", exception.Message, "OK");
            }
            finally
            {
                if (temporarySceneInstance != null)
                {
                    DestroyImmediate(temporarySceneInstance);
                }
            }
        }

        private static GameObject ResolveSourceInstance(UnityEngine.Object selectedObject, out GameObject temporarySceneInstance)
        {
            temporarySceneInstance = null;

            if (selectedObject is GameObject selectedGameObject)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(selectedGameObject) || AssetDatabase.Contains(selectedGameObject))
                {
                    temporarySceneInstance = (GameObject)PrefabUtility.InstantiatePrefab(selectedGameObject);
                    if (temporarySceneInstance == null)
                    {
                        temporarySceneInstance = Instantiate(selectedGameObject);
                    }

                    temporarySceneInstance.name = selectedGameObject.name + "_TEMP_PREFAB_BUILDER_SOURCE";
                    return temporarySceneInstance;
                }

                return selectedGameObject;
            }

            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            GameObject loadedGameObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (loadedGameObject == null)
            {
                return null;
            }

            temporarySceneInstance = (GameObject)PrefabUtility.InstantiatePrefab(loadedGameObject);
            if (temporarySceneInstance == null)
            {
                temporarySceneInstance = Instantiate(loadedGameObject);
            }

            temporarySceneInstance.name = loadedGameObject.name + "_TEMP_PREFAB_BUILDER_SOURCE";
            return temporarySceneInstance;
        }

        private void ApplyBoundsAlignment(GameObject importedModel)
        {
            Bounds? nullableBounds = TryCalculateRendererBounds(importedModel);
            if (!nullableBounds.HasValue)
            {
                nullableBounds = TryCalculateColliderBounds(importedModel);
            }

            if (!nullableBounds.HasValue)
            {
                return;
            }

            Bounds bounds = nullableBounds.Value;
            Vector3 correctionWorld = Vector3.zero;

            if (centerVisualOnRootXZ)
            {
                correctionWorld.x = -bounds.center.x;
                correctionWorld.z = -bounds.center.z;
            }

            if (placeVisualBottomOnGround)
            {
                correctionWorld.y = -bounds.min.y;
            }

            importedModel.transform.position += correctionWorld;
        }

        private static Bounds? TryCalculateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = new Bounds();

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? combinedBounds : null;
        }

        private static Bounds? TryCalculateColliderBounds(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            bool hasBounds = false;
            Bounds combinedBounds = new Bounds();

            foreach (Collider collider in colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds ? combinedBounds : null;
        }

        private static List<Transform> FindGuideTransforms(Transform root)
        {
            List<Transform> guides = new List<Transform>();

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root)
                {
                    continue;
                }

                if (child.name.StartsWith("Guide_", StringComparison.OrdinalIgnoreCase))
                {
                    guides.Add(child);
                }
            }

            return guides;
        }

        private static int ConvertGuideColliders(List<Transform> guideTransforms, Transform collidersRoot)
        {
            int count = 0;

            foreach (Transform guideTransform in guideTransforms)
            {
                if (!guideTransform.name.StartsWith("Guide_Collider_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Bounds? nullableBounds = TryCalculateRendererBounds(guideTransform.gameObject);
                if (!nullableBounds.HasValue)
                {
                    nullableBounds = TryCalculateColliderBounds(guideTransform.gameObject);
                }

                if (!nullableBounds.HasValue)
                {
                    continue;
                }

                Bounds worldBounds = nullableBounds.Value;
                GameObject colliderObject = new GameObject(CleanGuideName(guideTransform.name));
                colliderObject.transform.SetParent(collidersRoot, false);

                BoxCollider boxCollider = colliderObject.AddComponent<BoxCollider>();
                boxCollider.center = collidersRoot.InverseTransformPoint(worldBounds.center);
                boxCollider.size = WorldSizeToLocalSize(collidersRoot, worldBounds.size);

                count++;
            }

            return count;
        }

        private static int ConvertGuidePoints(
            List<Transform> guideTransforms,
            Transform interactionPointsRoot,
            Transform runtimePointsRoot)
        {
            int count = 0;

            foreach (Transform guideTransform in guideTransforms)
            {
                if (!guideTransform.name.StartsWith("Guide_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (guideTransform.name.StartsWith("Guide_Collider_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Transform parent = ClassifyGuidePointParent(guideTransform.name, interactionPointsRoot, runtimePointsRoot);

                GameObject pointObject = new GameObject(CleanGuideName(guideTransform.name));
                pointObject.transform.SetParent(parent, false);
                pointObject.transform.position = guideTransform.position;
                pointObject.transform.rotation = guideTransform.rotation;
                pointObject.transform.localScale = Vector3.one;

                count++;
            }

            return count;
        }

        private static Transform ClassifyGuidePointParent(
            string guideName,
            Transform interactionPointsRoot,
            Transform runtimePointsRoot)
        {
            string lowerName = guideName.ToLowerInvariant();

            if (lowerName.Contains("interaction") ||
                lowerName.Contains("sniff") ||
                lowerName.Contains("pickup") ||
                lowerName.Contains("chew") ||
                lowerName.Contains("drink") ||
                lowerName.Contains("turn") ||
                lowerName.Contains("open") ||
                lowerName.Contains("close") ||
                lowerName.Contains("load") ||
                lowerName.Contains("press"))
            {
                return interactionPointsRoot;
            }

            return runtimePointsRoot;
        }

        private void HandleOriginalGuideMeshes(List<Transform> guideTransforms, Transform hiddenGuidesRoot)
        {
            List<Transform> copy = new List<Transform>(guideTransforms);

            foreach (Transform guideTransform in copy)
            {
                if (guideTransform == null)
                {
                    continue;
                }

                switch (guideHandling)
                {
                    case GuideHandling.HideOriginalGuides:
                        guideTransform.gameObject.SetActive(false);
                        break;

                    case GuideHandling.MoveOriginalGuidesToHiddenGuides:
                        guideTransform.SetParent(hiddenGuidesRoot, true);
                        guideTransform.gameObject.SetActive(false);
                        break;

                    case GuideHandling.DeleteOriginalGuides:
                        DestroyImmediate(guideTransform.gameObject);
                        break;

                    default:
                        guideTransform.gameObject.SetActive(false);
                        break;
                }
            }
        }

        private static void CreateApproximateBodyCollider(GameObject importedModel, Transform collidersRoot)
        {
            Bounds? nullableBounds = TryCalculateRendererBounds(importedModel);
            if (!nullableBounds.HasValue)
            {
                return;
            }

            Bounds worldBounds = nullableBounds.Value;

            GameObject colliderObject = new GameObject("ApproxBodyCollider");
            colliderObject.transform.SetParent(collidersRoot, false);

            BoxCollider boxCollider = colliderObject.AddComponent<BoxCollider>();
            boxCollider.center = collidersRoot.InverseTransformPoint(worldBounds.center);
            boxCollider.size = WorldSizeToLocalSize(collidersRoot, worldBounds.size);
        }

        private static Vector3 WorldSizeToLocalSize(Transform localSpace, Vector3 worldSize)
        {
            Vector3 lossyScale = localSpace.lossyScale;
            return new Vector3(
                SafeDivide(worldSize.x, lossyScale.x),
                SafeDivide(worldSize.y, lossyScale.y),
                SafeDivide(worldSize.z, lossyScale.z));
        }

        private static float SafeDivide(float numerator, float denominator)
        {
            if (Mathf.Approximately(denominator, 0f))
            {
                return numerator;
            }

            return numerator / Mathf.Abs(denominator);
        }

        private static void ApplyPresetSpecificStructure(Transform prefabRoot, SetupPreset preset)
        {
            switch (preset)
            {
                case SetupPreset.PickupObject:
                    EnsureChild(prefabRoot, "HoldPoints");
                    break;

                case SetupPreset.Gate:
                    EnsureChild(prefabRoot, "StateVisuals");
                    break;

                case SetupPreset.WaterSource:
                    EnsureChild(prefabRoot, "WaterPoints");
                    break;

                case SetupPreset.ProjectileLauncher:
                    EnsureChild(prefabRoot, "ProjectilePoints");
                    break;

                case SetupPreset.AnimalAgent:
                    EnsureChild(prefabRoot, "AgentPoints");
                    break;

                case SetupPreset.StaticProp:
                case SetupPreset.InteractableProp:
                case SetupPreset.ScentSource:
                default:
                    break;
            }
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform existingChild = parent.Find(childName);
            if (existingChild != null)
            {
                return existingChild;
            }

            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void ApplyPresetSpecificComponents(GameObject prefabRoot, SetupPreset preset)
        {
            // Safe reflection-based component adding.
            // These do nothing if the class names do not exist in your project.
            switch (preset)
            {
                case SetupPreset.StaticProp:
                    TryAddComponentByTypeName(prefabRoot, "DogGame.WorldObjects.WorldObject");
                    break;

                case SetupPreset.InteractableProp:
                    TryAddComponentByTypeName(prefabRoot, "DogGame.WorldObjects.WorldObject");
                    TryAddComponentByTypeName(prefabRoot, "DogGame.Modules.InteractionModule");
                    break;

                case SetupPreset.PickupObject:
                    TryAddComponentByTypeName(prefabRoot, "DogGame.WorldObjects.WorldObject");
                    TryAddComponentByTypeName(prefabRoot, "DogGame.Modules.InteractionModule");
                    TryAddComponentByTypeName(prefabRoot, "DogGame.Modules.PickupModule");
                    break;

                case SetupPreset.ScentSource:
                    TryAddComponentByTypeName(prefabRoot, "DogGame.WorldObjects.WorldObject");
                    TryAddComponentByTypeName(prefabRoot, "DogGame.Modules.InteractionModule");
                    TryAddComponentByTypeName(prefabRoot, "DogGame.Modules.ScentSourceModule");
                    break;

                case SetupPreset.AnimalAgent:
                    TryAddComponentByTypeName(prefabRoot, "DogGame.WorldObjects.WorldObject");
                    break;

                case SetupPreset.Gate:
                case SetupPreset.WaterSource:
                case SetupPreset.ProjectileLauncher:
                    TryAddComponentByTypeName(prefabRoot, "DogGame.WorldObjects.WorldObject");
                    TryAddComponentByTypeName(prefabRoot, "DogGame.Modules.InteractionModule");
                    break;

                default:
                    break;
            }
        }

        private static void TryAddComponentByTypeName(GameObject target, string typeName)
        {
            Type componentType = FindTypeByName(typeName);
            if (componentType == null)
            {
                return;
            }

            if (!typeof(Component).IsAssignableFrom(componentType))
            {
                return;
            }

            if (target.GetComponent(componentType) != null)
            {
                return;
            }

            target.AddComponent(componentType);
        }

        private static Type FindTypeByName(string typeName)
        {
            Type directType = Type.GetType(typeName);
            if (directType != null)
            {
                return directType;
            }

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type foundType = assembly.GetType(typeName);
                if (foundType != null)
                {
                    return foundType;
                }
            }

            return null;
        }

        private static string CleanGuideName(string originalName)
        {
            string clean = originalName;

            if (clean.StartsWith("Guide_", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring("Guide_".Length);
            }

            clean = clean.Replace("_delete_or_hide", "", StringComparison.OrdinalIgnoreCase);
            clean = clean.Replace("delete_or_hide", "", StringComparison.OrdinalIgnoreCase);

            return SanitizeName(clean);
        }

        private static string SanitizeName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return "Unnamed";
            }

            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
            string sanitized = rawName.Trim();

            foreach (char invalidChar in invalidFileNameChars)
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            sanitized = sanitized.Replace(' ', '_');
            sanitized = sanitized.Replace('-', '_');

            while (sanitized.Contains("__", StringComparison.Ordinal))
            {
                sanitized = sanitized.Replace("__", "_");
            }

            return sanitized;
        }

        private static Vector3 SanitizeScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
                Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
                Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
        }
    }
}
#endif
