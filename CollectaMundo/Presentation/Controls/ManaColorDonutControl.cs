using CollectaMundo.DomainLogic.Decks.Models;
using System.Windows;
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
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var items = ItemsSource?.Where(x => x.Count > 0).ToList();

            if (items is null || items.Count == 0)
            {
                return;
            }

            var total = items.Sum(x => x.Count);

            if (total <= 0)
            {
                return;
            }

            var center = new Point(ActualWidth / 2, ActualHeight / 2);

            const double thickness = 18;

            var radius = Math.Min(ActualWidth, ActualHeight) / 2 - thickness / 2 - 1;

            if (radius <= 0)
            {
                return;
            }

            // Special case: one bucket = full circle.
            if (items.Count == 1)
            {
                var pen = new Pen(GetBrush(items[0].Label), thickness);

                drawingContext.DrawEllipse(null, pen, center, radius, radius);

                return;
            }

            var startAngle = -90.0;

            foreach (var item in items)
            {
                var sweepAngle = 360.0 * item.Count / total;

                DrawArc(drawingContext, center, radius, startAngle, sweepAngle, GetBrush(item.Label), thickness);

                startAngle += sweepAngle;
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

            drawingContext.DrawGeometry(null, new Pen(brush, thickness), geometry);
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
    }
}
