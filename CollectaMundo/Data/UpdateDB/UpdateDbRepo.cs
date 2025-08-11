using CollectaMundo.Data.Common;
using System.Data.SQLite;

namespace CollectaMundo.Data.UpdateDB
{
    public class UpdateDbRepo() : IUpdateDbRepo
    {
        public async Task<int> GetNumberOfSetsAsync(SQLiteConnection conn)
        {
            var sets = await DbHelpers.GetUniqueValuesAsync(conn, "sets", "code");
            return sets.Count;
        }





    }
}
