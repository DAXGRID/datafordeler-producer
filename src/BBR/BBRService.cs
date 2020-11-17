using Datafordelen.Config;
using Datafordelen.Kafka;
using Datafordelen.Ftp;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

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
    }
}