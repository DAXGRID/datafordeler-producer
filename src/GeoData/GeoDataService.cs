using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Datafordelen.Config;
using Datafordelen.Kafka;
using Datafordelen.Ftp;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Datafordelen.GeoData
{
    public class GeoDataService : IGeoDataService
    {
        private readonly AppSettings _appSettings;
        private readonly IFTPClient _client;
        private readonly IKafkaProducer _producer;
        private readonly ILogger<GeoDataService> _logger;

        public GeoDataService(IOptions<AppSettings> appSettings, ILogger<GeoDataService> logger, IKafkaProducer kakfkaProducer, IFTPClient ftpClient)
        {
            _appSettings = appSettings.Value;
            _logger = logger;
            _client = ftpClient;
            _producer = kakfkaProducer;
        }

        public async Task GetLatestGeoData()
        {
            await _client.GetFileFtp(_appSettings.FtpServer, _appSettings.GeoUserName, _appSettings.GeoPassword, _appSettings.GeoUnzipPath, _appSettings.GeoGmlPath);
            convertToGeojson(_appSettings.GeoFieldList, _appSettings.ConvertScriptFileName);
            ProcessGeoDirectory(_appSettings.GeoUnzipPath,
             _appSettings.GeoProcessedPath,
             _appSettings.GeoFieldList,
             _appSettings.MinX,
             _appSettings.MinY,
             _appSettings.MaxX,
             _appSettings.MaxY);
        }

        private void ProcessGeoDirectory(string sourceDirectory, string destinationDirectory, string geoFilter, double minX, double minY, double maxX, double maxY)
        {
            var fileEntries = Directory.GetFiles(sourceDirectory).ToList();
            var filtered = new List<String>();
            var filterList = geoFilter.Split(",").ToList();
            var result = fileEntries.Where(a => filterList.Any(b => a.Contains(b))).ToList();

            foreach (string fileName in result)
            {
                _logger.LogInformation(fileName);
                var fileNoExtension = Path.GetFileNameWithoutExtension(fileName);
                var dest = Path.Combine(destinationDirectory, fileNoExtension + ".json");
                filterGeoPosition(fileName, minX, maxX, minY, maxY);
                File.Move(fileName, dest);
                _logger.LogInformation(fileName + " moved in " + destinationDirectory);
            }
        }

        private void convertToGeojson(string list, string convertScriptFilename)
        {

            var filterList = list.Split(",").ToList();
            foreach (var item in filterList)
            {
                _logger.LogInformation(item);
                _logger.LogInformation(convertScriptFilename);
                var startInfo = new ProcessStartInfo()
                {
                    FileName = convertScriptFilename,

                    Arguments = item
                };

                var proc = new Process()
                {
                    StartInfo = startInfo,
                };

                proc.Start();
                proc.WaitForExit();
            }

        }

        private void filterGeoPosition(String fileName, double minX, double maxX, double minY, double maxY)
        {
            var jsonDoc = "";
            var batch = new List<string>();
            var boundingBox = new NetTopologySuite.Geometries.Envelope(minX, maxX, minY, maxY);
            var feature = new NetTopologySuite.Features.Feature();

            using (FileStream s = File.Open(fileName, FileMode.Open))
            using (var streamReader = new StreamReader(s))
            {
                var file = Path.GetFileNameWithoutExtension(fileName).Split(".");
                var typeName = file[1];

                using (var jsonreader = new Newtonsoft.Json.JsonTextReader(streamReader))
                {
                    while (jsonreader.Read())
                    {
                        var reader = new NetTopologySuite.IO.GeoJsonReader();
                        if (jsonreader.TokenType == Newtonsoft.Json.JsonToken.StartObject)
                        {
                            while (jsonreader.Read())
                            {
                                if (jsonreader.TokenType == Newtonsoft.Json.JsonToken.StartArray)
                                {
                                    while (jsonreader.Read())
                                    {
                                        try
                                        {
                                            if (jsonreader != null)
                                            {
                                                feature = reader.Read<NetTopologySuite.Features.Feature>(jsonreader);
                                            }

                                            var geo = feature.Geometry;
                                            var atr = feature.Attributes;
                                            if (boundingBox.Intersects(geo.EnvelopeInternal))
                                            {
                                                jsonDoc = createGeoObject(atr,geo,typeName);
                                                batch.Add(jsonDoc);
                                                if (batch.Count >= 5000)
                                                {
                                                    _producer.Produce(_appSettings.GeoDataTopicName, batch);
                                                    _logger.LogInformation("Wrote " + batch.Count + " objects into " + _appSettings.GeoDataTopicName);
                                                    batch.Clear();
                                                }
                                            }
                                        }
                                        //Loop gives reader exception when it reaches the last element from the file
                                        catch (Newtonsoft.Json.JsonReaderException e)
                                        {
                                            _logger.LogError("Error writing data: {0}.", e.GetType().Name);
                                            var geo = feature.Geometry;
                                            var atr = feature.Attributes;
                                            
                                            jsonDoc = createGeoObject(atr,geo,typeName);
                                            batch.Add(jsonDoc);
                                            _producer.Produce(_appSettings.GeoDataTopicName, batch);
                                            _logger.LogInformation("Wrote " + batch.Count + " objects into " + _appSettings.GeoDataTopicName);
                                            batch.Clear();
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (batch != null)
                        {
                            _producer.Produce(_appSettings.GeoDataTopicName, batch);
                            _logger.LogInformation("Wrote " + batch.Count + " objects into " + _appSettings.GeoDataTopicName);
                            batch.Clear();
                        }
                    }
                }
            }
        }

        private string createGeoObject(NetTopologySuite.Features.IAttributesTable atr, NetTopologySuite.Geometries.Geometry geo, string geoType)
        {
            var jsonObj = new
            {
                gml_id = atr.GetOptionalValue("gml_id"),
                id_lokalId = atr.GetOptionalValue("id_lokalid"),
                geo = geo.ToString(),
                type = geoType
            };
            var jsonDoc = JsonConvert.SerializeObject(jsonObj);
            return jsonDoc;
        }
    }
}
