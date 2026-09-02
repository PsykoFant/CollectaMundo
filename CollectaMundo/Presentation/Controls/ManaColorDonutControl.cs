using CollectaMundo.DomainLogic.Decks.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CollectaMundo.Presentation.Controls
{
    public sealed class ManaColorDonutControl : FrameworkElement
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(nameof(ItemsSource), typeof(IReadOnlyList<DeckStatsBucket>), typeof(ManaColorDonutControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public IReadOnlyList<DeckStatsBucket>? ItemsSource
        {
            get => (IReadOnlyList<DeckStatsBucket>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }
        private const double DonutThickness = 18;

        // ToolTip for displaying slice information
        private readonly ToolTip _toolTip = new() { Placement = PlacementMode.Mouse };
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var item = GetSliceAtPoint(e.GetPosition(this));

            if (item is null)
            {
                _toolTip.IsOpen = false;
                return;
            }

            var total = GetTotalCount();
            var percentage = total == 0 ? 0 : 100.0 * item.Count / total;

            _toolTip.Content = $"{GetColorName(item.Label)}: {item.Count} ({percentage:0.#}%)";
            _toolTip.IsOpen = true;
        }
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            _toolTip.IsOpen = false;
            base.OnMouseLeave(e);
        }
        private DeckStatsBucket? GetSliceAtPoint(Point point)
        {
            var slices = GetSlices();

            if (slices.Count == 0)
            {
                return null;
            }

            var center = GetCenter();
            var radius = GetRadius();

            var dx = point.X - center.X;
            var dy = point.Y - center.Y;

            var distance = Math.Sqrt(dx * dx + dy * dy);

            var innerRadius = radius - DonutThickness / 2;
            var outerRadius = radius + DonutThickness / 2;

            if (distance < innerRadius || distance > outerRadius)
            {
                return null;
            }

            var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            // Convert normal trig angle to:
            // 0° = top, increasing clockwise.
            angle = (angle + 90 + 360) % 360;

            return slices.FirstOrDefault(slice => angle >= slice.StartAngle && angle < slice.StartAngle + slice.SweepAngle)?.Bucket;
        }
        private static string GetColorName(string label)
        {
            return label switch
            {
                "W" => "White",
                "U" => "Blue",
                "B" => "Black",
                "R" => "Red",
                "G" => "Green",
                "M" => "Multicolor",
                "C" => "Colorless",
                _ => label
            };
        }

        // Rendering the donut chart
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var slices = GetSlices();

            if (slices.Count == 0)
            {
                return;
            }

            var center = GetCenter();
            var radius = GetRadius();

            if (radius <= 0)
            {
                return;
            }

            if (slices.Count == 1)
            {
                var item = slices[0].Bucket;

                drawingContext.DrawEllipse(null, new Pen(Brushes.Black, DonutThickness + 2), center, radius, radius);
                drawingContext.DrawEllipse(null, new Pen(GetBrush(item.Label), DonutThickness), center, radius, radius);

                return;
            }

            foreach (var slice in slices)
            {
                DrawArc(drawingContext, center, radius, slice.StartAngle - 90, slice.SweepAngle, GetBrush(slice.Bucket.Label), DonutThickness);
            }
        }
        private static void DrawArc(DrawingContext drawingContext, Point center, double radius, double startAngle, double sweepAngle, Brush brush, double thickness)
        {
            var startPoint = GetPoint(center, radius, startAngle);
            var endPoint = GetPoint(center, radius, startAngle + sweepAngle);
            var figure = new PathFigure
            {
                StartPoint = startPoint,
                IsClosed = false
            };

            figure.Segments.Add(new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = sweepAngle > 180
            });

            var geometry = new PathGeometry([figure]);

            var outlinePen = new Pen(Brushes.Black, thickness + 2)
            {
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat
            };

            var colorPen = new Pen(brush, thickness)
            {
                StartLineCap = PenLineCap.Flat,
                EndLineCap = PenLineCap.Flat
            };

            // Outline underneath
            drawingContext.DrawGeometry(null, outlinePen, geometry);

            // Colored segment on top
            drawingContext.DrawGeometry(null, colorPen, geometry);
        }
        private static Point GetPoint(Point center, double radius, double angle)
        {
            var radians = angle * Math.PI / 180.0;

            return new Point(center.X + radius * Math.Cos(radians), center.Y + radius * Math.Sin(radians));
        }
        private static Brush GetBrush(string label)
        {
            return label switch
            {
                "W" => Brushes.Bisque,
                "U" => Brushes.DodgerBlue,
                "B" => Brushes.DimGray,
                "R" => Brushes.IndianRed,
                "G" => Brushes.ForestGreen,
                "M" => Brushes.Goldenrod,
                "C" => Brushes.Silver,
                _ => Brushes.Gray
            };
        }

        // Helper methods for calculating slices and geometry
        private IReadOnlyList<DonutSlice> GetSlices()
        {
            var items = ItemsSource?.Where(x => x.Count > 0).ToList();

            if (items is null || items.Count == 0)
            {
                return [];
            }

            var total = items.Sum(x => x.Count);

            if (total <= 0)
            {
                return [];
            }

            var slices = new List<DonutSlice>();
            var startAngle = 0.0;

            foreach (var item in items)
            {
                var sweepAngle = 360.0 * item.Count / total;

                slices.Add(new DonutSlice(item, startAngle, sweepAngle));

                startAngle += sweepAngle;
            }

            return slices;
        }
        private Point GetCenter()
        {
            return new Point(ActualWidth / 2, ActualHeight / 2);
        }
        private double GetRadius()
        {
            return Math.Min(ActualWidth, ActualHeight) / 2 - DonutThickness / 2 - 1;
        }
        private int GetTotalCount()
        {
            return ItemsSource?.Where(x => x.Count > 0).Sum(x => x.Count) ?? 0;
        }
        private sealed record DonutSlice(DeckStatsBucket Bucket, double StartAngle, double SweepAngle);
    }
}
