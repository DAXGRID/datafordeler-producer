using Datafordelen.Config;
using Datafordelen.Kafka;
using Datafordelen.Ftp;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Datafordelen.BBR
{
    public class BBRService : IBBRService
    {
        private readonly AppSettings _appSettings;
        private readonly IFTPClient _client;
        private readonly IKafkaProducer _producer;
        private readonly ILogger<BBRService> _logger;

        public BBRService(IOptions<AppSettings> appSettings, ILogger<BBRService> logger, IKafkaProducer kakfkaProducer, IFTPClient ftpClient)
        {
            _appSettings = appSettings.Value;
            _logger = logger;
            _client = ftpClient;
            _producer = kakfkaProducer;
        }

        public async Task GetBBRData()
        {
            await _client.GetFileFtp(_appSettings.FtpServer, _appSettings.BBRUserName, _appSettings.BBRPassword, _appSettings.BBRUnzipPath, _appSettings.BBRUnzipPath);
            await ProcessBBRFiless(
                _appSettings.BBRUnzipPath,
                _appSettings.BBRProcessedPath,
                _appSettings.MinX,
                _appSettings.MinY,
                _appSettings.MaxX,
                _appSettings.MaxY
            );
        }

        private async Task ProcessBBRFiless(string sourceDirectory, string destinationDirectory, double minX, double minY, double maxX, double maxY)
        {
            var destinfo = new DirectoryInfo(destinationDirectory);
            if (destinfo.Exists == false)
            {
                Directory.CreateDirectory(destinationDirectory);
            }
            var sourceinfo = new DirectoryInfo(sourceDirectory);
            var dirs = sourceinfo.GetDirectories();

            var fileEntries = Directory.GetFiles(sourceDirectory).ToList();
            foreach (string fileName in fileEntries)
            {
                if (!fileName.Contains("Metadata") && !fileName.Contains(".zip"))
                {
                    await BBRToKafka(fileName, minX, minY, maxX, maxY);
                    var file = Path.GetFileName(fileName);
                    var destFile = Path.Combine(destinationDirectory, file);
                    File.Move(fileName, destFile);
                    _logger.LogInformation(fileName + " moved in " + destFile);
                }
                else if (fileName.Contains("Metada"))
                {
                    var file = Path.GetFileName(fileName);
                    var destFile = Path.Combine(destinationDirectory, file);
                    File.Move(fileName, destFile);
                    _logger.LogInformation(fileName + " moved in " + destFile);
                }
                else
                {
                    _logger.LogInformation("Zip file still in folder");
                }
            }
        }

        private async Task BBRToKafka(string filename, double minX, double minY, double maxX, double maxY)
        {
            
        }
    }
}