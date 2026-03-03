using CollectaMundo.ViewModels.Pages;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CollectaMundo.Presentation.Behaviors
{
    public static class DataGridColumnResizerBehavior
    {
        // =============== Attached properties ===============

        public static readonly DependencyProperty EnableAutoResizeProperty =
            DependencyProperty.RegisterAttached(
                "EnableAutoResize",
                typeof(bool),
                typeof(DataGridColumnResizerBehavior),
                new PropertyMetadata(false, OnEnableAutoResizeChanged));

        public static void SetEnableAutoResize(DependencyObject obj, bool value) => obj.SetValue(EnableAutoResizeProperty, value);
        public static bool GetEnableAutoResize(DependencyObject obj) => (bool)obj.GetValue(EnableAutoResizeProperty);

        public static readonly DependencyProperty DataGridIndexProperty = DependencyProperty.RegisterAttached("DataGridIndex", typeof(int), typeof(DataGridColumnResizerBehavior), new PropertyMetadata(-1));
        public static void SetDataGridIndex(DependencyObject obj, int value) => obj.SetValue(DataGridIndexProperty, value);
        public static int GetDataGridIndex(DependencyObject obj) => (int)obj.GetValue(DataGridIndexProperty);


        // External “poke” from VM to force recompute (e.g., when page becomes visible)
        public static readonly DependencyProperty UpdateOnTokenProperty = DependencyProperty.RegisterAttached("UpdateOnToken", typeof(int), typeof(DataGridColumnResizerBehavior), new PropertyMetadata(0, OnUpdateOnTokenChanged));
        public static void SetUpdateOnToken(DependencyObject d, int value) => d.SetValue(UpdateOnTokenProperty, value);
        public static int GetUpdateOnToken(DependencyObject d) => (int)d.GetValue(UpdateOnTokenProperty);


        // =============== Wiring ===============
        private static void OnEnableAutoResizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dg)
            {
                return;
            }

            var enable = (bool)e.NewValue;
            if (enable)
            {
                dg.Loaded += DataGrid_Loaded;
                dg.SizeChanged += OnDataGridSizeChanged;
                dg.IsVisibleChanged += OnIsVisibleChanged;

                // If already loaded & visible, do an initial render-late update
                try
                {
                    if (dg.IsLoaded && dg.IsVisible && PresentationSource.FromVisual(dg) != null)
                    {
                        BeginRenderUpdate(dg);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DataGridColumnResizerBehavior initial update ERROR: {ex.Message}");
                }
            }
            else
            {
                dg.Loaded -= DataGrid_Loaded;
                dg.SizeChanged -= OnDataGridSizeChanged;
                dg.IsVisibleChanged -= OnIsVisibleChanged;
            }
        }
        private static void DataGrid_Loaded(object? sender, RoutedEventArgs e)
        {
            if (sender is not DataGrid dg)
            {
                return;
            }

            // Wrap in try-catch to guard against invalid hwnd
            try
            {
                if (PresentationSource.FromVisual(dg) != null)
                {
                    BeginRenderUpdate(dg);
                }
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"DataGrid_Loaded error: {ex.Message}");
            }
        }
        private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not DataGrid dg)
            {
                return;
            }

            try
            {
                if (dg.IsVisible && PresentationSource.FromVisual(dg) != null)
                {
                    BeginRenderUpdate(dg);
                }
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"OnIsVisibleChanged error: {ex.Message}");
            }
        }
        private static void OnDataGridSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is not DataGrid dg)
            {
                return;
            }

            if (!dg.IsLoaded || !dg.IsVisible)
            {
                return;
            }

            try
            {
                if (PresentationSource.FromVisual(dg) == null)
                {
                    return;
                }
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"OnDataGridSizeChanged error: {ex.Message}");
                return;
            }

            // Use a short throttle to coalesce layout noise during resize
            ThrottledUpdate(dg, reason: "SizeChanged");
        }
        private static void OnUpdateOnTokenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dg)
            {
                return;
            }

            if (!dg.IsLoaded || !dg.IsVisible)
            {
                return;
            }

            try
            {
                if (PresentationSource.FromVisual(dg) == null)
                {
                    return;
                }
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"OnUpdateOnTokenChanged error: {ex.Message}");
                return;
            }

            BeginRenderUpdate(dg);
        }

        // =============== Update helpers ===============

        private static void BeginRenderUpdate(DataGrid dg)
        {
            dg.Dispatcher.BeginInvoke((Action)(() => UpdateColumnWidths(dg)), DispatcherPriority.Render);
        }
        private static void ThrottledUpdate(DataGrid dg, string reason)
        {
            if (dg.Tag is not ConditionalWeakTable<DataGrid, DispatcherTimer> table)
            {
                table = [];
                dg.Tag = table;
            }

            if (!table.TryGetValue(dg, out var timer))
            {
                timer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(80)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    BeginRenderUpdate(dg);
                };
                table.Add(dg, timer);
            }

            timer.Stop();
            timer.Start();
        }
        private static void UpdateColumnWidths(DataGrid dataGrid)
        {
            try
            {
                if (DesignerProperties.GetIsInDesignMode(dataGrid))
                {
                    return;
                }

                if (!dataGrid.IsLoaded || !dataGrid.IsVisible)
                {
                    return;
                }

                if (PresentationSource.FromVisual(dataGrid) == null)
                {
                    return;
                }

                if (dataGrid.Columns.Count == 0)
                {
                    return;
                }

                if (dataGrid.DataContext is not CardListPageViewModel vm)
                {
                    return;
                }

                var paddings = vm.HeaderPaddings;

                int cols = Math.Min(Math.Min(paddings.Count, dataGrid.Columns.Count), vm.ColumnWidths.Count);

                for (int col = 0; col < cols; col++)
                {
                    double actual = dataGrid.Columns[col].ActualWidth;
                    double desired = Math.Max(0, actual - paddings[col]);

                    vm.SetComboWidth(col, desired);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{dataGrid} update ERROR: {ex.Message}");
            }
        }
    }
}
