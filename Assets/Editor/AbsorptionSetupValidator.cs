using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class AbsorptionSetupValidator
{
    private const string BookName = "Book";
    private const string BookModelName = "BookModel";
    private const string BookAbsorptionTargetName = "BookAbsorptionTarget";
    private const string BookFailureSequenceName = "BookFailureSequence";
    private const string BridgeMethodName = "AbsorbCurrentFailedPlayer";
    private const string NoRealPlayerMessage = "Absorption cannot move a visible player until the active Seat exposes a real player Transform. Debug occupants only prove occupancy and are not visible player models.";

    [MenuItem("Tools/Incantation/Validate Absorption Setup")]
    public static void ValidateAbsorptionSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        AbsorptionSetup setup = GatherSetup();
        List<string> failureReasons = new List<string>();
        StringBuilder report = new StringBuilder();

        AppendHeader(report, activeScene);
        AppendHierarchyReport(report, setup);
        AppendComponentReport(report, setup);
        AppendDemonHandEventReport(report, setup);
        AppendSeatReport(report, setup);
        CollectFailures(setup, failureReasons);
        AppendSummary(report, failureReasons);

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Incantation/Repair Absorption Setup")]
    public static void RepairAbsorptionSetup()
    {
        GameObject book = FindSceneGameObjectByName(BookName);
        Transform bookModel = book != null ? FindDirectChildByName(book.transform, BookModelName) : FindSceneGameObjectByName(BookModelName)?.transform;

        if (book == null)
        {
            Debug.LogError($"Absorption setup repair failed. Could not find '{BookName}' in the open scene.");
            return;
        }

        if (bookModel == null)
        {
            Debug.LogError($"Absorption setup repair failed. Could not find '{BookModelName}' under '{GetHierarchyPath(book.transform)}'.");
            return;
        }

        Transform absorptionTarget = FindDirectChildByName(bookModel, BookAbsorptionTargetName);

        if (absorptionTarget == null)
        {
            GameObject absorptionTargetObject = new GameObject(BookAbsorptionTargetName);
            Undo.RegisterCreatedObjectUndo(absorptionTargetObject, "Create Book Absorption Target");
            absorptionTarget = absorptionTargetObject.transform;
            absorptionTarget.SetParent(bookModel, false);
            absorptionTarget.localPosition = Vector3.zero;
            absorptionTarget.localRotation = Quaternion.identity;
            absorptionTarget.localScale = Vector3.one;
        }

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

        PlayerAbsorptionController absorptionController = failureSequence.GetComponent<PlayerAbsorptionController>();

        if (absorptionController == null)
            absorptionController = Undo.AddComponent<PlayerAbsorptionController>(failureSequence.gameObject);

        RitualFailureAbsorptionBridge bridge = failureSequence.GetComponent<RitualFailureAbsorptionBridge>();

        if (bridge == null)
            bridge = Undo.AddComponent<RitualFailureAbsorptionBridge>(failureSequence.gameObject);

        RitualController ritualController = FindSceneObject<RitualController>();

        Undo.RecordObject(absorptionController, "Repair Player Absorption Controller");
        SerializedObject serializedAbsorptionController = new SerializedObject(absorptionController);
        SetObjectReference(serializedAbsorptionController, "absorptionTarget", absorptionTarget);
        serializedAbsorptionController.ApplyModifiedProperties();
        EditorUtility.SetDirty(absorptionController);

        Undo.RecordObject(bridge, "Repair Ritual Failure Absorption Bridge");
        SerializedObject serializedBridge = new SerializedObject(bridge);
        SetObjectReference(serializedBridge, "ritualController", ritualController);
        SetObjectReference(serializedBridge, "playerAbsorptionController", absorptionController);
        serializedBridge.ApplyModifiedProperties();
        EditorUtility.SetDirty(bridge);

        DemonHandController demonHandController = FindDemonHandControllerUnderBookModel(bookModel);
        bool addedGrabListener = EnsureGrabMomentListener(demonHandController, bridge);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Absorption setup repair complete.\n"
            + $"BookModel: {GetHierarchyPath(bookModel)}\n"
            + $"BookAbsorptionTarget: {GetHierarchyPath(absorptionTarget)}\n"
            + $"BookFailureSequence: {GetHierarchyPath(failureSequence)}\n"
            + $"PlayerAbsorptionController: {GetHierarchyPath(absorptionController.transform)}\n"
            + $"RitualFailureAbsorptionBridge: {GetHierarchyPath(bridge.transform)}\n"
            + $"bridge.ritualController assigned: {FormatBool(ritualController != null)}\n"
            + $"DemonHandController listener added or already present: {FormatBool(addedGrabListener)}\n"
            + "Seat realPlayerTransform assignments are runtime-bound by Seat.Occupy().");

        ValidateAbsorptionSetup();
    }

    private static AbsorptionSetup GatherSetup()
    {
        AbsorptionSetup setup = new AbsorptionSetup();
        setup.Book = FindSceneGameObjectByName(BookName);
        setup.BookModel = setup.Book != null ? FindDirectChildByName(setup.Book.transform, BookModelName) : FindSceneGameObjectByName(BookModelName)?.transform;
        setup.BookAbsorptionTarget = setup.BookModel != null ? FindDirectChildByName(setup.BookModel, BookAbsorptionTargetName) : null;
        setup.BookFailureSequence = setup.BookModel != null ? FindDirectChildByName(setup.BookModel, BookFailureSequenceName) : FindSceneGameObjectByName(BookFailureSequenceName)?.transform;
        setup.PlayerAbsorptionController = FindSceneObject<PlayerAbsorptionController>();
        setup.RitualFailureAbsorptionBridge = FindSceneObject<RitualFailureAbsorptionBridge>();
        setup.RitualController = FindSceneObject<RitualController>();
        setup.DemonHandController = setup.BookModel != null ? FindDemonHandControllerUnderBookModel(setup.BookModel) : null;
        setup.Seats = FindSceneObjects<Seat>();

        if (setup.RitualFailureAbsorptionBridge != null)
        {
            SerializedObject serializedBridge = new SerializedObject(setup.RitualFailureAbsorptionBridge);
            setup.BridgeRitualController = GetObjectReference<RitualController>(serializedBridge, "ritualController");
            setup.BridgeAbsorptionController = GetObjectReference<PlayerAbsorptionController>(serializedBridge, "playerAbsorptionController");
        }

        if (setup.PlayerAbsorptionController != null)
        {
            SerializedObject serializedAbsorptionController = new SerializedObject(setup.PlayerAbsorptionController);
            setup.AbsorptionTarget = GetObjectReference<Transform>(serializedAbsorptionController, "absorptionTarget");
        }

        setup.DemonHandHasGrabListener = HasGrabMomentListener(setup.DemonHandController, setup.RitualFailureAbsorptionBridge);
        return setup;
    }

    private static void AppendHeader(StringBuilder report, Scene activeScene)
    {
        report.AppendLine("Absorption Setup Validation Report");
        report.AppendLine("==================================");
        report.AppendLine($"Open scene path: {activeScene.path}");
        report.AppendLine();
    }

    private static void AppendHierarchyReport(StringBuilder report, AbsorptionSetup setup)
    {
        report.AppendLine("Required Hierarchy");
        report.AppendLine("------------------");
        report.AppendLine($"Book exists: {FormatBool(setup.Book != null)}{FormatPath(setup.Book)}");
        report.AppendLine($"BookModel exists: {FormatBool(setup.BookModel != null)}{FormatPath(setup.BookModel)}");
        report.AppendLine($"BookAbsorptionTarget exists under BookModel: {FormatBool(setup.BookAbsorptionTarget != null)}{FormatPath(setup.BookAbsorptionTarget)}");
        report.AppendLine($"BookFailureSequence exists: {FormatBool(setup.BookFailureSequence != null)}{FormatPath(setup.BookFailureSequence)}");
        report.AppendLine();
    }

    private static void AppendComponentReport(StringBuilder report, AbsorptionSetup setup)
    {
        report.AppendLine("Components And References");
        report.AppendLine("-------------------------");
        report.AppendLine($"PlayerAbsorptionController exists: {FormatBool(setup.PlayerAbsorptionController != null)}{FormatPath(setup.PlayerAbsorptionController != null ? setup.PlayerAbsorptionController.transform : null)}");
        report.AppendLine($"RitualFailureAbsorptionBridge exists: {FormatBool(setup.RitualFailureAbsorptionBridge != null)}{FormatPath(setup.RitualFailureAbsorptionBridge != null ? setup.RitualFailureAbsorptionBridge.transform : null)}");
        report.AppendLine($"bridge.ritualController assigned: {FormatBool(setup.BridgeRitualController != null)}{FormatObjectName(setup.BridgeRitualController)}");
        report.AppendLine($"bridge.playerAbsorptionController assigned: {FormatBool(setup.BridgeAbsorptionController != null)}{FormatObjectName(setup.BridgeAbsorptionController)}");
        report.AppendLine($"PlayerAbsorptionController.absorptionTarget assigned: {FormatBool(setup.AbsorptionTarget != null)}{FormatPath(setup.AbsorptionTarget)}");
        report.AppendLine($"DemonHandController exists under BookModel: {FormatBool(setup.DemonHandController != null)}{FormatPath(setup.DemonHandController != null ? setup.DemonHandController.transform : null)}");
        report.AppendLine();
    }

    private static void AppendDemonHandEventReport(StringBuilder report, AbsorptionSetup setup)
    {
        report.AppendLine("Demon Hand Grab Event");
        report.AppendLine("---------------------");
        report.AppendLine($"DemonHandController.onGrabMoment has listener to RitualFailureAbsorptionBridge.AbsorbCurrentFailedPlayer(): {FormatBool(setup.DemonHandHasGrabListener)}");
        report.AppendLine();
    }

    private static void AppendSeatReport(StringBuilder report, AbsorptionSetup setup)
    {
        report.AppendLine("Seats");
        report.AppendLine("-----");
        report.AppendLine($"Seat components found: {setup.Seats.Length}");
        report.AppendLine($"Occupied Seats found: {GetOccupiedSeatCount(setup.Seats)}");
        report.AppendLine("Seat-to-player binding is validated for occupied Seats. If no Seats are occupied, runtime binding has not been exercised yet.");

        if (setup.Seats.Length == 0)
            report.AppendLine("None found.");

        for (int i = 0; i < setup.Seats.Length; i++)
        {
            Seat seat = setup.Seats[i];
            SerializedObject serializedSeat = new SerializedObject(seat);
            Transform realPlayerTransform = GetObjectReference<Transform>(serializedSeat, "realPlayerTransform");
            Transform resolvedPlayerTransform = seat.GetRealPlayerTransform();

            report.AppendLine($"[{i + 1}] {seat.name}");
            report.AppendLine($"    Path: {GetHierarchyPath(seat.transform)}");
            report.AppendLine($"    currentPlayer assigned: {FormatBool(seat.currentPlayer != null)}{FormatObjectName(seat.currentPlayer)}");
            report.AppendLine($"    realPlayerTransform assigned by Seat.Occupy: {FormatBool(realPlayerTransform != null)}{FormatPath(realPlayerTransform)}");
            report.AppendLine($"    GetRealPlayerTransform returns valid target: {FormatBool(resolvedPlayerTransform != null)}{FormatPath(resolvedPlayerTransform)}");
        }

        report.AppendLine();
    }

    private static void CollectFailures(AbsorptionSetup setup, List<string> failureReasons)
    {
        if (setup.Book == null)
            failureReasons.Add("Book was not found in the open scene.");

        if (setup.BookModel == null)
            failureReasons.Add("BookModel was not found under Book.");

        if (setup.BookAbsorptionTarget == null)
            failureReasons.Add("BookAbsorptionTarget was not found under BookModel.");

        if (setup.BookFailureSequence == null)
            failureReasons.Add("BookFailureSequence was not found.");

        if (setup.PlayerAbsorptionController == null)
            failureReasons.Add("PlayerAbsorptionController was not found.");

        if (setup.RitualFailureAbsorptionBridge == null)
            failureReasons.Add("RitualFailureAbsorptionBridge was not found.");

        if (setup.BridgeRitualController == null)
            failureReasons.Add("RitualFailureAbsorptionBridge.ritualController is not assigned.");

        if (setup.BridgeAbsorptionController == null)
            failureReasons.Add("RitualFailureAbsorptionBridge.playerAbsorptionController is not assigned.");

        if (setup.AbsorptionTarget == null)
            failureReasons.Add("PlayerAbsorptionController.absorptionTarget is not assigned.");

        if (setup.DemonHandController == null)
            failureReasons.Add("DemonHandController was not found under BookModel.");

        if (!setup.DemonHandHasGrabListener)
            failureReasons.Add("DemonHandController.onGrabMoment does not invoke RitualFailureAbsorptionBridge.AbsorbCurrentFailedPlayer().");

        if (setup.Seats.Length == 0)
        {
            failureReasons.Add("No Seat components were found in the open scene.");
            return;
        }

        int occupiedSeatCount = 0;

        for (int i = 0; i < setup.Seats.Length; i++)
        {
            Seat seat = setup.Seats[i];

            if (seat == null || seat.IsFree())
                continue;

            occupiedSeatCount++;

            if (seat.GetRealPlayerTransform() == null)
                failureReasons.Add($"{NoRealPlayerMessage} Seat '{seat.name}' is occupied but does not expose a valid player Transform.");
        }

        if (occupiedSeatCount == 0)
            return;
    }

    private static void AppendSummary(StringBuilder report, List<string> failureReasons)
    {
        report.AppendLine("Final Summary");
        report.AppendLine("-------------");

        if (failureReasons.Count == 0)
        {
            report.AppendLine("PASS: Absorption setup is wired and every occupied Seat exposes a real player Transform.");
            return;
        }

        report.AppendLine("FAIL: Absorption setup needs repair or scene assignment.");

        for (int i = 0; i < failureReasons.Count; i++)
            report.AppendLine($"- {failureReasons[i]}");
    }

    private static int GetOccupiedSeatCount(Seat[] seats)
    {
        int occupiedSeatCount = 0;

        for (int i = 0; i < seats.Length; i++)
        {
            if (seats[i] != null && !seats[i].IsFree())
                occupiedSeatCount++;
        }

        return occupiedSeatCount;
    }

    private static bool EnsureGrabMomentListener(DemonHandController demonHandController, RitualFailureAbsorptionBridge bridge)
    {
        if (demonHandController == null || bridge == null)
            return false;

        UnityEvent grabMomentEvent = GetGrabMomentEvent(demonHandController);

        if (grabMomentEvent == null)
            return false;

        if (HasGrabMomentListener(demonHandController, bridge))
            return true;

        Undo.RecordObject(demonHandController, "Repair Demon Hand Grab Listener");
        UnityAction listener = new UnityAction(bridge.AbsorbCurrentFailedPlayer);
        UnityEventTools.AddPersistentListener(grabMomentEvent, listener);
        EditorUtility.SetDirty(demonHandController);
        return true;
    }

    private static bool HasGrabMomentListener(DemonHandController demonHandController, RitualFailureAbsorptionBridge bridge)
    {
        if (demonHandController == null || bridge == null)
            return false;

        UnityEvent grabMomentEvent = GetGrabMomentEvent(demonHandController);

        if (grabMomentEvent == null)
            return false;

        int listenerCount = grabMomentEvent.GetPersistentEventCount();

        for (int i = 0; i < listenerCount; i++)
        {
            if (grabMomentEvent.GetPersistentTarget(i) == bridge &&
                grabMomentEvent.GetPersistentMethodName(i) == BridgeMethodName)
            {
                return true;
            }
        }

        return false;
    }

    private static UnityEvent GetGrabMomentEvent(DemonHandController demonHandController)
    {
        FieldInfo fieldInfo = typeof(DemonHandController).GetField("onGrabMoment", BindingFlags.Instance | BindingFlags.NonPublic);
        return fieldInfo != null ? fieldInfo.GetValue(demonHandController) as UnityEvent : null;
    }

    private static DemonHandController FindDemonHandControllerUnderBookModel(Transform bookModel)
    {
        if (bookModel == null)
            return null;

        DemonHandController[] controllers = FindSceneObjects<DemonHandController>();

        for (int i = 0; i < controllers.Length; i++)
        {
            DemonHandController controller = controllers[i];

            if (controller != null && IsSameOrDescendantOf(controller.transform, bookModel))
                return controller;
        }

        return null;
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

    private static bool IsSameOrDescendantOf(Transform transform, Transform ancestor)
    {
        Transform current = transform;

        while (current != null)
        {
            if (current == ancestor)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static T GetObjectReference<T>(SerializedObject serializedObject, string propertyName) where T : UnityEngine.Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
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

    private sealed class AbsorptionSetup
    {
        public GameObject Book;
        public Transform BookModel;
        public Transform BookAbsorptionTarget;
        public Transform BookFailureSequence;
        public PlayerAbsorptionController PlayerAbsorptionController;
        public RitualFailureAbsorptionBridge RitualFailureAbsorptionBridge;
        public RitualController RitualController;
        public RitualController BridgeRitualController;
        public PlayerAbsorptionController BridgeAbsorptionController;
        public Transform AbsorptionTarget;
        public DemonHandController DemonHandController;
        public bool DemonHandHasGrabListener;
        public Seat[] Seats = new Seat[0];
    }
}
