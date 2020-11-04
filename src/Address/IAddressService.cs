using System.Threading.Tasks;

namespace Datafordelen.Address
{
    public interface IAddressService
    {
        Task GetinitialAddressData();
        Task GetLatestAddressData();
    }
}
