using PlanetSurvival.World.Exploration;
using UnityEngine;

namespace PlanetSurvival.UI.HUD
{
    /// <summary>
    /// Renders a wider, player-centred copy of the surface camera into the HUD. The player artwork is
    /// deliberately excluded and replaced with a fixed marker so the minimap never duplicates its animation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapView : MonoBehaviour
    {
        private const string FrameResourcePath = "UI/MinimapFrame";
        private const string CameraObjectName = "Minimap Camera";
        private const int PlayerPresentationLayer = 31;
        private const float CompactOrthographicSize = 32f;
        private const float ExpandedOrthographicSize = 80f;
        private const float MinimumExpandedOrthographicSize = 18f;
        private const float MaximumExpandedOrthographicSize = 160f;
        private const float ZoomStep = 1.12f;
        private const float ScreenMargin = 14f;
        private const float CompactTop = 176f;
        private const float MaximumRenderTextureSize = 1024f;
        private const float VisionRadius = 14f;
        private const float MinimumCameraDistance = 18f;
        private const float CameraDistanceScale = .85f;
        private const float FogUpdateDistance = .5f;
        private const int MaximumFogTextureSize = 256;

        private static readonly Color MapBackgroundColor = new(.025f, .035f, .04f, 1f);
        private static readonly Color MarkerOutlineColor = new(.02f, .08f, .1f, .95f);
        private static readonly Color MarkerColor = new(.15f, .9f, 1f, 1f);
        private static readonly Color32 HiddenFogColor = new(3, 7, 10, 248);
        private static readonly Color32 RevealedFogColor = new(0, 0, 0, 0);

        private Transform _target;
        private Camera _sourceCamera;
        private Camera _mapCamera;
        private WorldExplorationMap _exploration;
        private RenderTexture _mapTexture;
        private Texture2D _fogTexture;
        private Color32[] _fogPixels = System.Array.Empty<Color32>();
        private Texture2D _frameTexture;
        private GUIStyle _buttonStyle;
        private GUIStyle _hintStyle;
        private bool _isExpanded;
        private float _expandedViewSize = ExpandedOrthographicSize;
        private Vector2 _expandedPanOffset;
        private Vector2 _lastDragPosition;
        private int _renderWidth;
        private int _renderHeight;
        private bool _fogDirty = true;
        private Vector3 _lastFogPosition = new(float.PositiveInfinity, 0f, float.PositiveInfinity);
        private Vector3 _lastRevealPosition = new(float.PositiveInfinity, 0f, float.PositiveInfinity);
        private float _lastFogOrthographicSize = -1f;

        public bool IsExpanded => _isExpanded;
        public Camera MapCamera => _mapCamera;
        public float ExpandedViewSize => _expandedViewSize;
        public Vector2 ExpandedPanOffset => _expandedPanOffset;

        public void ToggleExpanded()
        {
            _isExpanded = !_isExpanded;
            _expandedViewSize = ExpandedOrthographicSize;
            _expandedPanOffset = Vector2.zero;
            _fogDirty = true;
        }

        public void ZoomExpandedMap(float scrollDelta)
        {
            if (!_isExpanded || float.IsNaN(scrollDelta) || float.IsInfinity(scrollDelta))
            {
                return;
            }

            _expandedViewSize = Mathf.Clamp(
                _expandedViewSize * Mathf.Pow(ZoomStep, scrollDelta),
                MinimumExpandedOrthographicSize,
                MaximumExpandedOrthographicSize);
            _fogDirty = true;
        }

        public void PanExpandedMap(Vector2 worldDelta)
        {
            if (!_isExpanded || !IsFinite(worldDelta.x) || !IsFinite(worldDelta.y))
            {
                return;
            }

            _expandedPanOffset += worldDelta;
            _fogDirty = true;
        }

        private void Awake()
        {
            _frameTexture = Resources.Load<Texture2D>(FrameResourcePath);
            if (_frameTexture == null)
            {
                Debug.LogError($"{nameof(MinimapView)} could not load '{FrameResourcePath}'.", this);
            }
        }

        public void Bind(Transform target, Camera sourceCamera, WorldExplorationMap exploration)
        {
            _target = target;
            _sourceCamera = sourceCamera;
            _exploration = exploration;

            if (_target == null || _sourceCamera == null || _exploration == null)
            {
                Debug.LogError(
                    $"{nameof(MinimapView)} requires a player target, source camera, and exploration state.",
                    this);
                enabled = false;
                return;
            }

            ExcludePlayerPresentation();
            CreateMapCamera();
            RevealAroundTarget();
            UpdateMapCameraTransform();
            enabled = true;
        }

        private void LateUpdate()
        {
            if (_mapCamera == null || _target == null)
            {
                return;
            }

            UpdateMapCameraTransform();
            RevealAroundTarget();

            Vector3 fogDelta = CurrentMapCenter() - _lastFogPosition;
            fogDelta.y = 0f;
            if (fogDelta.sqrMagnitude >= FogUpdateDistance * FogUpdateDistance
                || !Mathf.Approximately(_lastFogOrthographicSize, _mapCamera.orthographicSize))
            {
                _fogDirty = true;
            }
        }

        private void OnGUI()
        {
            if (_mapCamera == null || _target == null)
            {
                return;
            }

            int previousDepth = GUI.depth;
            GUI.depth = -40;

            Rect frameArea = CalculateFrameArea();
            Rect mapArea = CalculateMapArea(frameArea);
            EnsureRenderTexture(mapArea);
            EnsureFogTexture(mapArea);
            HandleMapInput(mapArea);

            if (Event.current.type == EventType.Repaint && _mapTexture != null)
            {
                _mapCamera.Render();
                UpdateFogMaskIfNeeded();
            }

            if (_isExpanded)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture,
                    ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, .72f), Vector4.zero, Vector4.zero);
            }

            GUI.DrawTexture(mapArea, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                MapBackgroundColor, Vector4.zero, Vector4.zero);
            if (_mapTexture != null)
            {
                GUI.DrawTexture(mapArea, _mapTexture, ScaleMode.StretchToFill, false);
            }

            if (_fogTexture != null)
            {
                GUI.DrawTexture(mapArea, _fogTexture, ScaleMode.StretchToFill, true);
            }

            DrawPlayerMarker(mapArea);
            if (_frameTexture != null)
            {
                GUI.DrawTexture(frameArea, _frameTexture, ScaleMode.StretchToFill, true);
            }

            if (_isExpanded)
            {
                EnsureHintStyle();
                GUI.Label(new Rect(frameArea.x + 90f, frameArea.yMax - 38f, frameArea.width - 180f, 22f),
                    "SCROLL TO ZOOM  ·  DRAG TO PAN", _hintStyle);
            }

            EnsureButtonStyle();
            string label = _isExpanded ? "CLOSE" : "EXPAND";
            var buttonArea = new Rect(frameArea.xMax - 91f, frameArea.y + 13f, 72f, 25f);
            if (GUI.Button(buttonArea, label, _buttonStyle))
            {
                ToggleExpanded();
            }

            GUI.depth = previousDepth;
        }

        private void CreateMapCamera()
        {
            if (_mapCamera != null)
            {
                return;
            }

            var cameraObject = new GameObject(CameraObjectName);
            cameraObject.transform.SetParent(transform, false);
            _mapCamera = cameraObject.AddComponent<Camera>();
            _mapCamera.CopyFrom(_sourceCamera);
            _mapCamera.enabled = false;
            _mapCamera.clearFlags = CameraClearFlags.SolidColor;
            _mapCamera.backgroundColor = MapBackgroundColor;
            _mapCamera.cullingMask &= ~(1 << PlayerPresentationLayer);
            _mapCamera.orthographicSize = CompactOrthographicSize;
        }

        private void UpdateMapCameraTransform()
        {
            float orthographicSize = _isExpanded
                ? _expandedViewSize
                : CompactOrthographicSize;
            _mapCamera.orthographicSize = orthographicSize;
            _mapCamera.transform.rotation = _sourceCamera.transform.rotation;

            // With an oblique orthographic camera, increasing only the size makes the lower viewport rays
            // originate below the terrain plane. Scaling the distance keeps every ray above ground and
            // removes the stationary black band that appeared in the expanded view.
            float distance = Mathf.Max(MinimumCameraDistance, orthographicSize * CameraDistanceScale);
            _mapCamera.transform.position = CurrentMapCenter() - _mapCamera.transform.forward * distance;
        }

        private Vector3 CurrentMapCenter()
        {
            if (!_isExpanded)
            {
                return _target.position;
            }

            return _target.position + new Vector3(_expandedPanOffset.x, 0f, _expandedPanOffset.y);
        }

        private void RevealAroundTarget()
        {
            Vector3 delta = _target.position - _lastRevealPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude < FogUpdateDistance * FogUpdateDistance)
            {
                return;
            }

            if (_exploration.Reveal(_target.position, VisionRadius))
            {
                _fogDirty = true;
            }

            _lastRevealPosition = _target.position;
        }

        private void ExcludePlayerPresentation()
        {
            Renderer[] renderers = _target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].gameObject.layer = PlayerPresentationLayer;
            }
        }

        private Rect CalculateFrameArea()
        {
            if (_isExpanded)
            {
                float expandedSize = Mathf.Max(240f, Mathf.Min(Screen.width - 64f, Screen.height - 64f));
                return new Rect(
                    (Screen.width - expandedSize) * .5f,
                    (Screen.height - expandedSize) * .5f,
                    expandedSize,
                    expandedSize);
            }

            float compactSize = Mathf.Clamp(
                Mathf.Min(Screen.width * .22f, Screen.height * .34f),
                190f,
                300f);
            float top = Mathf.Min(CompactTop, Screen.height - compactSize - ScreenMargin);
            return new Rect(Screen.width - compactSize - ScreenMargin, Mathf.Max(ScreenMargin, top),
                compactSize, compactSize);
        }

        private static Rect CalculateMapArea(Rect frameArea)
        {
            // The supplied artwork has asymmetric mechanical rails around its transparent opening.
            float left = frameArea.width * .09f;
            float right = frameArea.width * .09f;
            float top = frameArea.height * .115f;
            float bottom = frameArea.height * .085f;
            return new Rect(
                frameArea.x + left,
                frameArea.y + top,
                frameArea.width - left - right,
                frameArea.height - top - bottom);
        }

        private void EnsureRenderTexture(Rect mapArea)
        {
            int width = Mathf.Clamp(Mathf.CeilToInt(mapArea.width), 64, (int)MaximumRenderTextureSize);
            int height = Mathf.Clamp(Mathf.CeilToInt(mapArea.height), 64, (int)MaximumRenderTextureSize);
            if (_mapTexture != null && width == _renderWidth && height == _renderHeight)
            {
                return;
            }

            ReleaseRenderTexture();
            _mapTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
            {
                name = "Minimap Render Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _mapTexture.Create();
            _mapCamera.targetTexture = _mapTexture;
            _mapCamera.aspect = width / (float)height;
            _renderWidth = width;
            _renderHeight = height;
        }

        private void EnsureFogTexture(Rect mapArea)
        {
            int width = Mathf.Clamp(Mathf.CeilToInt(mapArea.width * .5f), 64, MaximumFogTextureSize);
            int height = Mathf.Clamp(Mathf.CeilToInt(mapArea.height * .5f), 64, MaximumFogTextureSize);
            if (_fogTexture != null && _fogTexture.width == width && _fogTexture.height == height)
            {
                return;
            }

            ReleaseFogTexture();
            _fogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = "Minimap Exploration Fog",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _fogPixels = new Color32[width * height];
            _fogDirty = true;
        }

        private void UpdateFogMaskIfNeeded()
        {
            if (!_fogDirty || _fogTexture == null || _exploration == null)
            {
                return;
            }

            if (!TryGetGroundPoint(0f, 0f, out Vector3 bottomLeft)
                || !TryGetGroundPoint(1f, 0f, out Vector3 bottomRight)
                || !TryGetGroundPoint(0f, 1f, out Vector3 topLeft))
            {
                for (int i = 0; i < _fogPixels.Length; i++)
                {
                    _fogPixels[i] = HiddenFogColor;
                }
            }
            else
            {
                Vector3 horizontal = bottomRight - bottomLeft;
                Vector3 vertical = topLeft - bottomLeft;
                int width = _fogTexture.width;
                int height = _fogTexture.height;
                int index = 0;
                for (int y = 0; y < height; y++)
                {
                    float v = (y + .5f) / height;
                    Vector3 rowStart = bottomLeft + vertical * v;
                    for (int x = 0; x < width; x++)
                    {
                        float u = (x + .5f) / width;
                        Vector3 worldPosition = rowStart + horizontal * u;
                        _fogPixels[index++] = _exploration.IsExplored(worldPosition.x, worldPosition.z)
                            ? RevealedFogColor
                            : HiddenFogColor;
                    }
                }
            }

            _fogTexture.SetPixels32(_fogPixels);
            _fogTexture.Apply(false, false);
            _lastFogPosition = CurrentMapCenter();
            _lastFogOrthographicSize = _mapCamera.orthographicSize;
            _fogDirty = false;
        }

        private void HandleMapInput(Rect mapArea)
        {
            if (!_isExpanded)
            {
                return;
            }

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(nameof(MinimapView).GetHashCode(), FocusType.Passive, mapArea);
            EventType eventType = current.GetTypeForControl(controlId);

            if (eventType == EventType.ScrollWheel && mapArea.Contains(current.mousePosition))
            {
                bool hasAnchor = TryGetGroundPoint(mapArea, current.mousePosition, out Vector3 anchorBeforeZoom);
                ZoomExpandedMap(current.delta.y);
                UpdateMapCameraTransform();
                if (hasAnchor
                    && TryGetGroundPoint(mapArea, current.mousePosition, out Vector3 anchorAfterZoom))
                {
                    Vector3 correction = anchorBeforeZoom - anchorAfterZoom;
                    PanExpandedMap(new Vector2(correction.x, correction.z));
                    UpdateMapCameraTransform();
                }

                current.Use();
                return;
            }

            if (eventType == EventType.MouseDown && current.button == 0 && mapArea.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                _lastDragPosition = current.mousePosition;
                current.Use();
                return;
            }

            if (eventType == EventType.MouseDrag && GUIUtility.hotControl == controlId && current.button == 0)
            {
                if (TryGetGroundPoint(mapArea, _lastDragPosition, out Vector3 previousWorldPosition)
                    && TryGetGroundPoint(mapArea, current.mousePosition, out Vector3 currentWorldPosition))
                {
                    Vector3 movement = previousWorldPosition - currentWorldPosition;
                    PanExpandedMap(new Vector2(movement.x, movement.z));
                    UpdateMapCameraTransform();
                }

                _lastDragPosition = current.mousePosition;
                current.Use();
                return;
            }

            if (eventType == EventType.MouseUp && GUIUtility.hotControl == controlId && current.button == 0)
            {
                GUIUtility.hotControl = 0;
                current.Use();
            }
        }

        private bool TryGetGroundPoint(Rect mapArea, Vector2 guiPosition, out Vector3 point)
        {
            float viewportX = Mathf.InverseLerp(mapArea.xMin, mapArea.xMax, guiPosition.x);
            float viewportY = 1f - Mathf.InverseLerp(mapArea.yMin, mapArea.yMax, guiPosition.y);
            return TryGetGroundPoint(viewportX, viewportY, out point);
        }

        private bool TryGetGroundPoint(float viewportX, float viewportY, out Vector3 point)
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            Ray ray = _mapCamera.ViewportPointToRay(new Vector3(viewportX, viewportY));
            if (ground.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private void DrawPlayerMarker(Rect mapArea)
        {
            Vector3 viewportPosition = _mapCamera.WorldToViewportPoint(_target.position);
            if (viewportPosition.z <= 0f
                || viewportPosition.x < 0f || viewportPosition.x > 1f
                || viewportPosition.y < 0f || viewportPosition.y > 1f)
            {
                return;
            }

            var center = new Vector2(
                mapArea.x + viewportPosition.x * mapArea.width,
                mapArea.y + (1f - viewportPosition.y) * mapArea.height);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, center);
            GUI.DrawTexture(new Rect(center.x - 9f, center.y - 9f, 18f, 18f), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0f, MarkerOutlineColor, Vector4.zero, new Vector4(3f, 3f, 3f, 3f));
            GUI.DrawTexture(new Rect(center.x - 6f, center.y - 6f, 12f, 12f), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0f, MarkerColor, Vector4.zero, new Vector4(2f, 2f, 2f, 2f));
            GUI.matrix = previousMatrix;
        }

        private void EnsureButtonStyle()
        {
            if (_buttonStyle != null)
            {
                return;
            }

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _buttonStyle.normal.textColor = new Color(.55f, .95f, 1f);
            _buttonStyle.hover.textColor = Color.white;
        }

        private void EnsureHintStyle()
        {
            if (_hintStyle != null)
            {
                return;
            }

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            _hintStyle.normal.textColor = new Color(.5f, .86f, .92f);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnDestroy()
        {
            ReleaseRenderTexture();
            ReleaseFogTexture();
        }

        private void ReleaseRenderTexture()
        {
            if (_mapCamera != null)
            {
                _mapCamera.targetTexture = null;
            }

            if (_mapTexture == null)
            {
                return;
            }

            _mapTexture.Release();
            if (Application.isPlaying)
            {
                Destroy(_mapTexture);
            }
            else
            {
                DestroyImmediate(_mapTexture);
            }

            _mapTexture = null;
            _renderWidth = 0;
            _renderHeight = 0;
        }

        private void ReleaseFogTexture()
        {
            if (_fogTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_fogTexture);
            }
            else
            {
                DestroyImmediate(_fogTexture);
            }

            _fogTexture = null;
            _fogPixels = System.Array.Empty<Color32>();
        }
    }
}
