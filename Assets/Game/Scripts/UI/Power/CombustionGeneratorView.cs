using System.Collections.Generic;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.Power.Definitions;
using PlanetSurvival.Power.Domain;
using PlanetSurvival.Transport.Runtime;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI.Power
{
    /// <summary>Four item slots around a combustion display; each slot opens a source or destination picker.</summary>
    [DisallowMultipleComponent]
    public sealed class CombustionGeneratorView : MonoBehaviour
    {
        private enum Slot { None, Fuel, Oxygen, PrimaryOutput, SecondaryOutput }

        private const float PanelWidth = 960f;
        private const float PanelHeight = 590f;
        private const float PopupWidth = 286f;
        private const float PopupHeight = 276f;
        private const string FlameEffectResourcePath = "Power/CombustionFlameEffect";
        private const string GlowTextureResourcePath = "Power/CombustionGlowParticle";
        private static readonly Color Gold = new(1f, .72f, .27f);
        private static readonly Color Muted = new(.79f, .78f, .72f);

        private BuildSite _site;
        private ItemTransferSystem _transferSystem;
        private PlayerInventory _inventory;
        private PlanarPlayerMotor _motor;
        private PlayerInteractor _interactor;
        private InventoryView _inventoryView;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private bool _restoreInventoryView;
        private Slot _openSlot;
        private string _expandedSourceId = string.Empty;
        private Vector2 _pickerScroll;
        private string _feedback = string.Empty;
        private bool _wasBurning;
        private List<ItemDefinition> _outputItems;
        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _chemical;
        private Texture2D _hydrogenTexture;
        private Texture2D _waterTexture;
        private Texture2D _glowTexture;
        private GameObject _flameEffectPrefab;
        private CombustionFlamePreview _flamePreview;

        public bool IsOpen { get; private set; }

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
            CaptureAndLockControls(player);
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            ReleaseControls();
            _site = null;
            _transferSystem = null;
            _inventory = null;
            _outputItems = null;
            _openSlot = Slot.None;
            _wasBurning = false;
            _flamePreview?.SetBurning(false);
        }

        private void OnDisable() => Close();
        private void OnDestroy()
        {
            Close();
            _flamePreview?.Dispose();
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
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
            EnsureStyles();
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .7f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;

            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            Rect panel = new((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            PanelBackground.Draw(panel, new Color(.065f, .052f, .037f, .98f));
            CombustionGenerator generator = _site.CombustionGenerator;
            if (GUI.Button(new Rect(panel.xMax - 88f, panel.y + 17f, 68f, 28f), "CLOSE")) { Close(); return; }
            SpriteIcon.Draw(new Rect(panel.x + 19f, panel.y + 15f, 32f, 32f), _site.Definition.MenuIcon);
            GUI.Label(new Rect(panel.x + 60f, panel.y + 16f, width - 160f, 28f),
                "COMBUSTION GENERATOR", _title);
            GUI.Label(new Rect(panel.x + 22f, panel.y + 51f, width - 44f, 38f), StateDescription(generator), _small);

            float contentTop = panel.y + 98f;
            float contentBottom = panel.yMax - 72f;
            float contentHeight = contentBottom - contentTop;
            float leftWidth = width * .275f;
            float middleWidth = width * .41f;
            float rightX = panel.x + leftWidth + middleWidth;
            DrawDivider(panel.x + leftWidth, contentTop, contentBottom);
            DrawDivider(rightX, contentTop, contentBottom);

            List<ItemDefinition> outputs = _outputItems;
            ItemDefinition firstOutput = outputs.Count > 0 ? outputs[0] : null;
            ItemDefinition secondOutput = outputs.Count > 1 ? outputs[1] : null;
            float slotSize = Mathf.Clamp(Mathf.Min(width * .12f, contentHeight * .26f), 50f, 110f);
            float leftX = panel.x + (leftWidth - slotSize) * .5f;
            float rightSlotX = rightX + (width - leftWidth - middleWidth - slotSize) * .5f;
            Rect fuelRect = new(leftX, contentTop + 35f, slotSize, slotSize);
            Rect oxygenRect = new(leftX, contentBottom - slotSize - 46f, slotSize, slotSize);
            Rect primaryRect = new(rightSlotX, contentTop + contentHeight * .2f, slotSize, slotSize);
            Rect secondaryRect = new(rightSlotX, contentBottom - slotSize - 46f, slotSize, slotSize);

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

            DrawFire(new Rect(panel.x + leftWidth + 22f, contentTop + 52f, middleWidth - 44f,
                Mathf.Min(232f, contentHeight * .55f)), generator);
            CombustionFuelRecipe recipe = default;
            bool hasRecipe = generator.FuelItem != null &&
                generator.Definition.TryGetRecipe(generator.FuelItem, out recipe);
            float power = generator.CurrentPowerWatts;
            GUI.Label(new Rect(rightX + 13f, contentTop + 13f, width - (rightX - panel.x) - 26f, 33f),
                $"⚡ POWER {power:0.##} W", _title);
            if (hasRecipe)
                GUI.Label(new Rect(rightX + 16f, contentTop + 48f, width - (rightX - panel.x) - 28f, 42f),
                    $"{recipe.ConversionEfficiency:P0} efficiency", _small);
            GUI.Label(new Rect(panel.x + 24f, panel.yMax - 58f, width - 48f, 24f), _feedback, _small);
            GUI.Label(new Rect(panel.x + 24f, panel.yMax - 33f, width - 48f, 20f),
                "Unused power is wasted; connect a power pole to use it.", _small);

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
        }

        private void DrawItemSlot(Rect rect, ItemDefinition item, string fallback, int quantity, int capacity, Slot slot)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = _openSlot == slot ? Gold : new Color(.34f, .29f, .23f);
            if (GUI.Button(rect, GUIContent.none))
            {
                _openSlot = _openSlot == slot ? Slot.None : slot;
                _expandedSourceId = string.Empty;
                _pickerScroll = Vector2.zero;
            }
            GUI.backgroundColor = old;
            DrawItemIcon(new Rect(rect.x + 9f, rect.y + 9f, rect.width - 18f, rect.height - 18f), item, fallback);
            GUI.Label(new Rect(rect.x - 22f, rect.yMax + 5f, rect.width + 44f, 22f),
                item?.DisplayName ?? fallback, _label);
            GUI.Label(new Rect(rect.x - 22f, rect.yMax + 26f, rect.width + 44f, 20f),
                $"{quantity} / {capacity}  ▾", _small);
        }

        private void DrawFire(Rect rect, CombustionGenerator generator)
        {
            bool burning = _wasBurning;
            generator.Definition.TryGetRecipe(generator.FuelItem, out CombustionFuelRecipe recipe);
            FillRect(rect, new Color(.025f, .022f, .02f, 1f));
            DrawFrame(rect, new Color(.42f, .28f, .14f, 1f));
            FillRect(new Rect(rect.x + 1f, rect.y + 32f, rect.width - 2f, 1f),
                new Color(.33f, .24f, .14f, 1f));
            GUI.Label(new Rect(rect.x + 12f, rect.y + 5f, rect.width - 24f, 22f),
                "REACTION CHAMBER", _small);

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
            FillRect(new Rect(rect.x + 35f, grateY, rect.width - 70f, 3f),
                burning ? new Color(.75f, .34f, .1f, 1f) : new Color(.28f, .21f, .16f, 1f));
            for (int i = 0; i < 7; i++)
            {
                float x = rect.x + 44f + i * (rect.width - 88f) / 6f;
                FillRect(new Rect(x, grateY - 3f, 2f, 9f), new Color(.37f, .27f, .18f, 1f));
            }

            Rect indicator = new(rect.x + 13f, rect.yMax - 32f, 7f, 7f);
            FillRect(indicator, burning ? Gold : Muted);
            GUI.Label(new Rect(rect.x + 27f, rect.yMax - 37f, rect.width - 39f, 20f),
                burning ? $"BURNING  ·  {generator.BurnProgressSeconds:0.#} / {recipe.BurnSeconds:0.#} s"
                    : "STANDBY", _small);
            Rect progressTrack = new(rect.x + 12f, rect.yMax - 12f, rect.width - 24f, 3f);
            FillRect(progressTrack, new Color(.23f, .18f, .13f, 1f));
            if (burning && recipe.BurnSeconds > 0f)
            {
                float progress = Mathf.Clamp01(generator.BurnProgressSeconds / recipe.BurnSeconds);
                FillRect(new Rect(progressTrack.x, progressTrack.y, progressTrack.width * progress,
                    progressTrack.height), Gold);
            }
        }

        private static void DrawFrame(Rect rect, Color color)
        {
            FillRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            FillRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            FillRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            FillRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static void FillRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawPicker(Rect panel, Rect anchor, ItemDefinition outputItem)
        {
            bool isInput = _openSlot == Slot.Fuel || _openSlot == Slot.Oxygen;
            float x = isInput ? anchor.xMax + 12f : anchor.x - PopupWidth - 12f;
            float y = Mathf.Clamp(anchor.y - 12f, panel.y + 76f, panel.yMax - PopupHeight - 12f);
            Rect popup = new(x, y, PopupWidth, PopupHeight);
            PanelBackground.Draw(popup, new Color(.11f, .095f, .075f, .99f));
            GUILayout.BeginArea(new Rect(popup.x + 10f, popup.y + 9f, popup.width - 20f, popup.height - 18f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(isInput ? "SELECT INPUT" : $"SEND {outputItem?.DisplayName.ToUpperInvariant()}", _label);
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
                label + suffix, _small);
            if (choices.Count == 1)
            {
                DrawItemIcon(new Rect(row.x + 143f, row.y + 5f, 24f, 24f), choices[0], "?");
                GUI.Label(new Rect(row.x + 171f, row.y + 4f, row.width - 174f, 27f),
                    choices[0].DisplayName, _small);
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
                            item.DisplayName, _small);
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
                $"BACKPACK  #01  · take {generator.Byproducts.GetQuantity(item.ItemId)}", _small);
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
                    _small);
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
            else GUI.Label(rect, ChemicalName(item, fallback), _chemical);
        }

        private void DrawSourceIcon(Rect rect, Sprite icon, string fallback)
        {
            if (icon != null) SpriteIcon.Draw(rect, icon);
            else GUI.Label(rect, fallback, _chemical);
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

        private static void DrawDivider(float x, float top, float bottom)
        {
            Color old = GUI.color;
            GUI.color = new Color(1f, .75f, .36f, .35f);
            GUI.DrawTexture(new Rect(x, top, 1f, bottom - top), Texture2D.whiteTexture);
            GUI.color = old;
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

        private void CaptureAndLockControls(GameObject player)
        {
            _motor = player.GetComponent<PlanarPlayerMotor>();
            _interactor = player.GetComponent<PlayerInteractor>();
            _inventoryView = GetComponent<InventoryView>();
            _restoreMotor = _motor != null && _motor.enabled;
            _restoreInteractor = _interactor != null && _interactor.enabled;
            _restoreInventoryView = _inventoryView != null && _inventoryView.enabled;
            if (_motor != null) _motor.enabled = false;
            if (_interactor != null) _interactor.enabled = false;
            if (_inventoryView != null) _inventoryView.enabled = false;
        }

        private void ReleaseControls()
        {
            if (_motor != null) _motor.enabled = _restoreMotor;
            if (_interactor != null) _interactor.enabled = _restoreInteractor;
            if (_inventoryView != null) _inventoryView.enabled = _restoreInventoryView;
            _motor = null;
            _interactor = null;
            _inventoryView = null;
            _restoreMotor = false;
            _restoreInteractor = false;
            _restoreInventoryView = false;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Gold;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter };
            _label.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true,
                alignment = TextAnchor.MiddleLeft };
            _small.normal.textColor = Muted;
            _chemical = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter };
            _chemical.normal.textColor = Gold;
        }
    }
}
