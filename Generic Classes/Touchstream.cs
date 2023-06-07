namespace TouchstreamHelper
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using Helper;
    using Newtonsoft.Json;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Common.Objects.Exceptions;
    using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
    using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
    using Skyline.DataMiner.Net.Sections;
    public enum ChannelType
    {
        VL = 1,
        SLE = 2,
    }

    public enum ConfigurationAction // Action
    {
        Provision = 0,
        Deactivate = 1,
        Follow = 2,
        Waiting = 3,
        Update = 4,
    }

    public enum StreamType // Configuration Type
    {
        Regular = 0,
        Adobe = 1,
    }

    public class TouchstreamRequest
    {
        [JsonProperty("Action", NullValueHandling = NullValueHandling.Ignore)]
        public long? Action { get; set; }

        [JsonProperty("AssetId")]
        public object AssetId { get; set; }

        [JsonProperty("BookingId", NullValueHandling = NullValueHandling.Ignore)]
        public string BookingId { get; set; }

        [JsonProperty("ConfigurationType", NullValueHandling = NullValueHandling.Ignore)]
        public long? ConfigurationType { get; set; }

        [JsonProperty("EventId")]
        public object EventId { get; set; }

        [JsonProperty("EventLabel", NullValueHandling = NullValueHandling.Ignore)]
        public string EventLabel { get; set; }

        [JsonProperty("EventName", NullValueHandling = NullValueHandling.Ignore)]
        public string EventName { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("Manifests", NullValueHandling = NullValueHandling.Ignore)]
        public List<MediaTailorManifest> Manifests { get; set; }

        [JsonProperty("RowId")]
        public object RowId { get; set; }

        [JsonProperty("TemplateName", NullValueHandling = NullValueHandling.Ignore)]
        public string TemplateName { get; set; }

        [JsonProperty("YoSpaceStreamIdHls", NullValueHandling = NullValueHandling.Ignore)]
        public string YoSpaceStreamIdHls { get; set; }

        [JsonProperty("YoSpaceStreamIdMpd", NullValueHandling = NullValueHandling.Ignore)]
        public string YoSpaceStreamIdMpd { get; set; }

        [JsonProperty("DynamicGroup", NullValueHandling = NullValueHandling.Ignore)]
        public string DynamicGroup { get; set; }

        [JsonProperty("ReducedTemplate", NullValueHandling = NullValueHandling.Ignore)]
        public bool ReducedTemplate { get; set; }

        [JsonProperty("EventStartDate", NullValueHandling = NullValueHandling.Ignore)]
        public double EventStartDate { get; set; }

        [JsonProperty("EventEndDate", NullValueHandling = NullValueHandling.Ignore)]
        public double EventEndDate { get; set; }

        [JsonProperty("ForceUpdate", NullValueHandling = NullValueHandling.Ignore)]
        public bool ForceUpdate { get; set; }
    }

    public class MediaTailorManifest
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("cdn", NullValueHandling = NullValueHandling.Ignore)]
        public string Cdn { get; set; }

        [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
        public string Format { get; set; }

        [JsonProperty("eventId", NullValueHandling = NullValueHandling.Ignore)]
        public string EventId { get; set; }

        [JsonProperty("site", NullValueHandling = NullValueHandling.Ignore)]
        public string Site { get; set; }

        [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }

        [JsonProperty("product", NullValueHandling = NullValueHandling.Ignore)]
        public string Product { get; set; }

        [JsonProperty("manifestUrl", NullValueHandling = NullValueHandling.Ignore)]
        public Uri ManifestUrl { get; set; }

        [JsonProperty("touchstreamProvisionId", NullValueHandling = NullValueHandling.Ignore)]
        public string TouchstreamProvisionId { get; set; }
    }

    public class Touchstream
    {
        public string SourceElement { get; set; }

        public string SourceId { get; set; }

        public string Element { get; set; }

        public string AssetId { get; set; }

        public string EventId { get; set; }

        public string Label { get; set; }

        public string EventName { get; set; }

        public string TemplateName { get; set; }

        public string YoSpaceHls { get; set; }

        public string YoSpaceMpd { get; set; }

        public string ReducedTemplate { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public string ForcedUpdate { get; set; }

        public List<Guid> MediaTailor { get; set; }

        public string InstanceId { get; set; }

        public string DynamicGroup { get; set; }

        /// <summary>
        /// Retry until success or until timeout.
        /// </summary>
        /// <param name="func">Operation to retry.</param>
        /// <param name="timeout">Max TimeSpan during which the operation specified in <paramref name="func"/> can be retried.</param>
        /// <returns><c>true</c> if one of the retries succeeded within the specified <paramref name="timeout"/>. Otherwise <c>false</c>.</returns>
        public static bool Retry(Func<bool> func, TimeSpan timeout)
        {
            bool success = false;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            do
            {
                success = func();
                if (!success)
                {
                    Thread.Sleep(3000);
                }
            }
            while (!success && sw.Elapsed <= timeout);

            return success;
        }

        public static Touchstream GetDOMData(PaProfileLoadDomHelper helper)
        {
            return new Touchstream
            {
                AssetId = helper.GetParameterValue<string>("Asset ID (Touchstream)"),
                Element = helper.GetParameterValue<string>("Touchstream Element (Touchstream)"),
                EndDate = helper.GetParameterValue<DateTime>("Event End Date (Touchstream)"),
                EventId = helper.GetParameterValue<string>("Event ID (Touchstream)"),
                ForcedUpdate = helper.GetParameterValue<string>("Forced Update (Touchstream)"),
                InstanceId = helper.TryGetParameterValue("InstanceId (Touchstream)", out string instanceId) ? instanceId : String.Empty,
                Label = helper.GetParameterValue<string>("Event Label (Touchstream)"),
                MediaTailor = helper.TryGetParameterValue("MediaTailor (Touchstream)", out List<Guid> channels) ? channels : new List<Guid>(),
                EventName = helper.GetParameterValue<string>("Event Name (Touchstream)"),
                ReducedTemplate = helper.GetParameterValue<string>("Reduced Template (Touchstream)"),
                SourceElement = helper.TryGetParameterValue("Source Element (Touchstream)", out string sourceElement) ? sourceElement : String.Empty,
                SourceId = helper.TryGetParameterValue("Source ID (Touchstream)", out string sourceId) ? sourceId : String.Empty,
                StartDate = helper.GetParameterValue<DateTime>("Event Start Date (Touchstream)"),
                TemplateName = helper.GetParameterValue<string>("Template Name (Touchstream)"),
                YoSpaceHls = helper.GetParameterValue<string>("YoSpace Stream ID HLS (Touchstream)"),
                YoSpaceMpd = helper.GetParameterValue<string>("YoSpace Stream ID MPD (Touchstream)"),
                DynamicGroup = helper.TryGetParameterValue("Dynamic Group (Touchstream)", out string dynamicGroup) ? dynamicGroup : String.Empty,
            };
        }

        public static bool CheckStatus(string instanceId, DomHelper domHelper, string[] statuses, out string currentStatus)
        {
            if (String.IsNullOrWhiteSpace(instanceId))
            {
                currentStatus = String.Empty;
                return false;
            }

            var instanceFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(instanceId)));
            var instance = domHelper.DomInstances.Read(instanceFilter).First();
            var instanceStatus = instance.StatusId;
            var multiCondition = false;

            foreach (var status in statuses)
            {
                multiCondition = multiCondition || instanceStatus.Equals(status);
            }

            currentStatus = instanceStatus;
            return multiCondition;
        }

        public void PerformCallback(Engine engine, PaProfileLoadDomHelper helper, DomHelper domHelper)
        {
            try
            {
                var filter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(InstanceId)));
                var touchstreamInstances = domHelper.DomInstances.Read(filter);
                var touchstreamInstance = touchstreamInstances.First();

                var sourceElementIds = helper.GetParameterValue<string>("Source Element (Touchstream)");
                var sourceId = helper.GetParameterValue<string>("Source ID (Touchstream)");
                if (!string.IsNullOrWhiteSpace(sourceElementIds))
                {
                    ExternalResponse updateMessage = new ExternalResponse
                    {
                        Type = "Process Automation",
                        ProcessResponse = new ProcessResponse
                        {
                            EventName = sourceId,
                            Touchstream = new TouchstreamResponse
                            {
                                Status = touchstreamInstance.StatusId == "active" ? "Active" : "Complete",
                            },
                        },
                    };

                    var elementSplit = sourceElementIds.Split('/');
                    var sourceElement = engine.FindElement(Convert.ToInt32(elementSplit[0]), Convert.ToInt32(elementSplit[1]));
                    sourceElement.SetParameter(Convert.ToInt32(elementSplit[2]), JsonConvert.SerializeObject(updateMessage));
                }
            }
            catch (FieldValueNotFoundException e)
            {
                // no action
            }
        }
    }
}

public class MediaTailor
{
    public string Element { get; set; }

    public string Product { get; set; }

    public string Cdn { get; set; }

    public string Format { get; set; }

    public string Payload { get; set; }
}
