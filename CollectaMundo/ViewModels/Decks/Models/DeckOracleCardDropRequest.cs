using CollectaMundo.DomainLogic.Decks.Models.Enums;
using CollectaMundo.DomainLogic.Shared.CardModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ViewModels.Decks.Models
{
    public sealed record DeckOracleCardDropRequest(OracleCard Card, DeckSection DestinationSection, int Quantity);
}
