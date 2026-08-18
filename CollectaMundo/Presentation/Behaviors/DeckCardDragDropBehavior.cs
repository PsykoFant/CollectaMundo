using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;
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
using System.Windows.Media.Effects;

namespace CollectaMundo.Presentation.Behaviors
{
    public sealed partial class DeckCardDragDropBehavior : Behavior<DataGrid>
    {
        // Drag payload identifier shared by source and destination grids.
        private const string DeckCardDragDataFormat = "CollectaMundo.DeckCard";
        private const string OracleCardDragDataFormat = "CollectaMundo.OracleCard";

        // XAML-configurable destination zone and move command.
        public static readonly DependencyProperty DestinationSectionProperty = DependencyProperty.Register(nameof(DestinationSection), typeof(DeckSection), typeof(DeckCardDragDropBehavior));

        // State belonging to the current drag operation.
        private Point _dragStartPoint;
        private DeckCardEntryViewModel? _draggedCard;


        // Visual feedback state.
        private DragAdorner? _dragAdorner;
        private DeckCardDragContext? _activeDragContext;
        private AdornerLayer? _adornerLayer;
        private UIElement? _adornerRoot;

        // Public dependency-property wrappers.
        public DeckSection DestinationSection
        {
            get => (DeckSection)GetValue(DestinationSectionProperty);
            set => SetValue(DestinationSectionProperty, value);
        }
        public static readonly DependencyProperty DragCommandProperty = DependencyProperty.Register(nameof(DragCommand), typeof(ICommand), typeof(DeckCardDragDropBehavior));
        public ICommand? DragCommand
        {
            get => (ICommand?)GetValue(DragCommandProperty);
            set => SetValue(DragCommandProperty, value);
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

            var context = new DeckCardDragContext
            {
                Card = card
            };

            _activeDragContext = context;

            try
            {
                var data = new DataObject();
                data.SetData(DragDataFormat, context);

                ShowDragAdorner(context);

                AssociatedObject.GiveFeedback += OnGiveFeedback;

                var effect = DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Move);

                // Only an actually unaccepted drop means delete.
                if (effect == DragDropEffects.None)
                {
                    ExecuteDelete(context.Card);
                }
            }
            finally
            {
                AssociatedObject.GiveFeedback -= OnGiveFeedback;

                RemoveDragAdorner();

                _draggedCard = null;
                _activeDragContext = null;
            }
        }
        private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            UpdateDragAdornerFromScreenPosition();

            if (_dragAdorner is not null && _activeDragContext is not null)
            {
                _dragAdorner.UpdateText(GetDragText(_activeDragContext));
            }

            e.UseDefaultCursors = true;
        }
        private void ExecuteDelete(DeckCardEntryViewModel card)
        {
            var request = new DeckCardDragRequest(card, DestinationSection: null, GetMoveQuantity(card));

            if (DragCommand?.CanExecute(request) == true)
            {
                DragCommand.Execute(request);
            }
        }

        // Drop target handling.
        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (!TryGetDragContext(e, out var context))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // Hovering the originating zone is a no-op
            if (context.Card.Section == DestinationSection)
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

            var request = new DeckCardDragRequest(context.Card, DestinationSection, GetMoveQuantity(context.Card));

            var canMove = DragCommand?.CanExecute(request) == true;

            context.IsOverValidTarget = canMove;
            context.DestinationSection = canMove ? DestinationSection : null;

            e.Effects = canMove ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }
        private void OnDragLeave(object sender, DragEventArgs e)
        {
            if (!TryGetDragContext(e, out var context))
            {
                return;
            }

            if (context.Card.Section == DestinationSection)
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
            if (!TryGetDragContext(e, out var context))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // Dropping back onto the source zone is an accepted no-op.
            if (context.Card.Section == DestinationSection)
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
        private bool TryCreateMoveRequest(DragEventArgs e, out DeckCardDragRequest request)
        {
            request = null!;

            if (!TryGetDragContext(e, out var context))
            {
                return false;
            }

            if (context.Card.Section == DestinationSection)
            {
                return false;
            }

            request = new DeckCardDragRequest(context.Card, DestinationSection, GetMoveQuantity(context.Card));

            return true;
        }

        // Drag semantics.
        private static int GetMoveQuantity(DeckCardEntryViewModel card)
        {
            // Shift moves the entire source quantity; otherwise move one.
            return Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? card.DesiredQuantity : 1;
        }
        private static string GetDragText(DeckCardDragContext context)
        {
            var quantity = GetMoveQuantity(context.Card);

            if (context.IsOverSourceZone)
            {
                return "DO NOTHING";
            }

            var action = context.IsOverValidTarget ? "MOVE" : "DELETE";

            return $"{action}: {context.Card.CardName} x{quantity}";
        }
        private static bool TryGetDragContext(DragEventArgs e, out DeckCardDragContext context)
        {
            context = null!;

            if (!e.Data.GetDataPresent(DragDataFormat))
            {
                return false;
            }

            if (e.Data.GetData(DragDataFormat)
                is not DeckCardDragContext dragContext)
            {
                return false;
            }

            context = dragContext;
            return true;
        }

        // Drag visual management.
        private void ShowDragAdorner(DeckCardDragContext context)
        {
            // All deck-zone grids share the same decorator and adorner layer.
            var decorator = FindAncestor<AdornerDecorator>(AssociatedObject);

            if (decorator?.Child is not UIElement root)
            {
                return;
            }

            _adornerRoot = root;
            _adornerLayer = decorator.AdornerLayer;
            _dragAdorner = new DragAdorner(root, GetDragText(context));
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
        private sealed class DeckCardDragContext
        {
            public required DeckCardEntryViewModel Card { get; init; }
            public bool IsOverValidTarget { get; set; }
            public bool IsOverSourceZone { get; set; }
            public DeckSection? DestinationSection { get; set; }
        }
        private sealed class OracleCardDragContext
        {
            public required OracleCard Card { get; init; }
            public bool IsOverValidTarget { get; set; }
            public DeckSection? DestinationSection { get; set; }
        }
    }
}
