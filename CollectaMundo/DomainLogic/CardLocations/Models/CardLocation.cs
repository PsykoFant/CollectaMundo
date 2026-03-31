using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.DomainLogic.CardLocations.Models 
{ 
    public sealed class CardLocation 
    { 
        public int Id { get; init; } 
        public string Name { get; init; } = string.Empty; 
        public CardLocationType Type { get; init; } 
    } 
    public enum CardLocationType { Storage = 1, Deck = 2 } 
}

