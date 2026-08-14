using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.ViewModels.Decks;
using CollectaMundo.ViewModels.Decks.Models;
using Microsoft.Xaml.Behaviors;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace CollectaMundo.Presentation.Behaviors
{
    public sealed partial class DeckCardDragDropBehavior : Behavior<DataGrid>
    {
        // Drag payload identifier shared by source and destination grids.
        private const string DragDataFormat = "CollectaMundo.DeckCard";

        // XAML-configurable destination zone and move command.
        public static readonly DependencyProperty DestinationSectionProperty = DependencyProperty.Register(nameof(DestinationSection), typeof(DeckSection), typeof(DeckCardDragDropBehavior));
        public static readonly DependencyProperty MoveCommandProperty = DependencyProperty.Register(nameof(MoveCommand), typeof(ICommand), typeof(DeckCardDragDropBehavior));

        // State belonging to the current drag operation.
        private Point _dragStartPoint;
        private DeckCardEntryViewModel? _draggedCard;


        // Visual feedback state.
        private DragAdorner? _dragAdorner;
        private AdornerLayer? _adornerLayer;
        private UIElement? _adornerRoot;

        // Public dependency-property wrappers.
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

        // Behavior lifecycle.
        protected override void OnAttached()
        {
            base.OnAttached();

            // Enable dropping and subscribe to permanent DataGrid events.
            AssociatedObject.AllowDrop = true;
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
            AssociatedObject.DragOver += OnDragOver;
            AssociatedObject.Drop += OnDrop;
        }
        protected override void OnDetaching()
        {
            // Remove permanent subscriptions and clean up any active drag.
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.Drop -= OnDrop;
            AssociatedObject.GiveFeedback -= OnGiveFeedback;
            RemoveDragAdorner();

            base.OnDetaching();
        }

        // Drag source handling.
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Remember where dragging could start and which row was pressed.

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
            // Start dragging only after the normal Windows drag threshold.

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
            // Create payload, show feedback, run WPF drag loop, then clean up.

            try
            {
                var data = new DataObject();
                data.SetData(DragDataFormat, card);

                ShowDragAdorner(card);

                AssociatedObject.GiveFeedback += OnGiveFeedback;

                DragDrop.DoDragDrop(
                    AssociatedObject,
                    data,
                    DragDropEffects.Move);
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
            // Keep the drag visual positioned and update Shift-sensitive text.

            UpdateDragAdornerFromScreenPosition();

            if (_draggedCard is not null && _dragAdorner is not null)
            {
                _dragAdorner.UpdateText(GetDragText(_draggedCard));
            }

            e.UseDefaultCursors = true;
        }

        // Drop target handling.
        private void OnDragOver(object sender, DragEventArgs e)
        {
            // Check whether this grid can accept the current drag.
            if (!TryCreateMoveRequest(e, out var request))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = MoveCommand?.CanExecute(request) == true
                ? DragDropEffects.Move
                : DragDropEffects.None;

            e.Handled = true;
        }
        private void OnDrop(object sender, DragEventArgs e)
        {
            // Execute exactly the same move shape accepted by DragOver.
            if (!TryCreateMoveRequest(e, out var request))
            {
                return;
            }

            if (MoveCommand?.CanExecute(request) != true)
            {
                return;
            }

            MoveCommand.Execute(request);

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        private bool TryCreateMoveRequest(DragEventArgs e, out DeckCardMoveRequest request)
        {
            // Validate payload, reject same-zone moves and determine quantity.

            request = null!;

            if (!TryGetDraggedCard(e, out var card))
            {
                return false;
            }

            if (card.Section == DestinationSection)
            {
                return false;
            }

            request = new DeckCardMoveRequest(card, DestinationSection, GetMoveQuantity(card));

            return true;
        }

        // Drag semantics.
        /// <summary>
        ///  vi er nået hertil...
        /// </summary>



        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCursorPos(out POINT point);  
        

        private AdornerDecorator? _adornerDecorator;









        private static int GetMoveQuantity(
    DeckCardEntryViewModel card)
        {
            return Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
                ? card.DesiredQuantity
                : 1;
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

        private void UpdateDragAdornerFromScreenPosition()
        {
            if (_dragAdorner is null ||
                _adornerRoot is null)
            {
                return;
            }

            if (!GetCursorPos(out var screenPoint))
            {
                return;
            }

            var position = _adornerRoot.PointFromScreen(
                new Point(
                    screenPoint.X,
                    screenPoint.Y));

            _dragAdorner.UpdatePosition(
                position.X + 16,
                position.Y + 16);
        }
        private void ShowDragAdorner(
    DeckCardEntryViewModel card)
        {
            _adornerDecorator =
                FindAncestor<AdornerDecorator>(
                    AssociatedObject);

            if (_adornerDecorator is null)
            {
                return;
            }

            _adornerRoot =
                _adornerDecorator.Child;

            _adornerLayer =
                _adornerDecorator.AdornerLayer;


            if (_adornerRoot is null ||
                _adornerLayer is null)
            {
                return;
            }

            _dragAdorner =
                new DragAdorner(
                    _adornerRoot,
                    GetDragText(card));

            _adornerLayer.Add(_dragAdorner);
        }


        private static string GetDragText(
    DeckCardEntryViewModel card)
        {
            var quantity = GetMoveQuantity(card);

            return $"MOVE: {card.CardName} x{quantity}";
        }
        private void RemoveDragAdorner()
        {
            if (_dragAdorner is not null &&
                _adornerLayer is not null)
            {
                _adornerLayer.Remove(
                    _dragAdorner);
            }

            _dragAdorner = null;
            _adornerLayer = null;
            _adornerRoot = null;
            _adornerDecorator = null;
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
            private string _text;

            private double _left;
            private double _top;

            public DragAdorner(
    UIElement adornedElement,
    string text)
    : base(adornedElement)
            {
                _text = text;
                IsHitTestVisible = false;
            }

            public void UpdateText(string text)
            {
                if (_text == text)
                {
                    return;
                }

                _text = text;
                InvalidateVisual();
            }
            public void UpdatePosition(
                double left,
                double top)
            {
                _left = left;
                _top = top;

                InvalidateVisual();
            }

            protected override Size MeasureOverride(Size constraint)
            {
                return AdornedElement.RenderSize;
            }

            protected override Size ArrangeOverride(Size finalSize)
            {
                return finalSize;
            }

            protected override void OnRender(
                DrawingContext drawingContext)
            {

                base.OnRender(drawingContext);

                var pixelsPerDip =
                    VisualTreeHelper.GetDpi(this).PixelsPerDip;

                var text = new FormattedText(
                    _text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(
                        new FontFamily("Segoe UI"),
                            FontStyles.Normal,
                        FontWeights.SemiBold,
                        FontStretches.Normal),
                    13,
                    SystemColors.InfoTextBrush,
                    pixelsPerDip);

                const double horizontalPadding = 8;
                const double verticalPadding = 5;

                var rect = new Rect(
                    _left,
                    _top,
                    text.Width + horizontalPadding * 2,
                    text.Height + verticalPadding * 2);

                drawingContext.DrawRoundedRectangle(
                    SystemColors.InfoBrush,
                    new Pen(
                        SystemColors.ActiveBorderBrush,
                        1),
                    rect,
                    3,
                    3);

                drawingContext.DrawText(
                    text,
                    new Point(
                        _left + horizontalPadding,
                        _top + verticalPadding));
            }
        }
    }
}
