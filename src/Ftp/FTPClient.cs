using System;
using System.Xml;
using System.ServiceModel.Syndication;
using System.Net;
using System.IO;
using System.Collections.Generic;
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

        public async Task GetAddressInitialFtp(string host, string path, string extractPath)
        {
            FtpClient client = new FtpClient(host);
            var items = new List<string>();

            try
            {
                client = new FtpClient(host);

                await client.ConnectAsync();
                // get a list of files and directories in the "/" folder
                foreach (var item in await client.GetListingAsync("/"))
                {
                    // if this is a file
                    if (item.Type == FtpFileSystemObjectType.File)
                    {
                        //Download the first item from the folder as the newer documents from 2021 contains no data
                        items.Add(item.FullName);
                        await DownloadFileFtp(client, path + items[0], "/" + items[0], extractPath);
                    }
                }
            }
            catch (Exception e)
            {
                if (e is FtpAuthenticationException)
                {
                    _logger.LogError("Wrong credentials");
                    Environment.Exit(1);
                }
                else if (e is FtpException || e is System.ArgumentNullException)
                {
                    _logger.LogError("Host not available or wrong host");
                    Environment.Exit(1);
                }
            }
            // TODO Might not be needed
            await client.DisconnectAsync();
        }

        public async Task GetFileFtp(string host, string userName, string password, string path, string extractPath)
        {
            FtpClient client = new FtpClient(host);
            var items = new List<string>();

            try
            {
                client = new FtpClient(host);
                client.Credentials = new NetworkCredential(userName, password);

                await client.ConnectAsync();
                // get a list of files and directories in the "/" folder
                foreach (var item in await client.GetListingAsync("/"))
                {
                    // if this is a file
                    if (item.Type == FtpFileSystemObjectType.File)
                    {
                        //Specific check for the BBR data
                        if (item.FullName.Contains("BBR_Aktuelt") == true)
                        {
                            items.Add(item.FullName);
                            await DownloadFileFtp(client, path + items[0], "/" + items[0], extractPath);
                        }
                        else
                        {
                            await DownloadFileFtp(client, path + item.FullName, "/" + item.FullName, extractPath);
                        }

                    }
                }
            }
            catch (Exception e)
            {
                if (e is FtpAuthenticationException)
                {
                    _logger.LogError("Wrong credentials");
                    Environment.Exit(1);
                }
                else if (e is FtpException || e is System.ArgumentNullException)
                {
                    _logger.LogError("Host not available or wrong host");
                    Environment.Exit(1);
                }
            }
            // TODO Might not be needed
            await client.DisconnectAsync();
        }



        private void UnzipFile(string fileName, string extractPath)
        {
            ZipFile.ExtractToDirectory(fileName, extractPath);
            _logger.LogInformation("File unzipped");
        }

        private async Task DownloadFileFtp(FtpClient client, string localPath, string remotePath, string extractPath)
        {
            //Check if the file already exists in the local path
            if (await client.CompareFileAsync(localPath, "/" + remotePath, FtpCompareOption.Size) == FtpCompareResult.Equal)
            {

                _logger.LogInformation("item already exists " + remotePath);
            }
            else
            {
                _logger.LogInformation("Item needs to be added " + remotePath);
                await client.DownloadFileAsync(localPath, "/" + remotePath);

                UnzipFile(localPath, extractPath);
            }
        }
    }
}
