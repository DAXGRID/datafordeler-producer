using Datafordelen.Config;
using Datafordelen.Kafka;
using Datafordelen.Ftp;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

            Directory.CreateDirectory(destinationDirectory);

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
            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs))
            using (var reader = new JsonTextReader(sr))
            {
                while (await reader.ReadAsync())
                {
                    var listName = "BBRSagList";
                    if (reader.TokenType != JsonToken.StartArray)
                    {
                        if (reader.TokenType == JsonToken.PropertyName)
                        {
                            listName = reader?.Value.ToString();
                            _logger.LogInformation(listName);
                        }

                        await reader.ReadAsync();
                    }

                    var jsonText = new List<string>();

                    while (await reader.ReadAsync())
                    {
                        if (reader.TokenType == JsonToken.EndArray)
                            break;

                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            dynamic obj = await JObject.LoadAsync(reader);

                            var item = addTypeField(obj, listName);

                            jsonText.Add(item);

                            if (jsonText.Count >= 100000)
                            {
                                if (listName.Equals("BygningList") || listName.Equals("TekniskAnlægList"))
                                {
                                    var boundingBatch = FilterPosition(jsonText, minX, minY, maxX, maxY);
                                    _producer.Produce(_appSettings.BBRTopicName, boundingBatch);
                                    _logger.LogInformation("Wrote " + boundingBatch.Count + " objects into " + _appSettings.BBRTopicName);
                                    boundingBatch.Clear();
                                    jsonText.Clear();
                                }
                                else
                                {
                                    _producer.Produce(_appSettings.BBRTopicName, jsonText);
                                    jsonText.Clear();

                                }

                            }

                        }

                    }
                    if (listName.Equals("BygningList") || listName.Equals("TekniskAnlægList"))
                    {
                        var boundingBatch = FilterPosition(jsonText, minX, minY, maxX, maxY);
                        _producer.Produce(_appSettings.BBRTopicName, boundingBatch);
                        _logger.LogInformation("Wrote " + boundingBatch.Count + " objects into " + _appSettings.BBRTopicName);
                        boundingBatch.Clear();
                        jsonText.Clear();
                    }
                    else
                    {
                        _producer.Produce(_appSettings.BBRTopicName, jsonText);
                        jsonText.Clear();

                    }
                }
            }
        }

        private JObject addTypeField(JObject obj, string type)
        {
            obj["type"] = type;
            return obj;
        }

        private List<string> FilterPosition(List<string> batch, double minX, double minY, double maxX, double maxY)
        {
            return null;
        }
    }
}