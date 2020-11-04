using System.Collections.Generic;

namespace Datafordelen.Kafka
{
     public interface IKafkaProducer
     {
        void Produce(string topicname, List<string> batch);

     }
}