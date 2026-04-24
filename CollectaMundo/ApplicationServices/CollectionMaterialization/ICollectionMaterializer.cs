using CollectaMundo.DomainLogic.CardLists.Models;
using CollectaMundo.DomainLogic.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollectaMundo.ApplicationServices.CollectionMaterialization
{
    public interface ICollectionMaterializer
    {
        CardSet MaterializeFromRow(MyCollectionRow row,IReadOnlyDictionary<string, CardCore> coreByUuid);
        IReadOnlyList<CardSet> MaterializeRows(IEnumerable<MyCollectionRow> rows, IReadOnlyDictionary<string, CardCore> coreByUuid);
        CardSet MergeIntoExisting(CardSet existing,CardSet incoming);
    }
}
