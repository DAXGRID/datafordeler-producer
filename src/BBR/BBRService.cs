using Datafordelen.Config;
using Datafordelen.Kafka;
using Datafordelen.Ftp;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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

        public async Task  GetBBRData()
        {
            await _client.GetFileFtp(_appSettings.FtpServer, _appSettings.AdressUserName, _appSettings.AdressPassword, _appSettings.InitialAddressDataUnzipPath,_appSettings.InitialAddressDataUnzipPath);
        }
    }
}