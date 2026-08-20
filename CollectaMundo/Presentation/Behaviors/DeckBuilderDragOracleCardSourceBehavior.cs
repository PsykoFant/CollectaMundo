using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.Decks.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CollectaMundo.Presentation.Behaviors
{
    public sealed class DeckBuilderDragOracleCardSourceBehavior : DeckBuilderDragBehaviorBase
    {
        private const string OracleCardDragDataFormat = "CollectaMundo.OracleCard";
        private OracleCardDragContext? _activeDragContext;
        private Point _dragStartPoint;
        private IReadOnlyList<OracleCard> _draggedCards = [];
        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
        }
        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            base.OnDetaching();
        }
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(AssociatedObject);
            _draggedCards = [];

            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is not OracleCard clickedCard)
            {
                return;
            }

            var selectedCards = AssociatedObject.SelectedItems.OfType<OracleCard>().ToList();

            _draggedCards = selectedCards.Contains(clickedCard) ? selectedCards : [clickedCard];
        }
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
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
        private void StartDrag(IReadOnlyList<OracleCard> cards)
        {
            var context = new OracleCardDragContext { Cards = cards };

            _activeDragContext = context;

            try
            {
                var data = new DataObject();

                data.SetData(OracleCardDragDataFormat, context);

                ShowDragFeedback(GetDragFeedback(context));

                AssociatedObject.GiveFeedback += OnGiveFeedback;

                DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Copy);
            }
            finally
            {
                AssociatedObject.GiveFeedback -= OnGiveFeedback;
                HideDragFeedback();
                _activeDragContext = null;
                _draggedCards = [];
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
        private static DragFeedback GetDragFeedback(OracleCardDragContext context)
        {
            var isBulk = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            var quantity = isBulk ? 4 : 1;

            if (context.Cards.Count == 1)
            {
                var card = context.Cards[0];

                var quantityText = isBulk
                    ? "⇧ ×4"
                    : "×1";

                return context.IsOverValidTarget
                    ? new(DragFeedbackKind.Add, $"ADD: {card.Name}", quantityText, isBulk)
                    : new(DragFeedbackKind.NoOp, "DO NOTHING", quantityText, isBulk);
            }

            var totalQuantity = context.Cards.Count * quantity;
            var multiQuantityText = isBulk
                ? $"⇧ ×4 each · {totalQuantity} total"
                : "×1 each";

            return context.IsOverValidTarget
                ? new(DragFeedbackKind.Add, $"ADD: {context.Cards.Count} cards", multiQuantityText, isBulk)
                : new(DragFeedbackKind.NoOp, $"DO NOTHING: {context.Cards.Count} cards", multiQuantityText, isBulk);
        }
    }
}
