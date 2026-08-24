using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.ViewModels.Decks;
using CollectaMundo.ViewModels.Decks.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CollectaMundo.Presentation.Behaviors
{
    public sealed class DeckBuilderDragDeckCardBehavior : DeckBuilderDragBehaviorBase
    {
        // Drag payload identifier shared by source and destination grids.
        private const string DeckCardDragDataFormat = "CollectaMundo.DeckCard";
        private const string OracleCardDragDataFormat = "CollectaMundo.OracleCard";

        // XAML-configurable destination zone and move command.
        public static readonly DependencyProperty DestinationSectionProperty = DependencyProperty.Register(nameof(DestinationSection), typeof(DeckSection), typeof(DeckBuilderDragDeckCardBehavior));

        // State belonging to the current drag operation.
        private Point _dragStartPoint;
        private IReadOnlyList<DeckCardEntryViewModel> _draggedCards = [];

        // Visual feedback state.
        private DeckCardDragContext? _activeDragContext;

        // Public dependency-property wrappers.
        public DeckSection DestinationSection
        {
            get => (DeckSection)GetValue(DestinationSectionProperty);
            set => SetValue(DestinationSectionProperty, value);
        }
        public static readonly DependencyProperty DragCommandProperty = DependencyProperty.Register(nameof(DragCommand), typeof(ICommand), typeof(DeckBuilderDragDeckCardBehavior));
        public static readonly DependencyProperty AddOracleCardCommandProperty = DependencyProperty.Register(nameof(AddOracleCardCommand), typeof(ICommand), typeof(DeckBuilderDragDeckCardBehavior));
        public ICommand? DragCommand
        {
            get => (ICommand?)GetValue(DragCommandProperty);
            set => SetValue(DragCommandProperty, value);
        }
        public ICommand? AddOracleCardCommand
        {
            get => (ICommand?)GetValue(AddOracleCardCommandProperty);
            set => SetValue(AddOracleCardCommandProperty, value);
        }

        // Behavior lifecycle.
        protected override void OnAttached()
        {
            base.OnAttached();

            // Enable dropping and subscribe to permanent DataGrid events.
            AssociatedObject.AllowDrop = true;
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
            AssociatedObject.DragOver += OnDragOver;
            AssociatedObject.DragLeave += OnDragLeave;
            AssociatedObject.Drop += OnDrop;
        }
        protected override void OnDetaching()
        {
            // Remove permanent subscriptions and clean up any active drag.
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.DragLeave -= OnDragLeave;
            AssociatedObject.Drop -= OnDrop;
            AssociatedObject.GiveFeedback -= OnGiveFeedback;
            HideDragFeedback();

            base.OnDetaching();
        }

        // Drag source handling.
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(AssociatedObject);
            _draggedCards = [];

            if (IsInteractiveElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not DeckCardEntryViewModel clickedCard)
            {
                return;
            }

            var selectedCards = AssociatedObject.SelectedItems.OfType<DeckCardEntryViewModel>().ToList();

            if (selectedCards.Contains(clickedCard))
            {
                _draggedCards = selectedCards;

                if (selectedCards.Count > 1)
                {
                    e.Handled = true;
                }
            }
            else
            {
                _draggedCards = [clickedCard];
            }
        }
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            // Start dragging only after the normal Windows drag threshold.

            if (e.LeftButton != MouseButtonState.Pressed || _draggedCards.Count == 0)
            {
                return;
            }

            var currentPosition = e.GetPosition(AssociatedObject);

            if (!HasExceededDragThreshold(_dragStartPoint, currentPosition))
            {
                return;
            }

            StartDrag(_draggedCards);
        }
        private void StartDrag(IReadOnlyList<DeckCardEntryViewModel> cards)
        {
            var context = new DeckCardDragContext { Cards = cards };

            _activeDragContext = context;

            try
            {
                var data = new DataObject();

                data.SetData(DeckCardDragDataFormat, context);

                ShowDragFeedback(GetDragFeedback(context));

                AssociatedObject.GiveFeedback += OnGiveFeedback;

                var effect = DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Move);

                // Unaccepted deck drag means delete.
                if (effect == DragDropEffects.None)
                {
                    ExecuteDelete(context);
                }
            }
            finally
            {
                AssociatedObject.GiveFeedback -= OnGiveFeedback;

                HideDragFeedback();

                _draggedCards = [];
                _activeDragContext = null;
            }
        }
        private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (_activeDragContext is not null)
            {
                UpdateDragFeedback(GetDragFeedback(_activeDragContext));
            }

            e.UseDefaultCursors = false;
            Mouse.SetCursor(Cursors.Arrow);
            e.Handled = true;
        }
        private void ExecuteDelete(DeckCardDragContext context)
        {
            var request = new DeckCardDragRequest(CreateDragItems(context.Cards), DestinationSection: null);

            if (DragCommand?.CanExecute(request) == true)
            {
                DragCommand.Execute(request);
            }
        }

        // Drop target handling.
        private void OnDragOver(object sender, DragEventArgs e)
        {
            // First handle OracleCard drags from AvailableCardsDataGrid.
            if (TryGetOracleCardDragContext(e, out var oracleContext))
            {
                HandleOracleCardDragOver(e, oracleContext);
                return;
            }

            // Otherwise handle existing DeckCard drag.
            HandleDeckCardDragOver(e);
        }
        private void HandleOracleCardDragOver(DragEventArgs e, OracleCardDragContext context)
        {
            var request = new DeckOracleCardDropRequest(context.Cards, DestinationSection, GetOracleCardAddQuantity());
            var canAdd = AddOracleCardCommand?.CanExecute(request) == true;

            context.IsOverValidTarget = canAdd;
            context.DestinationSection = canAdd ? DestinationSection : null;

            e.Effects = canAdd ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }
        private static int GetOracleCardAddQuantity()
        {
            return Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 4 : 1;
        }
        private void HandleDeckCardDragOver(DragEventArgs e)
        {
            if (!TryGetDragContext(e, out var context))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // Hovering the originating zone is a no-op
            if (IsSourceZone(context, DestinationSection))
            {
                context.IsOverSourceZone = true;
                context.IsOverValidTarget = false;
                context.DestinationSection = null;

                // Accept the drop so WPF raises Drop and DoDragDrop does not return None.
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            context.IsOverSourceZone = false;

            var request = new DeckCardDragRequest(CreateDragItems(context.Cards), DestinationSection);

            var canMove = DragCommand?.CanExecute(request) == true;

            context.IsOverValidTarget = canMove;
            context.DestinationSection = canMove ? DestinationSection : null;

            e.Effects = canMove ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }
        private void OnDragLeave(object sender, DragEventArgs e)
        {
            // Oracle card leaving a deck-zone target.
            if (TryGetOracleCardDragContext(e, out var oracleContext))
            {
                if (oracleContext.DestinationSection == DestinationSection)
                {
                    oracleContext.IsOverValidTarget = false;
                    oracleContext.DestinationSection = null;
                }

                return;
            }

            // Deck card leaving a deck-zone target.
            if (!TryGetDragContext(e, out var context))
            {
                return;
            }

            if (IsSourceZone(context, DestinationSection))
            {
                context.IsOverSourceZone = false;
            }

            if (context.DestinationSection == DestinationSection)
            {
                context.IsOverValidTarget = false;
                context.DestinationSection = null;
            }
        }
        private void OnDrop(object sender, DragEventArgs e)
        {
            // Oracle card dragged from AvailableCardsDataGrid.
            if (TryGetOracleCardDragContext(e, out var oracleContext))
            {
                HandleOracleCardDrop(e, oracleContext);
                return;
            }

            // Existing deck-card drag.
            HandleDeckCardDrop(e);
        }
        private void HandleDeckCardDrop(DragEventArgs e)
        {
            if (!TryGetDragContext(e, out var context))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // Dropping back onto the source zone is an accepted no-op.
            if (IsSourceZone(context, DestinationSection))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            if (!TryCreateMoveRequest(e, out var request) || DragCommand?.CanExecute(request) != true)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            DragCommand.Execute(request);

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        private void HandleOracleCardDrop(DragEventArgs e, OracleCardDragContext context)
        {
            var request = new DeckOracleCardDropRequest(context.Cards, DestinationSection, GetOracleCardAddQuantity());

            if (AddOracleCardCommand?.CanExecute(request) != true)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            AddOracleCardCommand.Execute(request);

            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        private bool TryCreateMoveRequest(DragEventArgs e, out DeckCardDragRequest request)
        {
            request = null!;

            if (!TryGetDragContext(e, out var context))
            {
                return false;
            }

            if (IsSourceZone(context, DestinationSection))
            {
                return false;
            }

            request = new DeckCardDragRequest(CreateDragItems(context.Cards), DestinationSection);

            return true;
        }

        // Drag semantics.
        private static IReadOnlyList<DeckCardDragItem> CreateDragItems(IReadOnlyList<DeckCardEntryViewModel> cards)
        {
            var moveAll = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            return [.. cards.Select(card => new DeckCardDragItem(card, moveAll ? card.DesiredQuantity : 1))];
        }
        private static DragFeedback GetDragFeedback(DeckCardDragContext context)
        {
            var isBulk = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            if (context.Cards.Count == 1)
            {
                var card = context.Cards[0];
                var quantity = isBulk ? card.DesiredQuantity : 1;

                var quantityText = isBulk ? $"⇧ ALL · {quantity} total" : "×1";

                if (context.IsOverSourceZone)
                {
                    return new(DragFeedbackKind.NoOp, "DO NOTHING", quantityText, isBulk);
                }

                if (context.IsOverValidTarget)
                {
                    return new(DragFeedbackKind.Move, $"MOVE: {card.CardName}", quantityText, isBulk);
                }

                return new(DragFeedbackKind.Delete, $"DELETE: {card.CardName}", quantityText, isBulk);
            }

            var totalQuantity = isBulk
                ? context.Cards.Sum(card => card.DesiredQuantity)
                : context.Cards.Count;

            var multiQuantityText = isBulk
                ? $"⇧ ALL · {totalQuantity} total"
                : "×1 each";

            if (context.IsOverSourceZone)
            {
                return new(DragFeedbackKind.NoOp, "DO NOTHING", multiQuantityText, isBulk);
            }

            if (context.IsOverValidTarget)
            {
                return new(DragFeedbackKind.Move, $"MOVE: {context.Cards.Count} cards", multiQuantityText, isBulk);
            }

            return new(DragFeedbackKind.Delete, $"DELETE: {context.Cards.Count} cards", multiQuantityText, isBulk);
        }
        private static bool TryGetDragContext(DragEventArgs e, out DeckCardDragContext context)
        {
            context = null!;

            if (!e.Data.GetDataPresent(DeckCardDragDataFormat))
            {
                return false;
            }

            if (e.Data.GetData(DeckCardDragDataFormat) is not DeckCardDragContext dragContext)
            {
                return false;
            }

            context = dragContext;
            return true;
        }
        private static bool TryGetOracleCardDragContext(DragEventArgs e, out OracleCardDragContext context)
        {
            context = null!;

            if (!e.Data.GetDataPresent(OracleCardDragDataFormat))
            {
                return false;
            }

            if (e.Data.GetData(OracleCardDragDataFormat) is not OracleCardDragContext dragContext)
            {
                return false;
            }

            context = dragContext;
            return true;
        }
        private static bool IsSourceZone(DeckCardDragContext context, DeckSection destinationSection)
        {
            return context.Cards.All(card => card.Section == destinationSection);
        }
        private sealed class DeckCardDragContext
        {
            public required IReadOnlyList<DeckCardEntryViewModel> Cards { get; init; }
            public bool IsOverValidTarget { get; set; }
            public bool IsOverSourceZone { get; set; }
            public DeckSection? DestinationSection { get; set; }
        }
    }
}
