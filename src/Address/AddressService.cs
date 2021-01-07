using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Datafordelen.Config;
using Datafordelen.Kafka;
using Datafordelen.Ftp;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;


namespace Datafordelen.Address
{
    public class AddressService : IAddressService
    {
        private readonly AppSettings _appSettings;
        private IFTPClient _client;
        private IKafkaProducer _kafkaProducer;

        private readonly ILogger<AddressService> _logger;
        private List<JObject> adresspunktBatch = new List<JObject>();
        private List<JObject> HussnumerNew = new List<JObject>();
        private List<JObject> NavngivenVejNew = new List<JObject>();

        public AddressService(IOptions<AppSettings> appSettings, ILogger<AddressService> logger, IFTPClient ftpClient, IKafkaProducer kafkaProducer)
        {
            _appSettings = appSettings.Value;
            _client = ftpClient;
            _kafkaProducer = kafkaProducer;
            _logger = logger;
        }

        public async Task GetinitialAddressData()
        {
            //_client.GetAddressInitialLoad(_appSettings.InitialAddressDataUrl, _appSettings.InitialAddressDataZipFilePath, _appSettings.InitialAddressDataUnzipPath);
            await _client.GetAddressInitialFtp(_appSettings.FtpServer, _appSettings.InitialAddressDataUnzipPath, _appSettings.InitialAddressDataUnzipPath);
            await ProcessLatestAdresses(
                _appSettings.InitialAddressDataUnzipPath,
                _appSettings.InitialAddressDataProcessedPath,
                _appSettings.MinX,
                _appSettings.MinY,
                _appSettings.MaxX,
                _appSettings.MaxY);
        }

        public async Task GetLatestAddressData()
        {
            await _client.GetFileFtp(_appSettings.FtpServer, _appSettings.AdressUserName, _appSettings.AdressPassword, _appSettings.InitialAddressDataUnzipPath, _appSettings.InitialAddressDataUnzipPath);
            await ProcessLatestAdresses(
                _appSettings.InitialAddressDataUnzipPath,
                _appSettings.InitialAddressDataProcessedPath,
                _appSettings.MinX,
                _appSettings.MinY,
                _appSettings.MaxX,
                _appSettings.MaxY);
        }

        private async Task ProcessLatestAdresses(string sourceDirectory, string destinationDirectory, double minX, double minY, double maxX, double maxY)
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
                    await AdressToKafka(fileName, minX, minY, maxX, maxY);
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
                /*
                else
                {
                    _logger.LogInformation("Zip file still in folder");
                }
                */
            }
        }

        private async Task AdressToKafka(string filename, double minX, double minY, double maxX, double maxY)
        {

            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs))
            using (var reader = new JsonTextReader(sr))
            {
                while (await reader.ReadAsync())
                {
                    // Advance the reader to start of first array,
                    // which should be value of the "Stores" property
                    var listName = "AdresseList";
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

                    // Now process each store individually
                    while (await reader.ReadAsync())
                    {
                        if (reader.TokenType == JsonToken.EndArray)
                            break;

                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            dynamic obj = await JObject.LoadAsync(reader);


                            var item = addTypeField(obj, listName);

                            jsonText.Add(ChangeAdressNames(item));

                            if (jsonText.Count >= 100000)
                            {
                                _logger.LogInformation("This is the listname " + listName);
                                ProcessDataForKafka(listName, jsonText, minX, minY, maxX, maxY);
                            }
                        }
                    }

                    ProcessDataForKafka(listName, jsonText, minX, minY, maxX, maxY);
                }
            }
        }

        private void ProcessDataForKafka(string listName, List<JObject> documents, double minX, double minY, double maxX, double maxY)
        {

            var hussnummerBatch = new List<JObject>();
            var newHussnummerBatch = new List<JObject>();
            var HussnumerNavngivej = new List<JObject>();

            if (listName.Equals("AdressepunktList"))
            {
                var boundingBatch = newfilterAdressPosition(documents, minX, minY, maxX, maxY);
                foreach (var b in boundingBatch)
                {
                    adresspunktBatch.Add(b);
                }
                _kafkaProducer.Produce(_appSettings.AdressTopicName, checkLatestDataDuplicates(boundingBatch));
                _logger.LogInformation(@$"Wrote Adressepunkt {checkLatestDataDuplicates(boundingBatch).Count}");
                boundingBatch.Clear();


                documents.Clear();


            }
            else if (listName.Equals("HusnummerList"))
            {
                foreach (var ob in documents)
                {
                    hussnummerBatch.Add(ob);
                }
                documents.Clear();

                var noDuplicatesAdressPunkt = checkLatestDataDuplicates(adresspunktBatch);
                var noDuplicatesHussnumer = checkLatestDataDuplicates(hussnummerBatch);


                if (noDuplicatesAdressPunkt.Count > 0 && noDuplicatesHussnumer.Count > 0)
                {
                    newHussnummerBatch = AddPositionToHouseObjects(noDuplicatesAdressPunkt, noDuplicatesHussnumer);

                    foreach (var doc in newHussnummerBatch)
                    {
                        HussnumerNew.Add(doc);
                    }

                    //_kafkaProducer.Produce(_appSettings.AdressTopicName, newHussnummerBatch);

                    //_logger.LogInformation(@$"Wrote  merged objects  {newHussnummerBatch.Count}  objects into  {_appSettings.AdressTopicName}");
                    noDuplicatesHussnumer.Clear();
                    hussnummerBatch.Clear();
                    newHussnummerBatch.Clear();

                }

            }
            else if (listName.Equals("NavngivenVejList"))
            {
                var boundingBatch = newfilterAdressPosition(documents, minX, minY, maxX, maxY);
                var noDuplicatesNavngivenVej = checkLatestDataDuplicates(boundingBatch);

                _kafkaProducer.Produce(_appSettings.AdressTopicName, noDuplicatesNavngivenVej);
                _logger.LogInformation(@$"Wrote Navnigivenvej  {boundingBatch.Count}   objects into  {_appSettings.AdressTopicName}");
                foreach (var doc in noDuplicatesNavngivenVej)
                {
                    NavngivenVejNew.Add(doc);
                }


                noDuplicatesNavngivenVej.Clear();
                boundingBatch.Clear();
                documents.Clear();


            }
            else if (listName.Equals("NavngivenVejKommunedelList"))
            {
                var noDuplicates = checkLatestDataDuplicates(documents);

                _kafkaProducer.Produce(_appSettings.AdressTopicName, noDuplicates);
                _logger.LogInformation(@$"Wrote  {documents.Count}   objects into  {_appSettings.AdressTopicName}");
                documents.Clear();

                //Trick used to add the roadname into the house objects as NavngivenVejList is after HussnumerList we need all the data from both lists in order to make the dictionary lookup 
                HussnumerNavngivej = AddRoadNameToHouseObjects(NavngivenVejNew, HussnumerNew);
                _kafkaProducer.Produce(_appSettings.AdressTopicName, HussnumerNavngivej);
                _logger.LogInformation(@$"Wrote newHussnumer  {HussnumerNavngivej.Count}   objects into  {_appSettings.AdressTopicName}");
            }
            else
            {
                var noDuplicates = checkLatestDataDuplicates(documents);
                _kafkaProducer.Produce(_appSettings.AdressTopicName, noDuplicates);
                _logger.LogInformation(@$"Wrote  {documents.Count}   objects into  {_appSettings.AdressTopicName}");
                documents.Clear();


            }
        }

        private List<JObject> AddPositionToHouseObjects(List<JObject> addresspunktItems, List<JObject> HussnummerItems)
        {
            var newHussnummerItems = new List<JObject>();


            var addrespunktLookup = addresspunktItems.Select(x => new KeyValuePair<string, JObject>(x["id_lokalId"].ToString(), x)).ToDictionary(x => x.Key, x => x.Value);


            foreach (var house in HussnummerItems)
            {
                var houseAdressPoint = (string)house["addressPoint"];

                if (addrespunktLookup.TryGetValue(houseAdressPoint, out var address))
                {
                    house["position"] = address["position"];
                    //it may not even be necessary to add the documents
                    newHussnummerItems.Add(house);
                }
            }

            return newHussnummerItems;
        }

        private List<JObject> AddRoadNameToHouseObjects(List<JObject> roadNameItems, List<JObject> HussnummerItems)
        {
            var newHussnummerItems = new List<JObject>();
            //Extra check for duplicates
            roadNameItems = checkLatestDataDuplicates(roadNameItems);



            var roadNameLookup = roadNameItems.Select(x => new KeyValuePair<string, JObject>(x["id_lokalId"].ToString(), x)).ToDictionary(x => x.Key, x => x.Value);

            foreach (var house in HussnummerItems)
            {

                var houseRoadName = (string)house["namedRoad"];
                try
                {
                    if (roadNameLookup.TryGetValue(houseRoadName, out var road))
                    {
                        house["roadName"] = road["roadName"];
                        //it may not even be necessary to add the documents
                        newHussnummerItems.Add(house);
                    }
                }
                catch (ArgumentNullException)
                {
                    _logger.LogError("No road object " + house);
                }
            }

            return newHussnummerItems;
        }


        private JObject addTypeField(JObject obj, string type)
        {
            obj["type"] = type;
            return obj;
        }

        private List<JObject> newfilterAdressPosition(List<JObject> batch, double minX, double minY, double maxX, double maxY)
        {

            var filteredBatch = new List<JObject>();
            var geometryFactory = new GeometryFactory();
            Geometry line;
            Geometry point;
            Geometry polygon;
            Geometry multipoint;
            string name;
            string value = "";
            var rdr = new WKTReader(geometryFactory);
            var boundingBox = new NetTopologySuite.Geometries.Envelope(minX, maxX, minY, maxY);
            foreach (var document in batch)
            {
                foreach (var jp in document.Properties().ToList())
                {
                    try
                    {
                        if (jp.Name == "position")
                        {
                            point = rdr.Read(jp.Value.ToString());
                            if (boundingBox.Intersects(point.EnvelopeInternal))
                            {
                                filteredBatch.Add(document);
                            }
                        }
                        else if (jp.Name == "roadRegistrationRoadLine")
                        {

                            if (jp.Value != null)
                            {
                                line = rdr.Read(jp.Value.ToString());
                                if (boundingBox.Intersects(line.EnvelopeInternal))
                                {
                                    filteredBatch.Add(document);
                                }
                            }
                            else
                            {
                                if (jp.Name == "roadRegistrationRoadArea")
                                {
                                    name = jp.Name;
                                    value = (string)jp.Value;
                                    polygon = rdr.Read(jp.Value.ToString());

                                    if (boundingBox.Intersects(polygon.EnvelopeInternal))
                                    {
                                        filteredBatch.Add(document);
                                    }
                                }

                                else if (jp.Name == "roadRegistrationRoadConnectionPoints")
                                {
                                    multipoint = rdr.Read(jp.Value.ToString());

                                    if (boundingBox.Intersects(multipoint.EnvelopeInternal))
                                    {
                                        filteredBatch.Add(document);
                                    }
                                }
                            }
                        }



                    }
                    /* Gets parse exception when they are values in both roadRegistrationRoadConnectionPoints and roadRegistrationArea,
                          also when they are null values in those fields
                       */
                    catch (NetTopologySuite.IO.ParseException e)
                    {
                        _logger.LogError("Error writing data: {0}.", e.GetType().Name);
                        _logger.LogInformation(document.ToString());
                        break;
                    }
                }
            }

            return filteredBatch;
        }

        private List<JObject> checkLatestDataDuplicates(List<JObject> batch)
        {
            var dictionary = new Dictionary<string, JObject>();
            var list = new List<JObject>();

            foreach (var currentItem in batch)
            {
                JObject parsedItem;
                if (dictionary.TryGetValue(currentItem["id_lokalId"].ToString(), out parsedItem))
                {
                    //Set the time variables 
                    var registrationFrom = DateTime.MinValue;
                    var itemRegistrationFrom = DateTime.MinValue;
                    var registrationTo = DateTime.MinValue;
                    var itemRegistrationTo = DateTime.MinValue;
                    var effectFrom = DateTime.MinValue;
                    var itemEffectFrom = DateTime.MinValue;
                    var effectTo = DateTime.MinValue;
                    var itemEffectTo = DateTime.MinValue;
                    //Check if it contains the null string 
                    bool CheckIfNull(JObject jObject, string key)
                    {
                        return jObject[key]! is null && jObject[key].ToString() != "null";
                    }

                    if (CheckIfNull(parsedItem, "registrationFrom"))
                    {
                        registrationFrom = DateTime.Parse(parsedItem["registrationFrom"].ToString());
                    }

                    if (CheckIfNull(currentItem, "registrationFrom"))
                    {
                        itemRegistrationFrom = DateTime.Parse(currentItem["registrationFrom"].ToString());
                    }

                    if (CheckIfNull(parsedItem, "registrationTo"))
                    {
                        registrationTo = DateTime.Parse(parsedItem["registrationTo"].ToString());
                    }

                    if (CheckIfNull(currentItem, "registrationTo"))
                    {
                        itemRegistrationTo = DateTime.Parse(currentItem["registrationTo"].ToString());
                    }

                    if (CheckIfNull(parsedItem, "effectFrom"))
                    {
                        effectFrom = DateTime.Parse(parsedItem["effectFrom"].ToString());
                    }

                    if (CheckIfNull(currentItem, "effectFrom"))
                    {
                        itemEffectFrom = DateTime.Parse(currentItem["effectFrom"].ToString());
                    }

                    if (CheckIfNull(parsedItem, "effectTo"))
                    {
                        effectTo = DateTime.Parse(parsedItem["effectTo"].ToString());
                    }

                    if (CheckIfNull(currentItem, "effectTo"))
                    {
                        itemEffectTo = DateTime.Parse(currentItem["effectTo"].ToString());
                    }

                    var idLokalIdItem = dictionary[currentItem["id_lokalId"]?.ToString()];
                    //Compare the date time values and return only the latest
                    if (registrationFrom < itemRegistrationFrom)
                    {
                        idLokalIdItem = currentItem;

                    }
                    else if (registrationFrom == itemRegistrationFrom)
                    {
                        if (registrationTo < itemRegistrationTo)
                        {
                            idLokalIdItem = currentItem;
                        }
                        else if (registrationTo == itemRegistrationTo)
                        {
                            if (effectFrom < itemEffectFrom)
                            {
                                idLokalIdItem = currentItem;

                            }
                            else if (effectFrom == itemEffectFrom)
                            {
                                if (effectTo < itemEffectTo)
                                {
                                    idLokalIdItem = currentItem;

                                }
                            }
                        }
                    }
                }
                else
                {
                    dictionary[currentItem["id_lokalId"].ToString()] = currentItem;
                }
            }

            //Add in the list only the objects with the latest date
            foreach (var d in dictionary)
            {
                list.Add(d.Value);
            }

            return list;
        }

        private JObject ChangeAdressNames(JObject jo)
        {
            foreach (var jp in jo.Properties().ToList())
            {
                switch (jp.Name)
                {
                    case "forretningshændelse":
                        jp.Replace(new JProperty("businessEvent", jp.Value));
                        break;
                    case "forretningsområde":
                        jp.Replace(new JProperty("businessArea", jp.Value));
                        break;
                    case "forretningsproces":
                        jp.Replace(new JProperty("businessProces", jp.Value));
                        break;
                    case "registreringFra":
                        var dateValue = (string)jp.Value;
                        jp.Value = ChangeDateFormat(dateValue);
                        jp.Replace(new JProperty("registrationFrom", jp.Value));
                        break;
                    case "registreringsaktør":
                        jp.Replace(new JProperty("registrationActor", jp.Value));
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
                    case "virkningsaktør":
                        jp.Replace(new JProperty("effectActor", jp.Value));
                        break;
                    case "virkningTil":
                        dateValue = (string)jp.Value;
                        jp.Value = ChangeDateFormat(dateValue);
                        jp.Replace(new JProperty("effectTo", jp.Value));
                        break;
                    case "adressebetegnelse":
                        jp.Replace(new JProperty("unitAddressDescription", jp.Value));
                        break;
                    case "dørbetegnelse":
                        jp.Replace(new JProperty("door", jp.Value));
                        break;
                    case "dørpunkt":
                        jp.Replace(new JProperty("doorPoint", jp.Value));
                        break;
                    case "etagebetegnelse":
                        jp.Replace(new JProperty("floor", jp.Value));
                        break;
                    case "bygning":
                        jp.Replace(new JProperty("building", jp.Value));
                        break;
                    case "husnummer":
                        jp.Replace(new JProperty("houseNumber", jp.Value));
                        break;
                    case "oprindelse_kilde":
                        jp.Replace(new JProperty("registrationSource", jp.Value));
                        break;
                    case "oprindelse_nøjagtighedsklasse":
                        jp.Replace(new JProperty("registrationPrecision", jp.Value));
                        break;
                    case "oprindelse_registrering":
                        jp.Replace(new JProperty("registration", jp.Value));
                        break;
                    case "oprindelse_tekniskStandard":
                        jp.Replace(new JProperty("registrationTechicalStandard", jp.Value));
                        break;
                    case "adgangsadressebetegnelse":
                        jp.Replace(new JProperty("accessAddressDescription", jp.Value));
                        break;
                    case "adgangspunkt":
                        jp.Replace(new JProperty("addressPoint", jp.Value));
                        break;
                    case "husnummerretning":
                        jp.Replace(new JProperty("houseNumberDirection", jp.Value));
                        break;
                    case "husnummertekst":
                        jp.Replace(new JProperty("houseNumberText", jp.Value));
                        break;
                    case "vejpunkt":
                        jp.Replace(new JProperty("roadPoint", jp.Value));
                        break;
                    case "jordstykke":
                        jp.Replace(new JProperty("plot", jp.Value));
                        break;
                    case "placeretPåForeløbigtJordstykke":
                        jp.Replace(new JProperty("placedOnTemporaryPlot", jp.Value));
                        break;
                    case "geoDanmarkBygning":
                        jp.Replace(new JProperty("geoDenmarkBuilding", jp.Value));
                        break;
                    case "adgangTilBygning":
                        jp.Replace(new JProperty("buildingAccess", jp.Value));
                        break;
                    case "adgangTilTekniskAnlæg":
                        jp.Replace(new JProperty("technicalStructureAccess", jp.Value));
                        break;
                    case "vejmidte":
                        jp.Replace(new JProperty("roadMid", jp.Value));
                        break;
                    case "afstemningsområde":
                        jp.Replace(new JProperty("electionDistrict", jp.Value));
                        break;
                    case "kommuneinddeling":
                        jp.Replace(new JProperty("municipalityDistrict", jp.Value));
                        break;
                    case "menighedsrådsafstemningsområde":
                        jp.Replace(new JProperty("churchVotingDistrict", jp.Value));
                        break;
                    case "sogneinddeling":
                        jp.Replace(new JProperty("churchDistrict", jp.Value));
                        break;
                    case "supplerendeBynavn":
                        jp.Replace(new JProperty("additionalCityName", jp.Value));
                        break;
                    case "navngivenVej":
                        jp.Replace(new JProperty("namedRoad", jp.Value));
                        break;
                    case "postnummer":
                        jp.Replace(new JProperty("postalCode", jp.Value));
                        break;
                    case "administreresAfKommune":
                        jp.Replace(new JProperty("municipalityAdministration", jp.Value));
                        break;
                    case "beskrivelse":
                        jp.Replace(new JProperty("description", jp.Value));
                        break;
                    case "udtaltVejnavn":
                        jp.Replace(new JProperty("pronouncedRoadName", jp.Value));
                        break;
                    case "vejadresseringsnavn":
                        jp.Replace(new JProperty("shortRoadName", jp.Value));
                        break;
                    case "vejnavn":
                        jp.Replace(new JProperty("roadName", jp.Value));
                        break;
                    case "vejnavnebeliggenhed_oprindelse_kilde":
                        jp.Replace(new JProperty("roadRegistrationSource", jp.Value));
                        break;
                    case "vejnavnebeliggenhed_oprindelse_nøjagtighedsklasse":
                        jp.Replace(new JProperty("roadRegistrationPrecision", jp.Value));
                        break;
                    case "vejnavnebeliggenhed_oprindelse_registrering":
                        jp.Replace(new JProperty("roadRegistration", jp.Value));
                        break;
                    case "vejnavnebeliggenhed_oprindelse_tekniskStandard":
                        jp.Replace(new JProperty("roadRegistrationTechicalStandard", jp.Value));
                        break;
                    case "vejnavnebeliggenhed_vejnavnelinje":
                        jp.Replace(new JProperty("roadRegistrationRoadLine", jp.Value));
                        break;
                    case "vejnavnebeliggenhed_vejnavneområde":
                        jp.Replace(new JProperty("roadRegistrationRoadArea", jp.Value));
                        break;
                    case "vejnavnebeliggenhed_vejtilslutningspunkter":
                        jp.Replace(new JProperty("roadRegistrationRoadConnectionPoints", jp.Value));
                        break;
                    case "kommune":
                        jp.Replace(new JProperty("municipality", jp.Value));
                        break;
                    case "vejkode":
                        jp.Replace(new JProperty("roadCode", jp.Value));
                        break;
                    case "postnummerinddeling":
                        jp.Replace(new JProperty("postalCodeDistrict", jp.Value));
                        break;
                    default:
                        break;
                }
            }

            return jo;
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
    }
}
