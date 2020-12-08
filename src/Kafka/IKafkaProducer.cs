using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Datafordelen.Kafka
{
     public interface IKafkaProducer
     {
        void Produce(string topicname, List<JObject> batch);

     }
}