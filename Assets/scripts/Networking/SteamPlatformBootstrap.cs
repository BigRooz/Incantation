using System;
using Steamworks;
using UnityEngine;

namespace Incantation.Networking
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class SteamPlatformBootstrap : MonoBehaviour
    {
        private const uint SpacewarAppId = 480;

        public static SteamPlatformBootstrap Instance { get; private set; }
        public bool IsInitialized { get; private set; }
        public ulong LocalSteamId64 { get; private set; }
        public uint ActiveAppId { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("ONLINE-001A rejected a duplicate SteamPlatformBootstrap.", this);
                enabled = false;
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (SteamSpikeTransportArguments.Resolve(SteamSpikeTransportMode.TugboatDevelopment) !=
                SteamSpikeTransportMode.SteamSpike)
            {
                Debug.Log("ONLINE-001A Steam initialization skipped because TugboatDevelopment is selected.", this);
                return;
            }

            InitializeSteam();
        }

        private void Update()
        {
            if (IsInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        private void OnDestroy()
        {
            if (IsInitialized)
            {
                SteamAPI.Shutdown();
                IsInitialized = false;
                Debug.Log("ONLINE-001A Steam API shut down cleanly.", this);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void InitializeSteam()
        {
            try
            {
                if (!Packsize.Test())
                {
                    FailureReason = "Steamworks.NET Packsize test failed.";
                    Debug.LogError($"ONLINE-001A {FailureReason}", this);
                    return;
                }

                if (!DllCheck.Test())
                {
                    FailureReason = "Steamworks.NET DLL compatibility test failed.";
                    Debug.LogError($"ONLINE-001A {FailureReason}", this);
                    return;
                }

                if (!SteamAPI.Init())
                {
                    FailureReason = "SteamAPI.Init returned false. Confirm Steam is running, the account owns the App ID, and steam_appid.txt is available for development.";
                    Debug.LogError($"ONLINE-001A {FailureReason}", this);
                    return;
                }

                IsInitialized = true;
                ActiveAppId = SteamUtils.GetAppID().m_AppId;
                LocalSteamId64 = SteamUser.GetSteamID().m_SteamID;

                if (ActiveAppId != SpacewarAppId)
                {
                    Debug.LogWarning(
                        $"ONLINE-001A initialized App ID {ActiveAppId}; this spike was designed for development App ID {SpacewarAppId}.",
                        this);
                }

                Debug.Log(
                    $"ONLINE-001A Steam initialized. AppID={ActiveAppId}, SteamID64={LocalSteamId64}.",
                    this);
            }
            catch (Exception exception)
            {
                FailureReason = $"Steam initialization threw {exception.GetType().Name}: {exception.Message}";
                Debug.LogError($"ONLINE-001A {FailureReason}", this);
            }
        }
    }
}
