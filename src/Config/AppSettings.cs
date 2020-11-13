using System.Collections.Generic;


namespace Datafordelen.Config
{
    public class AppSettings
    {
        public string InitialAddressDataUrl { get; set; }
        public string InitialAddressDataZipFilePath { get; set; }
        public string InitialAddressDataUnzipPath { get; set; }
        public string InitialAddressDataProcessedPath { get; set; }
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }

        public string FtpServer { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string GeoUnzipPath { get; set; }

        public string GeoGmlPath { get; set; }

        public string GeoProcessedPath { get; set; }

        public string GeoFieldList { get; set; }

        public string KafkaBootstrapServer { get; set; }

        public string ConvertScriptFileName { get; set; }

        public string AdressTopicName {get;set;}

        public string GeoDataTopicName {get;set;}

    }
}
