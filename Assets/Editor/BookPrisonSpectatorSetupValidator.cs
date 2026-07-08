using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class BookPrisonSpectatorSetupValidator
{
    private const int MaximumPrisonSlotCount = 8;
    private const string BookName = "Book";
    private const string BookModelName = "BookModel";
    private const string BookFailureSequenceName = "BookFailureSequence";
    private const string PrisonSpawnPointPrefix = "BookPrisonSpawnPoint";
    private const string SpectatorCameraPrefix = "BookPrisonSpectatorCamera";
    private const string BookPortalCameraName = "BookPortalCamera";
    private const string BookPortalScreenName = "BookPortalScreen";
    private const string SpectatorMethodName = "SendCurrentFailedPlayerToBookPrison";

    [MenuItem("Tools/Incantation/Validate Book Prison Spectator")]
    public static void ValidateBookPrisonSpectator()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        BookPrisonSpectatorSetup setup = GatherSetup();
        List<string> failureReasons = new List<string>();
        StringBuilder report = new StringBuilder();

        AppendHeader(report, activeScene);
        AppendHierarchyReport(report, setup);
        AppendReferenceReport(report, setup);
        AppendSlotReport(report, setup);
        AppendPortalReport(report, setup);
        AppendEventReport(report, setup);
        CollectFailures(setup, failureReasons);
        AppendSummary(report, failureReasons);

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Incantation/Repair Book Prison Spectator")]
    public static void RepairBookPrisonSpectator()
    {
        Transform repairTarget = ResolveRepairTarget();

        if (repairTarget == null)
        {
            Debug.LogError($"Book Prison spectator repair failed. Could not find '{BookName}' or '{BookModelName}' in the open scene.");
            return;
        }

        BookPrisonSpectatorController spectatorController = repairTarget.GetComponent<BookPrisonSpectatorController>();

        if (spectatorController == null)
            spectatorController = Undo.AddComponent<BookPrisonSpectatorController>(repairTarget.gameObject);

        RitualController ritualController = FindSceneObject<RitualController>();
        Camera bookPortalCamera = GetNamedComponent<Camera>(BookPortalCameraName);
        Renderer portalScreenRenderer = GetNamedComponent<Renderer>(BookPortalScreenName);

        Undo.RecordObject(spectatorController, "Repair Book Prison Spectator Controller");
        SerializedObject serializedSpectator = new SerializedObject(spectatorController);
        SetObjectReferenceIfEmpty(serializedSpectator, "ritualController", ritualController);
        SetObjectReferenceIfEmpty(serializedSpectator, "bookPortalCamera", bookPortalCamera);
        SetObjectReferenceIfEmpty(serializedSpectator, "portalScreenRenderer", portalScreenRenderer);
        int namedSlotCount = FillEmptyPrisonSlotsFromNamedObjects(serializedSpectator);
        serializedSpectator.ApplyModifiedProperties();
        EditorUtility.SetDirty(spectatorController);

        BookAftermathController aftermathController = FindSceneObject<BookAftermathController>();
        bool listenerAddedOrPresent = EnsureAftermathFinishedListener(aftermathController, spectatorController);

        BookPrisonSpectatorSetup repairedSetup = GatherSetup();
        bool portalCameraTextureAssigned = AssignPortalCameraTexture(repairedSetup);
        bool portalScreenTextureAssigned = AssignPortalScreenTexture(repairedSetup);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Book Prison spectator repair complete.\n"
            + $"BookPrisonSpectatorController: {GetHierarchyPath(spectatorController.transform)}\n"
            + $"ritualController assigned or preserved: {FormatBool(GetAssignedRitualController(spectatorController) != null)}\n"
            + $"Named prison slots added from exact numbered objects: {namedSlotCount}\n"
            + $"Existing manual prisonSlots preserved when already assigned: {FormatBool(namedSlotCount == 0 && GetAssignedPrisonSlotCount(spectatorController) > 0)}\n"
            + $"BookPortalCamera assigned or preserved: {FormatBool(GetAssignedBookPortalCamera(spectatorController) != null)}\n"
            + $"BookPortalScreen assigned or preserved: {FormatBool(GetAssignedPortalScreenRenderer(spectatorController) != null)}\n"
            + $"BookAftermathController.onAftermathFinished listener added or already present: {FormatBool(listenerAddedOrPresent)}\n"
            + $"BookPortalCamera.targetTexture assigned from existing controller references: {FormatBool(portalCameraTextureAssigned)}\n"
            + $"BookPortalScreen material texture assigned from existing controller references: {FormatBool(portalScreenTextureAssigned)}");

        ValidateBookPrisonSpectator();
    }

    private static BookPrisonSpectatorSetup GatherSetup()
    {
        BookPrisonSpectatorSetup setup = new BookPrisonSpectatorSetup();
        setup.Book = FindSceneGameObjectByName(BookName);
        setup.BookModel = setup.Book != null ? FindDirectChildByName(setup.Book.transform, BookModelName) : FindSceneGameObjectByName(BookModelName)?.transform;
        setup.BookFailureSequence = setup.BookModel != null ? FindDirectChildByName(setup.BookModel, BookFailureSequenceName) : FindSceneGameObjectByName(BookFailureSequenceName)?.transform;
        setup.BookPrisonSpectatorController = FindSceneObject<BookPrisonSpectatorController>();
        setup.BookAftermathController = FindSceneObject<BookAftermathController>();

        if (setup.BookPrisonSpectatorController != null)
        {
            SerializedObject serializedSpectator = new SerializedObject(setup.BookPrisonSpectatorController);
            setup.RitualController = GetObjectReference<RitualController>(serializedSpectator, "ritualController");
            setup.BookPortalCamera = GetObjectReference<Camera>(serializedSpectator, "bookPortalCamera");
            setup.BookPortalRenderTexture = GetObjectReference<RenderTexture>(serializedSpectator, "bookPortalRenderTexture");
            setup.PortalScreenRenderer = GetObjectReference<Renderer>(serializedSpectator, "portalScreenRenderer");
            setup.PrisonSlots = GetPrisonSlotReports(serializedSpectator);
        }

        setup.PortalCameraTargetTextureMatches = setup.BookPortalCamera != null &&
            setup.BookPortalRenderTexture != null &&
            setup.BookPortalCamera.targetTexture == setup.BookPortalRenderTexture;
        setup.PortalScreenMaterialUsesTexture = DoesPortalScreenUseRenderTexture(setup.PortalScreenRenderer, setup.BookPortalRenderTexture);
        setup.AftermathHasSpectatorListener = HasAftermathFinishedListener(setup.BookAftermathController, setup.BookPrisonSpectatorController);
        return setup;
    }

    private static void AppendHeader(StringBuilder report, Scene activeScene)
    {
        report.AppendLine("Book Prison Spectator Validation Report");
        report.AppendLine("=======================================");
        report.AppendLine($"Open scene path: {activeScene.path}");
        report.AppendLine();
    }

    private static void AppendHierarchyReport(StringBuilder report, BookPrisonSpectatorSetup setup)
    {
        report.AppendLine("Expected Book-Side Host");
        report.AppendLine("-----------------------");
        report.AppendLine($"Book exists: {FormatBool(setup.Book != null)}{FormatPath(setup.Book)}");
        report.AppendLine($"BookModel exists: {FormatBool(setup.BookModel != null)}{FormatPath(setup.BookModel)}");
        report.AppendLine($"BookFailureSequence exists: {FormatBool(setup.BookFailureSequence != null)}{FormatPath(setup.BookFailureSequence)}");
        report.AppendLine();
    }

    private static void AppendReferenceReport(StringBuilder report, BookPrisonSpectatorSetup setup)
    {
        report.AppendLine("Controller References");
        report.AppendLine("---------------------");
        report.AppendLine($"BookPrisonSpectatorController exists: {FormatBool(setup.BookPrisonSpectatorController != null)}{FormatPath(setup.BookPrisonSpectatorController != null ? setup.BookPrisonSpectatorController.transform : null)}");
        report.AppendLine($"ritualController assigned: {FormatBool(setup.RitualController != null)}{FormatObjectName(setup.RitualController)}");
        report.AppendLine($"bookPortalCamera assigned: {FormatBool(setup.BookPortalCamera != null)}{FormatObjectName(setup.BookPortalCamera)}");
        report.AppendLine($"bookPortalRenderTexture assigned: {FormatBool(setup.BookPortalRenderTexture != null)}{FormatObjectName(setup.BookPortalRenderTexture)}");
        report.AppendLine($"portalScreenRenderer assigned: {FormatBool(setup.PortalScreenRenderer != null)}{FormatPath(setup.PortalScreenRenderer != null ? setup.PortalScreenRenderer.transform : null)}");
        report.AppendLine();
    }

    private static void AppendSlotReport(StringBuilder report, BookPrisonSpectatorSetup setup)
    {
        report.AppendLine("Fixed Prison Slots");
        report.AppendLine("------------------");
        report.AppendLine($"prisonSlots count: {setup.PrisonSlots.Count}");

        if (setup.PrisonSlots.Count == 0)
        {
            report.AppendLine("No slots assigned. Create up to 8 fixed slots manually or use exact names BookPrisonSpawnPoint1 and BookPrisonSpectatorCamera1 through 8 before running repair.");
            report.AppendLine();
            return;
        }

        for (int i = 0; i < setup.PrisonSlots.Count; i++)
        {
            PrisonSlotReport slot = setup.PrisonSlots[i];
            report.AppendLine($"Slot {i + 1}");
            report.AppendLine($"    spawn assigned: {FormatBool(slot.SpawnPoint != null)}{FormatPath(slot.SpawnPoint)}");
            report.AppendLine($"    camera assigned: {FormatBool(slot.SpectatorCamera != null)}{FormatObjectName(slot.SpectatorCamera)}");
        }

        report.AppendLine();
    }

    private static void AppendPortalReport(StringBuilder report, BookPrisonSpectatorSetup setup)
    {
        report.AppendLine("Portal RenderTexture");
        report.AppendLine("--------------------");
        report.AppendLine($"bookPortalCamera.targetTexture matches render texture: {FormatBool(setup.PortalCameraTargetTextureMatches)}");
        report.AppendLine($"portalScreenRenderer material uses render texture: {FormatBool(setup.PortalScreenMaterialUsesTexture)}");
        report.AppendLine();
    }

    private static void AppendEventReport(StringBuilder report, BookPrisonSpectatorSetup setup)
    {
        report.AppendLine("Sequence Hook");
        report.AppendLine("-------------");
        report.AppendLine($"BookAftermathController exists: {FormatBool(setup.BookAftermathController != null)}{FormatPath(setup.BookAftermathController != null ? setup.BookAftermathController.transform : null)}");
        report.AppendLine($"BookAftermathController.onAftermathFinished invokes BookPrisonSpectatorController.SendCurrentFailedPlayerToBookPrison(): {FormatBool(setup.AftermathHasSpectatorListener)}");
        report.AppendLine();
    }

    private static void CollectFailures(BookPrisonSpectatorSetup setup, List<string> failureReasons)
    {
        if (setup.BookPrisonSpectatorController == null)
            failureReasons.Add("BookPrisonSpectatorController was not found. Run repair to add it to Book > BookModel > BookFailureSequence when that hierarchy exists.");

        if (setup.RitualController == null)
            failureReasons.Add("BookPrisonSpectatorController.ritualController is not assigned. Assign the scene RitualController so SendCurrentFailedPlayerToBookPrison can read CurrentFailedPlayer.");

        if (setup.PrisonSlots.Count == 0)
            failureReasons.Add("BookPrisonSpectatorController.prisonSlots has no entries. Assign up to 8 fixed slots manually, or create exact numbered objects and run repair.");

        for (int i = 0; i < setup.PrisonSlots.Count; i++)
        {
            PrisonSlotReport slot = setup.PrisonSlots[i];

            if (slot.SpawnPoint == null)
                failureReasons.Add($"BookPrisonSpectatorController.prisonSlots[{i}] is missing spawnPoint. Assign BookPrisonSpawnPoint{i + 1} or a manual fixed spawn Transform.");

            if (slot.SpectatorCamera == null)
                failureReasons.Add($"BookPrisonSpectatorController.prisonSlots[{i}] is missing spectatorCamera. Assign BookPrisonSpectatorCamera{i + 1} or a manual fixed camera.");
        }

        if (setup.BookPortalCamera == null)
            failureReasons.Add($"BookPrisonSpectatorController.bookPortalCamera is not assigned. Manually place a portal camera named '{BookPortalCameraName}' or assign the field directly.");

        if (setup.BookPortalRenderTexture == null)
            failureReasons.Add("BookPrisonSpectatorController.bookPortalRenderTexture is not assigned. Create and assign a RenderTexture, for example Assets/RenderTextures/RT_BookPortal.renderTexture.");

        if (setup.PortalScreenRenderer == null)
            failureReasons.Add($"BookPrisonSpectatorController.portalScreenRenderer is not assigned. Manually place a portal screen renderer named '{BookPortalScreenName}' or assign the field directly.");

        if (setup.BookPortalCamera != null &&
            setup.BookPortalRenderTexture != null &&
            !setup.PortalCameraTargetTextureMatches)
        {
            failureReasons.Add("BookPortalCamera.targetTexture does not match BookPrisonSpectatorController.bookPortalRenderTexture.");
        }

        if (setup.PortalScreenRenderer != null &&
            setup.BookPortalRenderTexture != null &&
            !setup.PortalScreenMaterialUsesTexture)
        {
            failureReasons.Add("BookPortalScreen material does not use BookPrisonSpectatorController.bookPortalRenderTexture.");
        }

        if (setup.BookAftermathController == null)
            failureReasons.Add("BookAftermathController was not found, so the spectator transition cannot be attached after aftermath.");

        if (!setup.AftermathHasSpectatorListener)
            failureReasons.Add("BookAftermathController.onAftermathFinished does not invoke BookPrisonSpectatorController.SendCurrentFailedPlayerToBookPrison().");
    }

    private static void AppendSummary(StringBuilder report, List<string> failureReasons)
    {
        report.AppendLine("Final Summary");
        report.AppendLine("-------------");

        if (failureReasons.Count == 0)
        {
            report.AppendLine("PASS: Book Prison spectator slots are assigned, aftermath transition is wired, and the portal RenderTexture references match.");
            return;
        }

        report.AppendLine("FAIL: Book Prison spectator setup needs repair or manual scene assignment.");

        for (int i = 0; i < failureReasons.Count; i++)
            report.AppendLine($"- {failureReasons[i]}");
    }

    private static Transform ResolveRepairTarget()
    {
        GameObject book = FindSceneGameObjectByName(BookName);
        Transform bookModel = book != null ? FindDirectChildByName(book.transform, BookModelName) : FindSceneGameObjectByName(BookModelName)?.transform;

        if (bookModel == null)
            return null;

        Transform failureSequence = FindDirectChildByName(bookModel, BookFailureSequenceName);

        if (failureSequence == null)
        {
            GameObject failureSequenceObject = new GameObject(BookFailureSequenceName);
            Undo.RegisterCreatedObjectUndo(failureSequenceObject, "Create Book Failure Sequence");
            failureSequence = failureSequenceObject.transform;
            failureSequence.SetParent(bookModel, false);
            failureSequence.localPosition = Vector3.zero;
            failureSequence.localRotation = Quaternion.identity;
            failureSequence.localScale = Vector3.one;
        }

        return failureSequence;
    }

    private static int FillEmptyPrisonSlotsFromNamedObjects(SerializedObject serializedSpectator)
    {
        SerializedProperty prisonSlotsProperty = serializedSpectator.FindProperty("prisonSlots");

        if (prisonSlotsProperty == null || !prisonSlotsProperty.isArray)
            return 0;

        if (prisonSlotsProperty.arraySize > 0)
            return 0;

        List<NamedPrisonSlot> namedSlots = CollectNamedPrisonSlots();

        if (namedSlots.Count == 0)
            return 0;

        prisonSlotsProperty.arraySize = namedSlots.Count;

        for (int i = 0; i < namedSlots.Count; i++)
        {
            SerializedProperty slotProperty = prisonSlotsProperty.GetArrayElementAtIndex(i);
            SerializedProperty spawnPointProperty = slotProperty.FindPropertyRelative("spawnPoint");
            SerializedProperty spectatorCameraProperty = slotProperty.FindPropertyRelative("spectatorCamera");

            if (spawnPointProperty != null)
                spawnPointProperty.objectReferenceValue = namedSlots[i].SpawnPoint;

            if (spectatorCameraProperty != null)
                spectatorCameraProperty.objectReferenceValue = namedSlots[i].SpectatorCamera;
        }

        return namedSlots.Count;
    }

    private static List<NamedPrisonSlot> CollectNamedPrisonSlots()
    {
        List<NamedPrisonSlot> namedSlots = new List<NamedPrisonSlot>();

        for (int i = 1; i <= MaximumPrisonSlotCount; i++)
        {
            Transform spawnPoint = FindSceneGameObjectByName($"{PrisonSpawnPointPrefix}{i}")?.transform;
            Camera spectatorCamera = GetNamedComponent<Camera>($"{SpectatorCameraPrefix}{i}");

            if (spawnPoint == null && spectatorCamera == null)
                continue;

            namedSlots.Add(new NamedPrisonSlot(spawnPoint, spectatorCamera));
        }

        return namedSlots;
    }

    private static List<PrisonSlotReport> GetPrisonSlotReports(SerializedObject serializedSpectator)
    {
        List<PrisonSlotReport> slotReports = new List<PrisonSlotReport>();
        SerializedProperty prisonSlotsProperty = serializedSpectator.FindProperty("prisonSlots");

        if (prisonSlotsProperty == null || !prisonSlotsProperty.isArray)
            return slotReports;

        for (int i = 0; i < prisonSlotsProperty.arraySize; i++)
        {
            SerializedProperty slotProperty = prisonSlotsProperty.GetArrayElementAtIndex(i);
            SerializedProperty spawnPointProperty = slotProperty.FindPropertyRelative("spawnPoint");
            SerializedProperty spectatorCameraProperty = slotProperty.FindPropertyRelative("spectatorCamera");

            Transform spawnPoint = spawnPointProperty != null ? spawnPointProperty.objectReferenceValue as Transform : null;
            Camera spectatorCamera = spectatorCameraProperty != null ? spectatorCameraProperty.objectReferenceValue as Camera : null;
            slotReports.Add(new PrisonSlotReport(spawnPoint, spectatorCamera));
        }

        return slotReports;
    }

    private static bool AssignPortalCameraTexture(BookPrisonSpectatorSetup setup)
    {
        if (setup.BookPortalCamera == null || setup.BookPortalRenderTexture == null)
            return false;

        if (setup.BookPortalCamera.targetTexture == setup.BookPortalRenderTexture)
            return true;

        Undo.RecordObject(setup.BookPortalCamera, "Repair Book Portal Camera Target Texture");
        setup.BookPortalCamera.targetTexture = setup.BookPortalRenderTexture;
        EditorUtility.SetDirty(setup.BookPortalCamera);
        return true;
    }

    private static bool AssignPortalScreenTexture(BookPrisonSpectatorSetup setup)
    {
        if (setup.PortalScreenRenderer == null || setup.BookPortalRenderTexture == null)
            return false;

        Material sharedMaterial = setup.PortalScreenRenderer.sharedMaterial;

        if (sharedMaterial == null)
            return false;

        if (sharedMaterial.mainTexture == setup.BookPortalRenderTexture)
            return true;

        Undo.RecordObject(sharedMaterial, "Repair Book Portal Screen Texture");
        sharedMaterial.mainTexture = setup.BookPortalRenderTexture;
        EditorUtility.SetDirty(sharedMaterial);
        return true;
    }

    private static bool EnsureAftermathFinishedListener(BookAftermathController aftermathController, BookPrisonSpectatorController spectatorController)
    {
        if (aftermathController == null || spectatorController == null)
            return false;

        UnityEvent aftermathFinishedEvent = GetAftermathFinishedEvent(aftermathController);

        if (aftermathFinishedEvent == null)
            return false;

        if (HasAftermathFinishedListener(aftermathController, spectatorController))
            return true;

        Undo.RecordObject(aftermathController, "Repair Book Prison Spectator Listener");
        UnityAction listener = new UnityAction(spectatorController.SendCurrentFailedPlayerToBookPrison);
        UnityEventTools.AddPersistentListener(aftermathFinishedEvent, listener);
        EditorUtility.SetDirty(aftermathController);
        return true;
    }

    private static bool HasAftermathFinishedListener(BookAftermathController aftermathController, BookPrisonSpectatorController spectatorController)
    {
        if (aftermathController == null || spectatorController == null)
            return false;

        UnityEvent aftermathFinishedEvent = GetAftermathFinishedEvent(aftermathController);

        if (aftermathFinishedEvent == null)
            return false;

        int listenerCount = aftermathFinishedEvent.GetPersistentEventCount();

        for (int i = 0; i < listenerCount; i++)
        {
            if (aftermathFinishedEvent.GetPersistentTarget(i) == spectatorController &&
                aftermathFinishedEvent.GetPersistentMethodName(i) == SpectatorMethodName)
            {
                return true;
            }
        }

        return false;
    }

    private static UnityEvent GetAftermathFinishedEvent(BookAftermathController aftermathController)
    {
        FieldInfo fieldInfo = typeof(BookAftermathController).GetField("onAftermathFinished", BindingFlags.Instance | BindingFlags.NonPublic);
        return fieldInfo != null ? fieldInfo.GetValue(aftermathController) as UnityEvent : null;
    }

    private static bool DoesPortalScreenUseRenderTexture(Renderer portalScreenRenderer, RenderTexture renderTexture)
    {
        if (portalScreenRenderer == null || renderTexture == null)
            return false;

        Material sharedMaterial = portalScreenRenderer.sharedMaterial;
        return sharedMaterial != null && sharedMaterial.mainTexture == renderTexture;
    }

    private static T GetNamedComponent<T>(string objectName) where T : Component
    {
        GameObject gameObject = FindSceneGameObjectByName(objectName);
        return gameObject != null ? gameObject.GetComponent<T>() : null;
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

    private static void SetObjectReferenceIfEmpty(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            Debug.LogWarning($"Could not find serialized property '{propertyName}' on {serializedObject.targetObject.name}.");
            return;
        }

        if (property.objectReferenceValue != null || value == null)
            return;

        property.objectReferenceValue = value;
    }

    private static RitualController GetAssignedRitualController(BookPrisonSpectatorController spectatorController)
    {
        if (spectatorController == null)
            return null;

        SerializedObject serializedSpectator = new SerializedObject(spectatorController);
        return GetObjectReference<RitualController>(serializedSpectator, "ritualController");
    }

    private static int GetAssignedPrisonSlotCount(BookPrisonSpectatorController spectatorController)
    {
        if (spectatorController == null)
            return 0;

        SerializedObject serializedSpectator = new SerializedObject(spectatorController);
        SerializedProperty prisonSlotsProperty = serializedSpectator.FindProperty("prisonSlots");
        return prisonSlotsProperty != null && prisonSlotsProperty.isArray ? prisonSlotsProperty.arraySize : 0;
    }

    private static Camera GetAssignedBookPortalCamera(BookPrisonSpectatorController spectatorController)
    {
        if (spectatorController == null)
            return null;

        SerializedObject serializedSpectator = new SerializedObject(spectatorController);
        return GetObjectReference<Camera>(serializedSpectator, "bookPortalCamera");
    }

    private static Renderer GetAssignedPortalScreenRenderer(BookPrisonSpectatorController spectatorController)
    {
        if (spectatorController == null)
            return null;

        SerializedObject serializedSpectator = new SerializedObject(spectatorController);
        return GetObjectReference<Renderer>(serializedSpectator, "portalScreenRenderer");
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

    private sealed class NamedPrisonSlot
    {
        public Transform SpawnPoint { get; }
        public Camera SpectatorCamera { get; }

        public NamedPrisonSlot(Transform spawnPoint, Camera spectatorCamera)
        {
            SpawnPoint = spawnPoint;
            SpectatorCamera = spectatorCamera;
        }
    }

    private sealed class PrisonSlotReport
    {
        public Transform SpawnPoint { get; }
        public Camera SpectatorCamera { get; }

        public PrisonSlotReport(Transform spawnPoint, Camera spectatorCamera)
        {
            SpawnPoint = spawnPoint;
            SpectatorCamera = spectatorCamera;
        }
    }

    private sealed class BookPrisonSpectatorSetup
    {
        public GameObject Book;
        public Transform BookModel;
        public Transform BookFailureSequence;
        public BookPrisonSpectatorController BookPrisonSpectatorController;
        public RitualController RitualController;
        public List<PrisonSlotReport> PrisonSlots = new List<PrisonSlotReport>();
        public Camera BookPortalCamera;
        public RenderTexture BookPortalRenderTexture;
        public Renderer PortalScreenRenderer;
        public BookAftermathController BookAftermathController;
        public bool PortalCameraTargetTextureMatches;
        public bool PortalScreenMaterialUsesTexture;
        public bool AftermathHasSpectatorListener;
    }
}
