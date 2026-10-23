using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Figures
{
    /// <summary>
    /// Единственный класс со ссылкой на корневой конфиг. Собирает ядро явно, в коде,
    /// и раздаёт его сцене: порядок Awake между объектами не упорядочен, и сервис,
    /// разрешённый из того, кто успел первым, — классический баг на пустом месте.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    [DisallowMultipleComponent]
    public sealed class FiguresBootstrap : MonoBehaviour
    {
        public const int ExecutionOrder = -100;

        [SerializeField] private PrototypeConfig config;
        [SerializeField] private PartSpawner spawner;
        [SerializeField] private PartGrabber grabber;

        [Tooltip("Необязательно. Пусто — игрока в сцене нет или он на встроенных числах.")]
        [SerializeField] private FirstPersonPlayer player;

        [Tooltip("Необязательно. Пусто — вердикт уходит только в консоль.")]
        [SerializeField] private BoardStatusView status;

        [Tooltip("Необязательно. Пусто — коробки и фото заказывать негде.")]
        [SerializeField] private ComputerTerminal terminal;

        [Tooltip("Необязательно. Пусто — ячейки работают, но на экране их не видно.")]
        [SerializeField] private HotbarView hotbarView;

        [Tooltip("Необязательно. Пусто или выключена — фото публикуется кнопкой терминала, по всей витрине.")]
        [SerializeField] private PhotoCamera photoCamera;

        [Tooltip("Пусто — подставки соберутся со сцены по компоненту.")]
        [SerializeField] private List<FigureStandView> stands = new();

        private readonly StringBuilder _report = new();

        private PhotoPrints _prints;

        public AssemblyBoard Board { get; private set; }

        /// <summary>Игровой цикл: кошелёк, склад, магазин коробок и лента фото.</summary>
        public CollectionStudio Studio { get; private set; }

        /// <summary>Ячейки под рукой: куда убирается деталь из руки.</summary>
        public Hotbar Hotbar { get; private set; }

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogWarning("[Figures] Корневой конфиг не задан — собирать нечего.", this);
                enabled = false;
                return;
            }

            BindPlayer();

            if (!config.IsComplete)
            {
                Debug.LogWarning("[Figures] Конфиг сборки фигурок не заполнен — головоломка не собрана.", this);
                return;
            }

            if (stands.Count == 0)
                stands.AddRange(FindObjectsByType<FigureStandView>(FindObjectsSortMode.None));

            // Сцена с одной комнатой — обычный случай, а не поломка: там нет ни подноса,
            // ни подставок, и ругаться на их отсутствие незачем.
            if (spawner == null && stands.Count == 0)
            {
                DevLog.Info("[Figures] Ни подноса, ни подставок в сцене — собран только игрок.");
                return;
            }

            Board = new AssemblyBoard(config.Catalog);
            Board.Changed += OnBoardChanged;

            Studio = BuildStudio();

            WarnOnMissingStands();

            // На поднос выкладывается склад, а не весь каталог: детали приходят в
            // коробках, и витрина должна показывать то, что игрок действительно купил.
            if (spawner != null) spawner.Spawn(config, OwnedParts());
            Hotbar = new Hotbar(config.Interaction.HotbarSlots);

            if (grabber != null)
                grabber.Bind(Board, config.Interaction, stands, spawner?.Spawned, spawner?.Tray, Hotbar);
            if (hotbarView != null) hotbarView.Bind(Hotbar, grabber != null ? grabber.ViewOf : null);
            if (status != null) status.Bind(Board);
            var cameraShots = photoCamera != null && photoCamera.CanShoot;
            if (cameraShots)
            {
                _prints = new PhotoPrints(Studio.Album);
                photoCamera.Bind(Studio, Board, spawner?.Spawned, _prints);
            }

            if (terminal != null) terminal.Bind(Studio, Board, cameraShots, _prints);

            DevLog.Info($"[Figures] Собрано ядро: фигурок {Board.Figures.Count}, гнёзд {Board.SlotTotal}, " +
                        $"на складе деталей {Studio.Inventory.Count}, на счету {Studio.Wallet.Balance}.");
        }

        private void OnDestroy()
        {
            if (Board != null) Board.Changed -= OnBoardChanged;
            if (Studio != null) Studio.BoxDelivered -= OnBoxDelivered;
            _prints?.Dispose();
        }

        /// <summary>
        /// Цикл собирается здесь же, где и доска: деньги, склад и коробки держатся на
        /// том же каталоге, а ссылка на корневой конфиг есть только у загрузчика.
        /// </summary>
        private CollectionStudio BuildStudio()
        {
            var settings = config.Economy != null ? config.Economy.Settings : EconomySettings.Default;
            if (config.Economy == null)
                DevLog.Info("[Figures] В корневом конфиге нет EconomyConfig — цикл на встроенных числах.");

            var studio = new CollectionStudio(config.Catalog, settings);
            studio.BoxDelivered += OnBoxDelivered;
            return studio;
        }

        private IReadOnlyList<string> OwnedParts() =>
            Studio != null && Studio.Settings.UseInventory ? Studio.Inventory.Owned : null;

        private void OnBoxDelivered(BoxDelivery delivery)
        {
            if (spawner == null || !delivery.IsDelivered) return;
            if (!Studio.Settings.UseInventory) return;

            spawner.Deliver(config, delivery.Parts);
            DevLog.Info($"[Figures] Коробка «{delivery.Offer.Title}»: деталей {delivery.Count}.");
        }

        /// <summary>
        /// Числа игрока едут из конфига в сцену здесь: ссылка на корневой конфиг есть
        /// только у загрузчика, а игрок получает уже готовые настройки ядра.
        /// </summary>
        private void BindPlayer()
        {
            if (player == null) return;

            if (config.Player == null)
            {
                DevLog.Info("[Figures] В корневом конфиге нет PlayerConfig — игрок на встроенных числах.");
                return;
            }

            player.Bind(config.Player.Settings);
        }

        private void OnBoardChanged()
        {
            if (!Board.IsFull) return;

            var mistakes = Board.MistakeCount;
            if (mistakes == 0)
            {
                DevLog.Info("[Figures] Все детали на своих местах.");
                return;
            }

            _report.Clear();
            _report.Append($"[Figures] Чужих деталей: {mistakes}.");

            foreach (var figure in Board.Figures)
                foreach (var kind in figure.Slots)
                {
                    var partId = figure.PartAt(kind);
                    if (partId == null) continue;

                    var owner = Board.OwnerOf(partId);
                    if (owner == figure.FigureId) continue;

                    _report.Append($" {kind} у {figure.FigureId} принадлежит {owner};");
                }

            DevLog.Info(_report.ToString());
        }

        /// <summary>
        /// Фигурка из каталога без подставки на сцене — её гнёзда не заполнить никогда,
        /// и прототип молча не собирается. Ловить это глазами дорого.
        /// </summary>
        private void WarnOnMissingStands()
        {
            foreach (var figureId in config.Catalog.FigureIds)
            {
                var found = false;
                foreach (var stand in stands)
                    if (stand != null && stand.FigureId == figureId) { found = true; break; }

                if (!found)
                    Debug.LogWarning($"[Figures] Для фигурки '{figureId}' нет подставки на сцене.", this);
            }
        }
    }
}
