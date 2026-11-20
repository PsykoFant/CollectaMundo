using CollectaMundo.ApplicationServices.Filtering;

namespace CollectaMundo.Tests.TestUtils
{
    internal sealed class ImmediateFacetUpdateScheduler : IFacetUpdateScheduler
    {
        public void Cancel()
        {
        }

        public void Schedule(Action action)
        {
            action();
        }
    }
}
