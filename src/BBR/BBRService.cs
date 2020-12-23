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
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;

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

                    var jsonText = new List<JObject>();

                    while (await reader.ReadAsync())
                    {
                        if (reader.TokenType == JsonToken.EndArray)
                            break;

                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            dynamic obj = await JObject.LoadAsync(reader);

                            var item = addTypeField(obj, listName);
                            //var objText = JsonConvert.SerializeObject(item, Formatting.Indented);
                            jsonText.Add(TranslateTimeFields(item));

                            if (jsonText.Count >= 100000)
                            {
                                processBBRDataToKakfa(listName,jsonText,minX,minY,maxX,maxY);
                            }

                        }

                    }
                    processBBRDataToKakfa(listName,jsonText,minX,minY,maxX,maxY);
                }
            }
        }

        private void processBBRDataToKakfa(string listName, List<JObject> documents, double minX, double minY, double maxX, double maxY)
        {
            if (listName.Equals("BygningList") || listName.Equals("TekniskAnlægList"))
            {
                var boundingBatch = FilterPosition(documents, minX, minY, maxX, maxY);
                _producer.Produce(_appSettings.BBRTopicName, boundingBatch);
                _logger.LogInformation("Wrote " + boundingBatch.Count + " objects into " + _appSettings.BBRTopicName);
                boundingBatch.Clear();
                documents.Clear();
            }
            else
            {
                _producer.Produce(_appSettings.BBRTopicName, documents);
                _logger.LogInformation("Wrote " + documents.Count + " objects into " + _appSettings.BBRTopicName);
                documents.Clear();

            }

        }

        private JObject addTypeField(JObject obj, string type)
        {
            obj["type"] = type;
            return obj;
        }


        public string ChangeDateFormat(string dateString)
        {
            DateTime result;
            if (dateString == null)
            {
                //Do nothing
                dateString = "null";
                return dateString;
            }
            else
            {
                try
                {
                    result = DateTime.Parse(dateString, null, System.Globalization.DateTimeStyles.RoundtripKind);
                    return result.ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch (ArgumentNullException)
                {

                    _logger.LogError("{0} is not in the correct format.", dateString);
                }
            }
            return String.Empty;
        }

        private List<JObject> FilterPosition(List<JObject> batch, double minX, double minY, double maxX, double maxY)
        {
            var filteredBatch = new List<JObject>();
            var geometryFactory = new GeometryFactory();
            Geometry point;
            var rdr = new WKTReader(geometryFactory);
            var boundingBox = new NetTopologySuite.Geometries.Envelope(minX, maxX, minY, maxY);

            foreach (var document in batch)
            {
                try
                {
                    foreach (var jp in document.Properties().ToList())
                    {
                        if (jp.Name == "byg404Koordinat")
                        {
                            point = rdr.Read(jp.Value.ToString());
                            if (boundingBox.Intersects(point.EnvelopeInternal))
                            {
                                filteredBatch.Add(document);
                            }
                        }
                        else if (jp.Name == "tek109Koordinat")
                        {
                            point = rdr.Read(jp.Value.ToString());
                            if (boundingBox.Intersects(point.EnvelopeInternal))
                            {
                                filteredBatch.Add(document);
                            }
                        }
                    }
                }
                catch (NetTopologySuite.IO.ParseException e)
                {
                    _logger.LogError("Error writing data: {0}.", e.GetType().Name);
                    _logger.LogInformation(document.ToString());
                    break;
                }
            }

            return filteredBatch;
        }

        private JObject TranslateTimeFields(JObject jo)
        {
            foreach (var jp in jo.Properties().ToList())
            {
                switch (jp.Name)
                {
                    case "registreringFra":
                        var dateValue = (string)jp.Value;
                        jp.Value = ChangeDateFormat(dateValue);
                        jp.Replace(new JProperty("registrationFrom", jp.Value));
                        break;

                    case "registreringTil":
                        dateValue = (string)jp.Value;
                        jp.Value = ChangeDateFormat(dateValue);
                        jp.Replace(new JProperty("registrationTo", jp.Value));
                        break;

                    case "virkningFra":
                        dateValue = (string)jp.Value;
                        jp.Value = ChangeDateFormat(dateValue);
                        jp.Replace(new JProperty("effectFrom", jp.Value));
                        break;

                    case "virkningTil":
                        dateValue = (string)jp.Value;
                        jp.Value = ChangeDateFormat(dateValue);
                        jp.Replace(new JProperty("effectTo", jp.Value));
                        break;
                }
            }

            return jo;
        }
    }
}