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

public static class BookAftermathSetupValidator
{
    private const string BookName = "Book";
    private const string BookModelName = "BookModel";
    private const string BookFailureSequenceName = "BookFailureSequence";
    private const string PlayAftermathMethodName = "PlayAftermath";

    [MenuItem("Tools/Incantation/Validate Book Aftermath")]
    public static void ValidateBookAftermath()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        BookAftermathSetup setup = GatherSetup();
        List<string> failureReasons = new List<string>();
        List<string> warnings = new List<string>();
        StringBuilder report = new StringBuilder();

        AppendHeader(report, activeScene);
        AppendHierarchyReport(report, setup);
        AppendComponentReport(report, setup);
        AppendEventReport(report, setup);
        CollectFailures(setup, failureReasons, warnings);
        AppendSummary(report, failureReasons, warnings);

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Incantation/Repair Book Aftermath")]
    public static void RepairBookAftermath()
    {
        Transform repairTarget = ResolveRepairTarget();

        if (repairTarget == null)
        {
            Debug.LogError($"Book aftermath repair failed. Could not find '{BookFailureSequenceName}', '{BookModelName}', or '{BookName}' in the open scene.");
            return;
        }

        BookAftermathController aftermathController = repairTarget.GetComponent<BookAftermathController>();

        if (aftermathController == null)
            aftermathController = Undo.AddComponent<BookAftermathController>(repairTarget.gameObject);

        AudioSource audioSource = repairTarget.GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = Undo.AddComponent<AudioSource>(repairTarget.gameObject);

        ParticleSystem obviousSmokeParticles = FindObviousSmokeParticles(repairTarget);
        PlayerAbsorptionController absorptionController = FindSceneObject<PlayerAbsorptionController>();

        Undo.RecordObject(aftermathController, "Repair Book Aftermath Controller");
        SerializedObject serializedAftermath = new SerializedObject(aftermathController);
        SetObjectReferenceIfEmpty(serializedAftermath, "audioSource", audioSource);
        SetObjectReferenceIfEmpty(serializedAftermath, "smokeParticles", obviousSmokeParticles);
        serializedAftermath.ApplyModifiedProperties();
        EditorUtility.SetDirty(aftermathController);

        bool listenerAddedOrPresent = EnsureAbsorptionFinishedListener(absorptionController, aftermathController);

        if (audioSource != null)
        {
            Undo.RecordObject(audioSource, "Repair Book Aftermath Audio Source");
            audioSource.playOnAwake = false;
            EditorUtility.SetDirty(audioSource);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Book aftermath repair complete.\n"
            + $"BookAftermathController: {GetHierarchyPath(aftermathController.transform)}\n"
            + $"AudioSource assigned or preserved: {FormatBool(GetAssignedAudioSource(aftermathController) != null)}\n"
            + $"Optional Smoke ParticleSystem assigned or preserved: {FormatBool(GetAssignedSmokeParticles(aftermathController) != null)}\n"
            + $"Burp AudioClips assigned or preserved: {FormatBool(GetAssignedBurpClipCount(aftermathController) > 0)}\n"
            + $"PlayerAbsorptionController.onAbsorptionFinished listener added or already present: {FormatBool(listenerAddedOrPresent)}");

        ValidateBookAftermath();
    }

    private static BookAftermathSetup GatherSetup()
    {
        BookAftermathSetup setup = new BookAftermathSetup();
        setup.Book = FindSceneGameObjectByName(BookName);
        setup.BookModel = setup.Book != null ? FindDirectChildByName(setup.Book.transform, BookModelName) : FindSceneGameObjectByName(BookModelName)?.transform;
        setup.BookFailureSequence = setup.BookModel != null ? FindDirectChildByName(setup.BookModel, BookFailureSequenceName) : FindSceneGameObjectByName(BookFailureSequenceName)?.transform;
        setup.BookAftermathController = FindSceneObject<BookAftermathController>();
        setup.PlayerAbsorptionController = FindSceneObject<PlayerAbsorptionController>();

        if (setup.BookAftermathController != null)
        {
            SerializedObject serializedAftermath = new SerializedObject(setup.BookAftermathController);
            setup.AssignedAudioSource = GetObjectReference<AudioSource>(serializedAftermath, "audioSource");
            setup.BurpClipSlotCount = GetArraySize(serializedAftermath, "burpClips");
            setup.AssignedBurpClipCount = CountAssignedObjectReferences(serializedAftermath, "burpClips");
            setup.AssignedSmokeParticles = GetObjectReference<ParticleSystem>(serializedAftermath, "smokeParticles");
            setup.BurpDelay = GetFloat(serializedAftermath, "burpDelay");
        }

        setup.HasAbsorptionFinishedListener = HasAbsorptionFinishedListener(setup.PlayerAbsorptionController, setup.BookAftermathController);
        return setup;
    }

    private static void AppendHeader(StringBuilder report, Scene activeScene)
    {
        report.AppendLine("Book Aftermath Validation Report");
        report.AppendLine("================================");
        report.AppendLine($"Open scene path: {activeScene.path}");
        report.AppendLine();
    }

    private static void AppendHierarchyReport(StringBuilder report, BookAftermathSetup setup)
    {
        report.AppendLine("Required Hierarchy");
        report.AppendLine("------------------");
        report.AppendLine($"Book exists: {FormatBool(setup.Book != null)}{FormatPath(setup.Book)}");
        report.AppendLine($"BookModel exists: {FormatBool(setup.BookModel != null)}{FormatPath(setup.BookModel)}");
        report.AppendLine($"BookFailureSequence exists: {FormatBool(setup.BookFailureSequence != null)}{FormatPath(setup.BookFailureSequence)}");
        report.AppendLine();
    }

    private static void AppendComponentReport(StringBuilder report, BookAftermathSetup setup)
    {
        report.AppendLine("Components And References");
        report.AppendLine("-------------------------");
        report.AppendLine($"BookAftermathController exists: {FormatBool(setup.BookAftermathController != null)}{FormatPath(setup.BookAftermathController != null ? setup.BookAftermathController.transform : null)}");
        report.AppendLine($"audioSource assigned: {FormatBool(setup.AssignedAudioSource != null)}{FormatObjectName(setup.AssignedAudioSource)}");
        report.AppendLine($"smokeParticles assigned (optional): {FormatBool(setup.AssignedSmokeParticles != null)}{FormatPath(setup.AssignedSmokeParticles != null ? setup.AssignedSmokeParticles.transform : null)}");
        report.AppendLine($"burpClips slots: {setup.BurpClipSlotCount}");
        report.AppendLine($"burpClips assigned: {FormatBool(setup.AssignedBurpClipCount > 0)} ({setup.AssignedBurpClipCount})");
        report.AppendLine($"burpDelay: {FormatNullableFloat(setup.BurpDelay)}");
        report.AppendLine();
    }

    private static void AppendEventReport(StringBuilder report, BookAftermathSetup setup)
    {
        report.AppendLine("Sequence Hook");
        report.AppendLine("-------------");
        report.AppendLine($"PlayerAbsorptionController exists: {FormatBool(setup.PlayerAbsorptionController != null)}{FormatPath(setup.PlayerAbsorptionController != null ? setup.PlayerAbsorptionController.transform : null)}");
        report.AppendLine($"PlayerAbsorptionController.onAbsorptionFinished invokes BookAftermathController.PlayAftermath(): {FormatBool(setup.HasAbsorptionFinishedListener)}");
        report.AppendLine();
    }

    private static void CollectFailures(BookAftermathSetup setup, List<string> failureReasons, List<string> warnings)
    {
        if (setup.BookAftermathController == null)
            failureReasons.Add("BookAftermathController was not found in the open scene.");

        if (setup.AssignedAudioSource == null)
            failureReasons.Add("BookAftermathController.audioSource is not assigned.");

        if (setup.PlayerAbsorptionController == null)
            failureReasons.Add("PlayerAbsorptionController was not found, so the aftermath cannot be attached to absorption completion.");

        if (!setup.HasAbsorptionFinishedListener)
            failureReasons.Add("PlayerAbsorptionController.onAbsorptionFinished does not invoke BookAftermathController.PlayAftermath().");

        if (setup.AssignedBurpClipCount == 0)
            failureReasons.Add("BookAftermathController.burpClips is empty. Assign at least one burp AudioClip.");

        if (setup.AssignedSmokeParticles == null)
            warnings.Add("BookAftermathController.smokeParticles is not assigned. This optional smoke effect will be skipped.");

        if (setup.BurpDelay.HasValue && setup.BurpDelay.Value < 0f)
            failureReasons.Add("BookAftermathController.burpDelay is negative.");
    }

    private static void AppendSummary(StringBuilder report, List<string> failureReasons, List<string> warnings)
    {
        report.AppendLine("Final Summary");
        report.AppendLine("-------------");

        if (failureReasons.Count == 0)
            report.AppendLine("PASS: Book aftermath burp audio is wired to play after player absorption finishes.");
        else
            report.AppendLine("FAIL: Book aftermath setup needs repair or scene assignment.");

        for (int i = 0; i < failureReasons.Count; i++)
            report.AppendLine($"- {failureReasons[i]}");

        if (warnings.Count == 0)
            return;

        report.AppendLine();
        report.AppendLine("Warnings");
        report.AppendLine("--------");

        for (int i = 0; i < warnings.Count; i++)
            report.AppendLine($"- {warnings[i]}");
    }

    private static Transform ResolveRepairTarget()
    {
        BookAftermathController existingAftermath = FindSceneObject<BookAftermathController>();

        if (existingAftermath != null)
            return existingAftermath.transform;

        GameObject book = FindSceneGameObjectByName(BookName);
        Transform bookModel = book != null ? FindDirectChildByName(book.transform, BookModelName) : FindSceneGameObjectByName(BookModelName)?.transform;
        Transform failureSequence = bookModel != null ? FindDirectChildByName(bookModel, BookFailureSequenceName) : FindSceneGameObjectByName(BookFailureSequenceName)?.transform;

        if (failureSequence != null)
            return failureSequence;

        if (bookModel != null)
            return bookModel;

        return book != null ? book.transform : null;
    }

    private static bool EnsureAbsorptionFinishedListener(PlayerAbsorptionController absorptionController, BookAftermathController aftermathController)
    {
        if (absorptionController == null || aftermathController == null)
            return false;

        UnityEvent absorptionFinishedEvent = GetAbsorptionFinishedEvent(absorptionController);

        if (absorptionFinishedEvent == null)
            return false;

        if (HasAbsorptionFinishedListener(absorptionController, aftermathController))
            return true;

        Undo.RecordObject(absorptionController, "Repair Book Aftermath Listener");
        UnityAction listener = new UnityAction(aftermathController.PlayAftermath);
        UnityEventTools.AddPersistentListener(absorptionFinishedEvent, listener);
        EditorUtility.SetDirty(absorptionController);
        return true;
    }

    private static bool HasAbsorptionFinishedListener(PlayerAbsorptionController absorptionController, BookAftermathController aftermathController)
    {
        if (absorptionController == null || aftermathController == null)
            return false;

        UnityEvent absorptionFinishedEvent = GetAbsorptionFinishedEvent(absorptionController);

        if (absorptionFinishedEvent == null)
            return false;

        int listenerCount = absorptionFinishedEvent.GetPersistentEventCount();

        for (int i = 0; i < listenerCount; i++)
        {
            if (absorptionFinishedEvent.GetPersistentTarget(i) == aftermathController &&
                absorptionFinishedEvent.GetPersistentMethodName(i) == PlayAftermathMethodName)
            {
                return true;
            }
        }

        return false;
    }

    private static UnityEvent GetAbsorptionFinishedEvent(PlayerAbsorptionController absorptionController)
    {
        FieldInfo fieldInfo = typeof(PlayerAbsorptionController).GetField("onAbsorptionFinished", BindingFlags.Instance | BindingFlags.NonPublic);
        return fieldInfo != null ? fieldInfo.GetValue(absorptionController) as UnityEvent : null;
    }

    private static ParticleSystem FindObviousSmokeParticles(Transform repairTarget)
    {
        if (repairTarget == null)
            return null;

        ParticleSystem[] childParticles = repairTarget.GetComponentsInChildren<ParticleSystem>(true);
        ParticleSystem namedChildParticles = FindNamedSmokeParticles(childParticles);

        if (namedChildParticles != null)
            return namedChildParticles;

        if (childParticles.Length == 1)
            return childParticles[0];

        Transform bookModel = FindBookModel();

        if (bookModel == null)
            return null;

        ParticleSystem[] bookParticles = bookModel.GetComponentsInChildren<ParticleSystem>(true);
        return FindNamedSmokeParticles(bookParticles);
    }

    private static ParticleSystem FindNamedSmokeParticles(ParticleSystem[] particleSystems)
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];

            if (particleSystem == null)
                continue;

            string particleName = particleSystem.name;

            if (particleName.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0 ||
                particleName.IndexOf("ink", StringComparison.OrdinalIgnoreCase) >= 0 ||
                particleName.IndexOf("puff", StringComparison.OrdinalIgnoreCase) >= 0 ||
                particleName.IndexOf("aftermath", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return particleSystem;
            }
        }

        return null;
    }

    private static Transform FindBookModel()
    {
        GameObject book = FindSceneGameObjectByName(BookName);
        return book != null ? FindDirectChildByName(book.transform, BookModelName) : FindSceneGameObjectByName(BookModelName)?.transform;
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

    private static AudioSource GetAssignedAudioSource(BookAftermathController aftermathController)
    {
        if (aftermathController == null)
            return null;

        SerializedObject serializedAftermath = new SerializedObject(aftermathController);
        return GetObjectReference<AudioSource>(serializedAftermath, "audioSource");
    }

    private static int GetAssignedBurpClipCount(BookAftermathController aftermathController)
    {
        if (aftermathController == null)
            return 0;

        SerializedObject serializedAftermath = new SerializedObject(aftermathController);
        return CountAssignedObjectReferences(serializedAftermath, "burpClips");
    }

    private static ParticleSystem GetAssignedSmokeParticles(BookAftermathController aftermathController)
    {
        if (aftermathController == null)
            return null;

        SerializedObject serializedAftermath = new SerializedObject(aftermathController);
        return GetObjectReference<ParticleSystem>(serializedAftermath, "smokeParticles");
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

    private static int GetArraySize(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || !property.isArray)
            return 0;

        return property.arraySize;
    }

    private static int CountAssignedObjectReferences(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null || !property.isArray)
            return 0;

        int assignedCount = 0;

        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);

            if (element != null && element.objectReferenceValue != null)
                assignedCount++;
        }

        return assignedCount;
    }

    private sealed class BookAftermathSetup
    {
        public GameObject Book;
        public Transform BookModel;
        public Transform BookFailureSequence;
        public BookAftermathController BookAftermathController;
        public AudioSource AssignedAudioSource;
        public int BurpClipSlotCount;
        public int AssignedBurpClipCount;
        public ParticleSystem AssignedSmokeParticles;
        public PlayerAbsorptionController PlayerAbsorptionController;
        public bool HasAbsorptionFinishedListener;
        public float? BurpDelay;
    }
}
