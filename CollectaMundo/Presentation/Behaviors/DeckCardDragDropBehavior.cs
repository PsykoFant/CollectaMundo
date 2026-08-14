using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.ViewModels.Decks;
using CollectaMundo.ViewModels.Decks.Models;
using Microsoft.Xaml.Behaviors;
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
        private static int GetMoveQuantity(DeckCardEntryViewModel card)
        {
            // Shift moves the entire source quantity; otherwise move one.
            return Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? card.DesiredQuantity : 1;
        }
        private static string GetDragText(DeckCardEntryViewModel card)
        {
            // Keep displayed quantity consistent with actual move quantity.

            var quantity = GetMoveQuantity(card);

            return $"MOVE: {card.CardName} x{quantity}";
        }
        private static bool TryGetDraggedCard(DragEventArgs e, out DeckCardEntryViewModel card)
        {
            // Extract only payloads created by this behavior.

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

        // Drag visual management.
        private void ShowDragAdorner(DeckCardEntryViewModel card)
        {
            // All deck-zone grids share the same decorator and adorner layer.
            var decorator = FindAncestor<AdornerDecorator>(AssociatedObject);

            if (decorator?.Child is not UIElement root)
            {
                return;
            }

            _adornerRoot = root;
            _adornerLayer = decorator.AdornerLayer;
            _dragAdorner = new DragAdorner(root, GetDragText(card));
            _adornerLayer.Add(_dragAdorner);

            // Avoid briefly rendering at the upper-left corner.
            UpdateDragAdornerFromScreenPosition();
        }
        private void UpdateDragAdornerFromScreenPosition()
        {
            // Read native screen cursor position and convert it to adorner coordinates.

            if (_dragAdorner is null || _adornerRoot is null)
            {
                return;
            }

            if (!GetCursorPos(out var screenPoint))
            {
                return;
            }

            var position = _adornerRoot.PointFromScreen(new Point(screenPoint.X, screenPoint.Y));

            _dragAdorner.UpdatePosition(position.X + 16, position.Y + 16);
        }
        private void RemoveDragAdorner()
        {
            // Remove visual feedback and clear all adorner references.

            if (_dragAdorner is not null && _adornerLayer is not null)
            {
                _adornerLayer.Remove(_dragAdorner);
            }

            _dragAdorner = null;
            _adornerLayer = null;
            _adornerRoot = null;
        }

        // Visual-tree helpers.
        private static bool IsInteractiveElement(DependencyObject? source)
        {
            // Do not start row dragging from buttons, editors or scroll controls.

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
            // Walk upward through the WPF visual tree.

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

        // Windows interop used because WPF mouse coordinates are unreliable while the native drag/drop loop is active.

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCursorPos(out POINT point);

        // Lightweight visual rendered in the shared AdornerLayer.
        private sealed class DragAdorner : Adorner
        {
            private readonly Brush _background;
            private readonly Brush _foreground;
            private readonly Brush _borderBrush;
            private readonly FontFamily _fontFamily;
            private string _text;
            private double _left;
            private double _top;
            public DragAdorner(UIElement adornedElement, string text) : base(adornedElement)
            {
                _text = text;
                IsHitTestVisible = false;

                _background = TryFindResource("CollectaMundoBackground") as Brush ?? SystemColors.InfoBrush;
                _foreground = TryFindResource("CollectaMundoForeground") as Brush ?? SystemColors.InfoTextBrush;
                _borderBrush = TryFindResource("CollectaMundoBorder") as Brush ?? SystemColors.ActiveBorderBrush;
                _fontFamily = TryFindResource("CollectaMundoFont") as FontFamily ?? new FontFamily("Segoe UI");
            }

            public void UpdateText(string text)
            {
                // Redraw only when Shift changes the displayed quantity.

                if (_text == text)
                {
                    return;
                }

                _text = text;
                InvalidateVisual();
            }
            public void UpdatePosition(double left, double top)
            {
                // Move the rendered feedback next to the cursor.

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
            protected override void OnRender(DrawingContext drawingContext)
            {
                // Draw the tooltip-like background and drag description.

                base.OnRender(drawingContext);

                var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

                var text = new FormattedText(
                    _text, 
                    CultureInfo.CurrentCulture, 
                    FlowDirection.LeftToRight,
                    new Typeface(_fontFamily, 
                    FontStyles.Normal, 
                    FontWeights.SemiBold, 
                    FontStretches.Normal),
                    13,
                    _foreground,
                    pixelsPerDip);

                const double horizontalPadding = 8;
                const double verticalPadding = 5;

                var rect = new Rect(_left, _top, text.Width + horizontalPadding * 2, text.Height + verticalPadding * 2);

                drawingContext.DrawRoundedRectangle(_background, new Pen(_borderBrush, 1), rect, 3, 3);
                drawingContext.DrawText(text, new Point(_left + horizontalPadding, _top + verticalPadding));
            }
        }
    }
}
