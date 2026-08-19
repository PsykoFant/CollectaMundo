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
    public sealed class OracleCardDragSourceBehavior : Behavior<DataGrid>
    {
        private const string OracleCardDragDataFormat = "CollectaMundo.OracleCard";
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
            var horizontalDistance = Math.Abs(currentPosition.X - _dragStartPoint.X);
            var verticalDistance = Math.Abs(currentPosition.Y - _dragStartPoint.Y);

            if (horizontalDistance < SystemParameters.MinimumHorizontalDragDistance && verticalDistance < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var context = new OracleCardDragContext
            {
                Card = _draggedCard
            };

            var data = new DataObject();

            data.SetData(OracleCardDragDataFormat, context);

            DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Copy);

            _draggedCard = null;
        }
        private static T? FindAncestor<T>(DependencyObject? start)
            where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                if (current is T found)
                    return found;

                DependencyObject? parent = null;

                if (current is Visual || current is Visual3D)
                    parent = VisualTreeHelper.GetParent(current);

                if (parent == null)
                    parent = LogicalTreeHelper.GetParent(current);

                current = parent;
            }

            return null;
        }
    }
}
