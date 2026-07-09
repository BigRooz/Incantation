using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LobbySeatSetupValidator
{
    private const string CreatedLobbySeatPointName = "LobbySeatPoint";
    private static readonly string[] AutoAssignableSeatPointNames =
    {
        "SeatPoint",
        "SitPoint",
        "PlayerSeatPoint",
        "SpawnPoint"
    };

    private static readonly string[] OptionalOccupiedVisualNames =
    {
        "LobbyOccupiedMarker",
        "LobbyGhost",
        "OccupiedGhost",
        "PlayerGhost",
        "PlayerPreview"
    };

    [MenuItem("Tools/Incantation/Validate Lobby Seat Setup")]
    public static void ValidateLobbySeatSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        LobbySeatSetup setup = GatherSetup();
        List<string> failureReasons = new List<string>();
        StringBuilder report = new StringBuilder();

        AppendHeader(report, activeScene);
        AppendControllerReport(report, setup);
        AppendSeatReport(report, setup);
        AppendChairClickReport(report, setup);
        CollectFailures(setup, failureReasons);
        AppendSummary(report, failureReasons);

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Incantation/Repair Lobby Seat Setup")]
    public static void RepairLobbySeatSetup()
    {
        Seat[] seats = FindSceneObjects<Seat>();
        int assignedExistingSeatPoints = 0;
        int createdSeatPoints = 0;

        for (int i = 0; i < seats.Length; i++)
        {
            Seat seat = seats[i];

            if (seat == null || seat.playerSpawn != null)
                continue;

            Transform seatPoint = FindAutoAssignableSeatPoint(seat.transform);

            if (seatPoint == null)
            {
                GameObject seatPointObject = new GameObject(CreatedLobbySeatPointName);
                Undo.RegisterCreatedObjectUndo(seatPointObject, "Create Lobby Seat Point");
                seatPoint = seatPointObject.transform;
                seatPoint.SetParent(seat.transform, false);
                seatPoint.localPosition = Vector3.zero;
                seatPoint.localRotation = Quaternion.identity;
                seatPoint.localScale = Vector3.one;
                createdSeatPoints++;
            }
            else
            {
                assignedExistingSeatPoints++;
            }

            Undo.RecordObject(seat, "Assign Lobby Seat Point");
            seat.playerSpawn = seatPoint;
            EditorUtility.SetDirty(seat);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Lobby Seat setup repair complete.\n"
            + $"Seats found: {seats.Length}\n"
            + $"Existing clear seat points assigned: {assignedExistingSeatPoints}\n"
            + $"LobbySeatPoint children created and assigned: {createdSeatPoints}\n"
            + "Repair only assigns Seat.playerSpawn from exact child names or creates LobbySeatPoint under each Seat.");

        ValidateLobbySeatSetup();
    }

    private static LobbySeatSetup GatherSetup()
    {
        LobbySeatSetup setup = new LobbySeatSetup();
        setup.LobbyController = FindSceneObject<LobbyController>();
        setup.SeatManager = FindSceneObject<SeatManager>();
        setup.Seats = FindSceneObjects<Seat>();
        setup.ChairClicks = FindSceneObjects<ChairClick>();
        setup.TaggedPlayer = FindTaggedPlayer();

        if (setup.LobbyController != null)
        {
            SerializedObject serializedLobbyController = new SerializedObject(setup.LobbyController);
            setup.LobbyControllerSeatManager = GetObjectReference<SeatManager>(serializedLobbyController, "seatManager");
            setup.LobbyControllerLocalPlayer = GetObjectReference<GameObject>(serializedLobbyController, "localLobbyPlayer");
        }

        if (setup.SeatManager != null)
        {
            SerializedObject serializedSeatManager = new SerializedObject(setup.SeatManager);
            setup.SeatManagerLocalPlayer = GetObjectReference<GameObject>(serializedSeatManager, "localLobbyPlayer");
            setup.ShowLobbyOccupiedMarkers = GetBool(serializedSeatManager, "showLobbyOccupiedMarkers");
        }

        setup.ChairClicksBySeat = BuildChairClickLookup(setup.ChairClicks);
        return setup;
    }

    private static void AppendHeader(StringBuilder report, Scene activeScene)
    {
        report.AppendLine("Lobby Seat Setup Validation Report");
        report.AppendLine("==================================");
        report.AppendLine($"Open scene path: {activeScene.path}");
        report.AppendLine();
    }

    private static void AppendControllerReport(StringBuilder report, LobbySeatSetup setup)
    {
        report.AppendLine("Controllers");
        report.AppendLine("-----------");
        report.AppendLine($"LobbyController exists: {FormatBool(setup.LobbyController != null)}{FormatPath(setup.LobbyController != null ? setup.LobbyController.transform : null)}");
        report.AppendLine($"SeatManager exists: {FormatBool(setup.SeatManager != null)}{FormatPath(setup.SeatManager != null ? setup.SeatManager.transform : null)}");
        report.AppendLine($"LobbyController.seatManager assigned: {FormatBool(setup.LobbyControllerSeatManager != null)}{FormatObjectName(setup.LobbyControllerSeatManager)}");
        report.AppendLine($"LobbyController.localLobbyPlayer assigned: {FormatBool(setup.LobbyControllerLocalPlayer != null)}{FormatObjectName(setup.LobbyControllerLocalPlayer)}");
        report.AppendLine($"SeatManager.localLobbyPlayer assigned: {FormatBool(setup.SeatManagerLocalPlayer != null)}{FormatObjectName(setup.SeatManagerLocalPlayer)}");
        report.AppendLine($"Tagged Player instance found: {FormatBool(setup.TaggedPlayer != null)}{FormatObjectName(setup.TaggedPlayer)}");
        report.AppendLine($"Local lobby player can be resolved: {FormatBool(HasResolvableLocalLobbyPlayer(setup))}");
        report.AppendLine($"Runtime occupied markers enabled: {FormatBool(setup.ShowLobbyOccupiedMarkers)}");
        report.AppendLine();
    }

    private static void AppendSeatReport(StringBuilder report, LobbySeatSetup setup)
    {
        report.AppendLine("Seats");
        report.AppendLine("-----");
        report.AppendLine($"Seat components found: {setup.Seats.Length}");

        if (setup.Seats.Length == 0)
            report.AppendLine("None found.");

        for (int i = 0; i < setup.Seats.Length; i++)
        {
            Seat seat = setup.Seats[i];
            Transform spawnPoint = seat != null ? seat.playerSpawn : null;
            Transform clearChildSeatPoint = seat != null ? FindAnyKnownSeatPoint(seat.transform) : null;
            Transform optionalOccupiedVisual = seat != null ? FindOptionalOccupiedVisual(seat.transform) : null;
            bool canUseForLobbySeating = seat != null && spawnPoint != null;

            report.AppendLine($"[{i + 1}] {FormatObjectName(seat)}");
            report.AppendLine($"    Path: {FormatPath(seat != null ? seat.transform : null)}");
            report.AppendLine($"    Has seat/spawn point assigned: {FormatBool(spawnPoint != null)}{FormatPath(spawnPoint)}");
            report.AppendLine($"    Clear child seat point found: {FormatBool(clearChildSeatPoint != null)}{FormatPath(clearChildSeatPoint)}");
            report.AppendLine($"    Occupied visual/ghost required: NO (SeatManager can create lightweight runtime markers)");
            report.AppendLine($"    Optional occupied visual/ghost child found: {FormatBool(optionalOccupiedVisual != null)}{FormatPath(optionalOccupiedVisual)}");
            report.AppendLine($"    ChairClick linked to this Seat: {FormatBool(HasChairClickForSeat(setup, seat))}");
            report.AppendLine($"    Can be used for lobby seating: {FormatBool(canUseForLobbySeating)}");
        }

        report.AppendLine();
    }

    private static void AppendChairClickReport(StringBuilder report, LobbySeatSetup setup)
    {
        report.AppendLine("ChairClick Components");
        report.AppendLine("---------------------");
        report.AppendLine($"ChairClick components found: {setup.ChairClicks.Length}");

        if (setup.ChairClicks.Length == 0)
            report.AppendLine("None found.");

        for (int i = 0; i < setup.ChairClicks.Length; i++)
        {
            ChairClick chairClick = setup.ChairClicks[i];
            Seat linkedSeat = chairClick != null ? chairClick.seat : null;

            report.AppendLine($"[{i + 1}] {FormatObjectName(chairClick)}");
            report.AppendLine($"    Path: {FormatPath(chairClick != null ? chairClick.transform : null)}");
            report.AppendLine($"    Linked Seat: {FormatBool(linkedSeat != null)}{FormatObjectName(linkedSeat)}");
        }

        report.AppendLine();
    }

    private static void CollectFailures(LobbySeatSetup setup, List<string> failureReasons)
    {
        if (setup.LobbyController == null)
            failureReasons.Add("LobbyController was not found in the open scene.");

        if (setup.SeatManager == null)
            failureReasons.Add("SeatManager was not found in the open scene.");

        if (setup.LobbyController != null && setup.LobbyControllerSeatManager == null)
            failureReasons.Add("LobbyController.seatManager is not assigned.");

        if (!HasResolvableLocalLobbyPlayer(setup))
            failureReasons.Add("No local lobby player is assigned and no active scene object tagged Player was found.");

        if (setup.Seats.Length == 0)
            failureReasons.Add("No Seat components were found in the open scene.");

        for (int i = 0; i < setup.Seats.Length; i++)
        {
            Seat seat = setup.Seats[i];

            if (seat == null)
                continue;

            if (seat.playerSpawn == null)
                failureReasons.Add($"Seat '{seat.name}' has no PlayerSpawn / lobby seat point assigned.");

            if (!HasChairClickForSeat(setup, seat))
                failureReasons.Add($"Seat '{seat.name}' has no ChairClick linked to it.");
        }

        if (setup.ChairClicks.Length == 0)
            failureReasons.Add("No ChairClick components were found in the open scene.");

        for (int i = 0; i < setup.ChairClicks.Length; i++)
        {
            ChairClick chairClick = setup.ChairClicks[i];

            if (chairClick != null && chairClick.seat == null)
                failureReasons.Add($"ChairClick '{chairClick.name}' has no Seat assigned.");
        }
    }

    private static void AppendSummary(StringBuilder report, List<string> failureReasons)
    {
        report.AppendLine("Final Summary");
        report.AppendLine("-------------");

        if (failureReasons.Count == 0)
        {
            report.AppendLine("PASS: Lobby seat selection setup is ready.");
            return;
        }

        report.AppendLine("FAIL: Lobby seat selection setup needs repair or scene assignment.");

        for (int i = 0; i < failureReasons.Count; i++)
            report.AppendLine($"- {failureReasons[i]}");
    }

    private static Dictionary<Seat, int> BuildChairClickLookup(ChairClick[] chairClicks)
    {
        Dictionary<Seat, int> chairClicksBySeat = new Dictionary<Seat, int>();

        for (int i = 0; i < chairClicks.Length; i++)
        {
            ChairClick chairClick = chairClicks[i];

            if (chairClick == null || chairClick.seat == null)
                continue;

            if (!chairClicksBySeat.ContainsKey(chairClick.seat))
                chairClicksBySeat.Add(chairClick.seat, 0);

            chairClicksBySeat[chairClick.seat]++;
        }

        return chairClicksBySeat;
    }

    private static bool HasChairClickForSeat(LobbySeatSetup setup, Seat seat)
    {
        return seat != null &&
            setup.ChairClicksBySeat != null &&
            setup.ChairClicksBySeat.ContainsKey(seat) &&
            setup.ChairClicksBySeat[seat] > 0;
    }

    private static bool HasResolvableLocalLobbyPlayer(LobbySeatSetup setup)
    {
        return setup.LobbyControllerLocalPlayer != null ||
            setup.SeatManagerLocalPlayer != null ||
            setup.TaggedPlayer != null;
    }

    private static Transform FindAutoAssignableSeatPoint(Transform seatTransform)
    {
        Transform seatPoint = FindDirectChildByAnyName(seatTransform, AutoAssignableSeatPointNames);

        if (seatPoint != null)
            return seatPoint;

        return FindDirectChildByName(seatTransform, CreatedLobbySeatPointName);
    }

    private static Transform FindAnyKnownSeatPoint(Transform seatTransform)
    {
        return FindAutoAssignableSeatPoint(seatTransform);
    }

    private static Transform FindOptionalOccupiedVisual(Transform seatTransform)
    {
        return FindDirectChildByAnyName(seatTransform, OptionalOccupiedVisualNames);
    }

    private static Transform FindDirectChildByAnyName(Transform root, string[] childNames)
    {
        if (root == null || childNames == null)
            return null;

        for (int i = 0; i < childNames.Length; i++)
        {
            Transform child = FindDirectChildByName(root, childNames[i]);

            if (child != null)
                return child;
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

    private static GameObject FindTaggedPlayer()
    {
        try
        {
            return GameObject.FindWithTag("Player");
        }
        catch (UnityException)
        {
            return null;
        }
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

    private static T GetObjectReference<T>(SerializedObject serializedObject, string propertyName) where T : UnityEngine.Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
    }

    private static bool GetBool(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.propertyType == SerializedPropertyType.Boolean && property.boolValue;
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

    private static string FormatPath(Transform transform)
    {
        return transform != null ? $" ({GetHierarchyPath(transform)})" : string.Empty;
    }

    private sealed class LobbySeatSetup
    {
        public LobbyController LobbyController;
        public SeatManager SeatManager;
        public SeatManager LobbyControllerSeatManager;
        public GameObject LobbyControllerLocalPlayer;
        public GameObject SeatManagerLocalPlayer;
        public GameObject TaggedPlayer;
        public bool ShowLobbyOccupiedMarkers;
        public Seat[] Seats = new Seat[0];
        public ChairClick[] ChairClicks = new ChairClick[0];
        public Dictionary<Seat, int> ChairClicksBySeat = new Dictionary<Seat, int>();
    }
}
