using CollectaMundo.DomainLogic.Shared.CardModels;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace CollectaMundo.ViewModels.Decks.Models.RowViewModels
{
    public abstract class OracleCardRowViewModel : ObservableObject
    {
        public required OracleCard OracleCard { get; init; }

        public string OracleId => OracleCard.ScryfallOracleId;
        public string CardName => OracleCard.Name;
        public double? ManaValue => OracleCard.ManaValue;
        public ImageSource? ManaCostImage => OracleCard.ManaCostImage;
        public string? Type => OracleCard.Type;
    }
}
