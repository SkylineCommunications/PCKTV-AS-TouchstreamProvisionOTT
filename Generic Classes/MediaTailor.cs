namespace TouchstreamHelper
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;

    public class ExternalRequest
    {
        public string Type { get; set; }

        public List<ManifestRequest> ManifestRequest { get; set; }
    }

    public class ManifestRequest
    {
        [JsonProperty("cdn", NullValueHandling = NullValueHandling.Ignore)]
        public string Cdn { get; set; }

        [JsonProperty("eventId", NullValueHandling = NullValueHandling.Ignore)]
        public string EventId { get; set; }

        [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
        public string Format { get; set; }

        [JsonProperty("jsonStructure", NullValueHandling = NullValueHandling.Ignore)]
        public string JsonStructure { get; set; }

        [JsonProperty("site", NullValueHandling = NullValueHandling.Ignore)]
        public string Site { get; set; }

        [JsonProperty("domainUrl", NullValueHandling = NullValueHandling.Ignore)]
        public string DomainUrl { get; set; }

        [JsonProperty("product", NullValueHandling = NullValueHandling.Ignore)]
        public string Product { get; set; }

        [JsonProperty("touchstreamProvisionId", NullValueHandling = NullValueHandling.Ignore)]
        public string TouchstreamProvisionId { get; set; }

        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }
    }
}