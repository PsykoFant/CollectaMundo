using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.DeckManagement.Models;
using System.ComponentModel;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace CollectaMundo
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Set up varibales

        private static MainWindow? _currentInstance;
        public static MainWindow CurrentInstance
        {
            get
            {
                if (_currentInstance == null)
                {
                    throw new InvalidOperationException("CurrentInstance is not initialized.");
                }
                return _currentInstance;
            }
            private set => _currentInstance = value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        // Objects for deck management
        public readonly List<Deck> allDecks = [];
        public Deck CurrentDeck { get; set; } = new Deck();
        public List<string> allFormats = [];


        #endregion
        public MainWindow()
        {
            InitializeComponent();
            _currentInstance = this;

        }
    }
}
