using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.DomainLogic.Decks.Models
{
    public sealed class DeckCardValidationResult
    {
        public bool IsLegal { get; init; } = true;
        public string? Message { get; init; }
    }
}
