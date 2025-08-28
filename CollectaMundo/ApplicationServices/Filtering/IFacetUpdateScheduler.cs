namespace CollectaMundo.ApplicationServices.Filtering
{
    namespace CollectaMundo.ApplicationServices.Filtering
    {
        public interface IFacetUpdateScheduler
        {
            void Schedule(Action run);
            void Cancel();
        }
    }

}
