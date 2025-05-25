using CollectaMundo.Data;
using CollectaMundo.DomainLogic;
using System.Data.SQLite;

namespace CollectaMundo.ApplicationServices
{
    public class EditLogicFactory : IEditLogicFactory
    {
        public IEditCollectionLogic Create(SQLiteConnection conn)
        {
            var repo = new EditCollectionRepository(conn);
            return new EditCollectionLogic(repo);
        }
    }
}
