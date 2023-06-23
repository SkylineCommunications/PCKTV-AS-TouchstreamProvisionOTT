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
	using Helper;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Helpers.Logging;
	using Skyline.DataMiner.DataMinerSolutions.ProcessAutomation.Manager;
	using Skyline.DataMiner.ExceptionHelper;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Sections;
	using TouchstreamHelper;

	/// <summary>
	/// DataMiner Script Class.
	/// </summary>
	public class Script
	{
		private readonly int dsprovisionTable = 6400;
		private readonly int jsonRequestParameter = 20000;
		private DomHelper innerDomHelper;
		private IDmsElement element;

		/// <summary>
		/// The Script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(Engine engine)
		{
			var scriptName = "PA_TS_Deactivate Touchstream";
			var tseventName = String.Empty;
			engine.GenerateInformation("START " + scriptName);
			var helper = new PaProfileLoadDomHelper(engine);
			innerDomHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");

			var exceptionHelper = new ExceptionHelper(engine, innerDomHelper);
			var touchstream = Touchstream.GetDOMData(helper);

			var mainStatus = String.Empty;
			try
			{
				if (!Touchstream.CheckStatus(touchstream.InstanceId, innerDomHelper, new[] { "deactivate", "reprovision" }, out string status))
				{
					engine.GenerateInformation($"Skip Deactivate activity due to status: {status}");
					helper.ReturnSuccess();
					return;
				}

				mainStatus = status;

				if (mainStatus.Equals("deactivate"))
				{
					helper.TransitionState("deactivate_to_deactivating");
					// need to get instance again after a transition is executed
					var instanceFilter = DomInstanceExposers.Id.Equal(new DomInstanceId(Guid.Parse(touchstream.InstanceId)));
					var instance = innerDomHelper.DomInstances.Read(instanceFilter).First();
					mainStatus = instance.StatusId;
				}

				IDms dms = engine.GetDms();
				element = dms.GetElement(touchstream.Element);

				TouchstreamRequest tsrequest = new TouchstreamRequest
				{
					Action = (int)ConfigurationAction.Deactivate,
					AssetId = touchstream.AssetId,
					BookingId = touchstream.InstanceId,
					ConfigurationType = (int)StreamType.Regular,
					EventId = touchstream.EventId,
					EventLabel = touchstream.Label,
					EventName = touchstream.EventName,
					RowId = null,
					TemplateName = touchstream.TemplateName,
					YoSpaceStreamIdHls = touchstream.YoSpaceHls,
					YoSpaceStreamIdMpd = touchstream.YoSpaceMpd,
					EventStartDate = touchstream.StartDate.ToOADate(),
					EventEndDate = DateTime.Now.ToOADate(),
					ReducedTemplate = false,
					ForceUpdate = false,
					DynamicGroup = touchstream.DynamicGroup,
				};

				string sValue = JsonConvert.SerializeObject(tsrequest);
				element.GetStandaloneParameter<string>(jsonRequestParameter).SetValue(sValue);

				var mediaTailorElementName = SendMediaTailorDeleteMessage(dms, touchstream);
				var tsprovisionTable = element.GetTable(dsprovisionTable);

				bool CheckDeactivatedTsEvent()
				{
					try
					{
						bool tsCheck = CheckExistingProvisionRow(tsprovisionTable, touchstream.InstanceId);
						bool mediaTailorCheck = CheckMediaTailorTableRows(dms, touchstream.EventId, mediaTailorElementName);

						return tsCheck && mediaTailorCheck;
					}
					catch (Exception ex)
					{
						Touchstream.TransitionToError(helper, mainStatus);
						var log = new Log
						{
							AffectedItem = element.Name,
							AffectedService = tseventName,
							Timestamp = DateTime.Now,
							ErrorCode = new ErrorCode
							{
								ConfigurationItem = scriptName + " Script",
								ConfigurationType = ErrorCode.ConfigType.Automation,
								Severity = ErrorCode.SeverityType.Major,
								Source = "CheckDeactivatedTsEvent()",
							},
						};
						exceptionHelper.ProcessException(ex, log);
						return true;
					}
				}

				if (Touchstream.Retry(CheckDeactivatedTsEvent, new TimeSpan(0, 5, 0)))
				{
					engine.GenerateInformation($"TS Event {touchstream.EventName} deactivated.");
					touchstream.PerformCallback(engine, helper, innerDomHelper);

					if (mainStatus == "deactivating")
					{
						helper.TransitionState("deactivating_to_complete");
						helper.SendFinishMessageToTokenHandler();
					}
					else if (mainStatus == "reprovision")
					{
						helper.TransitionState("reprovision_to_ready");
						helper.ReturnSuccess();
					}
					else
					{
						Touchstream.TransitionToError(helper, mainStatus);
						var log = new Log
						{
							AffectedItem = element.Name,
							AffectedService = tseventName,
							Timestamp = DateTime.Now,
							LogNotes = $"Failed to execute transition status on event {tseventName}. Current status: {mainStatus}",
							ErrorCode = new ErrorCode
							{
								ConfigurationItem = scriptName + " Script",
								ConfigurationType = ErrorCode.ConfigType.Automation,
								Severity = ErrorCode.SeverityType.Major,
								Source = "CheckDeactivatedTsEvent()",
								Code = "DeactivationFailedEventUnknownStatus",
								Description = $"Failed to execute transition status.",
							},
						};
						exceptionHelper.GenerateLog(log);
						engine.GenerateInformation($"Failed to execute transition status. Current status: {mainStatus}");
						helper.SendFinishMessageToTokenHandler();
					}
				}
				else
				{
					helper.Log("Failed to deactivate TS Event within the timeout time.", PaLogLevel.Error);
					Touchstream.TransitionToError(helper, mainStatus);
					var log = new Log
					{
						AffectedItem = element.Name,
						AffectedService = tseventName,
						Timestamp = DateTime.Now,
						LogNotes = $"Failed to deactivate {tseventName} within the timeout time.",
						ErrorCode = new ErrorCode
						{
							ConfigurationItem = scriptName + " Script",
							ConfigurationType = ErrorCode.ConfigType.Automation,
							Severity = ErrorCode.SeverityType.Warning,
							Source = "Retry()",
							Code = "RetryTimeout",
							Description = "Failed to finish PA activity within the timeout time.",
						},
					};
					exceptionHelper.GenerateLog(log);
					helper.SendFinishMessageToTokenHandler();
				}
			}
			catch (Exception ex)
			{
				engine.GenerateInformation($"Failed to deactivate TS Event ({touchstream.EventName}) due to exception: " + ex);
				Touchstream.TransitionToError(helper,mainStatus);

				var log = new Log
				{
					AffectedItem = element.Name,
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
				helper.SendFinishMessageToTokenHandler();
			}
		}

		private static bool CheckMediaTailorTableRows(IDms dms, string eventId, string mediaTailorElementName)
		{
			if (String.IsNullOrWhiteSpace(mediaTailorElementName))
			{
				return true;
			}

			var mediaTailorElement = dms.GetElement(mediaTailorElementName);
			var eventsTable = mediaTailorElement.GetTable(1000);

			var eventsColumn = new ColumnFilter { ComparisonOperator = ComparisonOperator.Equal, Value = eventId, Pid = 1004 };
			var eventsData = eventsTable.QueryData(new List<ColumnFilter> { eventsColumn });
			return !eventsData.Any();
		}

		private bool CheckExistingProvisionRow(IDmsTable dsprovisionTable, string domInstanceId)
		{
			foreach (var tableRow in dsprovisionTable.GetRows())
			{
				var instanceId = Convert.ToString(tableRow[11]); // BookingId/InstanceId
				if (instanceId == domInstanceId)
				{
					return false;
				}
			}

			return true;
		}

		private string SendMediaTailorDeleteMessage(IDms dms, Touchstream touchstream)
		{
			if (!touchstream.MediaTailor.Any())
			{
				return String.Empty;
			}

			var mediaTailorInstanceId = touchstream.MediaTailor.First();
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

			var elementName = mediaTailorSectionData["MediaTailor Element (MediaTailor)"];
			ExternalRequest request = new ExternalRequest
			{
				Type = "ManifestDelete",
				ManifestRequest = new List<ManifestRequest> { new ManifestRequest { EventId = touchstream.EventId } },
			};

			var value = JsonConvert.SerializeObject(request);
			IDmsElement element = dms.GetElement(elementName);
			element.GetStandaloneParameter<string>(20).SetValue(value);

			return elementName;
		}

		private SectionDefinition SetSectionDefinitionById(SectionDefinitionID sectionDefinitionId)
		{
			return innerDomHelper.SectionDefinitions.Read(SectionDefinitionExposers.ID.Equal(sectionDefinitionId)).First();
		}
	}
}