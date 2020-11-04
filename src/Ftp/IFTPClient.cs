using System.Threading.Tasks;

namespace Datafordelen.Ftp
{
    public interface IFTPClient
    {
        void GetAddressInitialLoad(string url, string filepath);
        Task GetFileFtp(string host, string userName, string password, string path);
        void UnzipFile(string path, string extractPath);

    }
}