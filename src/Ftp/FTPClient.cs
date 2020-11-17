using System;
using System.Xml;
using System.ServiceModel.Syndication;
using System.Net;
using System.IO;
using FluentFTP;
using System.Threading.Tasks;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace Datafordelen.Ftp
{
    public class FTPClient : IFTPClient
    {
        private readonly ILogger<FTPClient> _logger;
        public FTPClient(ILogger<FTPClient> logger)
        {
            _logger = logger;
        }
        public void GetAddressInitialLoad(String url, String filepath, string extractPath)
        {
            var downloadLink = String.Empty;
            String[] feedwords = null;

            using var reader = XmlReader.Create(url);

            var feed = SyndicationFeed.Load(reader);
            reader.Close();
            foreach (var item in feed.Items)
            {
                var subject = item.Title.Text;
                var summary = item.Summary.Text;
                Console.WriteLine(summary);
                feedwords = summary.Split(' ');
            }

            // The link for downloading the necessary it is at the specified position
            downloadLink = feedwords[5];
            _logger.LogInformation(downloadLink);
            Directory.CreateDirectory(extractPath);

            var client = new WebClient();
            client.DownloadFile(
                downloadLink, filepath);

            UnzipFile(filepath, extractPath);

        }

        public async Task GetFileFtp(string host, string userName, string password, string path, string extractPath)
        {
            using var client = new FtpClient(host);
            if (client != null)
            {
                client.Credentials = new NetworkCredential(userName, password);
            }
            else
            {
                _logger.LogInformation("Host is not available");
            }

            if (client.Credentials != null)
            {
                await client.ConnectAsync();
                // get a list of files and directories in the "/" folder
                foreach (var item in await client.GetListingAsync("/"))
                {
                    // if this is a file
                    if (item.Type == FtpFileSystemObjectType.File)
                    {

                        //Check if the file already exists in the local path
                        if (await client.CompareFileAsync(path + item.FullName, "/" + item.FullName, FtpCompareOption.Size) == FtpCompareResult.Equal)
                        {

                            _logger.LogInformation("item already exists " + item.FullName);
                        }
                        else
                        {

                            _logger.LogInformation("Item needs to be added " + item.FullName);
                            await client.DownloadFileAsync(path + item.FullName, "/" + item.FullName);

                            UnzipFile(path + item.FullName, extractPath);
                        }
                    }
                }
            }
            else
            {
                _logger.LogInformation("Wrong credentials");
            }
            // TODO Might not be needed
            await client.DisconnectAsync();
        }

        private void UnzipFile(string fileName, string extractPath)
        {
            ZipFile.ExtractToDirectory(fileName, extractPath);
            _logger.LogInformation("File unzipped");
        }
    }
}
