using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DeathVisionSetupValidator
{
    private const string DeathVisionCanvasName = "DeathVisionCanvas";
    private const string DeathVisionVignetteName = "DeathVisionVignette";

    [MenuItem("Tools/Incantation/Validate Death Vision Setup")]
    public static void ValidateDeathVisionSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        DeathVisionSetup setup = GatherSetup();
        List<string> failureReasons = new List<string>();
        StringBuilder report = new StringBuilder();

        AppendHeader(report, activeScene);
        AppendHierarchyReport(report, setup);
        AppendComponentReport(report, setup);
        AppendTimingReport(report, setup);
        CollectFailures(setup, failureReasons);
        AppendSummary(report, failureReasons);

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Incantation/Repair Death Vision Setup")]
    public static void RepairDeathVisionSetup()
    {
        Canvas canvas = FindOrCreateDeathVisionCanvas();
        RectTransform vignetteRoot = FindOrCreateDeathVisionVignette(canvas.transform);
        CanvasGroup canvasGroup = EnsureCanvasGroup(vignetteRoot.gameObject);
        Image image = EnsureBlackImage(vignetteRoot.gameObject);
        DeathVisionVignetteController controller = EnsureDeathVisionController(vignetteRoot.gameObject);
        RitualFailureAbsorptionBridge bridge = FindSceneObject<RitualFailureAbsorptionBridge>();

        StretchToFullScreen(vignetteRoot);

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 1000);
        ConfigureCanvasScaler(canvas.gameObject);

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        image.color = Color.black;
        image.raycastTarget = false;

        Undo.RecordObject(controller, "Repair Death Vision Vignette Controller");
        SerializedObject serializedController = new SerializedObject(controller);
        SetObjectReference(serializedController, "vignetteCanvasGroup", canvasGroup);
        SetObjectReference(serializedController, "vignetteRoot", vignetteRoot);
        serializedController.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        if (bridge != null)
        {
            Undo.RecordObject(bridge, "Assign Death Vision Vignette Bridge Reference");
            SerializedObject serializedBridge = new SerializedObject(bridge);
            SetObjectReference(serializedBridge, "deathVisionVignetteController", controller);
            serializedBridge.ApplyModifiedProperties();
            EditorUtility.SetDirty(bridge);
        }

        EditorUtility.SetDirty(canvas);
        EditorUtility.SetDirty(canvasGroup);
        EditorUtility.SetDirty(image);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Death Vision setup repair complete.\n"
            + $"DeathVisionCanvas: {GetHierarchyPath(canvas.transform)}\n"
            + $"DeathVisionVignette: {GetHierarchyPath(vignetteRoot)}\n"
            + $"CanvasGroup: {FormatBool(canvasGroup != null)}\n"
            + $"Black Image: {FormatBool(image != null)}\n"
            + $"DeathVisionVignetteController: {GetHierarchyPath(controller.transform)}\n"
            + $"RitualFailureAbsorptionBridge assigned: {FormatBool(bridge != null)}");

        ValidateDeathVisionSetup();
    }

    private static DeathVisionSetup GatherSetup()
    {
        DeathVisionSetup setup = new DeathVisionSetup();
        setup.DeathVisionCanvasObject = FindSceneGameObjectByName(DeathVisionCanvasName);
        setup.DeathVisionCanvas = setup.DeathVisionCanvasObject != null ? setup.DeathVisionCanvasObject.GetComponent<Canvas>() : null;
        setup.CanvasScaler = setup.DeathVisionCanvasObject != null ? setup.DeathVisionCanvasObject.GetComponent<CanvasScaler>() : null;
        setup.GraphicRaycaster = setup.DeathVisionCanvasObject != null ? setup.DeathVisionCanvasObject.GetComponent<GraphicRaycaster>() : null;
        setup.DeathVisionVignette = setup.DeathVisionCanvasObject != null ? FindDirectChildByName(setup.DeathVisionCanvasObject.transform, DeathVisionVignetteName) : FindSceneGameObjectByName(DeathVisionVignetteName)?.transform;
        setup.DeathVisionCanvasGroup = setup.DeathVisionVignette != null ? setup.DeathVisionVignette.GetComponent<CanvasGroup>() : null;
        setup.DeathVisionImage = setup.DeathVisionVignette != null ? setup.DeathVisionVignette.GetComponent<Image>() : null;
        setup.DeathVisionVignetteController = setup.DeathVisionVignette != null ? setup.DeathVisionVignette.GetComponent<DeathVisionVignetteController>() : FindSceneObject<DeathVisionVignetteController>();
        setup.RitualFailureAbsorptionBridge = FindSceneObject<RitualFailureAbsorptionBridge>();

        if (setup.DeathVisionVignetteController != null)
        {
            SerializedObject serializedController = new SerializedObject(setup.DeathVisionVignetteController);
            setup.AssignedCanvasGroup = GetObjectReference<CanvasGroup>(serializedController, "vignetteCanvasGroup");
            setup.AssignedRoot = GetObjectReference<RectTransform>(serializedController, "vignetteRoot");
            setup.InitialDelayAfterGrab = GetFloat(serializedController, "initialDelayAfterGrab");
            setup.AbsorptionDelayAfterGrab = GetFloat(serializedController, "absorptionDelayAfterGrab");
            setup.FullBlackDelayAfterGrab = GetFloat(serializedController, "fullBlackDelayAfterGrab");
            setup.TotalDuration = GetFloat(serializedController, "totalDuration");
        }

        if (setup.RitualFailureAbsorptionBridge != null)
        {
            SerializedObject serializedBridge = new SerializedObject(setup.RitualFailureAbsorptionBridge);
            setup.BridgeDeathVisionVignetteController = GetObjectReference<DeathVisionVignetteController>(serializedBridge, "deathVisionVignetteController");
        }

        return setup;
    }

    private static void AppendHeader(StringBuilder report, Scene activeScene)
    {
        report.AppendLine("Death Vision Setup Validation Report");
        report.AppendLine("====================================");
        report.AppendLine($"Open scene path: {activeScene.path}");
        report.AppendLine();
    }

    private static void AppendHierarchyReport(StringBuilder report, DeathVisionSetup setup)
    {
        report.AppendLine("Required Hierarchy");
        report.AppendLine("------------------");
        report.AppendLine($"DeathVisionCanvas exists: {FormatBool(setup.DeathVisionCanvasObject != null)}{FormatPath(setup.DeathVisionCanvasObject)}");
        report.AppendLine($"DeathVisionVignette exists: {FormatBool(setup.DeathVisionVignette != null)}{FormatPath(setup.DeathVisionVignette)}");
        report.AppendLine();
    }

    private static void AppendComponentReport(StringBuilder report, DeathVisionSetup setup)
    {
        report.AppendLine("Components And References");
        report.AppendLine("-------------------------");
        report.AppendLine($"Canvas exists: {FormatBool(setup.DeathVisionCanvas != null)}{FormatObjectName(setup.DeathVisionCanvas)}");
        report.AppendLine($"Canvas renderMode is ScreenSpaceOverlay: {FormatBool(setup.DeathVisionCanvas != null && setup.DeathVisionCanvas.renderMode == RenderMode.ScreenSpaceOverlay)}");
        report.AppendLine($"CanvasScaler exists: {FormatBool(setup.CanvasScaler != null)}{FormatObjectName(setup.CanvasScaler)}");
        report.AppendLine($"GraphicRaycaster exists: {FormatBool(setup.GraphicRaycaster != null)}{FormatObjectName(setup.GraphicRaycaster)}");
        report.AppendLine($"CanvasGroup exists: {FormatBool(setup.DeathVisionCanvasGroup != null)}{FormatObjectName(setup.DeathVisionCanvasGroup)}");
        report.AppendLine($"Full-screen black Image exists: {FormatBool(IsValidBlackImage(setup.DeathVisionImage))}{FormatObjectName(setup.DeathVisionImage)}");
        report.AppendLine($"DeathVisionVignette is full-screen RectTransform: {FormatBool(IsFullScreenRect(setup.DeathVisionVignette as RectTransform))}");
        report.AppendLine($"DeathVisionVignetteController exists: {FormatBool(setup.DeathVisionVignetteController != null)}{FormatPath(setup.DeathVisionVignetteController != null ? setup.DeathVisionVignetteController.transform : null)}");
        report.AppendLine($"DeathVisionVignetteController.vignetteCanvasGroup assigned: {FormatBool(setup.AssignedCanvasGroup != null)}{FormatObjectName(setup.AssignedCanvasGroup)}");
        report.AppendLine($"DeathVisionVignetteController.vignetteRoot assigned: {FormatBool(setup.AssignedRoot != null)}{FormatPath(setup.AssignedRoot)}");
        report.AppendLine($"RitualFailureAbsorptionBridge exists: {FormatBool(setup.RitualFailureAbsorptionBridge != null)}{FormatPath(setup.RitualFailureAbsorptionBridge != null ? setup.RitualFailureAbsorptionBridge.transform : null)}");
        report.AppendLine($"bridge.deathVisionVignetteController assigned: {FormatBool(setup.BridgeDeathVisionVignetteController != null)}{FormatObjectName(setup.BridgeDeathVisionVignetteController)}");
        report.AppendLine();
    }

    private static void AppendTimingReport(StringBuilder report, DeathVisionSetup setup)
    {
        report.AppendLine("Timing");
        report.AppendLine("------");
        report.AppendLine($"initialDelayAfterGrab: {FormatNullableFloat(setup.InitialDelayAfterGrab)}");
        report.AppendLine($"absorptionDelayAfterGrab: {FormatNullableFloat(setup.AbsorptionDelayAfterGrab)}");
        report.AppendLine($"fullBlackDelayAfterGrab: {FormatNullableFloat(setup.FullBlackDelayAfterGrab)}");
        report.AppendLine($"totalDuration: {FormatNullableFloat(setup.TotalDuration)}");
        report.AppendLine($"Timing values are reasonable: {FormatBool(HasReasonableTiming(setup))}");
        report.AppendLine();
    }

    private static void CollectFailures(DeathVisionSetup setup, List<string> failureReasons)
    {
        if (setup.DeathVisionCanvasObject == null)
            failureReasons.Add("DeathVisionCanvas was not found in the open scene.");

        if (setup.DeathVisionVignette == null)
            failureReasons.Add("DeathVisionVignette was not found.");

        if (setup.DeathVisionCanvas == null)
            failureReasons.Add("DeathVisionCanvas does not have a Canvas component.");
        else if (setup.DeathVisionCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            failureReasons.Add("DeathVisionCanvas Canvas renderMode is not ScreenSpaceOverlay.");

        if (setup.CanvasScaler == null)
            failureReasons.Add("DeathVisionCanvas does not have a CanvasScaler component.");

        if (setup.GraphicRaycaster == null)
            failureReasons.Add("DeathVisionCanvas does not have a GraphicRaycaster component.");

        if (setup.DeathVisionCanvasGroup == null)
            failureReasons.Add("DeathVisionVignette does not have a CanvasGroup component.");

        if (!IsValidBlackImage(setup.DeathVisionImage))
            failureReasons.Add("DeathVisionVignette does not have a full-screen black Image component.");

        if (!IsFullScreenRect(setup.DeathVisionVignette as RectTransform))
            failureReasons.Add("DeathVisionVignette is not stretched full-screen.");

        if (setup.DeathVisionVignetteController == null)
            failureReasons.Add("DeathVisionVignetteController was not found.");

        if (setup.AssignedCanvasGroup == null)
            failureReasons.Add("DeathVisionVignetteController.vignetteCanvasGroup is not assigned.");

        if (setup.AssignedRoot == null)
            failureReasons.Add("DeathVisionVignetteController.vignetteRoot is not assigned.");

        if (setup.RitualFailureAbsorptionBridge == null)
            failureReasons.Add("RitualFailureAbsorptionBridge was not found.");

        if (setup.BridgeDeathVisionVignetteController == null)
            failureReasons.Add("RitualFailureAbsorptionBridge.deathVisionVignetteController is not assigned.");
        else if (setup.DeathVisionVignetteController != null && setup.BridgeDeathVisionVignetteController != setup.DeathVisionVignetteController)
            failureReasons.Add("RitualFailureAbsorptionBridge.deathVisionVignetteController does not match the detected DeathVisionVignetteController.");

        if (!HasReasonableTiming(setup))
            failureReasons.Add("DeathVisionVignetteController timing values are not reasonable.");
    }

    private static void AppendSummary(StringBuilder report, List<string> failureReasons)
    {
        report.AppendLine("Final Summary");
        report.AppendLine("-------------");

        if (failureReasons.Count == 0)
        {
            report.AppendLine("PASS: Death Vision vignette UI and bridge timing are wired.");
            return;
        }

        report.AppendLine("FAIL: Death Vision setup needs repair or scene assignment.");

        for (int i = 0; i < failureReasons.Count; i++)
            report.AppendLine($"- {failureReasons[i]}");
    }

    private static Canvas FindOrCreateDeathVisionCanvas()
    {
        GameObject canvasObject = FindSceneGameObjectByName(DeathVisionCanvasName);

        if (canvasObject == null)
        {
            canvasObject = new GameObject(DeathVisionCanvasName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Death Vision Canvas");
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();

        if (canvas == null)
            canvas = Undo.AddComponent<Canvas>(canvasObject);

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
            Undo.AddComponent<GraphicRaycaster>(canvasObject);

        ConfigureCanvasScaler(canvasObject);
        return canvas;
    }

    private static RectTransform FindOrCreateDeathVisionVignette(Transform canvasTransform)
    {
        Transform existingVignette = FindDirectChildByName(canvasTransform, DeathVisionVignetteName);
        RectTransform existingRectTransform = existingVignette as RectTransform;

        if (existingRectTransform != null)
            return existingRectTransform;

        if (existingVignette != null)
        {
            Undo.RecordObject(existingVignette.gameObject, "Rename Malformed Death Vision Vignette");
            existingVignette.name = $"{DeathVisionVignetteName}_Malformed";
            EditorUtility.SetDirty(existingVignette.gameObject);
        }

        GameObject vignetteObject = new GameObject(DeathVisionVignetteName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(vignetteObject, "Create Death Vision Vignette");
        RectTransform vignetteRoot = vignetteObject.GetComponent<RectTransform>();
        vignetteRoot.SetParent(canvasTransform, false);
        return vignetteRoot;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = Undo.AddComponent<CanvasGroup>(target);

        return canvasGroup;
    }

    private static Image EnsureBlackImage(GameObject target)
    {
        Image image = target.GetComponent<Image>();

        if (image == null)
            image = Undo.AddComponent<Image>(target);

        return image;
    }

    private static DeathVisionVignetteController EnsureDeathVisionController(GameObject target)
    {
        DeathVisionVignetteController controller = target.GetComponent<DeathVisionVignetteController>();

        if (controller == null)
            controller = Undo.AddComponent<DeathVisionVignetteController>(target);

        return controller;
    }

    private static void ConfigureCanvasScaler(GameObject canvasObject)
    {
        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();

        if (canvasScaler == null)
            canvasScaler = Undo.AddComponent<CanvasScaler>(canvasObject);

        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;
        EditorUtility.SetDirty(canvasScaler);
    }

    private static void StretchToFullScreen(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.anchoredPosition = Vector2.zero;
        EditorUtility.SetDirty(rectTransform);
    }

    private static bool HasReasonableTiming(DeathVisionSetup setup)
    {
        if (!setup.InitialDelayAfterGrab.HasValue ||
            !setup.AbsorptionDelayAfterGrab.HasValue ||
            !setup.FullBlackDelayAfterGrab.HasValue ||
            !setup.TotalDuration.HasValue)
        {
            return false;
        }

        return setup.InitialDelayAfterGrab.Value >= 0f &&
            setup.AbsorptionDelayAfterGrab.Value > 0f &&
            setup.FullBlackDelayAfterGrab.Value > setup.AbsorptionDelayAfterGrab.Value &&
            setup.TotalDuration.Value >= setup.FullBlackDelayAfterGrab.Value;
    }

    private static bool IsValidBlackImage(Image image)
    {
        if (image == null)
            return false;

        Color color = image.color;
        return Mathf.Approximately(color.r, 0f) &&
            Mathf.Approximately(color.g, 0f) &&
            Mathf.Approximately(color.b, 0f);
    }

    private static bool IsFullScreenRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return false;

        return rectTransform.anchorMin == Vector2.zero &&
            rectTransform.anchorMax == Vector2.one &&
            rectTransform.offsetMin == Vector2.zero &&
            rectTransform.offsetMax == Vector2.zero;
    }

    private static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        T[] objects = FindSceneObjects<T>();
        return objects.Length > 0 ? objects[0] : null;
    }

    private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        int count = 0;

        for (int i = 0; i < objects.Length; i++)
        {
            if (IsSceneObject(objects[i]))
                count++;
        }

        T[] sceneObjects = new T[count];
        int sceneObjectIndex = 0;

        for (int i = 0; i < objects.Length; i++)
        {
            if (!IsSceneObject(objects[i]))
                continue;

            sceneObjects[sceneObjectIndex] = objects[i];
            sceneObjectIndex++;
        }

        return sceneObjects;
    }

    private static bool IsSceneObject(UnityEngine.Object sceneObject)
    {
        if (sceneObject == null || EditorUtility.IsPersistent(sceneObject))
            return false;

        GameObject gameObject = null;

        if (sceneObject is GameObject directGameObject)
            gameObject = directGameObject;
        else if (sceneObject is Component component)
            gameObject = component.gameObject;

        return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
    }

    private static GameObject FindSceneGameObjectByName(string objectName)
    {
        Transform[] transforms = FindSceneObjects<Transform>();

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == objectName)
                return transforms[i].gameObject;
        }

        return null;
    }

    private static Transform FindDirectChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == childName)
                return child;
        }

        return null;
    }

    private static T GetObjectReference<T>(SerializedObject serializedObject, string propertyName) where T : UnityEngine.Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static float? GetFloat(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || property.propertyType != SerializedPropertyType.Float)
            return null;

        return property.floatValue;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            Debug.LogWarning($"Could not find serialized property '{propertyName}' on {serializedObject.targetObject.name}.");
            return;
        }

        property.objectReferenceValue = value;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "<null>";

        string path = transform.name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }

    private static string FormatBool(bool value)
    {
        return value ? "YES" : "NO";
    }

    private static string FormatObjectName(UnityEngine.Object value)
    {
        return value != null ? $" ({value.name})" : string.Empty;
    }

    private static string FormatPath(GameObject gameObject)
    {
        return gameObject != null ? $" ({GetHierarchyPath(gameObject.transform)})" : string.Empty;
    }

    private static string FormatPath(Transform transform)
    {
        return transform != null ? $" ({GetHierarchyPath(transform)})" : string.Empty;
    }

    private static string FormatNullableFloat(float? value)
    {
        return value.HasValue ? value.Value.ToString("0.###") : "<missing>";
    }

    private sealed class DeathVisionSetup
    {
        public GameObject DeathVisionCanvasObject;
        public Canvas DeathVisionCanvas;
        public CanvasScaler CanvasScaler;
        public GraphicRaycaster GraphicRaycaster;
        public Transform DeathVisionVignette;
        public CanvasGroup DeathVisionCanvasGroup;
        public Image DeathVisionImage;
        public DeathVisionVignetteController DeathVisionVignetteController;
        public CanvasGroup AssignedCanvasGroup;
        public RectTransform AssignedRoot;
        public RitualFailureAbsorptionBridge RitualFailureAbsorptionBridge;
        public DeathVisionVignetteController BridgeDeathVisionVignetteController;
        public float? InitialDelayAfterGrab;
        public float? AbsorptionDelayAfterGrab;
        public float? FullBlackDelayAfterGrab;
        public float? TotalDuration;
    }
}
