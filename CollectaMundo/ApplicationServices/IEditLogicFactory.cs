using CollectaMundo.DomainLogic;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices
{

    public interface IEditLogicFactory
    {
        IEditCollectionLogic Create(SQLiteConnection conn);
    }

}
