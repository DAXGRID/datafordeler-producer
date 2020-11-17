using System.Threading.Tasks;
using Datafordelen.GeoData;
using Datafordelen.Address;


namespace Datafordelen
{
    public class Startup
    {
        private readonly IAddressService _addressService;
        private readonly IGeoDataService _geoDataService;

        public Startup(IAddressService addressService, IGeoDataService geoDataService)
        {
            _addressService = addressService;
            _geoDataService = geoDataService;
        }

        public async Task StartAsync()
        {
            await _addressService.GetLatestAddressData();
            await _addressService.GetinitialAddressData();
            await _geoDataService.GetLatestGeoData();
            //await _geoDataService.GetLatestGeoData();
           
           
        }
    }
}
