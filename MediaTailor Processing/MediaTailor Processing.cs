/*
****************************************************************************
*  Copyright (c) 2023,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

dd/mm/2023	1.0.0.1		XXX, Skyline	Initial version
****************************************************************************
*/

namespace Script
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Helpers.Logging;
	using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
	using Skyline.DataMiner.ExceptionHelper;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;
	using TouchstreamHelper;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	public class Script
	{
		private DomHelper innerDomHelper;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(Engine engine)
		{
			var scriptName = "PA_TS_MediaTailor Processing";
			var tseventName = String.Empty;

			engine.GenerateInformation("START " + scriptName);
			var helper = new PaProfileLoadDomHelper(engine);
			innerDomHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");
			var exceptionHelper = new ExceptionHelper(engine, innerDomHelper);

			var mainStatus = String.Empty;
			try
			{
				var instanceId = helper.TryGetParameterValue("InstanceId (Touchstream)", out string id) ? id : String.Empty;

				if (!Touchstream.CheckStatus(instanceId, innerDomHelper, new[] { "ready" }, out string status))
				{
					var log = new Log
					{
						AffectedItem = scriptName,
						AffectedService = tseventName,
						Timestamp = DateTime.Now,
						ErrorCode = new ErrorCode
						{
							ConfigurationItem = scriptName + " Script",
							ConfigurationType = ErrorCode.ConfigType.Automation,
							Severity = ErrorCode.SeverityType.Major,
							Source = "Touchstream.CheckStatus()",
							Code = "CheckStatusReturnedFalse",
							Description = $"Activity not executed due to Instance status is not compatible to execute activity.",
						},
					};
					exceptionHelper.GenerateLog(log);
					Touchstream.TransitionToError(helper, status);
					helper.SendFinishMessageToTokenHandler();
					return;
				}

				mainStatus = status;
				var eventName = helper.GetParameterValue<string>("Event Name (Touchstream)");
				var mediaTailor = helper.TryGetParameterValue("MediaTailor (Touchstream)", out List<Guid> mediaTailorInstances) ? mediaTailorInstances : new List<Guid>();
				tseventName = eventName;

				if (mediaTailor.Count == 0)
				{
					helper.Log($"MediaTailor Activity not executed due to Instance status is not compatible to execute activity.", PaLogLevel.Information);
					helper.TransitionState("ready_to_inprogress");
					helper.ReturnSuccess();
					return;
				}

				IDms dms = engine.GetDms();
				var mediaTailorDictionary = SetMediaTailorDataToSend(mediaTailor);

				foreach (var pair in mediaTailorDictionary)
				{
					ExternalRequest mediaTailorRequest = new ExternalRequest
					{
						Type = "ManifestRequest",
						ManifestRequest = pair.Value,
					};

					var value = JsonConvert.SerializeObject(mediaTailorRequest);
					IDmsElement element = dms.GetElement(pair.Key);
					element.GetStandaloneParameter<string>(20).SetValue(value);
				}

				bool CheckMediaTailorResponseUrl()
				{
					try
					{
						int totalMediaTailorManifests = mediaTailor.Count;
						int receivedManifests = CheckReceivedManifest(engine, mediaTailor);

						return totalMediaTailorManifests == receivedManifests;
					}
					catch (Exception ex)
					{
						var log = new Log
						{
							AffectedItem = scriptName,
							AffectedService = tseventName,
							Timestamp = DateTime.Now,
							ErrorCode = new ErrorCode
							{
								ConfigurationItem = scriptName + " Script",
								ConfigurationType = ErrorCode.ConfigType.Automation,
								Severity = ErrorCode.SeverityType.Major,
								Source = "CheckMediaTailorResponseUrl()",
								Code = "ExceptionThrownOnCheckReceivedManifest()",
								Description = "Exception thrown while checking MediaTailor Manifests",
							},
						};
						exceptionHelper.GenerateLog(log);
						Touchstream.TransitionToError(helper, mainStatus);
						throw;
					}
				}

				if (Touchstream.Retry(CheckMediaTailorResponseUrl, new TimeSpan(0, 5, 0)))
				{
					helper.Log($"MediaTailor Manifest URLs {eventName} received.", PaLogLevel.Information);
					helper.TransitionState("ready_to_inprogress");
					helper.ReturnSuccess();
				}
				else
				{
					var log = new Log
					{
						AffectedItem = scriptName,
						AffectedService = tseventName,
						Timestamp = DateTime.Now,
						ErrorCode = new ErrorCode
						{
							ConfigurationItem = scriptName + " Script",
							ConfigurationType = ErrorCode.ConfigType.Automation,
							Severity = ErrorCode.SeverityType.Warning,
							Source = "Retry()",
							Code = "RetryTimeout",
							Description = "Failed to get all MediaTailor Manifest URLs within the timeout time.",
						},
					};
					exceptionHelper.GenerateLog(log);
					Touchstream.TransitionToError(helper, mainStatus);
					helper.SendFinishMessageToTokenHandler();
				}
			}
			catch (Exception ex)
			{
				helper.Log($"Failed to get MediaTailor Manifests due to exception: " + ex, PaLogLevel.Error);
				engine.GenerateInformation($"Failed to get MediaTailor Manifests due to exception: " + ex);
				var log = new Log
				{
					AffectedItem = scriptName,
					AffectedService = tseventName,
					Timestamp = DateTime.Now,
					ErrorCode = new ErrorCode
					{
						ConfigurationItem = scriptName + " Script",
						ConfigurationType = ErrorCode.ConfigType.Automation,
						Severity = ErrorCode.SeverityType.Major,
						Source = "Run()",
					},
				};
				exceptionHelper.ProcessException(ex, log);
				Touchstream.TransitionToError(helper, mainStatus);
				helper.SendFinishMessageToTokenHandler();
				throw;
			}
		}

		private int CheckReceivedManifest(Engine engine, List<Guid> mediaTailor)
		{
			int receivedManifests = 0;
			foreach (var mediaTailorInstanceId in mediaTailor)
			{
				var mediaTailorInstanceFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(mediaTailorInstanceId));
				var mediaTailorInstances = innerDomHelper.DomInstances.Read(mediaTailorInstanceFilter);
				if (mediaTailorInstances.Count == 0)
				{
					engine.GenerateInformation("MediaTailor DOM Instance not found: " + mediaTailorInstanceId);
					continue;
				}

				var mediaTailorInstance = mediaTailorInstances.First();

				string resultUrl = String.Empty;

				if (mediaTailorInstance.Sections.Count == 0)
				{
					continue;
				}

				var section = mediaTailorInstance.Sections.First();
				section.Stitch(SetSectionDefinitionById);

				var fieldValue = section.FieldValues.First(field => field.GetFieldDescriptor().Name.Equals("Result URL (MediaTailor)"));
				resultUrl = mediaTailorInstance.GetFieldValue<string>(section.GetSectionDefinition(), fieldValue.GetFieldDescriptor()).Value;

				if (!String.IsNullOrWhiteSpace(resultUrl) && resultUrl != "_" /*default value*/)
				{
					receivedManifests++;
				}
			}

			return receivedManifests;
		}

		private Dictionary<string, List<ManifestRequest>> SetMediaTailorDataToSend(List<Guid> mediaTailor)
		{
			var mediaTailorDictionary = new Dictionary<string, List<ManifestRequest>>();

			foreach (var mediaTailorInstanceId in mediaTailor)
			{
				var mediaTailorInstanceFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(mediaTailorInstanceId));
				var mediaTailorInstance = innerDomHelper.DomInstances.Read(mediaTailorInstanceFilter).First();
				var mediaTailorSectionData = new Dictionary<string, string>();

				foreach (var section in mediaTailorInstance.Sections)
				{
					section.Stitch(SetSectionDefinitionById);

					foreach (var field in section.FieldValues)
					{
						mediaTailorSectionData[field.GetFieldDescriptor().Name] = field.Value.ToString();
					}
				}

				var eventId = mediaTailorSectionData["Event ID (MediaTailor)"];
				var url = mediaTailorSectionData["URL (MediaTailor)"].Replace("{{eventid}}", eventId);

				var mediaTailorRequest = new ManifestRequest
				{
					Cdn = mediaTailorSectionData["CDN (MediaTailor)"],
					EventId = eventId,
					Format = mediaTailorSectionData["Format (MediaTailor)"],
					DomainUrl = mediaTailorSectionData["Domain URL (MediaTailor)"],
					Url = url,
					JsonStructure = mediaTailorSectionData["Payload (MediaTailor)"],
					TouchstreamProvisionId = mediaTailorInstanceId.ToString(),
					Product = mediaTailorSectionData["Product (MediaTailor)"],
				};

				if (mediaTailorDictionary.ContainsKey(mediaTailorSectionData["MediaTailor Element (MediaTailor)"]))
				{
					mediaTailorDictionary[mediaTailorSectionData["MediaTailor Element (MediaTailor)"]].Add(mediaTailorRequest);
				}
				else
				{
					mediaTailorDictionary.Add(mediaTailorSectionData["MediaTailor Element (MediaTailor)"], new List<ManifestRequest> { mediaTailorRequest });
				}
			}

			return mediaTailorDictionary;
		}

		private SectionDefinition SetSectionDefinitionById(SectionDefinitionID sectionDefinitionId)
		{
			return innerDomHelper.SectionDefinitions.Read(SectionDefinitionExposers.ID.Equal(sectionDefinitionId)).First();
		}
	}
}