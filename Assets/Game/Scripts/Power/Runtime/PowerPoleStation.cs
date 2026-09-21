using PlanetSurvival.Building.Domain;
using PlanetSurvival.Power.Domain;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Power.Runtime
{
    /// <summary>World presentation for a power pole's three power states.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PowerPoleStation : MonoBehaviour
    {
        private BuildSite _site;
        private SpriteRenderer _renderer;
        private WorldSpriteView _spriteView;
        private TextMesh _numberLabel;

        public BuildSite Site => _site;
        public PowerPole Pole => _site?.PowerPole;

        public void Bind(BuildSite site, Transform body)
        {
            _site = site;
            if (site?.PowerPole == null || body == null)
            {
                Debug.LogError($"{nameof(PowerPoleStation)} requires a power-pole site and body.", this);
                enabled = false;
                return;
            }

            _spriteView = body.GetComponentInChildren<WorldSpriteView>(true);
            _renderer = _spriteView != null ? _spriteView.Renderer : body.GetComponent<SpriteRenderer>();
            CreateNumberLabel(body);
            Pole.Changed += RefreshPresentation;
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            if (_renderer == null || Pole == null) return;
            bool hasInput = Pole.LastInputPower > .0001f;
            bool fullyPowered = Pole.OutputEndpointIds.Count > 0 &&
                                Pole.LastPoweredOutputs == Pole.OutputEndpointIds.Count;
            Sprite sprite = !hasInput ? Site.Definition.PowerPoleOfflineSprite : fullyPowered
                ? Site.Definition.PowerPolePoweredSprite
                : Site.Definition.PowerPoleLimitedSprite;
            if (sprite == null)
            {
                return;
            }

            // WorldSpriteView restores its cached idle frame every LateUpdate. Reconfiguring it here makes
            // the selected power-state sprite that cached frame instead of a one-frame renderer override.
            if (_spriteView != null)
            {
                _spriteView.ConfigureGrounded(sprite, _spriteView.DisplayHeight);
            }
            else
            {
                _renderer.sprite = sprite;
            }
        }

        private void OnDestroy()
        {
            if (Pole != null) Pole.Changed -= RefreshPresentation;
        }

        private void CreateNumberLabel(Transform body)
        {
            var labelObject = new GameObject("Power Pole Number");
            Transform visual = _spriteView != null ? _spriteView.transform : body;
            float visualScale = Mathf.Max(.001f, Mathf.Abs(visual.lossyScale.x));
            labelObject.transform.SetParent(visual, false);
            labelObject.transform.localPosition = new Vector3(0f, .15f / visualScale, -.012f);
            _numberLabel = labelObject.AddComponent<TextMesh>();
            _numberLabel.text = $"{Pole.PoleNumber:00}";
            _numberLabel.anchor = TextAnchor.MiddleCenter;
            _numberLabel.alignment = TextAlignment.Center;
            _numberLabel.fontSize = 44;
            _numberLabel.characterSize = .025f / visualScale;
            _numberLabel.fontStyle = FontStyle.Bold;
            _numberLabel.color = Color.white;
            _numberLabel.GetComponent<MeshRenderer>().sortingOrder = 2;
        }
    }
}
