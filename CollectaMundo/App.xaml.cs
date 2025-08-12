using CollectaMundo.ApplicationServices.Startup;
using CollectaMundo.ApplicationServices.Utilities.Progress;
using CollectaMundo.ViewModels;
using System.Diagnostics;
using System.Windows;

namespace CollectaMundo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private StatusWindow? _statusWindow;
        protected override async void OnStartup(StartupEventArgs e)
        {

            base.OnStartup(e);

            var statusVM = new StatusViewModel();
            _statusWindow = new StatusWindow { DataContext = statusVM };
            _statusWindow.Show();

            try
            {
                // in StartupComposition (or even safer: in App.OnStartup and pass it down)
                var dispatcher = Application.Current.Dispatcher;

                var progressAdapter = new Progress<SetupProgress>(u =>
                {
                    dispatcher.Invoke(() =>
                    {
                        if (u.Headline is not null)
                        {
                            statusVM.StatusLabel1 = u.Headline;
                        }

                        if (u.Detail is not null)
                        {
                            statusVM.StatusLabel2 = u.Detail;
                        }

                        if (u.Step is not null)
                        {
                            statusVM.StatusLabel3 = u.Step;
                        }

                        if (u.Percent is not null)
                        {
                            statusVM.ProgressValue = u.Percent.Value;
                        }

                        if (u.IsProgressVisible is not null)
                        {
                            statusVM.ProgressVisibility = u.IsProgressVisible.Value ? Visibility.Visible : Visibility.Collapsed;
                        }
                    });
                });

                var progressCtx = new ProgressContext(progressAdapter);

                var rootVM = await StartupComposition.BuildAndStartAsync(statusVM, progressCtx);

                var mainWindow = new MainWindow
                {
                    DataContext = rootVM
                };

                _statusWindow.Close();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                await Task.Delay(10000);
                Shutdown(-1);
            }
        }
    }
}
