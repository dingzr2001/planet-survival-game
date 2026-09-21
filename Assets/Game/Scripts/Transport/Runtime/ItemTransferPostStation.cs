using PlanetSurvival.Building.Domain;
using PlanetSurvival.Transport.Domain;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Transport.Runtime
{
    /// <summary>World presentation for a transfer post, including its colour insert and group number.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class ItemTransferPostStation : MonoBehaviour
    {
        private BuildSite _site;
        private SpriteRenderer _colorLayer;
        private SpriteRenderer _bodyRenderer;
        private TextMesh _groupLabel;

        public BuildSite Site => _site;
        public ItemTransferPost Post => _site?.ItemTransferPost;
        public string EndpointId => _site == null ? string.Empty : ItemTransferSystem.PostEndpointId(_site.SiteId);

        public void Bind(BuildSite site, Transform body)
        {
            _site = site;
            if (site?.ItemTransferPost == null || body == null)
            {
                Debug.LogError($"{nameof(ItemTransferPostStation)} requires a transfer-post site and body.", this);
                enabled = false;
                return;
            }

            WorldSpriteView spriteView = body.GetComponentInChildren<WorldSpriteView>(true);
            Transform visual = spriteView != null ? spriteView.transform : body;
            _bodyRenderer = spriteView != null ? spriteView.Renderer : visual.GetComponent<SpriteRenderer>();
            float visualScale = Mathf.Max(.001f, Mathf.Abs(visual.lossyScale.x));

            if (site.Definition.TransferColorMask != null)
            {
                var layerObject = new GameObject("Group Color Mask");
                layerObject.transform.SetParent(visual, false);
                _colorLayer = layerObject.AddComponent<SpriteRenderer>();
                _colorLayer.sprite = site.Definition.TransferColorMask;
                _colorLayer.sortingOrder = -1;
            }

            var labelObject = new GameObject("Post Number");
            labelObject.transform.SetParent(visual, false);
            labelObject.transform.localPosition = new Vector3(0f, .28f / visualScale, -.012f);
            _groupLabel = labelObject.AddComponent<TextMesh>();
            _groupLabel.anchor = TextAnchor.MiddleCenter;
            _groupLabel.alignment = TextAlignment.Center;
            _groupLabel.fontSize = 48;
            _groupLabel.characterSize = .035f / visualScale;
            _groupLabel.fontStyle = FontStyle.Bold;
            MeshRenderer labelRenderer = _groupLabel.GetComponent<MeshRenderer>();
            labelRenderer.sortingOrder = 2;

            Post.Changed += RefreshPresentation;
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            if (Post == null)
            {
                return;
            }

            if (_colorLayer != null)
            {
                _colorLayer.color = Post.GroupColor;
            }

            if (_groupLabel != null)
            {
                _groupLabel.text = Post.PostNumber.ToString("00");
                _groupLabel.color = ContrastColor(Post.GroupColor);
            }
        }

        private void LateUpdate()
        {
            if (_bodyRenderer == null)
            {
                return;
            }

            if (_colorLayer != null) _colorLayer.sortingOrder = _bodyRenderer.sortingOrder - 1;
            if (_groupLabel != null) _groupLabel.GetComponent<MeshRenderer>().sortingOrder =
                _bodyRenderer.sortingOrder + 1;
        }

        private void OnDestroy()
        {
            if (Post != null)
            {
                Post.Changed -= RefreshPresentation;
            }
        }

        private static Color ContrastColor(Color background)
        {
            float luminance = background.r * .299f + background.g * .587f + background.b * .114f;
            return luminance > .58f ? new Color(.04f, .05f, .06f) : Color.white;
        }

    }
}
