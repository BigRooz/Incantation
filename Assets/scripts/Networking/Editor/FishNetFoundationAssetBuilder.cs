using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Observing;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FishNetSceneManager = FishNet.Managing.Scened.SceneManager;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Incantation.Networking.Editor
{
    [InitializeOnLoad]
    public static class FishNetFoundationAssetBuilder
    {
        private const string NetworkFolder = "Assets/Networking";
        private const string PrefabFolder = NetworkFolder + "/Prefabs";
        private const string PlayerPrefabPath = PrefabFolder + "/FishNetFoundationPlayer.prefab";
        private const string ManagerPrefabPath = PrefabFolder + "/IncantationNetworkManager.prefab";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        static FishNetFoundationAssetBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        [MenuItem("Incantation/Networking/Rebuild FishNet Foundation Assets")]
        public static void Build()
        {
            EnsureFolders();
            NetworkObject playerPrefab = BuildPlayerPrefab();
            GameObject managerPrefab = BuildManagerPrefab(playerPrefab);
            BuildBootstrapScene(managerPrefab);
            AddBootstrapSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FishNet foundation assets created.");
        }

        private static void BuildIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath) == null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) == null)
            {
                Build();
            }
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(NetworkFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Networking");
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder(NetworkFolder, "Prefabs");
            }
        }

        private static NetworkObject BuildPlayerPrefab()
        {
            GameObject player = new GameObject("FishNetFoundationPlayer");
            NetworkObject networkObject = player.AddComponent<NetworkObject>();
            player.AddComponent<FishNetFoundationPlayer>();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);
            return savedPrefab.GetComponent<NetworkObject>();
        }

        private static GameObject BuildManagerPrefab(NetworkObject playerPrefab)
        {
            GameObject manager = new GameObject("IncantationNetworkManager");
            manager.AddComponent<NetworkManager>();
            manager.AddComponent<ServerManager>();
            manager.AddComponent<ClientManager>();
            manager.AddComponent<TransportManager>();
            manager.AddComponent<TimeManager>();
            manager.AddComponent<FishNetSceneManager>();
            manager.AddComponent<ObserverManager>();

            Tugboat tugboat = manager.AddComponent<Tugboat>();
            TransportManager transportManager = manager.GetComponent<TransportManager>();
            transportManager.Transport = tugboat;

            PlayerSpawner playerSpawner = manager.AddComponent<PlayerSpawner>();
            playerSpawner.SetPlayerPrefab(playerPrefab);

            FishNetFoundationController controller = manager.AddComponent<FishNetFoundationController>();
            FishNetFoundationHud hud = manager.AddComponent<FishNetFoundationHud>();
            hud.SetFoundationController(controller);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(manager, ManagerPrefabPath);
            Object.DestroyImmediate(manager);
            return savedPrefab;
        }

        private static void BuildBootstrapScene(GameObject managerPrefab)
        {
            Scene previousScene = UnitySceneManager.GetActiveScene();
            string previousPath = previousScene.path;

            Scene bootstrapScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(managerPrefab, bootstrapScene);
            EditorSceneManager.SaveScene(bootstrapScene, BootstrapScenePath);

            if (!string.IsNullOrEmpty(previousPath))
            {
                EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            }
        }

        private static void AddBootstrapSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] updatedScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
            updatedScenes[0] = new EditorBuildSettingsScene(BootstrapScenePath, true);

            int writeIndex = 1;
            foreach (EditorBuildSettingsScene scene in existingScenes)
            {
                if (scene.path == BootstrapScenePath)
                {
                    continue;
                }

                updatedScenes[writeIndex] = scene;
                writeIndex++;
            }

            if (writeIndex != updatedScenes.Length)
            {
                System.Array.Resize(ref updatedScenes, writeIndex);
            }

            EditorBuildSettings.scenes = updatedScenes;
        }
    }
}
