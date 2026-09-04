using CollectaMundo.DomainLogic.Decks.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CollectaMundo.Presentation.Controls
{
    public sealed class ManaCurveChartControl : Control
    {
        private const double TopPadding = 18;
        private const double BottomLabelHeight = 18;
        private const double BaselineGap = 2;
        private const double BarWidthRatio = 0.55;
        private const double CountLabelGap = 2;

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IReadOnlyList<DeckStatsBucket>),
            typeof(ManaCurveChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
            nameof(BarBrush),
            typeof(Brush),
            typeof(ManaCurveChartControl),
            new FrameworkPropertyMetadata(SystemColors.HighlightBrush, FrameworkPropertyMetadataOptions.AffectsRender));
        public IReadOnlyList<DeckStatsBucket>? ItemsSource
        {
            get => (IReadOnlyList<DeckStatsBucket>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }
        public Brush BarBrush
        {
            get => (Brush)GetValue(BarBrushProperty);
            set => SetValue(BarBrushProperty, value);
        }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var buckets = ItemsSource;

            if (buckets is null || buckets.Count == 0)
            {
                return;
            }

            var width = ActualWidth;
            var height = ActualHeight;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            var maxCount = buckets.Max(bucket => bucket.Count);
            var baselineY = height - BottomLabelHeight - BaselineGap;
            var availableBarHeight = baselineY - TopPadding;

            if (availableBarHeight <= 0)
            {
                return;
            }

            var slotWidth = width / buckets.Count;
            var barWidth = slotWidth * BarWidthRatio;

            DrawBaseline(drawingContext, baselineY, width);

            for (var index = 0; index < buckets.Count; index++)
            {
                var bucket = buckets[index];

                var slotCenterX = index * slotWidth + slotWidth / 2;

                DrawBucket(drawingContext, bucket, slotCenterX, baselineY, availableBarHeight, barWidth, maxCount);
            }
        }
        private void DrawBucket(
            DrawingContext drawingContext,
            DeckStatsBucket bucket,
            double centerX,
            double baselineY,
            double availableBarHeight,
            double barWidth,
            int maxCount)
        {
            var barHeight = maxCount == 0 ? 0 : availableBarHeight * bucket.Count / maxCount;
            var barLeft = centerX - barWidth / 2;
            var barTop = baselineY - barHeight;

            if (barHeight > 0)
            {
                drawingContext.DrawRectangle(BarBrush, null, new Rect(barLeft, barTop, barWidth, barHeight));

                DrawCenteredText(drawingContext, bucket.Count.ToString(CultureInfo.CurrentCulture), centerX, barTop - CountLabelGap, placeAboveY: true);
            }

            DrawCenteredText(drawingContext, bucket.Label, centerX, baselineY + BaselineGap, placeAboveY: false);
        }
        private void DrawBaseline(DrawingContext drawingContext, double baselineY, double width)
        {
            var pen = new Pen(Foreground, 1);

            drawingContext.DrawLine(pen, new Point(0, baselineY), new Point(width, baselineY));
        }
        private void DrawCenteredText(DrawingContext drawingContext, string text, double centerX, double y, bool placeAboveY)
        {
            var formattedText = CreateFormattedText(text);
            var x = centerX - formattedText.Width / 2;
            var top = placeAboveY ? y - formattedText.Height : y;

            drawingContext.DrawText(formattedText, new Point(x, top));
        }
        private FormattedText CreateFormattedText(string text)
        {
            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            return new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                FontSize,
                Foreground,
                pixelsPerDip);
        }
    }
}
