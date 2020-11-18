using System.Threading.Tasks;
using Datafordelen.GeoData;
using Datafordelen.Address;
using Datafordelen.BBR;


namespace Datafordelen
{
    public class Startup
    {
        private readonly IAddressService _addressService;
        private readonly IGeoDataService _geoDataService;
        private readonly IBBRService _bbrService;

        public Startup(IAddressService addressService, IGeoDataService geoDataService, IBBRService bbrService)
        {
            _addressService = addressService;
            _geoDataService = geoDataService;
            _bbrService = bbrService;
        }

        public async Task StartAsync()
        {
            await _bbrService.GetBBRData();
            //await _addressService.GetLatestAddressData();
            //await _addressService.GetinitialAddressData();
            //await _geoDataService.GetLatestGeoData();
           
           
        }
    }
}
