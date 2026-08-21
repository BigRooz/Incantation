using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Incantation.Startup
{
    /// <summary>
    /// Prepares and plays the studio intro through a dedicated RenderTexture and overlay Canvas,
    /// then loads MainGame exactly once after completion, an allowed skip, or a playback failure.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StudioIntroController : MonoBehaviour
    {
        private const string MainGameSceneName = "MainGame";
        private const string IntroRelativePath = "Intro/intro OMG.mp4";

        [SerializeField, Min(1f)] private float preparationTimeoutSeconds = 15f;
        [SerializeField, Min(0f)] private float startTrimSeconds = 0.15f;
        [SerializeField, Min(0f)] private float endTrimSeconds = 0.15f;

        private VideoPlayer videoPlayer;
        private Camera introCamera;
        private RawImage videoImage;
        private AspectRatioFitter videoAspectRatioFitter;
        private RenderTexture videoRenderTexture;
        private float preparationStartedAt;
        private float seekStartedAt;
        private bool playbackStarted;
        private bool waitingForStartSeek;
        private bool transitionRequested;

        private void Awake()
        {
            CreateBlackPresentation();
            CreateVideoPresentation();
            ConfigureVideoPlayer();
        }

        private void OnEnable()
        {
            videoPlayer.prepareCompleted += HandlePrepared;
            videoPlayer.seekCompleted += HandleStartSeekCompleted;
            videoPlayer.loopPointReached += HandlePlaybackCompleted;
            videoPlayer.errorReceived += HandlePlaybackError;
        }

        private void Start()
        {
            preparationStartedAt = Time.realtimeSinceStartup;
            videoPlayer.Prepare();
        }

        private void Update()
        {
            if (transitionRequested)
            {
                return;
            }

            if (!videoPlayer.isPrepared &&
                Time.realtimeSinceStartup - preparationStartedAt >= preparationTimeoutSeconds)
            {
                WarnInDevelopment("Studio intro preparation timed out. Continuing to MainGame.");
                TransitionToMainGame();
                return;
            }

            if (waitingForStartSeek &&
                Time.realtimeSinceStartup - seekStartedAt >= preparationTimeoutSeconds)
            {
                WarnInDevelopment("Studio intro start seek timed out. Continuing to MainGame.");
                TransitionToMainGame();
                return;
            }

            if (playbackStarted &&
                videoPlayer.length > 0d &&
                videoPlayer.time >= videoPlayer.length - Mathf.Max(0f, endTrimSeconds))
            {
                TransitionToMainGame();
                return;
            }

            if (playbackStarted &&
                (Input.GetKeyDown(KeyCode.Escape) ||
                 Input.GetKeyDown(KeyCode.Space) ||
                 Input.GetMouseButtonDown(0)))
            {
                TransitionToMainGame();
            }
        }

        private void OnDisable()
        {
            if (videoPlayer == null)
            {
                return;
            }

            videoPlayer.prepareCompleted -= HandlePrepared;
            videoPlayer.seekCompleted -= HandleStartSeekCompleted;
            videoPlayer.loopPointReached -= HandlePlaybackCompleted;
            videoPlayer.errorReceived -= HandlePlaybackError;
        }

        private void OnDestroy()
        {
            StopPlaybackAndReleasePresentation();
        }

        private void ConfigureVideoPlayer()
        {
            videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url = Path.Combine(Application.streamingAssetsPath, IntroRelativePath);
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = null;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetDirectAudioMute(0, false);
            videoPlayer.SetDirectAudioVolume(0, 1f);
        }

        private void CreateBlackPresentation()
        {
            GameObject cameraObject = new GameObject("StudioIntroCamera");
            cameraObject.transform.SetParent(transform, false);
            introCamera = cameraObject.AddComponent<Camera>();
            introCamera.clearFlags = CameraClearFlags.SolidColor;
            introCamera.backgroundColor = Color.black;
            introCamera.cullingMask = 0;
            introCamera.depth = 100f;
        }

        private void CreateVideoPresentation()
        {
            GameObject canvasObject = new GameObject("StudioIntroCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = 0;

            GameObject backgroundObject = new GameObject("BlackBackground", typeof(RectTransform));
            backgroundObject.transform.SetParent(canvasObject.transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            StretchToParent(backgroundRect);
            Image background = backgroundObject.AddComponent<Image>();
            background.color = Color.black;
            background.raycastTarget = false;

            GameObject videoObject = new GameObject("VideoRawImage", typeof(RectTransform));
            videoObject.transform.SetParent(canvasObject.transform, false);
            RectTransform videoRect = videoObject.GetComponent<RectTransform>();
            StretchToParent(videoRect);
            videoImage = videoObject.AddComponent<RawImage>();
            videoImage.color = Color.white;
            videoImage.raycastTarget = false;
            videoImage.enabled = false;
            videoAspectRatioFitter = videoObject.AddComponent<AspectRatioFitter>();
            videoAspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        private void HandlePrepared(VideoPlayer preparedPlayer)
        {
            if (transitionRequested)
            {
                return;
            }

            if (!CreateRenderTexture(preparedPlayer))
            {
                WarnInDevelopment("Studio intro prepared without valid video dimensions. Continuing to MainGame.");
                TransitionToMainGame();
                return;
            }

            double trimmedStartTime = Mathf.Max(0f, startTrimSeconds);
            double trimmedEndTime = preparedPlayer.length - Mathf.Max(0f, endTrimSeconds);
            if (preparedPlayer.length > 0d && trimmedStartTime >= trimmedEndTime)
            {
                WarnInDevelopment("Studio intro trim values leave no playable video. Continuing to MainGame.");
                TransitionToMainGame();
                return;
            }

            if (trimmedStartTime <= 0d)
            {
                BeginVisiblePlayback(preparedPlayer);
                return;
            }

            waitingForStartSeek = true;
            seekStartedAt = Time.realtimeSinceStartup;
            preparedPlayer.time = trimmedStartTime;
        }

        private void HandleStartSeekCompleted(VideoPlayer seekedPlayer)
        {
            if (transitionRequested || !waitingForStartSeek)
            {
                return;
            }

            waitingForStartSeek = false;
            BeginVisiblePlayback(seekedPlayer);
        }

        private void BeginVisiblePlayback(VideoPlayer preparedPlayer)
        {
            if (transitionRequested)
            {
                return;
            }

            videoImage.enabled = true;
            playbackStarted = true;
            preparedPlayer.Play();
        }

        private bool CreateRenderTexture(VideoPlayer preparedPlayer)
        {
            if (preparedPlayer.width == 0 || preparedPlayer.height == 0)
            {
                return false;
            }

            int width = (int)preparedPlayer.width;
            int height = (int)preparedPlayer.height;
            videoRenderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "StudioIntroVideoTexture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            videoRenderTexture.Create();
            if (!videoRenderTexture.IsCreated())
            {
                Destroy(videoRenderTexture);
                videoRenderTexture = null;
                return false;
            }

            preparedPlayer.targetTexture = videoRenderTexture;
            videoImage.texture = videoRenderTexture;
            videoAspectRatioFitter.aspectRatio = (float)width / height;
            return true;
        }

        private void HandlePlaybackCompleted(VideoPlayer completedPlayer)
        {
            TransitionToMainGame();
        }

        private void HandlePlaybackError(VideoPlayer failedPlayer, string message)
        {
            WarnInDevelopment($"Studio intro playback failed: {message}. Continuing to MainGame.");
            TransitionToMainGame();
        }

        private void TransitionToMainGame()
        {
            if (transitionRequested)
            {
                return;
            }

            transitionRequested = true;
            playbackStarted = false;
            waitingForStartSeek = false;

            if (videoImage != null)
            {
                videoImage.enabled = false;
            }

            StopPlaybackAndReleasePresentation();
            StartCoroutine(LoadMainGameAfterBlackFrame());
        }

        private static IEnumerator LoadMainGameAfterBlackFrame()
        {
            yield return null;
            SceneManager.LoadScene(MainGameSceneName, LoadSceneMode.Single);
        }

        private void StopPlaybackAndReleasePresentation()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.targetTexture = null;
            }

            if (videoImage != null)
            {
                videoImage.texture = null;
            }

            if (videoRenderTexture == null)
            {
                return;
            }

            videoRenderTexture.Release();
            Destroy(videoRenderTexture);
            videoRenderTexture = null;
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void WarnInDevelopment(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }
    }
}
