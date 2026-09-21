using System;
using System.Collections.Generic;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Building.Runtime;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Mining.Domain;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Storage.Runtime;
using PlanetSurvival.Transport.Domain;
using PlanetSurvival.UI.Transport;
using UnityEngine;

namespace PlanetSurvival.Transport.Runtime
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// Resolves configured endpoints, advances item movement, opens posts on right click, and draws their
    /// coloured directional links only while the construction grid is visible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemTransferSystem : MonoBehaviour
    {
        private const string PostPrefix = "post:";
        private const string BuildingPrefix = "building:";
        private const string StoragePrefix = "storage:";
        private const float ScreenSelectionRadius = 52f;
        private const float LinkWidth = .055f;
        private const float LinkHeight = .045f;

        private readonly List<ItemTransferPostStation> _stations = new();
        private readonly Dictionary<ItemTransferPost, float> _outputAllowances = new();
        private readonly List<Vector3> _vertices = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _indices = new();

        private BuildingService _buildings;
        private BuildGridOverlay _gridOverlay;
        private ItemTransferPostView _view;
        private BuildingPlacementController _placement;
        private Transform _player;
        private Camera _camera;
        private Mesh _linkMesh;
        private MeshRenderer _linkRenderer;
        private Material _linkMaterial;

        public static string PostEndpointId(string siteId) => PostPrefix + siteId;

        public void Bind(BuildingService buildings, BuildGridOverlay gridOverlay, ItemTransferPostView view,
            Transform player, BuildingPlacementController placement, Camera targetCamera)
        {
            _buildings = buildings;
            _gridOverlay = gridOverlay;
            _view = view;
            _player = player;
            _placement = placement;
            _camera = targetCamera;
            EnsureLinkPresentation();
        }

        public void Register(ItemTransferPostStation station)
        {
            if (station != null && !_stations.Contains(station))
            {
                _stations.Add(station);
                _outputAllowances.TryAdd(station.Post, 0f);
            }
        }

        public void Unregister(ItemTransferPostStation station)
        {
            if (station == null)
            {
                return;
            }

            if (station.Post != null)
            {
                Disconnect(station.Post);
                _outputAllowances.Remove(station.Post);
            }
            _stations.Remove(station);
        }

        public IReadOnlyList<TransferEndpointOption> GetInputOptions(ItemTransferPostStation station)
        {
            return BuildOptions(station, true);
        }

        public IReadOnlyList<TransferEndpointOption> GetOutputOptions(ItemTransferPostStation station)
        {
            return BuildOptions(station, false);
        }

        public string GetEndpointLabel(string endpointId)
        {
            return TryResolveOption(endpointId, out TransferEndpointOption option) ? option.Label : "Not connected";
        }

        public ItemDefinition GetRouteItem(ItemTransferPostStation station)
        {
            if (station?.Post == null)
            {
                return null;
            }

            ItemTransferPost post = station.Post;
            if (post.BufferedItem != null)
            {
                return post.BufferedItem;
            }

            if (TryResolveSource(post.InputEndpointId, out ITransferSource source) && source.OutputItem != null)
            {
                return source.OutputItem;
            }

            return post.LastMovedItem;
        }

        public bool IsRouteFlowing(ItemTransferPostStation station)
        {
            ItemDefinition item = GetRouteItem(station);
            return item != null && station?.Post != null &&
                   TryResolveSource(station.Post.InputEndpointId, out _) &&
                   TryResolveSink(station.Post.OutputEndpointId, out ITransferSink sink) &&
                   sink.AcceptableQuantity(item, 1) > 0;
        }

        public string GetInputStatus(ItemTransferPostStation station)
        {
            if (station?.Post == null || string.IsNullOrEmpty(station.Post.InputEndpointId))
            {
                return "Input is not configured.";
            }

            if (!TryResolveSource(station.Post.InputEndpointId, out ITransferSource source))
            {
                return "Input unavailable; choose another source.";
            }

            return source.OutputItem == null
                ? $"Waiting for items from {GetEndpointLabel(station.Post.InputEndpointId)}."
                : $"Receiving {source.OutputItem.DisplayName} from {GetEndpointLabel(station.Post.InputEndpointId)}.";
        }

        public string GetOutputStatus(ItemTransferPostStation station)
        {
            if (station?.Post == null || string.IsNullOrEmpty(station.Post.OutputEndpointId))
            {
                return "Output is not configured.";
            }

            ItemTransferPost post = station.Post;
            if (post.BufferedItem == null)
            {
                return $"Waiting for input before sending to {GetEndpointLabel(post.OutputEndpointId)}.";
            }

            if (!TryResolveSink(post.OutputEndpointId, out ITransferSink sink))
            {
                return "Output unavailable; choose another target.";
            }

            return sink.AcceptableQuantity(post.BufferedItem, 1) > 0
                ? $"Sending {post.BufferedItem.DisplayName} to {GetEndpointLabel(post.OutputEndpointId)}."
                : $"Output blocked: {GetEndpointLabel(post.OutputEndpointId)} cannot accept {post.BufferedItem.DisplayName}.";
        }

        /// <summary>Opens the completed post nearest the pointer. Collider hits win; screen proximity is the fallback.</summary>
        public bool TryOpenPostAtScreenPosition(Vector2 screenPosition)
        {
            if (_view == null)
            {
                return false;
            }

            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                return false;
            }

            Ray ray = _camera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, _camera.farClipPlane, ~0,
                QueryTriggerInteraction.Collide);
            ItemTransferPostStation selected = null;
            float nearestHit = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                ItemTransferPostStation candidate = hits[i].collider.GetComponentInParent<ItemTransferPostStation>();
                if (!CanOpen(candidate) || hits[i].distance >= nearestHit)
                {
                    continue;
                }

                selected = candidate;
                nearestHit = hits[i].distance;
            }

            if (selected == null)
            {
                float nearestScreenDistance = ScreenSelectionRadius * ScreenSelectionRadius;
                for (int i = 0; i < _stations.Count; i++)
                {
                    ItemTransferPostStation candidate = _stations[i];
                    if (!CanOpen(candidate))
                    {
                        continue;
                    }

                    Vector3 projected = _camera.WorldToScreenPoint(candidate.transform.position);
                    if (projected.z <= 0f)
                    {
                        continue;
                    }

                    float distance = ((Vector2)projected - screenPosition).sqrMagnitude;
                    if (distance >= nearestScreenDistance)
                    {
                        continue;
                    }

                    selected = candidate;
                    nearestScreenDistance = distance;
                }
            }

            if (selected == null)
            {
                return false;
            }

            _view.Open(selected, this, _player != null ? _player.gameObject : gameObject);
            return true;
        }

        public void SetInput(ItemTransferPostStation station, string endpointId)
        {
            if (station?.Post == null)
            {
                return;
            }

            ItemTransferPost post = station.Post;
            ClearInputInverse(post);
            post.ConfigureInput(endpointId);
            if (TryGetPost(endpointId, out ItemTransferPostStation source))
            {
                ClearOutputInverse(source.Post);
                source.Post.ConfigureOutput(station.EndpointId);
                ApplyGroupToConnectedPosts(post, post.GroupNumber, post.GroupColor);
            }
        }

        public void SetOutput(ItemTransferPostStation station, string endpointId)
        {
            if (station?.Post == null)
            {
                return;
            }

            ItemTransferPost post = station.Post;
            ClearOutputInverse(post);
            post.ConfigureOutput(endpointId);
            if (TryGetPost(endpointId, out ItemTransferPostStation destination))
            {
                ClearInputInverse(destination.Post);
                destination.Post.ConfigureInput(station.EndpointId);
                ApplyGroupToConnectedPosts(post, post.GroupNumber, post.GroupColor);
            }
        }

        public void ApplyGroupToConnectedPosts(ItemTransferPost post, int number, Color color)
        {
            if (post == null)
            {
                return;
            }

            var pending = new Queue<ItemTransferPost>();
            var visited = new HashSet<ItemTransferPost>();
            pending.Enqueue(post);
            while (pending.Count > 0)
            {
                ItemTransferPost current = pending.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                current.ConfigureGroup(number, color);
                EnqueueLinkedPost(current.InputEndpointId, pending);
                EnqueueLinkedPost(current.OutputEndpointId, pending);
            }
        }

        private void Update()
        {
            if (_buildings == null || Time.timeScale <= 0f)
            {
                return;
            }

            AdvanceTransfers(Time.deltaTime);
            if (Input.GetMouseButtonDown(1) && (_placement == null ||
                (!_placement.IsPlacing && !_placement.CancelledPlacementThisFrame)))
            {
                TryOpenPostAtScreenPosition(Input.mousePosition);
            }
        }

        private void LateUpdate()
        {
            RefreshLinks();
        }

        private void OnDestroy()
        {
            if (_linkMaterial != null) Destroy(_linkMaterial);
            if (_linkMesh != null) Destroy(_linkMesh);
        }

        private void AdvanceTransfers(float elapsedSeconds)
        {
            for (int i = _stations.Count - 1; i >= 0; i--)
            {
                ItemTransferPostStation station = _stations[i];
                if (station == null)
                {
                    _stations.RemoveAt(i);
                    continue;
                }

                if (station.Site.State != BuildState.Completed || station.Post == null)
                {
                    continue;
                }

                PullInto(station, elapsedSeconds);

                ItemTransferPost post = station.Post;
                float allowance = Mathf.Min(ItemTransferPost.ItemsPerSecond,
                    _outputAllowances.GetValueOrDefault(post) + ItemTransferPost.ItemsPerSecond * elapsedSeconds);
                int available = Mathf.FloorToInt(allowance + .0001f);
                if (available <= 0 || post.BufferedItem == null ||
                    !TryResolveSink(post.OutputEndpointId, out ITransferSink sink))
                {
                    _outputAllowances[post] = allowance;
                    continue;
                }

                ItemDefinition item = post.BufferedItem;
                int accepted = sink.AcceptableQuantity(item, Mathf.Min(available, post.BufferedQuantity));
                if (accepted <= 0)
                {
                    _outputAllowances[post] = allowance;
                    continue;
                }

                int extracted = post.Extract(accepted);
                int inserted = sink.Insert(item, extracted);
                if (inserted != extracted)
                {
                    Debug.LogError($"Transfer sink accepted {accepted} but inserted {inserted}; {extracted - inserted} item(s) were rejected.", this);
                    post.Insert(item, extracted - inserted);
                }

                if (inserted > 0)
                {
                    allowance -= inserted;
                    post.RecordTransfer(item, inserted);
                }

                _outputAllowances[post] = Mathf.Max(0f, allowance);
            }
        }

        private void PullInto(ItemTransferPostStation station, float elapsedSeconds)
        {
            ItemTransferPost post = station.Post;
            if (!TryResolveSource(post.InputEndpointId, out ITransferSource source) || source.OutputItem == null)
            {
                return;
            }

            ItemDefinition item = source.OutputItem;
            int accepted = post.AcceptableQuantity(item, ItemTransferPost.BufferCapacity);
            if (accepted <= 0)
            {
                return;
            }

            int extracted = source.Extract(accepted, elapsedSeconds);
            int inserted = post.Insert(item, extracted);
            if (inserted != extracted)
            {
                Debug.LogError($"Transfer-post buffer accepted {accepted} but inserted {inserted}; returning rejected items failed.", this);
            }
        }

        private IReadOnlyList<TransferEndpointOption> BuildOptions(ItemTransferPostStation station, bool forInput)
        {
            var options = new List<TransferEndpointOption> { new(string.Empty, "Not connected", false) };
            if (station?.Site == null || _buildings == null)
            {
                return options;
            }

            for (int i = 0; i < _stations.Count; i++)
            {
                ItemTransferPostStation other = _stations[i];
                if (other == null || other == station || other.Site.State != BuildState.Completed)
                {
                    continue;
                }

                options.Add(new TransferEndpointOption(other.EndpointId,
                    $"Transfer post {other.Post.PostNumber:00}", true));
            }

            if (forInput)
            {
                for (int i = 0; i < _buildings.Sites.Count; i++)
                {
                    BuildSite site = _buildings.Sites[i];
                    if (site.State == BuildState.Completed && site.MiningDrill != null && IsAdjacent(station.Site, site))
                    {
                        options.Add(new TransferEndpointOption(BuildingPrefix + site.SiteId,
                            $"Adjacent {site.Definition.DisplayName}", false));
                    }
                }
            }
            else
            {
                for (int i = 0; i < _buildings.Sites.Count; i++)
                {
                    BuildSite site = _buildings.Sites[i];
                    if (site.State == BuildState.Completed &&
                        (site.MiningDrill != null || site.PlanterBox != null) && IsAdjacent(station.Site, site))
                    {
                        options.Add(new TransferEndpointOption(BuildingPrefix + site.SiteId,
                            $"Adjacent {site.Definition.DisplayName}", false));
                    }
                }
            }

            StorageContainer[] storages = FindObjectsByType<StorageContainer>(FindObjectsSortMode.None);
            for (int i = 0; i < storages.Length; i++)
            {
                StorageContainer storage = storages[i];
                if (storage.Inventory == null || HorizontalDistance(station.transform.position, storage.transform.position)
                    > _buildings.Grid.CellSize * 1.5f)
                {
                    continue;
                }

                options.Add(new TransferEndpointOption(StoragePrefix + storage.GetInstanceID(),
                    $"Adjacent {storage.DisplayName}", false));
            }

            return options;
        }

        private bool IsAdjacent(BuildSite postSite, BuildSite buildingSite)
        {
            if (!postSite.QuarterCell.HasValue)
            {
                return false;
            }

            Vector2Int postCell = new(
                Mathf.FloorToInt(postSite.QuarterCell.Value.x / 2f),
                Mathf.FloorToInt(postSite.QuarterCell.Value.y / 2f));
            for (int i = 0; i < buildingSite.Footprint.CellCount; i++)
            {
                Vector2Int offset = buildingSite.Footprint.CellAt(i) - postCell;
                if (Mathf.Abs(offset.x) + Mathf.Abs(offset.y) == 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanOpen(ItemTransferPostStation station)
        {
            return station != null && station.Post != null && station.Site.State == BuildState.Completed &&
                   station.isActiveAndEnabled;
        }

        private bool TryResolveSource(string endpointId, out ITransferSource source)
        {
            if (TryGetPost(endpointId, out ItemTransferPostStation postStation))
            {
                source = new PostEndpoint(postStation.Post);
                return true;
            }

            if (TryGetBuilding(endpointId, out BuildSite site) && site.MiningDrill != null)
            {
                source = new MiningEndpoint(site.MiningDrill);
                return true;
            }

            if (TryGetStorage(endpointId, out StorageContainer storage))
            {
                source = new InventoryEndpoint(storage.Inventory);
                return true;
            }

            source = null;
            return false;
        }

        private bool TryResolveSink(string endpointId, out ITransferSink sink)
        {
            if (TryGetPost(endpointId, out ItemTransferPostStation postStation))
            {
                sink = new PostEndpoint(postStation.Post);
                return true;
            }

            if (TryGetStorage(endpointId, out StorageContainer storage))
            {
                sink = new InventoryEndpoint(storage.Inventory);
                return true;
            }

            if (TryGetBuilding(endpointId, out BuildSite site) &&
                (site.MiningDrill != null || site.PlanterBox != null))
            {
                sink = new BuildingInputEndpoint(site.MiningDrill, site.PlanterBox);
                return true;
            }

            sink = null;
            return false;
        }

        private bool TryResolveOption(string endpointId, out TransferEndpointOption option)
        {
            if (TryGetPost(endpointId, out ItemTransferPostStation post))
            {
                option = new TransferEndpointOption(endpointId, $"Transfer post {post.Post.PostNumber:00}", true);
                return true;
            }

            if (TryGetBuilding(endpointId, out BuildSite site))
            {
                option = new TransferEndpointOption(endpointId, $"Adjacent {site.Definition.DisplayName}", false);
                return true;
            }

            if (TryGetStorage(endpointId, out StorageContainer storage))
            {
                option = new TransferEndpointOption(endpointId, $"Adjacent {storage.DisplayName}", false);
                return true;
            }

            option = default;
            return false;
        }

        private bool TryGetPost(string endpointId, out ItemTransferPostStation station)
        {
            station = null;
            if (string.IsNullOrEmpty(endpointId) || !endpointId.StartsWith(PostPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string siteId = endpointId.Substring(PostPrefix.Length);
            for (int i = 0; i < _stations.Count; i++)
            {
                if (_stations[i] != null && _stations[i].Site.SiteId == siteId)
                {
                    station = _stations[i];
                    return station.Site.State == BuildState.Completed;
                }
            }

            return false;
        }

        private bool TryGetBuilding(string endpointId, out BuildSite site)
        {
            site = null;
            if (_buildings == null || string.IsNullOrEmpty(endpointId) ||
                !endpointId.StartsWith(BuildingPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string siteId = endpointId.Substring(BuildingPrefix.Length);
            for (int i = 0; i < _buildings.Sites.Count; i++)
            {
                if (_buildings.Sites[i].SiteId == siteId)
                {
                    site = _buildings.Sites[i];
                    return site.State == BuildState.Completed;
                }
            }

            return false;
        }

        private static bool TryGetStorage(string endpointId, out StorageContainer storage)
        {
            storage = null;
            if (string.IsNullOrEmpty(endpointId) || !endpointId.StartsWith(StoragePrefix, StringComparison.Ordinal) ||
                !int.TryParse(endpointId.Substring(StoragePrefix.Length), out int instanceId))
            {
                return false;
            }

            StorageContainer[] storages = FindObjectsByType<StorageContainer>(FindObjectsSortMode.None);
            for (int i = 0; i < storages.Length; i++)
            {
                if (storages[i].GetInstanceID() == instanceId)
                {
                    storage = storages[i];
                    return storage.Inventory != null;
                }
            }

            return false;
        }

        private void ClearInputInverse(ItemTransferPost post)
        {
            if (TryGetPost(post.InputEndpointId, out ItemTransferPostStation oldSource) &&
                oldSource.Post.OutputEndpointId == FindEndpoint(post))
            {
                oldSource.Post.ConfigureOutput(string.Empty);
            }
        }

        private void ClearOutputInverse(ItemTransferPost post)
        {
            if (TryGetPost(post.OutputEndpointId, out ItemTransferPostStation oldDestination) &&
                oldDestination.Post.InputEndpointId == FindEndpoint(post))
            {
                oldDestination.Post.ConfigureInput(string.Empty);
            }
        }

        private void Disconnect(ItemTransferPost post)
        {
            ClearInputInverse(post);
            ClearOutputInverse(post);
            post.ClearLinks();
        }

        private string FindEndpoint(ItemTransferPost post)
        {
            for (int i = 0; i < _stations.Count; i++)
            {
                if (_stations[i] != null && ReferenceEquals(_stations[i].Post, post))
                {
                    return _stations[i].EndpointId;
                }
            }

            return string.Empty;
        }

        private void EnqueueLinkedPost(string endpointId, Queue<ItemTransferPost> pending)
        {
            if (TryGetPost(endpointId, out ItemTransferPostStation linked))
            {
                pending.Enqueue(linked.Post);
            }
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            left.y = right.y = 0f;
            return Vector3.Distance(left, right);
        }

        private void EnsureLinkPresentation()
        {
            if (_linkMesh != null)
            {
                return;
            }

            var linkObject = new GameObject("Transfer Links");
            linkObject.transform.SetParent(transform, false);
            MeshFilter filter = linkObject.AddComponent<MeshFilter>();
            _linkRenderer = linkObject.AddComponent<MeshRenderer>();
            _linkRenderer.sortingOrder = -15000;
            _linkMesh = new Mesh { name = "Runtime Transfer Links" };
            _linkMesh.MarkDynamic();
            filter.sharedMesh = _linkMesh;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            if (shader != null)
            {
                _linkMaterial = new Material(shader) { name = "Runtime Transfer Links", color = Color.white };
                _linkRenderer.sharedMaterial = _linkMaterial;
            }
        }

        private void RefreshLinks()
        {
            if (_linkRenderer == null || _linkMesh == null)
            {
                return;
            }

            bool visible = _gridOverlay != null && _gridOverlay.IsVisible;
            _linkRenderer.enabled = visible;
            if (!visible)
            {
                return;
            }

            _vertices.Clear();
            _colors.Clear();
            _indices.Clear();
            for (int i = 0; i < _stations.Count; i++)
            {
                ItemTransferPostStation source = _stations[i];
                if (source == null || source.Site.State != BuildState.Completed ||
                    !TryGetPost(source.Post.OutputEndpointId, out ItemTransferPostStation destination))
                {
                    continue;
                }

                AddArrow(source.transform.position, destination.transform.position, source.Post.GroupColor);
            }

            _linkMesh.Clear();
            _linkMesh.SetVertices(_vertices);
            _linkMesh.SetColors(_colors);
            _linkMesh.SetIndices(_indices, MeshTopology.Triangles, 0);
            _linkMesh.RecalculateBounds();
        }

        private void AddArrow(Vector3 start, Vector3 end, Color color)
        {
            start.y = end.y = LinkHeight;
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length < .01f)
            {
                return;
            }

            direction /= length;
            Vector3 side = new(-direction.z, 0f, direction.x);
            int index = _vertices.Count;
            _vertices.Add(transform.InverseTransformPoint(start - side * LinkWidth));
            _vertices.Add(transform.InverseTransformPoint(start + side * LinkWidth));
            _vertices.Add(transform.InverseTransformPoint(end - direction * .18f + side * LinkWidth));
            _vertices.Add(transform.InverseTransformPoint(end - direction * .18f - side * LinkWidth));
            Vector3 arrowBase = Vector3.Lerp(start, end, .7f);
            _vertices.Add(transform.InverseTransformPoint(arrowBase - direction * .18f - side * .14f));
            _vertices.Add(transform.InverseTransformPoint(arrowBase + direction * .18f));
            _vertices.Add(transform.InverseTransformPoint(arrowBase - direction * .18f + side * .14f));
            for (int i = 0; i < 7; i++) _colors.Add(color);
            _indices.Add(index); _indices.Add(index + 1); _indices.Add(index + 2);
            _indices.Add(index); _indices.Add(index + 2); _indices.Add(index + 3);
            _indices.Add(index + 4); _indices.Add(index + 5); _indices.Add(index + 6);
        }

        private interface ITransferSource
        {
            ItemDefinition OutputItem { get; }
            int Extract(int maximumQuantity, float elapsedSeconds);
        }

        private interface ITransferSink
        {
            int AcceptableQuantity(ItemDefinition item, int maximumQuantity);
            int Insert(ItemDefinition item, int quantity);
        }

        private sealed class PostEndpoint : ITransferSource, ITransferSink
        {
            private readonly ItemTransferPost _post;
            public PostEndpoint(ItemTransferPost post) => _post = post;
            public ItemDefinition OutputItem => _post.BufferedItem;
            public int Extract(int maximumQuantity, float elapsedSeconds) => _post.Extract(maximumQuantity);
            public int AcceptableQuantity(ItemDefinition item, int maximumQuantity) =>
                _post.AcceptableQuantity(item, maximumQuantity);
            public int Insert(ItemDefinition item, int quantity) => _post.Insert(item, quantity);
        }

        private sealed class MiningEndpoint : ITransferSource
        {
            private readonly IItemOutput _output;
            public MiningEndpoint(IItemOutput output) => _output = output;
            public ItemDefinition OutputItem => _output.OutputItem;
            public int Extract(int maximumQuantity, float elapsedSeconds) =>
                _output.Extract(maximumQuantity, elapsedSeconds);
        }

        private sealed class InventoryEndpoint : ITransferSource, ITransferSink
        {
            private readonly InventoryModel _inventory;
            public InventoryEndpoint(InventoryModel inventory) => _inventory = inventory;
            public ItemDefinition OutputItem => _inventory.Stacks.Count > 0 ? _inventory.Stacks[0].Definition : null;

            public int Extract(int maximumQuantity, float elapsedSeconds)
            {
                if (_inventory.Stacks.Count == 0 || maximumQuantity <= 0) return 0;
                ItemStack stack = _inventory.Stacks[0];
                int quantity = Mathf.Min(maximumQuantity, stack.Quantity);
                return _inventory.Remove(stack.StackId, quantity).Succeeded ? quantity : 0;
            }

            public int AcceptableQuantity(ItemDefinition item, int maximumQuantity)
            {
                int accepted = maximumQuantity;
                while (accepted > 0 && !_inventory.CanAdd(item, accepted).Succeeded) accepted--;
                return accepted;
            }

            public int Insert(ItemDefinition item, int quantity) =>
                quantity > 0 && _inventory.Add(item, quantity).Succeeded ? quantity : 0;
        }

        private sealed class BuildingInputEndpoint : ITransferSink
        {
            private readonly MiningDrill _miningDrill;
            private readonly PlanterBox _planterBox;

            public BuildingInputEndpoint(MiningDrill miningDrill, PlanterBox planterBox)
            {
                _miningDrill = miningDrill;
                _planterBox = planterBox;
            }

            public int AcceptableQuantity(ItemDefinition item, int maximumQuantity)
            {
                if (_miningDrill != null)
                {
                    return _miningDrill.AcceptableInputItems(item, maximumQuantity);
                }

                return _planterBox != null ? _planterBox.AcceptableInputItems(item, maximumQuantity) : 0;
            }

            public int Insert(ItemDefinition item, int quantity)
            {
                if (_miningDrill != null)
                {
                    return _miningDrill.InsertInputItems(item, quantity);
                }

                return _planterBox != null ? _planterBox.InsertInputItems(item, quantity) : 0;
            }
        }
    }

    public readonly struct TransferEndpointOption
    {
        public TransferEndpointOption(string id, string label, bool isRemotePost)
        {
            Id = id;
            Label = label;
            IsRemotePost = isRemotePost;
        }

        public string Id { get; }
        public string Label { get; }
        public bool IsRemotePost { get; }
    }
}
