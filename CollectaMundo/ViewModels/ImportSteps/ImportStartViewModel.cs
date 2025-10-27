using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace CollectaMundo.ViewModels.ImportSteps
{
    public class ImportStartViewModel
    {
        private readonly ImportViewModel _parent;

        public ICommand ContinueCommand { get; }

        public ImportStartViewModel(ImportViewModel parent)
        {
            _parent = parent;
            ContinueCommand = new RelayCommand(GoNext);
        }

        private void GoNext()
        {
            _parent.GoToNextStep(); // next step like ID mapping
        }
    }

}
