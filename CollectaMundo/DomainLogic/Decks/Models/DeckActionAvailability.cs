using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckActionAvailability
    {
        public bool CanSetAsCommander { get; init; }
        public bool CanSetAsCompanion { get; init; }
    }
}
