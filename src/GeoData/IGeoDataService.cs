using System.Threading.Tasks;

namespace Datafordelen.GeoData
{
    public interface IGeoDataService
    {
        Task GetLatestGeoData();
    }
}
