using Confluent.Kafka;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Datafordelen.Config;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;


namespace Datafordelen.Kafka
{
    public class KafkaProducer : IKafkaProducer
    {
        private readonly AppSettings _appSettings;
        private readonly ILogger<KafkaProducer> _logger;

        public KafkaProducer(IOptions<AppSettings> appSettings, ILogger<KafkaProducer> logger)
        {
            _appSettings = appSettings.Value;
            _logger = logger;
        }

        public void Produce(string topicname, List<JObject> batch)
        {
            var config = new ProducerConfig { BootstrapServers = _appSettings.KafkaBootstrapServer, LingerMs = 5, BatchNumMessages = 100000, QueueBufferingMaxMessages = 100000 };

            using (var p = new ProducerBuilder<string, string>(config).Build())
            {

                foreach (var document in batch)
                {
                    var id = String.Empty;

                    if (document["gml_id"] != null)
                    {
                        id = (string)document["gml_id"];
                    }
                    else
                    {
                        id = (string)document["id_lokalId"];
                    }

                    try
                    {
                        var textDoc = JsonConvert.SerializeObject(document, Formatting.Indented);
                        p.Produce(topicname, new Message<string, string> { Value = textDoc, Key = id });
                    }
                    catch (ProduceException<Null, string> e)
                    {

                        _logger.LogError($"Delivery failed: {e.Error.Reason}");

                    }

                }
                p.Flush(TimeSpan.FromSeconds(10));
            }
        }

    }
}
