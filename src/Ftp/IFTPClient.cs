using System.Threading.Tasks;

namespace Datafordelen.Ftp
{
    public interface IFTPClient
    {
        void GetAddressInitialLoad(string url, string filepath, string extractPath);
        Task GetFileFtp(string host, string userName, string password, string path, string extractPath);
        void UnzipFile(string fileName, string extractPath);

    }
}