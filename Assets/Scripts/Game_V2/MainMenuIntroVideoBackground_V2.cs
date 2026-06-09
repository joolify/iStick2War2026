using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace iStick2War_V2
{
    /*
     * MainMenuIntroVideoBackground_V2 — loops MainMenuIntroMovie as Main Camera far-plane background.
     * While the clip prepares: shows MainMenu-canvas/loadingBackground and hides SafeAreaRoot menu buttons.
     * After prepare: hides loadingBackground; reveals SafeAreaRoot once video is playing (UnscaledGameTime — menu uses timeScale 0).
     *
     * NAVIGATION: MainMenu_V2.cs ensures this on boot; assign Assets/Prefabs/IntroMovie/MainMenuIntroMovie.mp4.
     */
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-250)]
    public sealed class MainMenuIntroVideoBackground_V2 : MonoBehaviour
    {
        private const string DefaultStaticBackgroundObjectName = "bkg_main_menu";
        private const string MainMenuCanvasName = "MainMenu-canvas";
        private const string SafeAreaRootName = "SafeAreaRoot";
        private const string LoadingBackgroundName = "loadingBackground";

        [SerializeField] private VideoClip _introClip;
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private bool _loop = true;
        [SerializeField] private bool _playOnAwake = true;
        [SerializeField] private bool _muteVideoAudio = true;
        [SerializeField] private bool _hideStaticBackgroundSprite = true;
        [SerializeField] private string _staticBackgroundObjectName = DefaultStaticBackgroundObjectName;
        [SerializeField] private bool _showLoadingUiWhilePreparing = true;
        [SerializeField] private GameObject _loadingBackground;
        [SerializeField] private GameObject _safeAreaRoot;

        private VideoPlayer _videoPlayer;
        private SpriteRenderer _staticBackgroundSprite;
        private bool _staticBackgroundHiddenForVideo;
        private bool _loadingUiVisible;
        private bool _safeAreaActiveBeforeLoading = true;
        private bool _menuRevealedAfterVideoStart;
        private Coroutine _revealAfterVideoRoutine;

        private const float RevealAfterVideoTimeoutSeconds = 10f;

        public void Configure(VideoClip introClip, Camera targetCamera = null)
        {
            if (introClip != null)
            {
                _introClip = introClip;
            }

            if (targetCamera != null)
            {
                _targetCamera = targetCamera;
            }
        }

        private void Awake()
        {
            EnsurePlayback();
        }

        private void OnEnable()
        {
            if (_playOnAwake)
            {
                PlayIntro();
            }
        }

        private void OnDisable()
        {
            StopRevealRoutine();
            StopIntro();
            HideLoadingBackground();
            RestoreSafeAreaRoot();
            ShowStaticBackgroundPlaceholder();
        }

        public void EnsurePlayback()
        {
            if (_introClip == null)
            {
                return;
            }

            ResolveTargetCamera();
            CacheStaticBackgroundSprite();
            CacheLoadingUiReferences();
            ShowStaticBackgroundPlaceholder();
            if (_videoPlayer == null || !_videoPlayer.isPrepared)
            {
                ShowLoadingUi();
            }

            if (_videoPlayer == null)
            {
                _videoPlayer = GetComponent<VideoPlayer>();
                if (_videoPlayer == null)
                {
                    _videoPlayer = gameObject.AddComponent<VideoPlayer>();
                }
            }

            _videoPlayer.source = VideoSource.VideoClip;
            _videoPlayer.clip = _introClip;
            _videoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
            _videoPlayer.targetCamera = _targetCamera;
            _videoPlayer.isLooping = _loop;
            _videoPlayer.playOnAwake = false;
            _videoPlayer.skipOnDrop = true;
            _videoPlayer.waitForFirstFrame = true;
            // MainMenu_V2 freezes gameplay with timeScale 0; video must advance on unscaled time.
            _videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            _videoPlayer.prepareCompleted -= OnVideoPrepared;
            _videoPlayer.prepareCompleted += OnVideoPrepared;
            _videoPlayer.errorReceived -= OnVideoError;
            _videoPlayer.errorReceived += OnVideoError;

            ApplyVideoAudioMute();
        }

        public void PlayIntro()
        {
            EnsurePlayback();
            if (_videoPlayer == null || _introClip == null)
            {
                return;
            }

            if (!_videoPlayer.isPrepared && !_videoPlayer.isPlaying)
            {
                _menuRevealedAfterVideoStart = false;
                ShowStaticBackgroundPlaceholder();
                ShowLoadingUi();
            }

            ApplyVideoAudioMute();
            if (!_videoPlayer.isPlaying)
            {
                _videoPlayer.Play();
            }

            ApplyVideoAudioMute();
        }

        private void OnVideoPrepared(VideoPlayer source)
        {
            ApplyVideoAudioMute();
            HideLoadingBackground();

            if (_videoPlayer != null && !_videoPlayer.isPlaying)
            {
                _videoPlayer.Play();
            }

            BeginRevealWhenVideoVisible();
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            StopRevealRoutine();
            HideLoadingBackground();
            RestoreSafeAreaRoot();
            ShowStaticBackgroundPlaceholder();
        }

        private void BeginRevealWhenVideoVisible()
        {
            if (_menuRevealedAfterVideoStart)
            {
                return;
            }

            StopRevealRoutine();
            _revealAfterVideoRoutine = StartCoroutine(RevealWhenVideoVisibleRoutine());
        }

        private void StopRevealRoutine()
        {
            if (_revealAfterVideoRoutine == null)
            {
                return;
            }

            StopCoroutine(_revealAfterVideoRoutine);
            _revealAfterVideoRoutine = null;
        }

        private IEnumerator RevealWhenVideoVisibleRoutine()
        {
            float elapsed = 0f;
            while (_videoPlayer != null &&
                   !_videoPlayer.isPlaying &&
                   elapsed < RevealAfterVideoTimeoutSeconds)
            {
                if (_videoPlayer.isPrepared)
                {
                    _videoPlayer.Play();
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_videoPlayer == null || !_videoPlayer.isPlaying)
            {
                HideLoadingBackground();
                RestoreSafeAreaRoot();
                ShowStaticBackgroundPlaceholder();
                _revealAfterVideoRoutine = null;
                yield break;
            }

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            RevealMenuAfterVideoStarted();
            _revealAfterVideoRoutine = null;
        }

        private void RevealMenuAfterVideoStarted()
        {
            if (_menuRevealedAfterVideoStart)
            {
                return;
            }

            _menuRevealedAfterVideoStart = true;
            HideStaticBackgroundForVideo();
            RestoreSafeAreaRoot();
        }

        private void ApplyVideoAudioMute()
        {
            if (_videoPlayer == null || !_muteVideoAudio)
            {
                return;
            }

            _videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            _videoPlayer.controlledAudioTrackCount = 0;

            ushort trackCount = _videoPlayer.audioTrackCount;
            for (ushort trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                _videoPlayer.EnableAudioTrack(trackIndex, false);
                _videoPlayer.SetDirectAudioMute(trackIndex, true);
                _videoPlayer.SetDirectAudioVolume(trackIndex, 0f);
            }
        }

        private void OnDestroy()
        {
            StopRevealRoutine();

            if (_videoPlayer != null)
            {
                _videoPlayer.prepareCompleted -= OnVideoPrepared;
                _videoPlayer.errorReceived -= OnVideoError;
            }
        }

        public void StopIntro()
        {
            if (_videoPlayer != null && _videoPlayer.isPlaying)
            {
                _videoPlayer.Stop();
            }
        }

        private void ResolveTargetCamera()
        {
            if (_targetCamera != null)
            {
                return;
            }

            _targetCamera = Camera.main;
            if (_targetCamera == null)
            {
                _targetCamera = FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            }
        }

        private void CacheStaticBackgroundSprite()
        {
            if (!_hideStaticBackgroundSprite || _staticBackgroundSprite != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_staticBackgroundObjectName))
            {
                return;
            }

            GameObject background = GameObject.Find(_staticBackgroundObjectName);
            if (background == null)
            {
                return;
            }

            _staticBackgroundSprite = background.GetComponent<SpriteRenderer>();
        }

        private void ShowStaticBackgroundPlaceholder()
        {
            if (!_hideStaticBackgroundSprite || _staticBackgroundSprite == null)
            {
                return;
            }

            _staticBackgroundHiddenForVideo = false;
            _staticBackgroundSprite.enabled = true;
        }

        private void HideStaticBackgroundForVideo()
        {
            if (!_hideStaticBackgroundSprite || _staticBackgroundSprite == null || _staticBackgroundHiddenForVideo)
            {
                return;
            }

            _staticBackgroundHiddenForVideo = true;
            _staticBackgroundSprite.enabled = false;
        }

        private void CacheLoadingUiReferences()
        {
            GameObject canvas = GameObject.Find(MainMenuCanvasName);
            if (canvas == null)
            {
                return;
            }

            if (_loadingBackground == null)
            {
                Transform loadingBackground = canvas.transform.Find(LoadingBackgroundName);
                if (loadingBackground == null)
                {
                    loadingBackground = FindDescendantTransform(canvas.transform, LoadingBackgroundName);
                }

                if (loadingBackground != null)
                {
                    _loadingBackground = loadingBackground.gameObject;
                }
            }

            if (_safeAreaRoot == null)
            {
                Transform safeArea = canvas.transform.Find(SafeAreaRootName);
                if (safeArea != null)
                {
                    _safeAreaRoot = safeArea.gameObject;
                }
            }
        }

        private void ShowLoadingUi()
        {
            if (!_showLoadingUiWhilePreparing || _loadingUiVisible)
            {
                return;
            }

            CacheLoadingUiReferences();

            if (_safeAreaRoot != null)
            {
                _safeAreaActiveBeforeLoading = _safeAreaRoot.activeSelf;
                _safeAreaRoot.SetActive(false);
            }

            if (_loadingBackground != null)
            {
                _loadingBackground.SetActive(true);
            }

            _loadingUiVisible = true;
        }

        private void HideLoadingBackground()
        {
            if (!_loadingUiVisible)
            {
                return;
            }

            if (_loadingBackground != null)
            {
                _loadingBackground.SetActive(false);
            }

            _loadingUiVisible = false;
        }

        private void RestoreSafeAreaRoot()
        {
            if (_safeAreaRoot == null)
            {
                return;
            }

            _safeAreaRoot.SetActive(_safeAreaActiveBeforeLoading);
        }

        private static Transform FindDescendantTransform(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null &&
                    candidate.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
