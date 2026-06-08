using TMPro;

using UnityEngine;

using UnityEngine.UI;



namespace iStick2War_V2

{

    /*

 * SettingsCanvasLayout_V2 (Settings-canvas — scene layout + parchment sync + Go Back)

 *

 * PURPOSE:

 * Screen Space Camera settings UI. SettingsParchmentBounds tracks the world parchment sprite

 * on screen (fixes 4:3 / Blue Safe Area mismatch with OrthographicCameraAspectFitter). Scene

 * controls keep worldPositionStays under that bounds. Settings_Btn_GoBack sits bottom-left

 * inside the parchment with padding. World bounds drive the frame on wide aspects; inset on 4:3.

 *

 * ---------------------------------------------------------

 * NAVIGATION (Game_V2)

 *

 * Settings panel owner → SettingsPanelLayout_V2.cs

 * Go Back action → MainMenuNavUiButton_V2.cs + MainMenu_V2.HandleHideSettings

 */

    [DisallowMultipleComponent]

    [DefaultExecutionOrder(-240)]

    public sealed class SettingsCanvasLayout_V2 : MonoBehaviour

    {

        private const string SafeAreaRootName = "SafeAreaRoot";

        private const string ParchmentBoundsFrameName = "SettingsParchmentBounds";

        private const string SettingsItemsHostName = "settings-items";

        private const string GoBackButtonName = "Settings_Btn_GoBack";

        private const string SettingsTitleName = "txt_settings_title";



        private static readonly string[] GoBackTemplateButtonNames =

        {

            "MainMenu_Btn_Continue",

            "MainMenu_Btn_StartGame",

            "MainMenu_Btn_Settings",

        };



        private static readonly string[] ReparentUnderParchmentNames =

        {

            SettingsItemsHostName,

            "txt_settings_title",

            "MasterVolumeSlider",

            "MusicSlider",

            "SFXSlider",

            "ScreenShakeToggle",

            "FullScreenToggle",

            "VSyncToggle",

            "FPSDropdown",

            "ResolutionDropdown",

            "MouseSensitivitySlider",

        };



        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);

        [SerializeField] private float _matchWidthOrHeight = 0.5f;

        [SerializeField] private float _canvasPlaneDistance = 1f;

        [SerializeField] private float _leftPadding = 72f;

        [SerializeField] private float _bottomPadding = 48f;

        [SerializeField] private float _buttonWidth = 496f;

        [SerializeField] private float _buttonHeight = 93f;

        [SerializeField] private float _titleTopPadding = 48f;

        // Trim transparent / torn-edge margin on the parchment sprite (fraction of bounds width/height).

        [SerializeField] private float _parchmentContentInsetX = 0.06f;

        [SerializeField] private float _parchmentContentInsetY = 0.13f;

        [SerializeField] private bool _useSafeArea = true;



        private bool _applied;

        private float _cachedEdgePaddingFraction = 0.08f;

        private float? _cachedParchmentAspect;

        private Bounds? _cachedWorldParchmentBounds;



        private void OnRectTransformDimensionsChange()

        {

            if (_applied)

            {

                ApplyIfNeeded(_cachedEdgePaddingFraction, _cachedParchmentAspect, _cachedWorldParchmentBounds);

            }

        }



        internal void ApplyIfNeeded(

            float backgroundEdgePaddingFraction,

            float? parchmentAspect = null,

            Bounds? worldParchmentBounds = null)

        {

            _cachedEdgePaddingFraction = Mathf.Clamp(backgroundEdgePaddingFraction, 0f, 0.4f);

            if (parchmentAspect.HasValue)

            {

                _cachedParchmentAspect = parchmentAspect;

            }



            if (worldParchmentBounds.HasValue)

            {

                _cachedWorldParchmentBounds = worldParchmentBounds;

            }



            DetachFromWorldOffsetParentIfNeeded();

            EnsureScreenSpaceCameraCanvas();

            NormalizeCanvasTransform();

            ConfigureCanvasScaler();

            EnsureSafeAreaRootIfNeeded();

            Canvas.ForceUpdateCanvases();



            RectTransform layoutRoot = ResolveLayoutRoot();

            RectTransform parchmentBounds = EnsureParchmentBoundsFrame(layoutRoot);

            RectTransform goBackRect = EnsureGoBackButton(layoutRoot);



            if (parchmentBounds != null && layoutRoot != null)

            {

                CleanupLegacyRuntimeLayout(layoutRoot, parchmentBounds);

                ApplyParchmentBounds(parchmentBounds, layoutRoot);

                Canvas.ForceUpdateCanvases();

                ReparentSceneLayoutUnderParchment(parchmentBounds, layoutRoot);

                StripRuntimeLayoutComponents(layoutRoot);

                ApplyTitleLayout(parchmentBounds, layoutRoot);

            }



            ApplyGoBackLayout(goBackRect, parchmentBounds);

            _applied = true;

        }



        private void ApplyParchmentBounds(RectTransform parchmentBounds, RectTransform layoutRoot)

        {

            RectTransform canvasRoot = (RectTransform)transform;



            if (_cachedWorldParchmentBounds.HasValue &&

                TryResolveParchmentLocalRect(

                    _cachedWorldParchmentBounds.Value,

                    layoutRoot,

                    canvasRoot,

                    out Rect layoutLocalRect))

            {

                ApplyParchmentBoundsFromLocalRect(

                    parchmentBounds,

                    ApplyVisualParchmentInset(layoutLocalRect));

                return;

            }



            if (TryTransformRectBetweenParents(

                    ComputeInsetLocalRect(canvasRoot, _cachedEdgePaddingFraction, _cachedParchmentAspect),

                    canvasRoot,

                    layoutRoot,

                    out Rect canvasInsetInLayout))

            {

                ApplyParchmentBoundsFromLocalRect(parchmentBounds, canvasInsetInLayout);

                return;

            }



            ApplyParchmentBoundsFromLocalRect(

                parchmentBounds,

                ComputeInsetLocalRect(layoutRoot, _cachedEdgePaddingFraction, _cachedParchmentAspect));

        }



        private bool TryResolveParchmentLocalRect(

            Bounds worldBounds,

            RectTransform layoutRoot,

            RectTransform canvasRoot,

            out Rect layoutLocalRect)

        {

            layoutLocalRect = default;

            Rect? directRect = null;

            Rect? canvasMappedRect = null;



            if (TryWorldBoundsToLocalRect(layoutRoot, worldBounds, out Rect directCandidate) &&

                IsReasonableParchmentRect(directCandidate, layoutRoot.rect))

            {

                directRect = directCandidate;

            }



            if (TryWorldBoundsToLocalRect(canvasRoot, worldBounds, out Rect canvasCandidate) &&

                IsReasonableParchmentRect(canvasCandidate, canvasRoot.rect) &&

                TryTransformRectBetweenParents(canvasCandidate, canvasRoot, layoutRoot, out Rect transformedCandidate) &&

                IsReasonableParchmentRect(transformedCandidate, layoutRoot.rect))

            {

                canvasMappedRect = transformedCandidate;

            }



            if (!directRect.HasValue && !canvasMappedRect.HasValue)

            {

                return false;

            }



            if (!directRect.HasValue)

            {

                layoutLocalRect = canvasMappedRect.Value;

                return true;

            }



            if (!canvasMappedRect.HasValue)

            {

                layoutLocalRect = directRect.Value;

                return true;

            }



            layoutLocalRect = PickBetterParchmentRect(directRect.Value, canvasMappedRect.Value);

            return true;

        }



        private Rect PickBetterParchmentRect(Rect directRect, Rect canvasMappedRect)

        {

            if (!_cachedParchmentAspect.HasValue || _cachedParchmentAspect.Value <= 0.01f)

            {

                return directRect;

            }



            float targetAspect = _cachedParchmentAspect.Value;

            float directAspect = directRect.width / Mathf.Max(directRect.height, 0.001f);

            float canvasAspect = canvasMappedRect.width / Mathf.Max(canvasMappedRect.height, 0.001f);

            float directError = Mathf.Abs(directAspect - targetAspect);

            float canvasError = Mathf.Abs(canvasAspect - targetAspect);

            return directError <= canvasError ? directRect : canvasMappedRect;

        }



        private void EnsureScreenSpaceCameraCanvas()

        {

            Canvas canvas = GetComponent<Canvas>();

            if (canvas == null)

            {

                return;

            }



            canvas.renderMode = RenderMode.ScreenSpaceCamera;

            if (canvas.worldCamera == null)

            {

                canvas.worldCamera = Camera.main;

            }



            canvas.planeDistance = _canvasPlaneDistance;

            canvas.overrideSorting = true;

            canvas.sortingOrder = 320;

            canvas.enabled = true;

        }



        private void DetachFromWorldOffsetParentIfNeeded()

        {

            Transform parent = transform.parent;

            if (parent == null || parent == transform.root)

            {

                return;

            }



            transform.SetParent(null, worldPositionStays: false);

            RectTransform canvasRect = (RectTransform)transform;

            canvasRect.localPosition = Vector3.zero;

            canvasRect.localRotation = Quaternion.identity;

            canvasRect.localScale = Vector3.one;

        }



        private void NormalizeCanvasTransform()

        {

            RectTransform canvasRect = (RectTransform)transform;

            canvasRect.localPosition = Vector3.zero;

            canvasRect.localRotation = Quaternion.identity;

            if (canvasRect.localScale.sqrMagnitude < 0.0001f)

            {

                canvasRect.localScale = Vector3.one;

            }

        }



        private void ConfigureCanvasScaler()

        {

            CanvasScaler scaler = GetComponent<CanvasScaler>();

            if (scaler == null)

            {

                return;

            }



            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution = _referenceResolution;

            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            scaler.matchWidthOrHeight = Mathf.Clamp01(_matchWidthOrHeight);

        }



        private void EnsureSafeAreaRootIfNeeded()

        {

            if (!_useSafeArea)

            {

                return;

            }



            RectTransform canvasRect = (RectTransform)transform;

            Transform existing = canvasRect.Find(SafeAreaRootName);

            RectTransform safeArea = existing as RectTransform;

            bool created = false;

            if (safeArea == null)

            {

                GameObject safeAreaGo = new GameObject(SafeAreaRootName, typeof(RectTransform), typeof(SafeAreaFitter));

                safeArea = safeAreaGo.GetComponent<RectTransform>();

                safeArea.SetParent(canvasRect, false);

                created = true;

            }



            if (created)

            {

                StretchFull(safeArea);

            }



            SafeAreaFitter fitter = safeArea.GetComponent<SafeAreaFitter>();

            if (fitter != null)

            {

                fitter.Refresh();

            }



            ReparentCanvasChildrenUnder(safeArea, canvasRect);

        }



        private static RectTransform EnsureParchmentBoundsFrame(RectTransform layoutRoot)

        {

            if (layoutRoot == null)

            {

                return null;

            }



            Transform existing = layoutRoot.Find(ParchmentBoundsFrameName);

            RectTransform parchmentBounds = existing as RectTransform;

            if (parchmentBounds == null)

            {

                GameObject frameGo = new GameObject(ParchmentBoundsFrameName, typeof(RectTransform));

                parchmentBounds = frameGo.GetComponent<RectTransform>();

                parchmentBounds.SetParent(layoutRoot, false);

            }



            parchmentBounds.SetAsFirstSibling();

            return parchmentBounds;

        }



        private static void ApplyParchmentBoundsFromLocalRect(RectTransform parchmentBounds, Rect localRect)

        {

            // ScreenPointToLocalPointInRectangle is pivot-centered; center anchor avoids parent-corner offset bugs.

            parchmentBounds.anchorMin = new Vector2(0.5f, 0.5f);

            parchmentBounds.anchorMax = new Vector2(0.5f, 0.5f);

            parchmentBounds.pivot = new Vector2(0.5f, 0.5f);

            parchmentBounds.anchoredPosition = localRect.center;

            parchmentBounds.sizeDelta = new Vector2(localRect.width, localRect.height);

            parchmentBounds.localScale = Vector3.one;

            parchmentBounds.localRotation = Quaternion.identity;

        }



        private static bool IsReasonableParchmentRect(Rect localRect, Rect parentRect)

        {

            float parentArea = parentRect.width * parentRect.height;

            float rectArea = localRect.width * localRect.height;

            if (parentArea < 1f || rectArea < 1f)

            {

                return false;

            }



            // Blue Safe Area presets can project slightly outside the safe-area parent rect.

            if (rectArea < parentArea * 0.05f)

            {

                return false;

            }



            return true;

        }



        private static Rect ComputeInsetLocalRect(

            RectTransform layoutRoot,

            float edgePaddingFraction,

            float? parchmentAspect)

        {

            float margin = Mathf.Clamp(edgePaddingFraction, 0.04f, 0.2f);

            Rect parentRect = layoutRoot.rect;

            float padX = parentRect.width * margin;

            float padY = parentRect.height * margin;



            if (parchmentAspect.HasValue && parchmentAspect.Value > 0.01f)

            {

                float availW = parentRect.width - (padX * 2f);

                float availH = parentRect.height - (padY * 2f);

                if (availW > 1f && availH > 1f)

                {

                    float availAspect = availW / availH;

                    float frameW;

                    float frameH;

                    if (availAspect > parchmentAspect.Value)

                    {

                        frameH = availH;

                        frameW = availH * parchmentAspect.Value;

                    }

                    else

                    {

                        frameW = availW;

                        frameH = availW / parchmentAspect.Value;

                    }



                    padX += (availW - frameW) * 0.5f;

                    padY += (availH - frameH) * 0.5f;

                }

            }



            return Rect.MinMaxRect(

                parentRect.xMin + padX,

                parentRect.yMin + padY,

                parentRect.xMax - padX,

                parentRect.yMax - padY);

        }



        private bool TryWorldBoundsToLocalRect(RectTransform targetParent, Bounds worldBounds, out Rect localRect)

        {

            localRect = default;

            if (targetParent == null)

            {

                return false;

            }



            Camera camera = ResolveUiCamera();

            if (camera == null)

            {

                return false;

            }



            Vector3 center = worldBounds.center;

            Vector3 extents = worldBounds.extents;

            Vector3[] worldCorners =

            {

                new Vector3(center.x - extents.x, center.y - extents.y, center.z),

                new Vector3(center.x + extents.x, center.y - extents.y, center.z),

                new Vector3(center.x + extents.x, center.y + extents.y, center.z),

                new Vector3(center.x - extents.x, center.y + extents.y, center.z),

            };



            Vector2 localMin = new Vector2(float.MaxValue, float.MaxValue);

            Vector2 localMax = new Vector2(float.MinValue, float.MinValue);

            bool anyCorner = false;



            for (int i = 0; i < worldCorners.Length; i++)

            {

                Vector3 screenPoint = camera.WorldToScreenPoint(worldCorners[i]);

                if (screenPoint.z < 0f)

                {

                    continue;

                }



                Vector2 screenPoint2D = new Vector2(screenPoint.x, screenPoint.y);

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(

                        targetParent,

                        screenPoint2D,

                        camera,

                        out Vector2 localPoint))

                {

                    continue;

                }



                anyCorner = true;

                localMin = Vector2.Min(localMin, localPoint);

                localMax = Vector2.Max(localMax, localPoint);

            }



            if (!anyCorner || localMin.x >= localMax.x || localMin.y >= localMax.y)

            {

                return false;

            }



            localRect = Rect.MinMaxRect(localMin.x, localMin.y, localMax.x, localMax.y);

            return localRect.width > 1f && localRect.height > 1f;

        }



        private static bool TryTransformRectBetweenParents(

            Rect sourceRect,

            RectTransform sourceParent,

            RectTransform targetParent,

            out Rect targetRect)

        {

            targetRect = default;

            if (sourceParent == null || targetParent == null)

            {

                return false;

            }



            Vector2[] localCorners =

            {

                new Vector2(sourceRect.xMin, sourceRect.yMin),

                new Vector2(sourceRect.xMax, sourceRect.yMin),

                new Vector2(sourceRect.xMax, sourceRect.yMax),

                new Vector2(sourceRect.xMin, sourceRect.yMax),

            };



            Vector2 targetMin = new Vector2(float.MaxValue, float.MaxValue);

            Vector2 targetMax = new Vector2(float.MinValue, float.MinValue);



            for (int i = 0; i < localCorners.Length; i++)

            {

                Vector3 worldPoint = sourceParent.TransformPoint(localCorners[i]);

                Vector2 localPoint = targetParent.InverseTransformPoint(worldPoint);

                targetMin = Vector2.Min(targetMin, localPoint);

                targetMax = Vector2.Max(targetMax, localPoint);

            }



            targetRect = Rect.MinMaxRect(targetMin.x, targetMin.y, targetMax.x, targetMax.y);

            return targetRect.width > 1f && targetRect.height > 1f;

        }



        private Rect ApplyVisualParchmentInset(Rect localRect)

        {

            float insetX = localRect.width * Mathf.Clamp01(_parchmentContentInsetX);

            float insetY = localRect.height * Mathf.Clamp01(_parchmentContentInsetY);

            return Rect.MinMaxRect(

                localRect.xMin + insetX,

                localRect.yMin + insetY,

                localRect.xMax - insetX,

                localRect.yMax - insetY);

        }



        private Camera ResolveUiCamera()

        {

            Canvas canvas = GetComponent<Canvas>();

            if (canvas != null && canvas.worldCamera != null)

            {

                return canvas.worldCamera;

            }



            return Camera.main;

        }



        private static void CleanupLegacyRuntimeLayout(RectTransform layoutRoot, RectTransform parchmentBounds)

        {

            Transform legacyFrame = layoutRoot.Find("SettingsContentFrame");

            if (legacyFrame == null)

            {

                return;

            }



            RectTransform legacyRect = legacyFrame as RectTransform;

            if (legacyRect == null)

            {

                Object.Destroy(legacyFrame.gameObject);

                return;

            }



            for (int i = legacyRect.childCount - 1; i >= 0; i--)

            {

                Transform child = legacyRect.GetChild(i);

                if (child != null)

                {

                    child.SetParent(parchmentBounds, true);

                }

            }



            Object.Destroy(legacyFrame.gameObject);

        }



        private static void StripRuntimeLayoutComponents(RectTransform searchRoot)

        {

            RectTransform itemsHost = FindChildRect(searchRoot, SettingsItemsHostName);

            if (itemsHost == null)

            {

                return;

            }



            VerticalLayoutGroup layoutGroup = itemsHost.GetComponent<VerticalLayoutGroup>();

            if (layoutGroup != null)

            {

                Object.Destroy(layoutGroup);

            }



            LayoutElement[] layoutElements = itemsHost.GetComponentsInChildren<LayoutElement>(true);

            for (int i = 0; i < layoutElements.Length; i++)

            {

                LayoutElement element = layoutElements[i];

                if (element != null && element.gameObject != itemsHost.gameObject)

                {

                    Object.Destroy(element);

                }

            }

        }



        private void ReparentSceneLayoutUnderParchment(RectTransform parchmentBounds, RectTransform searchRoot)

        {

            if (parchmentBounds == null || searchRoot == null)

            {

                return;

            }



            RectTransform itemsHost = FindChildRect(searchRoot, SettingsItemsHostName);

            if (itemsHost != null && itemsHost.parent != parchmentBounds)

            {

                itemsHost.SetParent(parchmentBounds, true);

                return;

            }



            for (int i = 0; i < ReparentUnderParchmentNames.Length; i++)

            {

                RectTransform rect = FindChildRect(searchRoot, ReparentUnderParchmentNames[i]);

                if (rect == null || rect.parent == parchmentBounds)

                {

                    continue;

                }



                rect.SetParent(parchmentBounds, true);

            }

        }



        private RectTransform EnsureGoBackButton(RectTransform layoutRoot)

        {

            if (layoutRoot == null)

            {

                return null;

            }



            RectTransform goBackRect = FindChildRect(transform, GoBackButtonName);

            if (goBackRect == null)

            {

                goBackRect = SpawnGoBackButtonFromTemplate(layoutRoot);

            }



            if (goBackRect == null)

            {

                return null;

            }



            if (goBackRect.parent != layoutRoot)

            {

                goBackRect.SetParent(layoutRoot, false);

            }



            PrepareGoBackVisual(goBackRect);

            goBackRect.gameObject.SetActive(true);

            goBackRect.SetAsLastSibling();

            return goBackRect;

        }



        private void PrepareGoBackVisual(RectTransform goBackRect)

        {

            goBackRect.localRotation = Quaternion.identity;

            goBackRect.localScale = Vector3.one;



            Image image = goBackRect.GetComponent<Image>();

            if (image != null)

            {

                image.enabled = true;

                image.raycastTarget = true;

            }



            CanvasRenderer renderer = goBackRect.GetComponent<CanvasRenderer>();

            if (renderer != null)

            {

                renderer.cullTransparentMesh = false;

            }

        }



        private static RectTransform SpawnGoBackButtonFromTemplate(RectTransform layoutRoot)

        {

            RectTransform template = FindMenuButtonTemplate();

            if (template == null)

            {

                return null;

            }



            GameObject clone = Object.Instantiate(template.gameObject, layoutRoot);

            clone.name = GoBackButtonName;

            clone.SetActive(true);



            TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);

            if (label != null)

            {

                label.text = "GO BACK";

            }



            return clone.GetComponent<RectTransform>();

        }



        private static RectTransform FindMenuButtonTemplate()

        {

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)

            {

                Canvas canvas = canvases[i];

                if (canvas == null ||

                    !canvas.name.Contains("MainMenu", System.StringComparison.OrdinalIgnoreCase))

                {

                    continue;

                }



                for (int t = 0; t < GoBackTemplateButtonNames.Length; t++)

                {

                    RectTransform found = FindChildRect(canvas.transform, GoBackTemplateButtonNames[t]);

                    if (found != null)

                    {

                        return found;

                    }

                }

            }



            return null;

        }



        private void ApplyTitleLayout(RectTransform parchmentBounds, RectTransform searchRoot)

        {

            if (parchmentBounds == null)

            {

                return;

            }



            RectTransform title = FindChildRect(parchmentBounds, SettingsTitleName);

            if (title == null)

            {

                title = FindChildRect(searchRoot, SettingsTitleName);

            }



            if (title == null)

            {

                return;

            }



            if (title.parent != parchmentBounds)

            {

                title.SetParent(parchmentBounds, false);

            }



            title.anchorMin = new Vector2(0.5f, 1f);

            title.anchorMax = new Vector2(0.5f, 1f);

            title.pivot = new Vector2(0.5f, 1f);

            title.anchoredPosition = new Vector2(0f, -_titleTopPadding);

            title.localRotation = Quaternion.identity;

            title.localScale = Vector3.one;

        }



        private void ApplyGoBackLayout(RectTransform goBackRect, RectTransform parchmentBounds)

        {

            if (goBackRect == null || parchmentBounds == null)

            {

                return;

            }



            if (goBackRect.parent != parchmentBounds)

            {

                goBackRect.SetParent(parchmentBounds, false);

            }



            goBackRect.anchorMin = Vector2.zero;

            goBackRect.anchorMax = Vector2.zero;

            goBackRect.pivot = Vector2.zero;

            goBackRect.sizeDelta = new Vector2(_buttonWidth, _buttonHeight);



            Vector2 parchmentSize = parchmentBounds.sizeDelta;

            if (parchmentSize.x < 1f || parchmentSize.y < 1f)

            {

                parchmentSize = parchmentBounds.rect.size;

            }



            float targetX = _leftPadding;

            float targetY = _bottomPadding;

            float maxX = Mathf.Max(_leftPadding, parchmentSize.x - _buttonWidth - _leftPadding);

            float maxY = Mathf.Max(_bottomPadding, parchmentSize.y - _buttonHeight - _bottomPadding);



            targetX = Mathf.Clamp(targetX, _leftPadding, maxX);

            targetY = Mathf.Clamp(targetY, _bottomPadding, maxY);

            goBackRect.anchoredPosition = new Vector2(targetX, targetY);

            goBackRect.SetAsLastSibling();

        }



        private static bool TryGetRectInParentSpace(RectTransform rectTransform, RectTransform parent, out Rect localRect)

        {

            localRect = default;

            if (rectTransform == null || parent == null)

            {

                return false;

            }



            Vector3[] corners = new Vector3[4];

            rectTransform.GetWorldCorners(corners);

            Vector2 localMin = new Vector2(float.MaxValue, float.MaxValue);

            Vector2 localMax = new Vector2(float.MinValue, float.MinValue);



            for (int i = 0; i < corners.Length; i++)

            {

                Vector2 localPoint = parent.InverseTransformPoint(corners[i]);

                localMin = Vector2.Min(localMin, localPoint);

                localMax = Vector2.Max(localMax, localPoint);

            }



            localRect = Rect.MinMaxRect(localMin.x, localMin.y, localMax.x, localMax.y);

            return localRect.width > 1f && localRect.height > 1f;

        }



        private RectTransform ResolveLayoutRoot()

        {

            if (!_useSafeArea)

            {

                return (RectTransform)transform;

            }



            Transform safeArea = transform.Find(SafeAreaRootName);

            return safeArea as RectTransform ?? (RectTransform)transform;

        }



        private static RectTransform FindChildRect(Transform root, string objectName)

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

                    candidate.name.Equals(objectName, System.StringComparison.OrdinalIgnoreCase))

                {

                    return candidate as RectTransform;

                }

            }



            return null;

        }



        private static void ReparentCanvasChildrenUnder(RectTransform safeArea, RectTransform canvasRect)

        {

            for (int i = canvasRect.childCount - 1; i >= 0; i--)

            {

                Transform child = canvasRect.GetChild(i);

                if (child == null || child == safeArea)

                {

                    continue;

                }



                if (child.name.Equals(GoBackButtonName, System.StringComparison.OrdinalIgnoreCase))

                {

                    continue;

                }



                child.SetParent(safeArea, true);

            }

        }



        private static void StretchFull(RectTransform rect)

        {

            rect.anchorMin = Vector2.zero;

            rect.anchorMax = Vector2.one;

            rect.pivot = new Vector2(0.5f, 0.5f);

            rect.anchoredPosition = Vector2.zero;

            rect.sizeDelta = Vector2.zero;

            rect.localScale = Vector3.one;

            rect.localRotation = Quaternion.identity;

        }

    }

}


