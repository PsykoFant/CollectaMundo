using CollectaMundo.DomainLogic.Shared.CardModels;
using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CollectaMundo.Presentation.Behaviors
{
    public sealed class OracleCardDragSourceBehavior : DeckDragBehaviorBase
    {
        private const string OracleCardDragDataFormat = "CollectaMundo.OracleCard";
        private OracleCardDragContext? _activeDragContext;
        private Point _dragStartPoint;
        private OracleCard? _draggedCard;
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
            _draggedCard = null;

            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);  

            if (row?.DataContext is OracleCard card)
            {
                _draggedCard = card;
            }
        }
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedCard is null)
            {
                return;
            }

            var currentPosition = e.GetPosition(AssociatedObject);
            
            if (!HasExceededDragThreshold(_dragStartPoint, currentPosition))
            {
                return;
            }

            StartDrag(_draggedCard);
        }
        private void StartDrag(OracleCard card)
        {
            var context = new OracleCardDragContext { Card = card };

            _activeDragContext = context;

            try
            {
                var data = new DataObject();

                data.SetData(OracleCardDragDataFormat, context);

                ShowDragFeedback(GetDragText(context));

                AssociatedObject.GiveFeedback += OnGiveFeedback;

                DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Copy);
            }
            finally
            {
                AssociatedObject.GiveFeedback -= OnGiveFeedback;
                HideDragFeedback();
                _activeDragContext = null;
                _draggedCard = null;
            }
        }
        private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (_activeDragContext is not null)
            {
                UpdateDragFeedback(GetDragText(_activeDragContext));
            }

            e.UseDefaultCursors = true;
        }
        private static string GetDragText(OracleCardDragContext context)
        {
            var quantity = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 4 : 1;
            var action = context.IsOverValidTarget ? "ADD" : "DO NOTHING";

            return $"{action}: {context.Card.Name} x{quantity}";
        }
    }
}
