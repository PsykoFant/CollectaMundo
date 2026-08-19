using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
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
        protected void ShowDragFeedback(string text)
        {
            var decorator = FindAncestor<AdornerDecorator>(AssociatedObject);

            if (decorator?.Child is not UIElement root)
            {
                return;
            }

            _adornerRoot = root;
            _adornerLayer = decorator.AdornerLayer;

            _dragAdorner = new DragAdorner(root, text);
            _adornerLayer.Add(_dragAdorner);

            UpdateDragFeedbackPosition();
        }

        // Update both position and text during GiveFeedback.
        protected void UpdateDragFeedback(string text)
        {
            UpdateDragFeedbackPosition();
            _dragAdorner?.UpdateText(text);
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

        // Shared visual-tree helper.
        public static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
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
