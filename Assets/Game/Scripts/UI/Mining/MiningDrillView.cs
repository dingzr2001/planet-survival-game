using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Mining.Domain;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Movement;
using PlanetSurvival.UI.Inventory;
using UnityEngine;

namespace PlanetSurvival.UI.Mining
{
    /// <summary>Small operations panel for hand-loading petroleum and collecting buffered ore.</summary>
    [DisallowMultipleComponent]
    public sealed class MiningDrillView : MonoBehaviour
    {
        private const float PanelWidth = 560f;
        private const float PanelHeight = 420f;

        private MiningDrill _drill;
        private PlayerInventory _playerInventory;
        private PlanarPlayerMotor _playerMotor;
        private PlayerInteractor _playerInteractor;
        private InventoryView _inventoryView;
        private bool _restoreMotor;
        private bool _restoreInteractor;
        private bool _restoreInventoryView;
        private string _feedback = string.Empty;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _detailStyle;

        public bool IsOpen { get; private set; }

        public void Open(MiningDrill drill, PlayerInventory playerInventory,
            PlanarPlayerMotor playerMotor = null, PlayerInteractor playerInteractor = null)
        {
            if (drill == null || playerInventory == null)
            {
                Debug.LogError($"{nameof(MiningDrillView)} needs a drill and the player backpack.", this);
                return;
            }

            Close();
            _drill = drill;
            _playerInventory = playerInventory;
            _feedback = $"Load petroleum fuel or collect finished {_drill.OutputItem.DisplayName.ToLowerInvariant()}.";
            CaptureAndLockControls(playerMotor, playerInteractor);
            IsOpen = true;
        }

        public int LoadPetroleum(int requestedItems)
        {
            if (_drill == null || _playerInventory == null)
            {
                return 0;
            }

            int loaded = _drill.LoadPetroleumItems(requestedItems, _playerInventory.Inventory);
            _feedback = loaded > 0
                ? $"Loaded {loaded} petroleum canister(s)."
                : "No petroleum was loaded; check the backpack and tank space.";
            if (loaded > 0)
            {
                _playerInventory.RefreshQuickBarAssignments();
            }

            return loaded;
        }

        public int CollectOre(int requestedItems)
        {
            if (_drill == null || _playerInventory == null)
            {
                return 0;
            }

            int collected = _drill.CollectOre(requestedItems, _playerInventory.Inventory);
            _feedback = collected > 0
                ? $"Collected {collected} {_drill.OutputItem.DisplayName.ToLowerInvariant()}."
                : "No output was collected; check the machine and backpack capacity.";
            if (collected > 0)
            {
                _playerInventory.RefreshQuickBarAssignments();
            }

            return collected;
        }

        public void Close()
        {
            IsOpen = false;
            ReleaseControls();
            _drill = null;
            _playerInventory = null;
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            ReleaseControls();
        }

        private void OnGUI()
        {
            if (!IsOpen || _drill == null || _playerInventory == null)
            {
                return;
            }

            EnsureStyles();
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .68f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float width = Mathf.Min(PanelWidth, Screen.width - 24f);
            float height = Mathf.Min(PanelHeight, Screen.height - 24f);
            var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            PanelBackground.Draw(panel, new Color(.055f, .045f, .025f, .98f));

            GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 28f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(_drill.Definition.DisplayName.ToUpperInvariant(), _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE", GUILayout.Width(76f), GUILayout.Height(26f)))
            {
                Close();
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                return;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(14f);

            GUILayout.Label("MACHINE", _sectionStyle);
            GUILayout.Label(StateDescription(), _detailStyle);
            DrawMeter("OUTPUT STORAGE", _drill.StoredOre, _drill.Definition.OreCapacity, new Color(.72f, .28f, .12f));
            DrawMeter("PETROLEUM", _drill.StoredPetroleum, _drill.Definition.PetroleumCapacity, new Color(.78f, .58f, .16f));
            DrawMeter("ELECTRIC BUFFER", _drill.StoredElectricity, _drill.Definition.ElectricityCapacity,
                new Color(.3f, .7f, 1f));
            GUILayout.Space(10f);

            int petroleumInBackpack = _playerInventory.Inventory.GetQuantity(_drill.Definition.PetroleumItem.ItemId);
            GUILayout.Label($"BACKPACK PETROLEUM: {petroleumInBackpack}", _sectionStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = petroleumInBackpack > 0 && _drill.RemainingPetroleumCapacity >= _drill.Definition.PetroleumPerItem;
            if (GUILayout.Button("LOAD 1", GUILayout.Height(34f))) LoadPetroleum(1);
            if (GUILayout.Button("LOAD ALL", GUILayout.Height(34f))) LoadPetroleum(petroleumInBackpack);
            GUI.enabled = _drill.StoredOre > 0;
            if (GUILayout.Button("TAKE 1", GUILayout.Height(34f))) CollectOre(1);
            if (GUILayout.Button("TAKE ALL", GUILayout.Height(34f))) CollectOre(_drill.StoredOre);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(12f);
            GUILayout.Label(_feedback, _detailStyle);
            GUILayout.EndArea();
        }

        private string StateDescription()
        {
            return _drill.State switch
            {
                MiningDrillState.StorageFull => "Stopped: output storage is full.",
                MiningDrillState.Producing =>
                    $"Producing {_drill.Definition.ProductionPerSecond:0.##} items/s. Electricity is used before petroleum.",
                _ => "Stopped: connect electricity or load petroleum."
            };
        }

        private static void DrawMeter(string label, float value, float capacity, Color color)
        {
            GUILayout.Label($"{label}  {value:0.##} / {capacity:0.##}");
            Rect meter = GUILayoutUtility.GetRect(10f, 17f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(meter, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(.1f, .09f, .07f), Vector4.zero, new Vector4(4f, 4f, 4f, 4f));
            float normalized = capacity > 0f ? Mathf.Clamp01(value / capacity) : 0f;
            if (normalized <= 0f)
            {
                return;
            }

            var fill = new Rect(meter.x + 2f, meter.y + 2f, (meter.width - 4f) * normalized, meter.height - 4f);
            GUI.DrawTexture(fill, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                color, Vector4.zero, new Vector4(3f, 3f, 3f, 3f));
        }

        private void CaptureAndLockControls(PlanarPlayerMotor playerMotor, PlayerInteractor playerInteractor)
        {
            _playerMotor = playerMotor;
            _playerInteractor = playerInteractor;
            _inventoryView = GetComponent<InventoryView>();
            _restoreMotor = _playerMotor != null && _playerMotor.enabled;
            _restoreInteractor = _playerInteractor != null && _playerInteractor.enabled;
            _restoreInventoryView = _inventoryView != null && _inventoryView.enabled;
            if (_playerMotor != null) _playerMotor.enabled = false;
            if (_playerInteractor != null) _playerInteractor.enabled = false;
            if (_inventoryView != null) _inventoryView.enabled = false;
        }

        private void ReleaseControls()
        {
            if (_playerMotor != null) _playerMotor.enabled = _restoreMotor;
            if (_playerInteractor != null) _playerInteractor.enabled = _restoreInteractor;
            if (_inventoryView != null) _inventoryView.enabled = _restoreInventoryView;
            _playerMotor = null;
            _playerInteractor = null;
            _inventoryView = null;
            _restoreMotor = false;
            _restoreInteractor = false;
            _restoreInventoryView = false;
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = new Color(1f, .78f, .28f);
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            _sectionStyle.normal.textColor = new Color(.92f, .69f, .32f);
            _detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _detailStyle.normal.textColor = new Color(.82f, .82f, .78f);
        }
    }
}
