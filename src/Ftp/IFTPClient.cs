using System.Threading.Tasks;

namespace Datafordelen.Ftp
{
    public interface IFTPClient
    {
        void GetAddressInitialLoad(string url, string filepath, string extractPath);
        Task GetAddressInitialFtp(string host, string path, string extractPath);
        Task GetFileFtp(string host, string userName, string password, string path, string extractPath);

    }
}