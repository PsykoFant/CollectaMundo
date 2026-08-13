using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.ViewModels.Decks;
using CollectaMundo.ViewModels.Decks.Models;
using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace CollectaMundo.Presentation.Behaviors
{
    public sealed class DeckCardDragDropBehavior : Behavior<DataGrid>
    {
        private const string DragDataFormat = "CollectaMundo.DeckCard";
        public static readonly DependencyProperty DestinationSectionProperty = DependencyProperty.Register(nameof(DestinationSection), typeof(DeckSection), typeof(DeckCardDragDropBehavior));
        public static readonly DependencyProperty MoveCommandProperty = DependencyProperty.Register(nameof(MoveCommand), typeof(ICommand), typeof(DeckCardDragDropBehavior));

        private Point _dragStartPoint;
        private DeckCardEntryViewModel? _draggedCard;
        private DragAdorner? _dragAdorner;
        private AdornerLayer? _adornerLayer;
        public DeckSection DestinationSection
        {
            get => (DeckSection)GetValue(DestinationSectionProperty);
            set => SetValue(DestinationSectionProperty, value);
        }
        public ICommand? MoveCommand
        {
            get => (ICommand?)GetValue(MoveCommandProperty);
            set => SetValue(MoveCommandProperty, value);
        }
        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.AllowDrop = true;
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
            AssociatedObject.DragOver += OnDragOver;
            AssociatedObject.Drop += OnDrop;
        }
        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.Drop -= OnDrop;
            RemoveDragAdorner();
            base.OnDetaching();
        }
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(AssociatedObject);
            _draggedCard = null;

            if (IsInteractiveElement(e.OriginalSource as DependencyObject))
            {
                return;
            }

            var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);

            if (row?.DataContext is DeckCardEntryViewModel card)
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

            StartDrag(_draggedCard);
        }
        private void StartDrag(DeckCardEntryViewModel card)
        {
            try
            {
                var data = new DataObject();
                data.SetData(DragDataFormat, card);

                ShowDragAdorner(card);

                AssociatedObject.GiveFeedback += OnGiveFeedback;

                DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Move);
            }
            finally
            {
                AssociatedObject.GiveFeedback -= OnGiveFeedback;
                RemoveDragAdorner();
                _draggedCard = null;
            }
        }
        private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (_dragAdorner is null)
            {
                return;
            }

            var position = Mouse.GetPosition(AssociatedObject);

            _dragAdorner.UpdatePosition(position.X + 12, position.Y + 12);
        }
        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedCard(e, out var card))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            if (card.Section == DestinationSection)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var request = new DeckCardMoveRequest(card, DestinationSection);

            e.Effects = MoveCommand?.CanExecute(request) == true
                    ? DragDropEffects.Move
                    : DragDropEffects.None;

            e.Handled = true;
        }
        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedCard(e, out var card))
            {
                return;
            }

            if (card.Section == DestinationSection)
            {
                return;
            }

            var request = new DeckCardMoveRequest(card, DestinationSection);

            if (MoveCommand?.CanExecute(request) != true)
            {
                return;
            }

            MoveCommand.Execute(request);

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        private static bool TryGetDraggedCard(DragEventArgs e, out DeckCardEntryViewModel card)
        {
            card = null!;

            if (!e.Data.GetDataPresent(DragDataFormat))
            {
                return false;
            }

            if (e.Data.GetData(DragDataFormat)
                is not DeckCardEntryViewModel draggedCard)
            {
                return false;
            }

            card = draggedCard;
            return true;
        }
        private void ShowDragAdorner(DeckCardEntryViewModel card)
        {
            _adornerLayer = AdornerLayer.GetAdornerLayer(AssociatedObject);

            if (_adornerLayer is null)
            {
                return;
            }

            _dragAdorner = new DragAdorner(AssociatedObject, $"🃏 {card.CardName}");

            _adornerLayer.Add(_dragAdorner);
        }
        private void RemoveDragAdorner()
        {
            if (_dragAdorner is not null && _adornerLayer is not null)
            {
                _adornerLayer.Remove(_dragAdorner);
            }

            _dragAdorner = null;
            _adornerLayer = null;
        }
        private static bool IsInteractiveElement(DependencyObject? source)
        {
            if (source is null)
            {
                return false;
            }

            return
                FindAncestor<ButtonBase>(source)
                    is not null ||
                FindAncestor<TextBoxBase>(source)
                    is not null ||
                FindAncestor<Thumb>(source)
                    is not null ||
                FindAncestor<ScrollBar>(source)
                    is not null;
        }
        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T result)
                {
                    return result;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
        private sealed class DragAdorner : Adorner
        {
            private readonly Border _visual;
            private double _left;
            private double _top;
            public DragAdorner(UIElement adornedElement, string text) : base(adornedElement)
            {
                IsHitTestVisible = false;

                _visual = new Border
                {
                    Background = SystemColors.InfoBrush, BorderBrush = SystemColors.ActiveBorderBrush,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 3, 6, 3),
                    CornerRadius = new CornerRadius(3),
                    Child = new TextBlock
                    {
                        Text = text,
                        FontWeight =
                        FontWeights.SemiBold
                    }
                };

                AddVisualChild(_visual);
            }
            public void UpdatePosition(double left, double top)
            {
                _left = left;
                _top = top;

                InvalidateArrange();
            }
            protected override int VisualChildrenCount => 1;
            protected override Visual GetVisualChild(int index)
            {
                return index == 0
                    ? _visual
                    : throw new ArgumentOutOfRangeException(nameof(index));
            }
            protected override Size MeasureOverride(Size constraint)
            {
                _visual.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                return _visual.DesiredSize;
            }
            protected override Size ArrangeOverride(Size finalSize)
            {
                _visual.Arrange(new Rect(new Point( _left, _top), _visual.DesiredSize));
                return finalSize;
            }
        }
    }
}
