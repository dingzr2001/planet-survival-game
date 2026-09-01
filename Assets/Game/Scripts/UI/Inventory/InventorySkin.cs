using UnityEngine;

namespace PlanetSurvival.UI.Inventory
{
    /// <summary>
    /// Slot artwork shared by the quick bar and the inventory panel. The HUD is created from code at
    /// bootstrap and therefore has no Inspector to receive art through, so the references live in an
    /// asset that the composition root injects instead.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/UI/Inventory Skin", fileName = "InventorySkin")]
    public sealed class InventorySkin : ScriptableObject
    {
        [Header("Slot frame")]
        [SerializeField, Tooltip("Nine-sliced frame drawn behind every slot.")]
        private Texture2D _slotBackground;
        [SerializeField, Min(0), Tooltip("Corner size of the frame in source-texture pixels. These regions are never stretched, so they must stay smaller than half a slot.")]
        private int _slotBorder = 10;
        [SerializeField, Min(0f), Tooltip("Inset between the frame and the item icon.")]
        private float _iconPadding = 7f;

        [Header("Tints")]
        [SerializeField, Tooltip("Applied to a slot that holds an item.")]
        private Color _slotTint = Color.white;
        [SerializeField, Tooltip("Applied to an empty slot so it reads as available but inactive.")]
        private Color _emptySlotTint = new(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color _hoverTint = new(1f, 0.96f, 0.86f, 1f);
        [SerializeField, Tooltip("Outline drawn around the selected slot.")]
        private Color _selectionColor = new(1f, 0.72f, 0.28f, 1f);

        public Texture2D SlotBackground => _slotBackground;
        public int SlotBorder => _slotBorder;
        public float IconPadding => _iconPadding;
        public Color SlotTint => _slotTint;
        public Color EmptySlotTint => _emptySlotTint;
        public Color HoverTint => _hoverTint;
        public Color SelectionColor => _selectionColor;

        public void Configure(Texture2D slotBackground, int slotBorder, float iconPadding)
        {
            _slotBackground = slotBackground;
            _slotBorder = Mathf.Max(0, slotBorder);
            _iconPadding = Mathf.Max(0f, iconPadding);
        }
    }
}
