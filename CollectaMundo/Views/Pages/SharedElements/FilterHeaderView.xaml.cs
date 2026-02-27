using System.Windows;
using System.Windows.Controls;
using CollectaMundo.ViewModels;        // FilterItemViewModel
using CollectaMundo.ViewModels.Pages;  // SearchAndFilterPageViewModel (adjust if different)

namespace CollectaMundo.Views.Pages.SharedElements
{
    public partial class FilterHeaderView : UserControl
    {
        public FilterHeaderView()
        {
            InitializeComponent();
        }

        // ----- HeaderText (unchanged) -----
        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(
                nameof(HeaderText),
                typeof(string),
                typeof(FilterHeaderView),
                new PropertyMetadata(string.Empty));

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        // ----- ComboWidth (unchanged) -----
        public static readonly DependencyProperty ComboWidthProperty =
            DependencyProperty.Register(
                nameof(ComboWidth),
                typeof(double),
                typeof(FilterHeaderView),
                new PropertyMetadata(double.NaN));

        public double ComboWidth
        {
            get => (double)GetValue(ComboWidthProperty);
            set => SetValue(ComboWidthProperty, value);
        }

        // ----- NEW: FilterKey -----
        public static readonly DependencyProperty FilterKeyProperty =
            DependencyProperty.Register(
                nameof(FilterKey),
                typeof(string),
                typeof(FilterHeaderView),
                new PropertyMetadata(string.Empty, OnInputsChanged));

        public string FilterKey
        {
            get => (string)GetValue(FilterKeyProperty);
            set => SetValue(FilterKeyProperty, value);
        }

        // ----- NEW: PageVM (the page wrapper VM that contains FilterVM) -----
        public static readonly DependencyProperty PageVMProperty =
            DependencyProperty.Register(
                nameof(PageVM),
                typeof(SearchAndFilterPageViewModel),
                typeof(FilterHeaderView),
                new PropertyMetadata(null, OnInputsChanged));

        public SearchAndFilterPageViewModel? PageVM
        {
            get => (SearchAndFilterPageViewModel?)GetValue(PageVMProperty);
            set => SetValue(PageVMProperty, value);
        }

        // ----- NEW: ResolvedFilterItem (what the view actually binds to) -----
        public static readonly DependencyProperty ResolvedFilterItemProperty =
            DependencyProperty.Register(
                nameof(ResolvedFilterItem),
                typeof(FilterItemViewModel),
                typeof(FilterHeaderView),
                new PropertyMetadata(null));

        public FilterItemViewModel? ResolvedFilterItem
        {
            get => (FilterItemViewModel?)GetValue(ResolvedFilterItemProperty);
            private set => SetValue(ResolvedFilterItemProperty, value);
        }

        private static void OnInputsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterHeaderView view)
                view.ResolveFilterItem();
        }

        private void ResolveFilterItem()
        {
            ResolvedFilterItem = null;

            var vm = PageVM;
            if (vm == null)
                return;

            if (string.IsNullOrWhiteSpace(FilterKey))
                return;

            if (vm.FilterVM.Filters.TryGetValue(FilterKey, out var item))
                ResolvedFilterItem = item;
        }
    }
}
