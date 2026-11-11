using System.Collections.Generic;
using MountainEater.Core;
using MountainEater.Machines;
using MountainEater.Mining;
using MountainEater.Regions;
using MountainEater.Upgrades;
using UnityEngine;
using UnityEngine.UIElements;

namespace MountainEater.UI
{
    /// <summary>
    /// Ana HUD (UI Toolkit).
    ///   Sol üst: para + dağ ilerlemesi.
    ///   Sağ panel (biri görünür): Yükseltmeler (varsayılan) · Bölgeler · Mağaza · Makineler.
    ///   Alt çubuk: Shop · Machines · Regions.  Üst orta: yerleştirme bilgisi.
    ///   Dünya üzerinde: "+5 $", "Kilitli", "Matkap durdu" uçan yazıları.
    ///
    /// Satırlar sistemlerdeki kayıtlardan otomatik oluşur (veri odaklı). Paneller PointerUtility'ye kayıtlı:
    /// üzerlerine tıklamak kazmaz.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class HudController : MonoBehaviour
    {
        private const float ProgressRefreshInterval = 0.1f;
        private const float MachineRefreshInterval = 0.25f;
        private const float FloatingDuration = 0.9f;
        private const float MessageDuration = 2.2f;
        private const float FloatingRise = 45f;
        private const int MaxFloatingTexts = 24;

        private enum SidePanel
        {
            Upgrades,
            Regions,
            Shop,
            Machines,
        }

        private sealed class UpgradeRow
        {
            public UpgradeType Type;
            public Label Title;
            public Label Detail;
            public Button Button;
        }

        private sealed class RegionRow
        {
            public byte Id;
            public VisualElement Root;
            public Label Title;
            public Label Status;
            public Label Detail;
            public Button Button;
        }

        private sealed class ShopRow
        {
            public MachineType Type;
            public Label Stats;
            public Button Button;
        }

        private sealed class MachineRow
        {
            public int Id;
            public VisualElement Root;
            public Label Title;
            public Label Status;
            public Label Detail;
        }

        private sealed class FloatingText
        {
            public Label Label;
            public float Age;
            public float Duration;
            public Vector2 Start;
        }

        private readonly List<UpgradeRow> upgradeRows = new List<UpgradeRow>();
        private readonly List<RegionRow> regionRows = new List<RegionRow>();
        private readonly List<ShopRow> shopRows = new List<ShopRow>();
        private readonly List<MachineRow> machineRows = new List<MachineRow>();
        private readonly List<FloatingText> activeTexts = new List<FloatingText>();
        private readonly Stack<FloatingText> pooledTexts = new Stack<FloatingText>();
        private readonly List<VisualElement> blockingPanels = new List<VisualElement>();

        private HudServices services;
        private SidePanel currentPanel = SidePanel.Upgrades;

        private VisualElement root;
        private Label moneyLabel;
        private Label blocksLabel;
        private Label percentLabel;
        private VisualElement progressFill;
        private VisualElement floatingLayer;

        private VisualElement upgradePanel;
        private VisualElement upgradeList;
        private VisualElement regionPanel;
        private VisualElement regionList;
        private VisualElement shopPanel;
        private VisualElement shopList;
        private VisualElement machinesPanel;
        private VisualElement machineList;
        private Label machinesEmptyLabel;
        private VisualElement placementBanner;
        private Label placementTitle;
        private Label placementDetail;
        private Button shopButton;
        private Button machinesButton;
        private Button regionsButton;

        private float progressTimer;
        private float machineTimer;
        private int lastShownSolidCount = -1;

        public void Initialize(HudServices hudServices)
        {
            Unsubscribe();
            services = hudServices;

            if (!BindElements()) return;
            Subscribe();

            BuildUpgradeRows();
            BuildRegionRows();
            BuildShopRows();
            BuildMachineRows();
            ShowSidePanel(SidePanel.Upgrades);
            RefreshMoney();
            RefreshProgress(force: true);
            RefreshPlacementBanner();
        }

        // =====================================================================
        // Bağlama
        // =====================================================================

        private bool BindElements()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[HudController] UIDocument'in Visual Tree Asset'i yok.", this);
                return false;
            }

            root.pickingMode = PickingMode.Ignore; // tam ekran kapsayıcı dağa tıklamayı engellemesin

            moneyLabel = root.Q<Label>("money-label");
            blocksLabel = root.Q<Label>("blocks-label");
            percentLabel = root.Q<Label>("percent-label");
            progressFill = root.Q<VisualElement>("progress-fill");
            floatingLayer = root.Q<VisualElement>("floating-layer");
            upgradePanel = root.Q<VisualElement>("upgrade-panel");
            upgradeList = root.Q<VisualElement>("upgrade-list");
            regionPanel = root.Q<VisualElement>("region-panel");
            regionList = root.Q<VisualElement>("region-list");
            shopPanel = root.Q<VisualElement>("shop-panel");
            shopList = root.Q<VisualElement>("shop-list");
            machinesPanel = root.Q<VisualElement>("machines-panel");
            machineList = root.Q<VisualElement>("machine-list");
            machinesEmptyLabel = root.Q<Label>("machines-empty");
            placementBanner = root.Q<VisualElement>("placement-banner");
            placementTitle = root.Q<Label>("placement-title");
            placementDetail = root.Q<Label>("placement-detail");
            shopButton = root.Q<Button>("shop-button");
            machinesButton = root.Q<Button>("machines-button");
            regionsButton = root.Q<Button>("regions-button");

            if (moneyLabel == null || blocksLabel == null || percentLabel == null || progressFill == null ||
                floatingLayer == null || upgradePanel == null || upgradeList == null || regionPanel == null ||
                regionList == null || shopPanel == null || shopList == null || machinesPanel == null ||
                machineList == null || machinesEmptyLabel == null || placementBanner == null ||
                placementTitle == null || placementDetail == null || shopButton == null ||
                machinesButton == null || regionsButton == null)
            {
                Debug.LogError("[HudController] MainHud.uxml'de beklenen elemanlar bulunamadı. UXML güncel mi?", this);
                return false;
            }

            shopButton.clicked -= OnShopButton;
            shopButton.clicked += OnShopButton;
            machinesButton.clicked -= OnMachinesButton;
            machinesButton.clicked += OnMachinesButton;
            regionsButton.clicked -= OnRegionsButton;
            regionsButton.clicked += OnRegionsButton;

            RegisterBlockingPanel(root.Q<VisualElement>("progress-panel"));
            RegisterBlockingPanel(upgradePanel);
            RegisterBlockingPanel(regionPanel);
            RegisterBlockingPanel(shopPanel);
            RegisterBlockingPanel(machinesPanel);
            RegisterBlockingPanel(placementBanner);
            RegisterBlockingPanel(shopButton);
            RegisterBlockingPanel(machinesButton);
            RegisterBlockingPanel(regionsButton);
            return true;
        }

        private void RegisterBlockingPanel(VisualElement panel)
        {
            if (panel == null || blockingPanels.Contains(panel)) return;
            blockingPanels.Add(panel);
            PointerUtility.RegisterBlockingElement(panel);
        }

        private void Subscribe()
        {
            services.Economy.MoneyChanged += OnMoneyChanged;
            services.Upgrades.LevelChanged += OnLevelChanged;

            if (services.PlayerMining != null)
            {
                services.PlayerMining.PlayerHit += OnPlayerHit;
                services.PlayerMining.LockedTargetClicked += OnLockedTargetClicked;
            }

            if (services.Regions != null) services.Regions.RegionUnlocked += OnRegionUnlocked;
            if (services.RegionOverlay != null)
            {
                services.RegionOverlay.ActiveChanged += OnRegionModeChanged;
                services.RegionOverlay.SelectionChanged += OnRegionSelectionChanged;
            }

            if (services.Machines != null)
            {
                services.Machines.MachinePlaced += OnMachinePlaced;
                services.Machines.MachineMoved += OnMachineMoved;
                services.Machines.MachineStopped += OnMachineStopped;
            }

            if (services.Placement != null)
            {
                services.Placement.ActiveChanged += OnPlacementChanged;
                services.Placement.Message += OnPlacementMessage;
            }
        }

        private void Unsubscribe()
        {
            if (services == null) return;

            if (services.Economy != null) services.Economy.MoneyChanged -= OnMoneyChanged;
            if (services.Upgrades != null) services.Upgrades.LevelChanged -= OnLevelChanged;
            if (services.PlayerMining != null)
            {
                services.PlayerMining.PlayerHit -= OnPlayerHit;
                services.PlayerMining.LockedTargetClicked -= OnLockedTargetClicked;
            }

            if (services.Regions != null) services.Regions.RegionUnlocked -= OnRegionUnlocked;
            if (services.RegionOverlay != null)
            {
                services.RegionOverlay.ActiveChanged -= OnRegionModeChanged;
                services.RegionOverlay.SelectionChanged -= OnRegionSelectionChanged;
            }

            if (services.Machines != null)
            {
                services.Machines.MachinePlaced -= OnMachinePlaced;
                services.Machines.MachineMoved -= OnMachineMoved;
                services.Machines.MachineStopped -= OnMachineStopped;
            }

            if (services.Placement != null)
            {
                services.Placement.ActiveChanged -= OnPlacementChanged;
                services.Placement.Message -= OnPlacementMessage;
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
            foreach (VisualElement panel in blockingPanels) PointerUtility.UnregisterBlockingElement(panel);
            blockingPanels.Clear();
        }

        // =====================================================================
        // Döngü
        // =====================================================================

        private void Update()
        {
            if (services == null || services.World == null) return;
            float dt = Time.unscaledDeltaTime;

            progressTimer -= dt;
            if (progressTimer <= 0f)
            {
                progressTimer = ProgressRefreshInterval;
                RefreshProgress(force: false);
            }

            machineTimer -= dt;
            if (machineTimer <= 0f)
            {
                machineTimer = MachineRefreshInterval;
                if (currentPanel == SidePanel.Machines) RefreshMachineRows();
            }

            if (services.Placement != null && services.Placement.IsActive) RefreshPlacementBanner();
            AnimateFloatingTexts(dt);
        }

        // =====================================================================
        // Sağ panel seçimi
        // =====================================================================

        private void OnShopButton() => ShowSidePanel(currentPanel == SidePanel.Shop ? SidePanel.Upgrades : SidePanel.Shop);

        private void OnMachinesButton() =>
            ShowSidePanel(currentPanel == SidePanel.Machines ? SidePanel.Upgrades : SidePanel.Machines);

        private void OnRegionsButton()
        {
            if (services.RegionOverlay != null) services.RegionOverlay.Toggle(); // panel OnRegionModeChanged ile değişir
        }

        private void ShowSidePanel(SidePanel panel)
        {
            // Bölge paneli bölge moduna bağlı: başka panele geçince mod kapanır.
            if (panel != SidePanel.Regions && services.RegionOverlay != null && services.RegionOverlay.IsActive)
            {
                currentPanel = panel; // OnRegionModeChanged(false) yükseltmelere döndürmesin
                services.RegionOverlay.SetActive(false);
            }

            currentPanel = panel;
            upgradePanel.EnableInClassList("hidden", panel != SidePanel.Upgrades);
            regionPanel.EnableInClassList("hidden", panel != SidePanel.Regions);
            shopPanel.EnableInClassList("hidden", panel != SidePanel.Shop);
            machinesPanel.EnableInClassList("hidden", panel != SidePanel.Machines);

            shopButton.EnableInClassList("active", panel == SidePanel.Shop);
            machinesButton.EnableInClassList("active", panel == SidePanel.Machines);
            regionsButton.EnableInClassList("active", panel == SidePanel.Regions);

            switch (panel)
            {
                case SidePanel.Upgrades: RefreshUpgradeRows(); break;
                case SidePanel.Regions: RefreshRegionRows(); break;
                case SidePanel.Shop: RefreshShopRows(); break;
                case SidePanel.Machines: RefreshMachineRows(); break;
            }
        }

        private void OnRegionModeChanged(bool active)
        {
            if (active) ShowSidePanel(SidePanel.Regions);
            else if (currentPanel == SidePanel.Regions) ShowSidePanel(SidePanel.Upgrades);
        }

        // =====================================================================
        // Para ve ilerleme
        // =====================================================================

        private void OnMoneyChanged(long money)
        {
            RefreshMoney();
            RefreshUpgradeRows();
            RefreshRegionRows();
            RefreshShopRows();
        }

        private void RefreshMoney() => moneyLabel.text = NumberFormat.Money(services.Economy.Money);

        private void RefreshProgress(bool force)
        {
            int remaining = services.World.SolidCount;
            if (!force && remaining == lastShownSolidCount) return;
            lastShownSolidCount = remaining;

            int total = Mathf.Max(1, services.World.InitialSolidCount);
            double destroyedPercent = (total - remaining) * 100.0 / total;

            blocksLabel.text = $"{NumberFormat.Integer(remaining)} / {NumberFormat.Integer(services.World.InitialSolidCount)} blok kaldı";
            percentLabel.text = $"{NumberFormat.Percent(destroyedPercent)} yok edildi";
            progressFill.style.width = Length.Percent((float)destroyedPercent);
        }

        // =====================================================================
        // Yükseltmeler
        // =====================================================================

        private void BuildUpgradeRows()
        {
            upgradeList.Clear();
            upgradeRows.Clear();

            foreach (UpgradeType type in services.Upgrades.Types)
            {
                var rowElement = NewElement("upgrade-row");
                Label title = NewLabel("upgrade-title");
                Label detail = NewLabel("upgrade-detail");
                UpgradeType captured = type;
                var button = new Button(() => services.Upgrades.TryPurchase(captured));
                button.AddToClassList("upgrade-button");

                rowElement.Add(title);
                rowElement.Add(detail);
                rowElement.Add(button);
                upgradeList.Add(rowElement);
                upgradeRows.Add(new UpgradeRow { Type = type, Title = title, Detail = detail, Button = button });
            }

            RefreshUpgradeRows();
        }

        private void RefreshUpgradeRows()
        {
            UpgradeManager upgrades = services.Upgrades;
            foreach (UpgradeRow row in upgradeRows)
            {
                UpgradeInfo info = upgrades.GetInfo(row.Type);
                string current = NumberFormat.Value(upgrades.GetCurrentValue(row.Type));
                row.Title.text = $"{info.DisplayName}   Lv {upgrades.GetLevel(row.Type)}";

                if (upgrades.IsMaxed(row.Type))
                {
                    row.Detail.text = $"{info.ValueLabel}: {current}";
                    row.Button.text = "MAX";
                    row.Button.SetEnabled(false);
                }
                else
                {
                    row.Detail.text = $"{info.ValueLabel}: {current}  →  {NumberFormat.Value(upgrades.GetNextValue(row.Type))}";
                    row.Button.text = $"Satın al   {NumberFormat.Money(upgrades.GetNextCost(row.Type))}";
                    row.Button.SetEnabled(upgrades.CanPurchase(row.Type));
                }
            }
        }

        private void OnLevelChanged(UpgradeType type, int level)
        {
            RefreshUpgradeRows();
            RefreshShopRows(); // matkap hasarı/hızı mağaza kartında görünür
        }

        // =====================================================================
        // Bölgeler
        // =====================================================================

        private void BuildRegionRows()
        {
            regionList.Clear();
            regionRows.Clear();
            if (services.Regions == null) return;

            foreach (RegionState region in services.Regions.Regions)
            {
                var rowElement = NewElement("region-row");
                Label title = NewLabel("upgrade-title");
                Label status = NewLabel("upgrade-detail");
                Label detail = NewLabel("upgrade-detail");
                byte id = region.Id;
                var button = new Button(() => services.Regions.TryPurchase(id));
                button.AddToClassList("upgrade-button");
                rowElement.RegisterCallback<ClickEvent>(_ => services.RegionOverlay?.Select(id, focusCamera: true));

                rowElement.Add(title);
                rowElement.Add(status);
                rowElement.Add(detail);
                rowElement.Add(button);
                regionList.Add(rowElement);
                regionRows.Add(new RegionRow { Id = id, Root = rowElement, Title = title, Status = status, Detail = detail, Button = button });
            }

            RefreshRegionRows();
        }

        private void RefreshRegionRows()
        {
            if (services.Regions == null) return;
            byte selected = services.RegionOverlay != null ? services.RegionOverlay.SelectedRegion : (byte)0;

            foreach (RegionRow row in regionRows)
            {
                RegionState region = services.Regions.Find(row.Id);
                if (region == null) continue;

                int machineCount = services.Machines != null ? services.Machines.CountInRegion(row.Id) : 0;
                row.Root.EnableInClassList("selected", row.Id == selected);
                row.Title.text = region.DisplayName;
                row.Detail.text = $"{NumberFormat.Integer(region.VoxelCount)} blok · değer {NumberFormat.Money(region.TotalValue)}" +
                                  (machineCount > 0 ? $" · {machineCount} makine" : string.Empty);
                row.Status.EnableInClassList("region-status-unlocked", region.Unlocked);
                row.Status.EnableInClassList("region-status-locked", !region.Unlocked);

                if (region.Unlocked)
                {
                    row.Status.text = "Açık";
                    row.Button.style.display = DisplayStyle.None;
                }
                else
                {
                    row.Status.text = $"Kilitli · {NumberFormat.Money(region.Price)}";
                    row.Button.style.display = DisplayStyle.Flex;
                    row.Button.text = $"Bölgeyi aç   {NumberFormat.Money(region.Price)}";
                    row.Button.SetEnabled(services.Regions.CanPurchase(region.Id));
                }
            }
        }

        private void OnRegionSelectionChanged(byte regionId) => RefreshRegionRows();
        private void OnRegionUnlocked(RegionState region) => RefreshRegionRows();

        private void OnLockedTargetClicked(Vector3Int voxel, Vector3 worldPoint)
        {
            RegionState region = services.Regions?.Find(services.World.GetRegion(voxel.x, voxel.y, voxel.z));
            string text = region != null
                ? $"Kilitli: {region.DisplayName} · {NumberFormat.Money(region.Price)}"
                : "Kilitli bölge";
            ShowWorldText(text, worldPoint, warning: true);
        }

        // =====================================================================
        // Mağaza
        // =====================================================================

        private void BuildShopRows()
        {
            shopList.Clear();
            shopRows.Clear();
            if (services.Machines == null) return;

            foreach (MachineSpec spec in services.Machines.Specs)
            {
                var rowElement = NewElement("upgrade-row");
                Label title = NewLabel("upgrade-title");
                title.text = spec.DisplayName;
                Label description = NewLabel("upgrade-detail");
                description.AddToClassList("wrap");
                description.text = spec.Description;
                Label stats = NewLabel("upgrade-detail");
                MachineType type = spec.Type;
                var button = new Button(() => OnBuyMachine(type));
                button.AddToClassList("upgrade-button");

                rowElement.Add(title);
                rowElement.Add(description);
                rowElement.Add(stats);
                rowElement.Add(button);
                shopList.Add(rowElement);
                shopRows.Add(new ShopRow { Type = type, Stats = stats, Button = button });
            }

            RefreshShopRows();
        }

        private void RefreshShopRows()
        {
            if (services.Machines == null) return;
            MachineManager machines = services.Machines;
            bool placing = services.Placement != null && services.Placement.IsActive;

            foreach (ShopRow row in shopRows)
            {
                MachineSpec spec = machines.GetSpec(row.Type);
                long price = machines.GetNextPrice(row.Type);
                row.Stats.text = $"Hasar {machines.GetDamage(row.Type)} · her {NumberFormat.Value(machines.GetInterval(row.Type))} sn · " +
                                 $"alan {spec.WorkRadius} voxel · sahip: {machines.CountOfType(row.Type)}";
                row.Button.text = $"Satın al ve yerleştir   {NumberFormat.Money(price)}";
                row.Button.SetEnabled(!placing && services.Economy.CanAfford(price));
            }
        }

        private void OnBuyMachine(MachineType type) => services.Placement?.BeginBuy(type);

        // =====================================================================
        // Makineler
        // =====================================================================

        private void BuildMachineRows()
        {
            machineList.Clear();
            machineRows.Clear();
            if (services.Machines == null) return;

            foreach (Machine machine in services.Machines.Machines)
            {
                var rowElement = NewElement("region-row");
                Label title = NewLabel("upgrade-title");
                Label status = NewLabel("upgrade-detail");
                Label detail = NewLabel("upgrade-detail");
                int id = machine.Id;

                var buttons = NewElement("button-row");
                var focusButton = new Button(() => FocusMachine(id)) { text = "Odakla" };
                focusButton.AddToClassList("upgrade-button");
                var moveButton = new Button(() => services.Placement?.BeginMove(id)) { text = "Taşı" };
                moveButton.AddToClassList("upgrade-button");
                buttons.Add(focusButton);
                buttons.Add(moveButton);

                rowElement.Add(title);
                rowElement.Add(status);
                rowElement.Add(detail);
                rowElement.Add(buttons);
                machineList.Add(rowElement);
                machineRows.Add(new MachineRow { Id = id, Root = rowElement, Title = title, Status = status, Detail = detail });
            }

            machinesEmptyLabel.style.display = machineRows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshMachineRows();
        }

        private void RefreshMachineRows()
        {
            if (services.Machines == null) return;
            int selected = services.MachineController != null ? services.MachineController.SelectedMachineId : -1;

            foreach (MachineRow row in machineRows)
            {
                Machine machine = services.Machines.Find(row.Id);
                if (machine == null) continue;

                string regionName = services.Regions?.Find(machine.Region)?.DisplayName ?? $"Bölge {machine.Region}";
                bool working = machine.State == MachineState.Working;

                row.Root.EnableInClassList("selected", row.Id == selected);
                row.Title.text = $"{services.Machines.GetSpec(machine.Type).DisplayName} #{machine.Id} · {regionName}";
                row.Status.text = working ? "Çalışıyor" : "Durdu: çalışma alanında kaya kalmadı — taşı";
                row.Status.EnableInClassList("status-working", working);
                row.Status.EnableInClassList("status-stopped", !working);
                row.Detail.text = $"Kırılan {NumberFormat.Integer(machine.BlocksMined)} blok · {NumberFormat.Money(machine.MoneyEarned)}";
            }
        }

        private void FocusMachine(int machineId)
        {
            Machine machine = services.Machines?.Find(machineId);
            if (machine == null) return;

            if (services.MachineController != null)
            {
                services.MachineController.SelectedMachineId = machineId;
                if (services.OrbitCamera != null)
                {
                    services.OrbitCamera.SetFocus(services.MachineController.GetMachineWorldPosition(machine), snap: false);
                }
            }

            RefreshMachineRows();
        }

        private void OnMachinePlaced(Machine machine)
        {
            BuildMachineRows();
            RefreshShopRows();
            RefreshRegionRows();
        }

        private void OnMachineMoved(Machine machine)
        {
            RefreshMachineRows();
            RefreshRegionRows();
        }

        private void OnMachineStopped(Machine machine)
        {
            RefreshMachineRows();
            if (services.MachineController == null) return;

            string name = services.Machines.GetSpec(machine.Type).DisplayName;
            ShowWorldText($"{name} #{machine.Id} durdu: kaya kalmadı",
                services.MachineController.GetMachineWorldPosition(machine), warning: true, MessageDuration);
        }

        // =====================================================================
        // Yerleştirme
        // =====================================================================

        private void OnPlacementChanged(bool active)
        {
            RefreshPlacementBanner();
            RefreshShopRows();
        }

        private void RefreshPlacementBanner()
        {
            MachinePlacementController placement = services.Placement;
            bool active = placement != null && placement.IsActive;
            placementBanner.EnableInClassList("hidden", !active);
            if (!active) return;

            placementTitle.text = placement.IsMoving ? "Makineyi taşı" : "Makine yerleştir";
            bool hasProblem = !string.IsNullOrEmpty(placement.CurrentProblem);
            placementDetail.text = hasProblem
                ? $"{placement.CurrentProblem} · Esc: iptal"
                : "Açık bölgede bir kaya yüzeyine tıkla · Esc: iptal";
            placementDetail.EnableInClassList("problem", hasProblem);
        }

        private void OnPlacementMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || root == null) return;
            float width = root.layout.width > 0 ? root.layout.width : 1920f;
            ShowFloatingText(message, new Vector2(width * 0.5f - 80f, 140f), warning: false, MessageDuration);
        }

        // =====================================================================
        // Uçan yazılar
        // =====================================================================

        private void OnPlayerHit(MiningResult result, Vector3 worldPoint)
        {
            if (result.Reward <= 0) return;
            ShowWorldText($"+{NumberFormat.Money(result.Reward)}", worldPoint, warning: false);
        }

        private void ShowWorldText(string text, Vector3 worldPoint, bool warning, float duration = FloatingDuration)
        {
            Camera cam = services.Camera;
            if (cam == null || root?.panel == null) return;
            if (cam.WorldToViewportPoint(worldPoint).z <= 0f) return; // kameranın arkasında

            Vector2 panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, worldPoint, cam);
            ShowFloatingText(text, panelPosition, warning, duration);
        }

        private void ShowFloatingText(string text, Vector2 panelPosition, bool warning, float duration = FloatingDuration)
        {
            FloatingText item;
            if (activeTexts.Count >= MaxFloatingTexts)
            {
                item = activeTexts[0];
                activeTexts.RemoveAt(0);
            }
            else if (pooledTexts.Count > 0)
            {
                item = pooledTexts.Pop();
            }
            else
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("floating-text");
                floatingLayer.Add(label);
                item = new FloatingText { Label = label };
            }

            item.Label.text = text;
            item.Label.EnableInClassList("locked", warning);
            item.Label.style.display = DisplayStyle.Flex;
            item.Age = 0f;
            item.Duration = duration;
            item.Start = panelPosition;
            activeTexts.Add(item);
            PositionFloatingText(item, 0f);
        }

        private void AnimateFloatingTexts(float deltaTime)
        {
            for (int i = activeTexts.Count - 1; i >= 0; i--)
            {
                FloatingText item = activeTexts[i];
                item.Age += deltaTime;
                float t = item.Age / item.Duration;

                if (t >= 1f)
                {
                    item.Label.style.display = DisplayStyle.None;
                    activeTexts.RemoveAt(i);
                    pooledTexts.Push(item);
                    continue;
                }

                PositionFloatingText(item, t);
            }
        }

        private static void PositionFloatingText(FloatingText item, float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            item.Label.style.left = item.Start.x - 20f;
            item.Label.style.top = item.Start.y - 20f - FloatingRise * eased;
            item.Label.style.opacity = 1f - t * t;
        }

        // =====================================================================
        // Yardımcılar
        // =====================================================================

        private static VisualElement NewElement(string className)
        {
            var element = new VisualElement();
            element.AddToClassList(className);
            return element;
        }

        private static Label NewLabel(string className)
        {
            var label = new Label();
            label.AddToClassList(className);
            return label;
        }
    }
}
