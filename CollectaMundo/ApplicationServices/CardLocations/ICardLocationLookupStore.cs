using CollectaMundo.DomainLogic.CardLocations.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ApplicationServices.CardLocations
{
    public interface ICardLocationLookupStore
    {
        IReadOnlyList<CardLocation> GetAll();
        CardLocation? Get(int id);

        void ReplaceAll(IReadOnlyList<CardLocation> locations);
        void Upsert(CardLocation location);
        bool Remove(int id);

        event EventHandler? LocationsChanged;
    }
}
