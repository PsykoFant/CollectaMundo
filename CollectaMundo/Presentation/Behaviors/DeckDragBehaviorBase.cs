using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;
using CollectaMundo.ViewModels.Decks;
using Microsoft.Xaml.Behaviors;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace CollectaMundo.Presentation.Behaviors
{
    public abstract partial class DeckDragBehaviorBase : Behavior<DataGrid>
    {
        // Shared drag feedback state.
        private DragAdorner? _dragAdorner;
        private AdornerLayer? _adornerLayer;
        private UIElement? _adornerRoot;



        // Shared drag-threshold helper.
        protected static bool HasExceededDragThreshold(Point start, Point current)
        {
            var horizontalDistance = Math.Abs(current.X - start.X);
            var verticalDistance = Math.Abs(current.Y - start.Y);

            return horizontalDistance >= SystemParameters.MinimumHorizontalDragDistance || verticalDistance >= SystemParameters.MinimumVerticalDragDistance;
        }

        // Show the shared theme-aware drag feedback.
        protected void ShowDragFeedback(DragFeedback feedback)
        {
            var decorator = FindAncestor<AdornerDecorator>(AssociatedObject);

            if (decorator?.Child is not UIElement root)
            {
                return;
            }

            _adornerRoot = root;
            _adornerLayer = decorator.AdornerLayer;

            _dragAdorner = new DragAdorner(root, feedback);
            _adornerLayer.Add(_dragAdorner);

            UpdateDragFeedbackPosition();
        }

        // Update both position and text during GiveFeedback.
        protected void UpdateDragFeedback(DragFeedback feedback)
        {
            UpdateDragFeedbackPosition();
            _dragAdorner?.UpdateFeedback(feedback);
        }

        // Remove the shared drag feedback.
        protected void HideDragFeedback()
        {
            if (_dragAdorner is not null && _adornerLayer is not null)
            {
                _adornerLayer.Remove(_dragAdorner);
            }

            _dragAdorner = null;
            _adornerLayer = null;
            _adornerRoot = null;
        }

        // Position from native screen coordinates because WPF mouse coordinates
        // are unreliable inside the drag/drop loop.
        private void UpdateDragFeedbackPosition()
        {
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

        // Shared visual-tree helpers.
        protected static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                if (current is T found)
                {
                    return found;
                }

                DependencyObject? parent = null;

                if (current is Visual || current is Visual3D)
                {
                    parent = VisualTreeHelper.GetParent(current);
                }

                parent ??= LogicalTreeHelper.GetParent(current);

                current = parent;
            }

            return null;
        }

        // Do not start row dragging from buttons, editors or scroll controls.
        protected static bool IsInteractiveElement(DependencyObject? source)
        {
            if (source is null)
            {
                return false;
            }

            return
                FindAncestor<ButtonBase>(source) is not null ||
                FindAncestor<TextBoxBase>(source) is not null ||
                FindAncestor<Thumb>(source) is not null ||
                FindAncestor<ScrollBar>(source) is not null;
        }

        // Shared native cursor interop.
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCursorPos(out POINT point);

        // Shared theme-aware adorner implementation.
        private sealed class DragAdorner : Adorner
        {
            private readonly Brush _background;
            private readonly Brush _foreground;
            private readonly Brush _fallbackBorderBrush;
            private readonly FontFamily _fontFamily;
            private DragFeedback _dragFeedback;
            private double _left;
            private double _top;

            public DragAdorner(UIElement adornedElement, DragFeedback feedback) : base(adornedElement)
            {
                _dragFeedback = feedback;
                IsHitTestVisible = false;

                _background = TryFindResource("CollectaMundoBackground") as Brush ?? SystemColors.InfoBrush;
                _foreground = TryFindResource("CollectaMundoForeground") as Brush ?? SystemColors.InfoTextBrush;
                _fallbackBorderBrush = TryFindResource("CollectaMundoBorder") as Brush ?? SystemColors.ActiveBorderBrush;
                _fontFamily = TryFindResource("CollectaMundoFont") as FontFamily ?? new FontFamily("Segoe UI");
            }
            public void UpdateFeedback(DragFeedback feedback)
            {
                if (_dragFeedback == feedback)
                {
                    return;
                }

                _dragFeedback = feedback;
                InvalidateVisual();
            }
            public void UpdatePosition(double left, double top)
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
            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);

                var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                var actionBrush = GetActionBrush(_dragFeedback.Kind);
                var borderThickness = _dragFeedback.IsBulk ? 4.0 : 2.0;
                var symbol = GetActionSymbol(_dragFeedback.Kind);
                var actionText = $"{symbol} {_dragFeedback.Text}";
                var text = new FormattedText(actionText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(_fontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                    13, _foreground, pixelsPerDip);
                var quantityText = new FormattedText(_dragFeedback.QuantityText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(_fontFamily, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    13, actionBrush, pixelsPerDip);

                const double horizontalPadding = 8;
                const double verticalPadding = 5;
                const double quantityGap = 12;

                var width =
                    horizontalPadding +
                    text.Width +
                    quantityGap +
                    quantityText.Width +
                    horizontalPadding;

                var height = Math.Max(text.Height, quantityText.Height) + verticalPadding * 2;
                var rect = new Rect(_left, _top, width, height);

                drawingContext.DrawRoundedRectangle(_background, new Pen(actionBrush, borderThickness), rect, 4, 4);
                drawingContext.DrawText(text, new Point(_left + horizontalPadding, _top + verticalPadding));
                drawingContext.DrawText(quantityText, new Point(_left + width - horizontalPadding - quantityText.Width, _top + verticalPadding));
            }
            private Brush GetActionBrush(DragFeedbackKind kind)
            {
                var resourceKey = kind switch
                {
                    DragFeedbackKind.Add =>
                        "DragAddBrush",

                    DragFeedbackKind.Move =>
                        "DragMoveBrush",

                    DragFeedbackKind.Delete =>
                        "DragDeleteBrush",

                    DragFeedbackKind.NoOp =>
                        "DragNoOpBrush",

                    _ =>
                        "CollectaMundoBorder"
                };

                return TryFindResource(resourceKey) as Brush ?? _fallbackBorderBrush;
            }
            private static string GetActionSymbol(DragFeedbackKind kind)
            {
                return kind switch
                {
                    DragFeedbackKind.Add => "➕",
                    DragFeedbackKind.Move => "➡️",
                    DragFeedbackKind.Delete => "🗑️",
                    DragFeedbackKind.NoOp => "🚫",
                    _ => string.Empty
                };
            }
        }
        protected enum DragFeedbackKind
        {
            Add, Move, Delete, NoOp
        }
        protected readonly record struct DragFeedback(DragFeedbackKind Kind, string Text, string QuantityText, bool IsBulk);






    }
    public sealed class OracleCardDragContext
    {
        public required IReadOnlyList<OracleCard> Cards { get; init; }
        public bool IsOverValidTarget { get; set; }
        public DeckSection? DestinationSection { get; set; }
    }
    public sealed record DeckCardDragItem(DeckCardEntryViewModel Card, int Quantity);

}
