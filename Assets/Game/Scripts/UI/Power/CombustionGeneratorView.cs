using System.Collections.Generic;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Power.Definitions;
using PlanetSurvival.Power.Domain;
using PlanetSurvival.Transport.Runtime;
using UnityEngine;

namespace PlanetSurvival.UI.Power
{
    /// <summary>Four item slots around a combustion display; each slot opens a source or destination picker.</summary>
    [DisallowMultipleComponent]
    public sealed class CombustionGeneratorView : InteractionPanelView
    {
        private enum Slot { None, Fuel, Oxygen, PrimaryOutput, SecondaryOutput }

        private const float PanelWidth = 960f;
        private const float PanelHeight = 590f;
        private const float HeadlineInset = 52f;
        private const float PopupWidth = 286f;
        private const float PopupHeight = 276f;
        private const string FlameEffectResourcePath = "Power/CombustionFlameEffect";
        private const string GlowTextureResourcePath = "Power/CombustionGlowParticle";
        private static readonly Color Gold = InteractionPanelTheme.Thermal.Accent;
        private static readonly Color Muted = InteractionPanelTheme.Thermal.Muted;

        private BuildSite _site;
        private ItemTransferSystem _transferSystem;
        private PlayerInventory _inventory;
        private Slot _openSlot;
        private string _expandedSourceId = string.Empty;
        private Vector2 _pickerScroll;
        private string _feedback = string.Empty;
        private bool _wasBurning;
        private List<ItemDefinition> _outputItems;
        private Texture2D _hydrogenTexture;
        private Texture2D _waterTexture;
        private Texture2D _glowTexture;
        private GameObject _flameEffectPrefab;
        private CombustionFlamePreview _flamePreview;

        protected override InteractionPanelTheme Theme => InteractionPanelTheme.Thermal;

        private void Awake()
        {
            _hydrogenTexture = Resources.Load<Texture2D>("Hydrogen/Hydrogen");
            _waterTexture = Resources.Load<Texture2D>("Water/WaterBottle");
            _glowTexture = Resources.Load<Texture2D>(GlowTextureResourcePath);
            _flameEffectPrefab = Resources.Load<GameObject>(FlameEffectResourcePath);
            if (_flameEffectPrefab == null)
                Debug.LogError($"{nameof(CombustionGeneratorView)} could not load effect at {FlameEffectResourcePath}.", this);
        }

        public void Open(BuildSite site, ItemTransferSystem transferSystem, PlayerInventory inventory, GameObject player)
        {
            if (site?.CombustionGenerator == null || inventory == null || player == null)
            {
                Debug.LogError($"{nameof(CombustionGeneratorView)} needs a generator, backpack and player.", this);
                return;
            }
            Close();
            _site = site;
            _transferSystem = transferSystem;
            _inventory = inventory;
            _outputItems = GetOutputItems(site.CombustionGenerator);
            _openSlot = Slot.None;
            _wasBurning = false;
            _feedback = "Select an item slot to load supplies or choose a nearby transfer post.";
            if (_flamePreview == null && _flameEffectPrefab != null)
                _flamePreview = CombustionFlamePreview.Create(_flameEffectPrefab, transform);
            BeginSession(player);
        }

        protected override void OnClosed()
        {
            _site = null;
            _transferSystem = null;
            _inventory = null;
            _outputItems = null;
            _openSlot = Slot.None;
            _wasBurning = false;
            _flamePreview?.SetBurning(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _flamePreview?.Dispose();
        }

        protected override void OnOpenUpdate()
        {
            bool burning = _site?.CombustionGenerator?.IsBurning ?? false;
            _wasBurning = burning;
            _flamePreview?.SetBurning(burning);
        }

        private void LateUpdate()
        {
            if (IsOpen) _flamePreview?.Render();
        }

        private void OnGUI()
        {
            if (!IsOpen || _site?.CombustionGenerator == null || _inventory == null) return;
            Rect panel = InteractionPanel.Begin(PanelWidth, PanelHeight, Theme);
            CombustionGenerator generator = _site.CombustionGenerator;
            bool close = InteractionPanel.DrawHeader(panel, _site.Definition.MenuIcon,
                "COMBUSTION GENERATOR", StateDescription(generator), Styles);

            InteractionPanel.MachineColumns columns = InteractionPanel.DrawColumns(panel, Theme);
            float contentTop = columns.Inputs.y;
            float contentHeight = columns.Inputs.height;

            List<ItemDefinition> outputs = _outputItems;
            ItemDefinition firstOutput = outputs.Count > 0 ? outputs[0] : null;
            ItemDefinition secondOutput = outputs.Count > 1 ? outputs[1] : null;
            int outputSlots = secondOutput != null ? 2 : 1;
            float inputSlotSize = InteractionPanel.SlotSizeForStack(panel, columns.Inputs, 2, false);
            float outputSlotSize = InteractionPanel.SlotSizeForStack(
                panel, columns.Outputs, outputSlots, false, HeadlineInset);
            Rect fuelRect = InteractionPanel.StackedSlot(columns.Inputs, 0, 2, inputSlotSize, false);
            Rect oxygenRect = InteractionPanel.StackedSlot(columns.Inputs, 1, 2, inputSlotSize, false);
            Rect primaryRect = InteractionPanel.StackedSlot(
                columns.Outputs, 0, outputSlots, outputSlotSize, false, HeadlineInset);
            Rect secondaryRect = InteractionPanel.StackedSlot(
                columns.Outputs, 1, outputSlots, outputSlotSize, false, HeadlineInset);

            DrawItemSlot(fuelRect, generator.FuelItem ?? FindFuel(generator.FuelInputItemId) ??
                FirstFuel(generator), "FUEL", generator.FuelQuantity, generator.Definition.FuelCapacity, Slot.Fuel);
            DrawItemSlot(oxygenRect, generator.Definition.OxygenItem, "O₂", generator.OxygenQuantity,
                generator.Definition.OxygenCapacity, Slot.Oxygen);
            DrawItemSlot(primaryRect, firstOutput, "OUTPUT", firstOutput == null ? 0 :
                generator.Byproducts.GetQuantity(firstOutput.ItemId), generator.Definition.ByproductCapacity,
                Slot.PrimaryOutput);
            if (secondOutput != null)
                DrawItemSlot(secondaryRect, secondOutput, "OUTPUT", generator.Byproducts.GetQuantity(
                    secondOutput.ItemId), generator.Definition.ByproductCapacity, Slot.SecondaryOutput);

            // The chamber fills its column, so the middle of the panel is never an empty gap.
            DrawFire(new Rect(columns.Process.x + 22f, contentTop + 52f, columns.Process.width - 44f,
                Mathf.Max(160f, contentHeight - 74f)), generator);
            CombustionFuelRecipe recipe = default;
            bool hasRecipe = generator.FuelItem != null &&
                generator.Definition.TryGetRecipe(generator.FuelItem, out recipe);
            InteractionPanel.Headline(columns.Outputs,
                $"⚡ POWER {generator.CurrentPowerWatts:0.##} W", Styles);
            if (hasRecipe)
                GUI.Label(new Rect(columns.Outputs.x + 16f, contentTop + 48f, columns.Outputs.width - 28f, 42f),
                    $"{recipe.ConversionEfficiency:P0} efficiency", Styles.Detail);
            InteractionPanel.DrawFooter(panel, _feedback,
                "Unused power is wasted; connect a power pole to use it.", Styles);

            if (_openSlot != Slot.None)
            {
                Rect anchor = _openSlot switch
                {
                    Slot.Fuel => fuelRect,
                    Slot.Oxygen => oxygenRect,
                    Slot.PrimaryOutput => primaryRect,
                    _ => secondaryRect
                };
                DrawPicker(panel, anchor, _openSlot == Slot.PrimaryOutput ? firstOutput :
                    _openSlot == Slot.SecondaryOutput ? secondOutput : null);
            }

            if (close)
            {
                Close();
            }
        }

        private void DrawItemSlot(Rect rect, ItemDefinition item, string fallback, int quantity, int capacity, Slot slot)
        {
            if (InteractionPanel.SlotButton(rect, _openSlot == slot, Theme))
            {
                _openSlot = _openSlot == slot ? Slot.None : slot;
                _expandedSourceId = string.Empty;
                _pickerScroll = Vector2.zero;
            }

            DrawItemIcon(new Rect(rect.x + 9f, rect.y + 9f, rect.width - 18f, rect.height - 18f), item, fallback);
            InteractionPanel.SlotCaption(rect, item?.DisplayName ?? fallback, $"{quantity} / {capacity}  ▾", Styles);
        }

        private void DrawFire(Rect rect, CombustionGenerator generator)
        {
            bool burning = _wasBurning;
            generator.Definition.TryGetRecipe(generator.FuelItem, out CombustionFuelRecipe recipe);
            InteractionPanel.Chamber(rect, "REACTION CHAMBER", Styles, Theme);

            float size = Mathf.Min(168f, rect.height - 62f);
            Rect flameRect = new(rect.center.x - size * .5f, rect.y + 27f, size, size);
            if (burning && _glowTexture != null)
            {
                float breath = .88f + .12f * Mathf.Sin(Time.unscaledTime * 7.3f);
                Color old = GUI.color;
                GUI.color = new Color(1f, .31f, .06f, .27f * breath);
                GUI.DrawTexture(new Rect(rect.center.x - 94f, rect.y + 68f, 188f, 112f),
                    _glowTexture, ScaleMode.StretchToFill, true);
                GUI.color = old;
            }
            if (burning && _flamePreview != null)
                GUI.DrawTexture(flameRect, _flamePreview.Texture, ScaleMode.ScaleToFit, true);

            float grateY = rect.yMax - 48f;
            InteractionPanel.Fill(new Rect(rect.x + 35f, grateY, rect.width - 70f, 3f),
                burning ? new Color(.75f, .34f, .1f, 1f) : new Color(.28f, .21f, .16f, 1f));
            for (int i = 0; i < 7; i++)
            {
                float x = rect.x + 44f + i * (rect.width - 88f) / 6f;
                InteractionPanel.Fill(new Rect(x, grateY - 3f, 2f, 9f), new Color(.37f, .27f, .18f, 1f));
            }

            InteractionPanel.ChamberStatus(rect, burning,
                burning ? $"BURNING  ·  {generator.BurnProgressSeconds:0.#} / {recipe.BurnSeconds:0.#} s"
                    : "STANDBY",
                burning && recipe.BurnSeconds > 0f ? generator.BurnProgressSeconds / recipe.BurnSeconds : 0f,
                Styles, Theme);
        }

        private void DrawPicker(Rect panel, Rect anchor, ItemDefinition outputItem)
        {
            bool isInput = _openSlot == Slot.Fuel || _openSlot == Slot.Oxygen;
            float x = isInput ? anchor.xMax + 12f : anchor.x - PopupWidth - 12f;
            float y = Mathf.Clamp(anchor.y - 12f, panel.y + 76f, panel.yMax - PopupHeight - 12f);
            Rect popup = new(x, y, PopupWidth, PopupHeight);
            PanelBackground.Draw(popup, Theme.Popup);
            GUILayout.BeginArea(new Rect(popup.x + 10f, popup.y + 9f, popup.width - 20f, popup.height - 18f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(isInput ? "SELECT INPUT" : $"SEND {outputItem?.DisplayName.ToUpperInvariant()}", Styles.Caption);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", GUILayout.Width(24f))) _openSlot = Slot.None;
            GUILayout.EndHorizontal();
            _pickerScroll = GUILayout.BeginScrollView(_pickerScroll);
            if (isInput) DrawInputChoices();
            else if (outputItem != null) DrawOutputChoices(outputItem);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawInputChoices()
        {
            bool fuel = _openSlot == Slot.Fuel;
            List<ItemDefinition> backpackItems = new();
            if (fuel)
            {
                foreach (CombustionFuelRecipe recipe in _site.CombustionGenerator.Definition.Fuels)
                    if (recipe.Fuel != null && _inventory.Inventory.GetQuantity(recipe.Fuel.ItemId) > 0)
                        backpackItems.Add(recipe.Fuel);
            }
            else
            {
                ItemDefinition oxygen = _site.CombustionGenerator.Definition.OxygenItem;
                if (_inventory.Inventory.GetQuantity(oxygen.ItemId) > 0) backpackItems.Add(oxygen);
            }
            DrawSourceRow("backpack", "BACKPACK  #01", null, backpackItems, true);
            if (_transferSystem == null) return;
            foreach (ItemTransferPostStation station in _transferSystem.GetAdjacentPosts(_site))
            {
                List<ItemDefinition> choices = new();
                foreach (ItemDefinition item in _transferSystem.GetAvailableOutputItems(station))
                    if (item != null && (fuel ? _site.CombustionGenerator.Definition.TryGetRecipe(item, out _) :
                            item.ItemId == _site.CombustionGenerator.Definition.OxygenItem.ItemId))
                        choices.Add(item);
                bool selected = fuel ? _site.CombustionGenerator.FuelInputPostId == station.EndpointId :
                    _site.CombustionGenerator.OxygenInputPostId == station.EndpointId;
                DrawSourceRow(station.EndpointId, $"{(selected ? "✓ " : string.Empty)}POST  #{station.Post.PostNumber:00}",
                    station.Site.Definition.MenuIcon, choices, false, station);
            }
        }

        private void DrawSourceRow(string id, string label, Sprite icon, List<ItemDefinition> choices,
            bool backpack, ItemTransferPostStation station = null)
        {
            Rect row = GUILayoutUtility.GetRect(10f, 35f, GUILayout.ExpandWidth(true));
            bool multiple = choices.Count > 1;
            string suffix = choices.Count == 0 ? (backpack ? "  empty" : "  waiting") :
                multiple ? (_expandedSourceId == id ? "  ▴" : "  ▾") : string.Empty;
            if (GUI.Button(row, GUIContent.none))
            {
                if (multiple) _expandedSourceId = _expandedSourceId == id ? string.Empty : id;
                else if (choices.Count == 1) SelectInput(backpack, station, choices[0]);
                else SelectInput(backpack, station, null);
            }
            DrawSourceIcon(new Rect(row.x + 5f, row.y + 5f, 25f, 25f), icon, backpack ? "B" : "P");
            GUI.Label(new Rect(row.x + 37f, row.y + 4f, multiple ? row.width - 42f : 104f, 27f),
                label + suffix, Styles.Detail);
            if (choices.Count == 1)
            {
                DrawItemIcon(new Rect(row.x + 143f, row.y + 5f, 24f, 24f), choices[0], "?");
                GUI.Label(new Rect(row.x + 171f, row.y + 4f, row.width - 174f, 27f),
                    choices[0].DisplayName, Styles.Detail);
            }
            if (multiple && _expandedSourceId == id)
            {
                foreach (ItemDefinition item in choices)
                {
                    Rect child = GUILayoutUtility.GetRect(10f, 30f, GUILayout.ExpandWidth(true));
                    if (GUI.Button(child, GUIContent.none)) SelectInput(backpack, station, item);
                    DrawItemIcon(new Rect(child.x + 31f, child.y + 3f, 23f, 23f), item, "?");
                    GUI.Label(new Rect(child.x + 62f, child.y + 4f, child.width - 66f, 24f),
                        backpack ? $"{item.DisplayName} ×{_inventory.Inventory.GetQuantity(item.ItemId)}" :
                            item.DisplayName, Styles.Detail);
                }
            }
        }

        private void SelectInput(bool backpack, ItemTransferPostStation station, ItemDefinition item)
        {
            CombustionGenerator generator = _site.CombustionGenerator;
            bool fuel = _openSlot == Slot.Fuel;
            if (backpack)
            {
                if (fuel) generator.ConfigureFuelInput(string.Empty);
                else generator.ConfigureOxygenInput(string.Empty);
                if (item == null) _feedback = "Automatic input disconnected; use the backpack to load items.";
                else
                {
                    int count = _inventory.Inventory.GetQuantity(item.ItemId);
                    int loaded = generator.LoadFromInventory(item, count, _inventory.Inventory);
                    _feedback = loaded > 0 ? $"Loaded {loaded} × {item.DisplayName} from backpack." :
                        "Input is full or a different fuel is still loaded.";
                    if (loaded > 0) _inventory.RefreshQuickBarAssignments();
                }
            }
            else if (station != null)
            {
                foreach (ItemDefinition output in _outputItems)
                    if (generator.GetOutputPostId(output) == station.EndpointId)
                        generator.ConfigureOutputPost(output, string.Empty);
                if (fuel && generator.OxygenInputPostId == station.EndpointId)
                    generator.ConfigureOxygenInput(string.Empty);
                if (!fuel && generator.FuelInputPostId == station.EndpointId)
                    generator.ConfigureFuelInput(string.Empty);
                if (fuel) generator.ConfigureFuelInput(station.EndpointId, item);
                else generator.ConfigureOxygenInput(station.EndpointId);
                _transferSystem.SetInputItem(station, item);
                _transferSystem.SetOutput(station, string.Empty);
                _feedback = $"Connected post #{station.Post.PostNumber:00}" +
                    (item == null ? "; waiting for items." : $" for {item.DisplayName}.");
            }
            _openSlot = Slot.None;
        }

        private void DrawOutputChoices(ItemDefinition item)
        {
            CombustionGenerator generator = _site.CombustionGenerator;
            string chosen = generator.GetOutputPostId(item);
            Rect backpackRow = GUILayoutUtility.GetRect(10f, 35f, GUILayout.ExpandWidth(true));
            if (GUI.Button(backpackRow, GUIContent.none))
            {
                int taken = generator.CollectByproduct(item, generator.Byproducts.GetQuantity(item.ItemId),
                    _inventory.Inventory);
                _feedback = taken > 0 ? $"Collected {taken} × {item.DisplayName}." :
                    "No item to collect, or backpack is full.";
                if (taken > 0) _inventory.RefreshQuickBarAssignments();
                _openSlot = Slot.None;
            }
            DrawSourceIcon(new Rect(backpackRow.x + 5f, backpackRow.y + 5f, 25f, 25f), null, "B");
            GUI.Label(new Rect(backpackRow.x + 37f, backpackRow.y + 4f, backpackRow.width - 42f, 27f),
                $"BACKPACK  #01  · take {generator.Byproducts.GetQuantity(item.ItemId)}", Styles.Detail);
            if (GUILayout.Button(string.IsNullOrEmpty(chosen) ? "✓ NO AUTOMATIC OUTPUT" :
                    "NO AUTOMATIC OUTPUT", GUILayout.Height(31f)))
            {
                generator.ConfigureOutputPost(item, string.Empty);
                _openSlot = Slot.None;
            }
            if (_transferSystem == null) return;
            foreach (ItemTransferPostStation station in _transferSystem.GetAdjacentPosts(_site))
            {
                Rect row = GUILayoutUtility.GetRect(10f, 35f, GUILayout.ExpandWidth(true));
                if (GUI.Button(row, GUIContent.none))
                {
                    if (generator.FuelInputPostId == station.EndpointId)
                        generator.ConfigureFuelInput(string.Empty);
                    if (generator.OxygenInputPostId == station.EndpointId)
                        generator.ConfigureOxygenInput(string.Empty);
                    _transferSystem.SetInput(station, string.Empty);
                    generator.ConfigureOutputPost(item, station.EndpointId);
                    _feedback = $"Sending {item.DisplayName} to post #{station.Post.PostNumber:00}.";
                    _openSlot = Slot.None;
                }
                DrawSourceIcon(new Rect(row.x + 5f, row.y + 5f, 25f, 25f),
                    station.Site.Definition.MenuIcon, "P");
                GUI.Label(new Rect(row.x + 37f, row.y + 4f, row.width - 42f, 27f),
                    $"{(station.EndpointId == chosen ? "✓ " : string.Empty)}POST  #{station.Post.PostNumber:00}",
                    Styles.Detail);
            }
        }

        private static List<ItemDefinition> GetOutputItems(CombustionGenerator generator)
        {
            List<ItemDefinition> items = new();
            foreach (CombustionFuelRecipe recipe in generator.Definition.Fuels)
            {
                if (recipe.PrimaryByproduct != null && !items.Contains(recipe.PrimaryByproduct))
                    items.Add(recipe.PrimaryByproduct);
                if (recipe.SecondaryByproduct != null && !items.Contains(recipe.SecondaryByproduct))
                    items.Add(recipe.SecondaryByproduct);
            }
            return items;
        }

        private ItemDefinition FindFuel(string itemId)
        {
            foreach (CombustionFuelRecipe recipe in _site.CombustionGenerator.Definition.Fuels)
                if (recipe.Fuel != null && recipe.Fuel.ItemId == itemId) return recipe.Fuel;
            return null;
        }

        private static ItemDefinition FirstFuel(CombustionGenerator generator) =>
            generator.Definition.Fuels.Count > 0 ? generator.Definition.Fuels[0].Fuel : null;

        private void DrawItemIcon(Rect rect, ItemDefinition item, string fallback)
        {
            if (item?.Icon != null) SpriteIcon.Draw(rect, item.Icon);
            else if (item != null && item.ItemId.Contains("hydrogen") && _hydrogenTexture != null)
                GUI.DrawTexture(rect, _hydrogenTexture, ScaleMode.ScaleToFit, true);
            else if (item != null && item.ItemId.Contains("water") && _waterTexture != null)
                GUI.DrawTexture(rect, _waterTexture, ScaleMode.ScaleToFit, true);
            else GUI.Label(rect, ChemicalName(item, fallback), Styles.Glyph);
        }

        private void DrawSourceIcon(Rect rect, Sprite icon, string fallback)
        {
            if (icon != null) SpriteIcon.Draw(rect, icon);
            else GUI.Label(rect, fallback, Styles.Glyph);
        }

        private static string ChemicalName(ItemDefinition item, string fallback)
        {
            if (item == null) return fallback;
            string id = item.ItemId.ToLowerInvariant();
            if (id.Contains("methane")) return "CH₄";
            if (id.Contains("hydrogen")) return "H₂";
            if (id.Contains("oxygen")) return "O₂";
            if (id.Contains("water")) return "H₂O";
            if (id.Contains("carbon") || id.Contains("co2")) return "CO₂";
            return item.DisplayName.Length <= 8 ? item.DisplayName : fallback;
        }

        private static string StateDescription(CombustionGenerator generator)
        {
            if (generator.FuelItem == null) return "Stopped: choose fuel and oxygen on the left.";
            if (!generator.Definition.TryGetRecipe(generator.FuelItem, out CombustionFuelRecipe recipe))
                return "Stopped: fuel recipe unavailable.";
            if (generator.FuelQuantity < recipe.FuelItems) return "Stopped: waiting for more fuel.";
            if (generator.OxygenQuantity < recipe.OxygenItems) return "Stopped: waiting for oxygen.";
            if (generator.ByproductQuantity + recipe.PrimaryQuantity + recipe.SecondaryQuantity >
                generator.Definition.ByproductCapacity) return "Stopped: byproduct storage is full.";
            return $"Burning {generator.FuelItem.DisplayName} · {recipe.FuelItems} fuel + {recipe.OxygenItems} O₂ per cycle.";
        }

    }
}
