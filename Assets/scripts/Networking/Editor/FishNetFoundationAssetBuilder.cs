using FishNet.Component.Spawning;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Observing;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Managing.Timing;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Transporting;
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
        private const string PlayerPrefabPath = PrefabFolder + "/NetworkPlayer.prefab";
        private const string ObsoletePlayerPrefabPath = PrefabFolder + "/FishNetFoundationPlayer.prefab";
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
            RemoveObsoletePlayerPrefab();
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
            GameObject player = new GameObject("NetworkPlayer");
            NetworkObject networkObject = player.AddComponent<NetworkObject>();
            networkObject.SetIsGlobal(true);
            player.AddComponent<NetworkPlayer>();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
            return savedPrefab.GetComponent<NetworkObject>();
        }

        private static void RemoveObsoletePlayerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ObsoletePlayerPrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(ObsoletePlayerPrefabPath);
            }
        }

        private static GameObject BuildManagerPrefab(NetworkObject playerPrefab)
        {
            GameObject manager = new GameObject("IncantationNetworkManager");
            manager.AddComponent<SteamPlatformBootstrap>();
            SteamSpikeTransportSelector transportSelector = manager.AddComponent<SteamSpikeTransportSelector>();

            GameObject runtime = new GameObject("FishNetRuntime");
            runtime.transform.SetParent(manager.transform, false);
            NetworkManager networkManager = runtime.AddComponent<NetworkManager>();
            SerializedObject networkManagerSettings = new SerializedObject(networkManager);
            networkManagerSettings.FindProperty("_dontDestroyOnLoad").boolValue = false;
            networkManagerSettings.ApplyModifiedPropertiesWithoutUndo();
            runtime.AddComponent<ServerManager>();
            runtime.AddComponent<ClientManager>();
            runtime.AddComponent<TransportManager>();
            runtime.AddComponent<TimeManager>();
            runtime.AddComponent<FishNetSceneManager>();
            runtime.AddComponent<ObserverManager>();

            Tugboat tugboat = runtime.AddComponent<Tugboat>();
            System.Type steamTransportType =
                System.Type.GetType("FishySteamworks.FishySteamworks, Assembly-CSharp-firstpass") ??
                System.Type.GetType("FishySteamworks.FishySteamworks, Assembly-CSharp");
            if (steamTransportType == null || !typeof(Transport).IsAssignableFrom(steamTransportType))
            {
                throw new System.InvalidOperationException(
                    "FishySteamworks is unavailable. Resolve the pinned ONLINE-001A packages before rebuilding networking assets.");
            }

            Transport steamTransport = (Transport)runtime.AddComponent(steamTransportType);
            SerializedObject steamTransportSettings = new SerializedObject(steamTransport);
            steamTransportSettings.FindProperty("_peerToPeer").boolValue = true;
            steamTransportSettings.FindProperty("_maximumClients").intValue = 8;
            steamTransportSettings.ApplyModifiedPropertiesWithoutUndo();
            TransportManager transportManager = runtime.GetComponent<TransportManager>();
            transportManager.Transport = tugboat;

            PlayerSpawner playerSpawner = runtime.AddComponent<PlayerSpawner>();
            playerSpawner.SetPlayerPrefab(playerPrefab);

            FishNetFoundationController controller = runtime.AddComponent<FishNetFoundationController>();
            runtime.AddComponent<SteamRitualLobbyDirectory>();
            runtime.AddComponent<RitualSealService>();

            transportSelector.Configure(tugboat, steamTransport, runtime);
            runtime.SetActive(false);

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(manager, ManagerPrefabPath);
            UnityEngine.Object.DestroyImmediate(manager);
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
