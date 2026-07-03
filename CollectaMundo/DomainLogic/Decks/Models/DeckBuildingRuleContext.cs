using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckBuildingRuleContext
    {
        public string? Format { get; init; }
        public IReadOnlyList<DeckCardEntry> Entries { get; init; } = [];
    }
}
