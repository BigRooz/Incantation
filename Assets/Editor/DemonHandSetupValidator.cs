using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DemonHandSetupValidator
{
    private const string BookName = "Book";
    private const string BookModelName = "BookModel";
    private const string BookGhostNamePrefix = "BookGhost";
    private const string DemonHandAttackName = "DemonHandAttackV1";
    private const string DemonHandModelName = "DemonHandModel";
    private const string AnimatorControllerPath = "Assets/assets/Models/DemonHand/DemonHandController.controller";
    private const string DemonHandFbxPath = "Assets/assets/Models/DemonHand/DemonHandAttack.fbx";
    private const string AttackTriggerName = "Attack";
    private const string ResetTriggerName = "Reset";

    [MenuItem("Tools/Incantation/Validate Demon Hand Setup")]
    public static void ValidateDemonHandSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject book = FindSceneGameObjectByName(BookName);
        Transform bookModel = book != null ? FindDirectChildByName(book.transform, BookModelName) : null;
        Transform demonHandAttack = bookModel != null ? FindDescendantByName(bookModel, DemonHandAttackName) : null;
        Animator demonHandAnimator = demonHandAttack != null ? demonHandAttack.GetComponent<Animator>() : null;
        RuntimeAnimatorController runtimeController = demonHandAnimator != null ? demonHandAnimator.runtimeAnimatorController : null;
        AnimatorController animatorController = runtimeController as AnimatorController;
        DemonHandController[] controllers = FindSceneObjects<DemonHandController>();
        List<string> failureReasons = new List<string>();
        StringBuilder report = new StringBuilder();

        report.AppendLine("Demon Hand Setup Validation Report");
        report.AppendLine("===================================");
        report.AppendLine($"Open scene path: {activeScene.path}");
        report.AppendLine();
        report.AppendLine("Required Hierarchy");
        report.AppendLine("------------------");
        report.AppendLine($"Book exists: {FormatBool(book != null)}{FormatPath(book)}");
        report.AppendLine($"BookModel exists under Book: {FormatBool(bookModel != null)}{FormatPath(bookModel)}");
        report.AppendLine($"DemonHandAttackV1 exists under BookModel: {FormatBool(demonHandAttack != null)}{FormatPath(demonHandAttack)}");
        report.AppendLine();

        int controllersUnderBookModel = 0;
        int controllersUnderBookGhost = 0;
        int validControllersUnderBookModel = 0;

        report.AppendLine("DemonHandController Components");
        report.AppendLine("------------------------------");
        report.AppendLine($"Components found in open scene: {controllers.Length}");

        if (controllers.Length == 0)
            report.AppendLine("None found.");

        for (int i = 0; i < controllers.Length; i++)
        {
            DemonHandController controller = controllers[i];
            GameObject controllerObject = controller.gameObject;
            bool isUnderBookModel = bookModel != null && IsSameOrDescendantOf(controllerObject.transform, bookModel);
            bool isUnderBookGhost = IsUnderBookGhost(controllerObject.transform);

            if (isUnderBookModel)
                controllersUnderBookModel++;

            if (isUnderBookGhost)
                controllersUnderBookGhost++;

            SerializedObject serializedController = new SerializedObject(controller);
            GameObject handRoot = GetObjectReference<GameObject>(serializedController, "handRoot");
            Animator handAnimator = GetObjectReference<Animator>(serializedController, "handAnimator");
            RuntimeAnimatorController handRuntimeController = handAnimator != null ? handAnimator.runtimeAnimatorController : null;

            bool hasHandRoot = handRoot != null;
            bool hasHandAnimator = handAnimator != null;
            bool animatorHasController = handRuntimeController != null;
            bool animatorHasAvatar = handAnimator != null && handAnimator.avatar != null;
            bool hasAttackTrigger = HasAnimatorTrigger(handAnimator, AttackTriggerName);
            bool hasResetTrigger = HasAnimatorTrigger(handAnimator, ResetTriggerName);
            bool isOnExpectedObject = demonHandAttack != null && controllerObject == demonHandAttack.gameObject;
            bool handRootIsExpectedModel = demonHandAttack != null
                && handRoot != null
                && FindDescendantByName(demonHandAttack, DemonHandModelName) == handRoot.transform;
            bool handAnimatorIsExpectedAnimator = demonHandAnimator != null && handAnimator == demonHandAnimator;

            if (isUnderBookModel
                && isOnExpectedObject
                && hasHandRoot
                && handRootIsExpectedModel
                && hasHandAnimator
                && handAnimatorIsExpectedAnimator
                && animatorHasController
                && animatorHasAvatar
                && hasAttackTrigger
                && hasResetTrigger)
            {
                validControllersUnderBookModel++;
            }

            report.AppendLine($"[{i + 1}] {GetHierarchyPath(controllerObject.transform)}");
            report.AppendLine($"    GameObject path: {GetHierarchyPath(controllerObject.transform)}");
            report.AppendLine($"    Under BookModel: {FormatBool(isUnderBookModel)}");
            report.AppendLine($"    Under any BookGhost: {FormatBool(isUnderBookGhost)}");
            report.AppendLine($"    handRoot assigned object name: {FormatObjectNameOrNone(handRoot)}");
            report.AppendLine($"    handAnimator assigned object name: {FormatObjectNameOrNone(handAnimator)}");
            report.AppendLine($"    Animator has controller: {FormatBool(animatorHasController)}{FormatObjectName(handRuntimeController)}");
            report.AppendLine($"    Animator has avatar: {FormatBool(animatorHasAvatar)}{FormatObjectName(handAnimator != null ? handAnimator.avatar : null)}");
            report.AppendLine($"    Animator controller has Attack trigger: {FormatBool(hasAttackTrigger)}");
            report.AppendLine($"    Animator controller has Reset trigger: {FormatBool(hasResetTrigger)}");
            report.AppendLine();
        }

        report.AppendLine("Animator On DemonHandAttackV1");
        report.AppendLine("-----------------------------");
        report.AppendLine($"Animator exists: {FormatBool(demonHandAnimator != null)}");
        report.AppendLine($"Controller name: {FormatObjectNameOrNone(runtimeController)}");
        report.AppendLine($"Avatar name: {FormatObjectNameOrNone(demonHandAnimator != null ? demonHandAnimator.avatar : null)}");

        if (demonHandAnimator != null)
        {
            report.AppendLine($"Apply Root Motion: {demonHandAnimator.applyRootMotion}");
            report.AppendLine($"Culling Mode: {demonHandAnimator.cullingMode}");
        }
        else
        {
            report.AppendLine("Apply Root Motion: <no Animator>");
            report.AppendLine("Culling Mode: <no Animator>");
        }

        report.AppendLine();
        report.AppendLine("Animator Controller");
        report.AppendLine("-------------------");
        AppendAnimatorControllerReport(report, animatorController, runtimeController);

        if (book == null)
            failureReasons.Add("Book was not found in the open scene.");

        if (bookModel == null)
            failureReasons.Add("BookModel was not found directly under Book.");

        if (demonHandAttack == null)
            failureReasons.Add("DemonHandAttackV1 was not found under BookModel.");

        if (controllers.Length == 0)
            failureReasons.Add("No DemonHandController components were found in the open scene.");

        if (validControllersUnderBookModel != 1)
            failureReasons.Add($"Expected exactly one valid DemonHandController under BookModel, found {validControllersUnderBookModel}.");

        AppendControllerAssignmentFailures(failureReasons, controllers, bookModel, demonHandAttack, demonHandAnimator);

        if (controllersUnderBookModel > 1)
            failureReasons.Add($"Expected one DemonHandController under BookModel, found {controllersUnderBookModel}.");

        if (controllersUnderBookGhost > 0)
            failureReasons.Add($"Found {controllersUnderBookGhost} DemonHandController component(s) under BookGhost objects.");

        if (demonHandAnimator == null)
            failureReasons.Add("DemonHandAttackV1 does not have an Animator.");

        if (runtimeController == null)
            failureReasons.Add("DemonHandAttackV1 Animator does not have a controller.");

        if (demonHandAnimator != null && demonHandAnimator.avatar == null)
            failureReasons.Add("DemonHandAttackV1 Animator does not have an avatar.");

        if (runtimeController != null && !HasAnimatorTrigger(demonHandAnimator, AttackTriggerName))
            failureReasons.Add("Animator controller does not define an Attack trigger.");

        if (runtimeController != null && !HasAnimatorTrigger(demonHandAnimator, ResetTriggerName))
            failureReasons.Add("Animator controller does not define a Reset trigger.");

        if (animatorController == null && runtimeController != null)
            failureReasons.Add($"Animator controller '{runtimeController.name}' is not an AnimatorController asset, so states and transitions could not be inspected.");

        report.AppendLine();
        report.AppendLine("Final Summary");
        report.AppendLine("-------------");
        report.AppendLine($"DemonHandController components under BookModel: {controllersUnderBookModel}");
        report.AppendLine($"Valid DemonHandController components under BookModel: {validControllersUnderBookModel}");
        report.AppendLine($"DemonHandController components under BookGhost objects: {controllersUnderBookGhost}");

        if (failureReasons.Count == 0)
        {
            report.AppendLine("PASS: Exactly one valid DemonHandController exists under BookModel and the DemonHandAttackV1 setup is correct.");
        }
        else
        {
            report.AppendLine("FAIL: Demon hand setup needs repair.");

            for (int i = 0; i < failureReasons.Count; i++)
                report.AppendLine($"- {failureReasons[i]}");
        }

        Debug.Log(report.ToString());
    }

    [MenuItem("Tools/Incantation/Repair Demon Hand Setup")]
    public static void RepairDemonHandSetup()
    {
        GameObject bookModel = FindSceneGameObjectByName(BookModelName);

        if (bookModel == null)
        {
            Debug.LogError($"Demon hand repair failed. Could not find '{BookModelName}' in the open scene.");
            return;
        }

        Transform demonHandAttack = FindDescendantByName(bookModel.transform, DemonHandAttackName);

        if (demonHandAttack == null)
        {
            Debug.LogError($"Demon hand repair failed. Could not find '{DemonHandAttackName}' under '{GetHierarchyPath(bookModel.transform)}'.");
            return;
        }

        Transform demonHandModel = FindDescendantByName(demonHandAttack, DemonHandModelName);

        if (demonHandModel == null)
        {
            Debug.LogError($"Demon hand repair failed. Could not find '{DemonHandModelName}' under '{GetHierarchyPath(demonHandAttack)}'.");
            return;
        }

        Animator animator = demonHandAttack.GetComponent<Animator>();

        if (animator == null)
            animator = Undo.AddComponent<Animator>(demonHandAttack.gameObject);

        DemonHandController controller = demonHandAttack.GetComponent<DemonHandController>();

        if (controller == null)
            controller = Undo.AddComponent<DemonHandController>(demonHandAttack.gameObject);

        AnimatorController animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);

        if (animatorController == null)
        {
            Debug.LogError($"Demon hand repair failed. Could not load Animator Controller at '{AnimatorControllerPath}'.");
            return;
        }

        Avatar avatar = LoadAvatarFromModel(DemonHandFbxPath);

        if (avatar == null)
        {
            Debug.LogError($"Demon hand repair failed. Could not load an Avatar from '{DemonHandFbxPath}'.");
            return;
        }

        Undo.RecordObject(animator, "Repair Demon Hand Animator");
        animator.runtimeAnimatorController = animatorController;
        animator.avatar = avatar;
        EditorUtility.SetDirty(animator);

        Undo.RecordObject(controller, "Repair Demon Hand Controller");
        SerializedObject serializedController = new SerializedObject(controller);
        SetObjectReference(serializedController, "handRoot", demonHandModel.gameObject);
        SetObjectReference(serializedController, "handAnimator", animator);
        SetBool(serializedController, "hideOnAwake", true);
        SetBool(serializedController, "deactivateWhenIdle", true);
        SetBool(serializedController, "playOnStartForDebug", false);
        serializedController.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        int removedBookGhostControllers = RemoveControllersFromBookGhosts();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Demon hand setup repair complete.\n"
            + $"Repaired controller: {GetHierarchyPath(demonHandAttack)}\n"
            + $"handRoot: {GetHierarchyPath(demonHandModel)}\n"
            + $"Animator Controller: {AnimatorControllerPath}\n"
            + $"Animator Avatar: {avatar.name} from {DemonHandFbxPath}\n"
            + $"Removed DemonHandController components from BookGhost children: {removedBookGhostControllers}");

        ValidateDemonHandSetup();
    }

    private static int RemoveControllersFromBookGhosts()
    {
        DemonHandController[] controllers = FindSceneObjects<DemonHandController>();
        int removedCount = 0;

        for (int i = 0; i < controllers.Length; i++)
        {
            DemonHandController controller = controllers[i];

            if (controller == null)
                continue;

            Transform controllerTransform = controller.transform;

            if (!IsUnderBookGhost(controllerTransform) || IsUnderNamedAncestor(controllerTransform, BookModelName))
                continue;

            Undo.DestroyObjectImmediate(controller);
            removedCount++;
        }

        return removedCount;
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

    private static Transform FindDescendantByName(Transform root, string descendantName)
    {
        if (root == null)
            return null;

        if (root.name == descendantName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendantByName(root.GetChild(i), descendantName);

            if (found != null)
                return found;
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

    private static bool IsUnderNamedAncestor(Transform transform, string ancestorName)
    {
        Transform current = transform;

        while (current != null)
        {
            if (current.name == ancestorName)
                return true;

            current = current.parent;
        }

        return false;
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

    private static bool IsUnderBookGhost(Transform transform)
    {
        Transform current = transform;

        while (current != null)
        {
            if (current.name.StartsWith(BookGhostNamePrefix, StringComparison.OrdinalIgnoreCase))
                return true;

            current = current.parent;
        }

        return false;
    }

    private static bool HasAnimatorTrigger(Animator animator, string triggerName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
                return true;
        }

        return false;
    }

    private static void AppendControllerAssignmentFailures(
        List<string> failureReasons,
        DemonHandController[] controllers,
        Transform bookModel,
        Transform demonHandAttack,
        Animator demonHandAnimator)
    {
        if (controllers == null || controllers.Length == 0)
            return;

        Transform expectedHandRoot = demonHandAttack != null ? FindDescendantByName(demonHandAttack, DemonHandModelName) : null;

        for (int i = 0; i < controllers.Length; i++)
        {
            DemonHandController controller = controllers[i];

            if (controller == null)
                continue;

            Transform controllerTransform = controller.transform;
            bool isUnderBookModel = bookModel != null && IsSameOrDescendantOf(controllerTransform, bookModel);

            if (!isUnderBookModel)
                continue;

            SerializedObject serializedController = new SerializedObject(controller);
            GameObject handRoot = GetObjectReference<GameObject>(serializedController, "handRoot");
            Animator handAnimator = GetObjectReference<Animator>(serializedController, "handAnimator");

            if (demonHandAttack != null && controller.gameObject != demonHandAttack.gameObject)
                failureReasons.Add($"DemonHandController under BookModel is on '{GetHierarchyPath(controllerTransform)}' instead of '{GetHierarchyPath(demonHandAttack)}'.");

            if (expectedHandRoot != null && (handRoot == null || handRoot.transform != expectedHandRoot))
                failureReasons.Add($"DemonHandController on '{GetHierarchyPath(controllerTransform)}' handRoot is '{FormatObjectNameOrNone(handRoot)}' but should be '{expectedHandRoot.name}'.");

            if (demonHandAnimator != null && handAnimator != demonHandAnimator)
                failureReasons.Add($"DemonHandController on '{GetHierarchyPath(controllerTransform)}' handAnimator is '{FormatObjectNameOrNone(handAnimator)}' but should be the Animator on '{GetHierarchyPath(demonHandAttack)}'.");
        }
    }

    private static void AppendAnimatorControllerReport(StringBuilder report, AnimatorController animatorController, RuntimeAnimatorController runtimeController)
    {
        if (runtimeController == null)
        {
            report.AppendLine("Controller asset: <none>");
            report.AppendLine("Parameters found: <none>");
            report.AppendLine("States found: <none>");
            report.AppendLine("Idle motion: <none>");
            report.AppendLine("Attack motion: <none>");
            report.AppendLine("Transitions found: <none>");
            return;
        }

        report.AppendLine($"Controller asset: {runtimeController.name}");

        if (animatorController == null)
        {
            report.AppendLine("Parameters found: <not available>");
            report.AppendLine("States found: <not available>");
            report.AppendLine("Idle motion: <not available>");
            report.AppendLine("Attack motion: <not available>");
            report.AppendLine("Transitions found: <not available>");
            return;
        }

        report.AppendLine("Parameters found:");

        if (animatorController.parameters.Length == 0)
            report.AppendLine("    <none>");

        for (int i = 0; i < animatorController.parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = animatorController.parameters[i];
            report.AppendLine($"    {parameter.name} ({parameter.type})");
        }

        List<AnimatorState> states = new List<AnimatorState>();
        List<string> transitions = new List<string>();

        for (int i = 0; i < animatorController.layers.Length; i++)
        {
            AnimatorControllerLayer layer = animatorController.layers[i];

            if (layer.stateMachine == null)
                continue;

            CollectStateMachineInfo(layer.stateMachine, layer.name, states, transitions);
        }

        report.AppendLine("States found:");

        if (states.Count == 0)
            report.AppendLine("    <none>");

        for (int i = 0; i < states.Count; i++)
            report.AppendLine($"    {states[i].name}");

        AnimatorState idleState = FindStateByName(states, "Idle");
        AnimatorState attackState = FindStateByName(states, "Attack");
        report.AppendLine($"Idle motion: {FormatMotionName(idleState)}");
        report.AppendLine($"Attack motion: {FormatMotionName(attackState)}");

        report.AppendLine("Transitions found:");

        if (transitions.Count == 0)
            report.AppendLine("    <none>");

        for (int i = 0; i < transitions.Count; i++)
            report.AppendLine($"    {transitions[i]}");
    }

    private static void CollectStateMachineInfo(
        AnimatorStateMachine stateMachine,
        string path,
        List<AnimatorState> states,
        List<string> transitions)
    {
        for (int i = 0; i < stateMachine.anyStateTransitions.Length; i++)
            transitions.Add($"{path}/Any State -> {FormatTransitionDestination(stateMachine.anyStateTransitions[i])}{FormatTransitionConditions(stateMachine.anyStateTransitions[i])}");

        for (int i = 0; i < stateMachine.entryTransitions.Length; i++)
            transitions.Add($"{path}/Entry -> {FormatTransitionDestination(stateMachine.entryTransitions[i])}{FormatTransitionConditions(stateMachine.entryTransitions[i])}");

        for (int i = 0; i < stateMachine.states.Length; i++)
        {
            ChildAnimatorState childState = stateMachine.states[i];
            AnimatorState state = childState.state;

            if (state == null)
                continue;

            states.Add(state);

            for (int transitionIndex = 0; transitionIndex < state.transitions.Length; transitionIndex++)
                transitions.Add($"{path}/{state.name} -> {FormatTransitionDestination(state.transitions[transitionIndex])}{FormatTransitionConditions(state.transitions[transitionIndex])}");
        }

        for (int i = 0; i < stateMachine.stateMachines.Length; i++)
        {
            ChildAnimatorStateMachine childStateMachine = stateMachine.stateMachines[i];

            if (childStateMachine.stateMachine == null)
                continue;

            CollectStateMachineInfo(childStateMachine.stateMachine, $"{path}/{childStateMachine.stateMachine.name}", states, transitions);
        }
    }

    private static AnimatorState FindStateByName(List<AnimatorState> states, string stateName)
    {
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].name == stateName)
                return states[i];
        }

        return null;
    }

    private static string FormatMotionName(AnimatorState state)
    {
        if (state == null)
            return "<state missing>";

        return state.motion != null ? state.motion.name : "<none>";
    }

    private static string FormatTransitionDestination(AnimatorTransitionBase transition)
    {
        if (transition == null)
            return "<null transition>";

        if (transition.destinationState != null)
            return transition.destinationState.name;

        if (transition.destinationStateMachine != null)
            return transition.destinationStateMachine.name;

        return "<no destination>";
    }

    private static string FormatTransitionConditions(AnimatorTransitionBase transition)
    {
        if (transition == null || transition.conditions == null || transition.conditions.Length == 0)
            return " [conditions: none]";

        StringBuilder conditions = new StringBuilder(" [conditions: ");

        for (int i = 0; i < transition.conditions.Length; i++)
        {
            AnimatorCondition condition = transition.conditions[i];

            if (i > 0)
                conditions.Append(", ");

            conditions.Append($"{condition.mode} {condition.parameter}");
        }

        conditions.Append("]");
        return conditions.ToString();
    }

    private static Avatar LoadAvatarFromModel(string modelPath)
    {
        UnityEngine.Object[] modelAssets = AssetDatabase.LoadAllAssetsAtPath(modelPath);

        for (int i = 0; i < modelAssets.Length; i++)
        {
            if (modelAssets[i] is Avatar avatar)
                return avatar;
        }

        return null;
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

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            Debug.LogWarning($"Could not find serialized property '{propertyName}' on {serializedObject.targetObject.name}.");
            return;
        }

        property.boolValue = value;
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

    private static string FormatObjectNameOrNone(UnityEngine.Object value)
    {
        return value != null ? value.name : "<none>";
    }

    private static string FormatPath(GameObject gameObject)
    {
        return gameObject != null ? $" ({GetHierarchyPath(gameObject.transform)})" : string.Empty;
    }

    private static string FormatPath(Transform transform)
    {
        return transform != null ? $" ({GetHierarchyPath(transform)})" : string.Empty;
    }
}
