using CollectaMundo.Data;

namespace CollectaMundo.ApplicationServices
{
    public static class AppGlobals
    {
        public static IDbConnectionFactory? DbFactory { get; set; }
    }
}
