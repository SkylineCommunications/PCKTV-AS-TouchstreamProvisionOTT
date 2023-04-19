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
	using System.Threading;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Actions;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Buttons;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Concatenation;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Conditions;
	using Skyline.DataMiner.Net.Apps.DataMinerObjectModel.Status;
	using Skyline.DataMiner.Net.Apps.Sections.SectionDefinitions;
	using Skyline.DataMiner.Net.Messages.SLDataGateway;
	using Skyline.DataMiner.Net.Sections;

	/// <summary>
	/// Represents a DataMiner Automation script.
	/// </summary>
	internal class Script
	{
		private DomHelper domHelper;

		/// <summary>
		/// The script entry point.
		/// </summary>
		/// <param name="engine">Link with SLAutomation process.</param>
		public void Run(Engine engine)
		{
			var scriptName = "Create Touchstream DOM";
			engine.GenerateInformation("START " + scriptName);
			try
			{
				domHelper = new DomHelper(engine.SendSLNetMessages, "process_automation");

				var mediatailorDomDefintion = CreateMediatailorDefinition();
				CreateOrUpdateDomDefinition(mediatailorDomDefintion);
				Thread.Sleep(2000);
				var touchstreamDomDefinition = CreateTouchstreamDefinition();
				CreateOrUpdateDomDefinition(touchstreamDomDefinition);
			}
			catch (Exception ex)
			{
				engine.GenerateInformation(scriptName + $"|Failed to create touchstream DOM due to exception: " + ex);
			}
		}

		private void CreateOrUpdateDomDefinition(DomDefinition logsDomDefinition)
		{
			if (logsDomDefinition != null)
			{
				var domDefinition = domHelper.DomDefinitions.Read(DomDefinitionExposers.Name.Equal(logsDomDefinition.Name));
				if (domDefinition.Any())
				{
					logsDomDefinition.ID = domDefinition.FirstOrDefault()?.ID;
					domHelper.DomDefinitions.Update(logsDomDefinition);
				}
				else
				{
					domHelper.DomDefinitions.Create(logsDomDefinition);
				}
			}
		}

		private DomDefinition CreateTouchstreamDefinition()
		{
			// Create SectionDefinitions
			FieldDescriptorID nameDescriptor = new FieldDescriptorID();
			var touchstreamSectionDefinition = SectionDefinitions.CreateTouchstreamSection(domHelper, ref nameDescriptor);

			var sections = new List<SectionDefinition> { touchstreamSectionDefinition };

			// Create DomBehaviorDefinition
			var behaviorName = "Touchstream Behavior";
			var domBehaviorDefinition = BehaviorDefinitions.CreateTouchstreamBehaviorDefinition(sections, behaviorName);
			CreateOrUpdateDomBehaviorDefinition(domBehaviorDefinition);

			var nameDefinition = new ModuleSettingsOverrides
			{
				NameDefinition = new DomInstanceNameDefinition
				{
					ConcatenationItems = new List<IDomInstanceConcatenationItem>
					{
						new FieldValueConcatenationItem
						{
							FieldDescriptorId = nameDescriptor,
						},
					},
				},
			};

			return new DomDefinition
			{
				Name = "Touchstream",
				SectionDefinitionLinks = new List<SectionDefinitionLink> { new SectionDefinitionLink(touchstreamSectionDefinition.GetID()) },
				DomBehaviorDefinitionId = domBehaviorDefinition.ID,
				ModuleSettingsOverrides = nameDefinition,
			};
		}

		private DomDefinition CreateMediatailorDefinition()
		{
			// Create SectionDefinitions
			FieldDescriptorID eventIdDescriptor = new FieldDescriptorID();
			FieldDescriptorID cdnDescriptor = new FieldDescriptorID();
			FieldDescriptorID formatDescriptor = new FieldDescriptorID();
			var mediaTailorSection = SectionDefinitions.CreateMediaTailorSection(domHelper, ref eventIdDescriptor, ref cdnDescriptor, ref formatDescriptor);

			var sections = new List<SectionDefinition> { mediaTailorSection };

			// Create DomBehaviorDefinition
			var behaviorName = "MediaTailor Behavior";
			var domBehaviorDefinition = BehaviorDefinitions.CreateMediaTailorBehavior(sections, behaviorName);
			CreateOrUpdateDomBehaviorDefinition(domBehaviorDefinition);

			var nameDefinition = new ModuleSettingsOverrides
			{
				NameDefinition = new DomInstanceNameDefinition
				{
					ConcatenationItems = new List<IDomInstanceConcatenationItem>
					{
						new FieldValueConcatenationItem { FieldDescriptorId = eventIdDescriptor },
						new StaticValueConcatenationItem { Value = " - " },
						new FieldValueConcatenationItem { FieldDescriptorId = cdnDescriptor },
						new StaticValueConcatenationItem { Value = " - " },
						new FieldValueConcatenationItem { FieldDescriptorId = formatDescriptor },
					},
				},
			};

			return new DomDefinition
			{
				Name = "MediaTailor",
				SectionDefinitionLinks = new List<SectionDefinitionLink> { new SectionDefinitionLink(mediaTailorSection.GetID()) },
				DomBehaviorDefinitionId = domBehaviorDefinition.ID,
				ModuleSettingsOverrides = nameDefinition,
			};
		}

		private void CreateOrUpdateDomBehaviorDefinition(DomBehaviorDefinition newDomBehaviorDefinition)
		{
			if (newDomBehaviorDefinition != null)
			{
				var domBehaviorDefinition = domHelper.DomBehaviorDefinitions.Read(DomBehaviorDefinitionExposers.Name.Equal(newDomBehaviorDefinition.Name));
				if (domBehaviorDefinition.Any())
				{
					newDomBehaviorDefinition.ID = domBehaviorDefinition.FirstOrDefault()?.ID;
					domHelper.DomBehaviorDefinitions.Update(newDomBehaviorDefinition);
				}
				else
				{
					domHelper.DomBehaviorDefinitions.Create(newDomBehaviorDefinition);
				}
			}
		}

		public class SectionDefinitions
		{
			public static SectionDefinition CreateTouchstreamSection(DomHelper domHelper, ref FieldDescriptorID nameDescriptor)
			{
				var mediaTailorDef = domHelper.DomDefinitions.Read(DomDefinitionExposers.Name.Equal("MediaTailor")).First().ID;

				var sourceElement = CreateFieldDescriptorObject<string>("Source Element (Touchstream)", "(optional) A DMAID/ELID/PID to allow the process to report back to the source element that created this DOM.");
				var sourceId = CreateFieldDescriptorObject<string>("Source ID (Touchstream)", "(optional) An ID that can be used when reporting back to the source element to identify the response.");
				var element = CreateFieldDescriptorObject<string>("Touchstream Element (Touchstream)", "The name of the Touchstream element.");
				var assetId = CreateFieldDescriptorObject<string>("Asset ID (Touchstream)", "The Asset ID for the Provision.");
				var eventId = CreateFieldDescriptorObject<string>("Event ID (Touchstream)", "The Event ID for the Provision.");
				var eventLabel = CreateFieldDescriptorObject<string>("Event Label (Touchstream)", "Value used for the Touchstream Stream Label field.");
				var eventName = CreateFieldDescriptorObject<string>("Event Name (Touchstream)", "Valued used for the Touchstream Channel / Title Name field.");
				var template = CreateFieldDescriptorObject<string>("Template Name (Touchstream)", "Indicates the Touchstream Template to be used for this provision.");
				var yospaceHls = CreateFieldDescriptorObject<string>("YoSpace Stream ID HLS (Touchstream)", "Value that will be used in the Manifest URL if YoSpace Monitoring is used.");
				var yospaceMpd = CreateFieldDescriptorObject<string>("YoSpace Stream ID MPD (Touchstream)", "Value that will be used in the Manifest URL if YoSpace Monitoring is used.");
				var reducedTemplate = CreateFieldDescriptorObject<string>("Reduced Template (Touchstream)", "Is part of the Ranking Logic and indicates if this stream will be included if the rank bracket is in the Reduced Template mode.");
				var eventStartDate = CreateFieldDescriptorObject<DateTime>("Event Start Date (Touchstream)", "Date the Provision Starts.");
				var eventEndDate = CreateFieldDescriptorObject<DateTime>("Event End Date (Touchstream)", "Date the Provision End.");
				var forcedUpdate = CreateFieldDescriptorObject<string>("Forced Update (Touchstream)", "Prioritizes the processing of this message for such actions as updating Dynamic Groups.");
				var instanceId = CreateFieldDescriptorObject<string>("InstanceId (Touchstream)", "The id of the DOM instance.");
				var mediaTailor = CreateDomInstanceFieldDescriptorObject<List<Guid>>("MediaTailor (Touchstream)", "(optional) Links to the MediaTailor Instances for this provision if needed.", mediaTailorDef);
				var dynamicGroup = CreateFieldDescriptorObject<string>("Dynamic Group (Touchstream)", "(optional) DynamicGroup to be used for provision events.");

				nameDescriptor = eventName.ID;

				List<FieldDescriptor> fieldDescriptors = new List<FieldDescriptor>
				{
					sourceElement,
					sourceId,
					element,
					assetId,
					eventId,
					eventLabel,
					eventName,
					template,
					yospaceHls,
					yospaceMpd,
					reducedTemplate,
					eventStartDate,
					eventEndDate,
					forcedUpdate,
					instanceId,
					mediaTailor,
					dynamicGroup,
				};

				var sectionDefinition = CreateOrUpdateSection("Touchstream", domHelper, fieldDescriptors);

				return sectionDefinition;
			}

			public static SectionDefinition CreateMediaTailorSection(DomHelper domHelper, ref FieldDescriptorID eventIdId, ref FieldDescriptorID cdnId, ref FieldDescriptorID formatId)
			{
				var element = CreateFieldDescriptorObject<string>("MediaTailor Element (MediaTailor)", "The name of the MediaTailor element to run the scan.");
				var cdn = CreateFieldDescriptorObject<string>("CDN (MediaTailor)", "Indicates the CDN which MediaTailor will use.");
				var eventId = CreateFieldDescriptorObject<string>("Event ID (MediaTailor)", "The Event ID of the event to be scanned.");
				var format = CreateFieldDescriptorObject<string>("Format (MediaTailor)", "The encoding type of the stream.");
				var domainUrl = CreateFieldDescriptorObject<string>("Domain URL (MediaTailor)", "The base URL for MediaTailor.");
				var url = CreateFieldDescriptorObject<string>("URL (MediaTailor)", "Extends the Domain URL with event / formatting options.");
				var product = CreateFieldDescriptorObject<string>("Product (MediaTailor)", "The Touchstream project that will be used.");
				var payload = CreateFieldDescriptorObject<string>("Payload (MediaTailor)", "Describes the format of the payload.");
				var resultUrl = CreateFieldDescriptorObject<string>("Result URL (MediaTailor)", "Stores the URL provided by MediaTailor that will be used by Touchstream.");

				eventIdId = eventId.ID;
				cdnId = cdn.ID;
				formatId = format.ID;

				List<FieldDescriptor> fieldDescriptors = new List<FieldDescriptor>
				{
					cdn,
					eventId,
					format,
					element,
					domainUrl,
					url,
					product,
					payload,
					resultUrl,
				};

				var provisionInfoSection = CreateOrUpdateSection("MediaTailor", domHelper, fieldDescriptors);

				return provisionInfoSection;
			}

			private static SectionDefinition CreateOrUpdateSection(string name, DomHelper domHelper, List<FieldDescriptor> fieldDescriptors)
			{
				var domInstancesSectionDefinition = new CustomSectionDefinition
				{
					Name = name,
				};

				var domInstanceSection = domHelper.SectionDefinitions.Read(SectionDefinitionExposers.Name.Equal(domInstancesSectionDefinition.Name));
				SectionDefinition sectionDefinition;
				if (!domInstanceSection.Any())
				{
					foreach (var field in fieldDescriptors)
					{
						domInstancesSectionDefinition.AddOrReplaceFieldDescriptor(field);
					}

					sectionDefinition = domHelper.SectionDefinitions.Create(domInstancesSectionDefinition) as CustomSectionDefinition;
				}
				else
				{
					// Update Section Definition (Add missing fieldDescriptors)
					sectionDefinition = UpdateSectionDefinition(domHelper, fieldDescriptors, domInstanceSection);
				}

				return sectionDefinition;
			}

			private static SectionDefinition UpdateSectionDefinition(DomHelper domHelper, List<FieldDescriptor> fieldDescriptorList, List<SectionDefinition> sectionDefinition)
			{
				var existingSectionDefinition = sectionDefinition.First() as CustomSectionDefinition;
				var previousFieldNames = existingSectionDefinition.GetAllFieldDescriptors().Select(x => x.Name).ToList();
				List<FieldDescriptor> fieldDescriptorsToAdd = new List<FieldDescriptor>();

				// Check if there's a fieldDefinition to add
				foreach (var newfieldDescriptor in fieldDescriptorList)
				{
					if (!previousFieldNames.Contains(newfieldDescriptor.Name))
					{
						fieldDescriptorsToAdd.Add(newfieldDescriptor);
					}
				}

				if (fieldDescriptorsToAdd.Count > 0)
				{
					foreach (var field in fieldDescriptorsToAdd)
					{
						existingSectionDefinition.AddOrReplaceFieldDescriptor(field);
					}

					existingSectionDefinition = domHelper.SectionDefinitions.Update(existingSectionDefinition) as CustomSectionDefinition;
				}

				return existingSectionDefinition;
			}

			private static FieldDescriptor CreateFieldDescriptorObject<T>(string fieldName, string toolTip)
			{
				return new FieldDescriptor
				{
					FieldType = typeof(T),
					Name = fieldName,
					Tooltip = toolTip,
				};
			}

			private static DomInstanceFieldDescriptor CreateDomInstanceFieldDescriptorObject<T>(string fieldName, string toolTip, DomDefinitionId definitionId)
			{
				var fieldDescriptor = new DomInstanceFieldDescriptor("process_automation")
				{
					FieldType = typeof(T),
					Name = fieldName,
					Tooltip = toolTip,
				};

				fieldDescriptor.DomDefinitionIds.Add(definitionId);
				return fieldDescriptor;
			}
		}

		public class BehaviorDefinitions
		{
			public static DomBehaviorDefinition CreateTouchstreamBehaviorDefinition(List<SectionDefinition> sections, string behaviorName)
			{
				var statuses = new List<DomStatus>
				{
					new DomStatus("draft", "Draft"),
					new DomStatus("ready", "Ready"),
					new DomStatus("in_progress", "In Progress"),
					new DomStatus("active", "Active"),
					new DomStatus("reprovision", "Reprovision"),
					new DomStatus("deactivate", "Deactivate"),
					new DomStatus("deactivating", "Deactivating"),
					new DomStatus("complete", "Complete"),
				};

				var transitions = new List<DomStatusTransition>
				{
					new DomStatusTransition("draft_to_ready", "draft", "ready"),
					new DomStatusTransition("ready_to_inprogress", "ready", "in_progress"),
					new DomStatusTransition("inprogress_to_active", "in_progress", "active"),
					new DomStatusTransition("active_to_reprovision", "active", "reprovision"),
					new DomStatusTransition("active_to_deactivate", "active", "deactivate"),
					new DomStatusTransition("reprovision_to_inprogress", "reprovision", "in_progress"),
					new DomStatusTransition("deactivate_to_deactivating", "deactivate", "deactivating"),
					new DomStatusTransition("deactivating_to_complete", "deactivating", "complete"),
					new DomStatusTransition("complete_to_draft", "complete", "draft"),
				};

				List<IDomActionDefinition> behaviorActions = GetBehaviorActions("Touchstream Process", "Event Name");

				List<IDomButtonDefinition> domButtons = GetBehaviorButtons();

				return new DomBehaviorDefinition
				{
					Name = behaviorName,
					InitialStatusId = "draft",
					Statuses = statuses,
					StatusTransitions = transitions,
					StatusSectionDefinitionLinks = GetTouchstreamStatusLinks(sections),
					ActionDefinitions = behaviorActions,
					ButtonDefinitions = domButtons,
				};
			}

			public static DomBehaviorDefinition CreateMediaTailorBehavior(List<SectionDefinition> sections, string behaviorName)
			{
				var statuses = new List<DomStatus>
				{
					new DomStatus("ready", "Ready"),
					new DomStatus("complete", "Complete"),
				};

				var transitions = new List<DomStatusTransition>
				{
					new DomStatusTransition("ready_to_complete", "ready", "complete"),
					new DomStatusTransition("complete_to_ready", "complete", "ready"),
				};

				return new DomBehaviorDefinition
				{
					Name = behaviorName,
					InitialStatusId = "ready",
					Statuses = statuses,
					StatusTransitions = transitions,
					StatusSectionDefinitionLinks = GetMediaTailorBehaviorLinks(sections),
				};
			}

			private static List<DomStatusSectionDefinitionLink> GetTouchstreamStatusLinks(List<SectionDefinition> sections)
			{
				Dictionary<string, FieldDescriptorID> fieldsList = GetFieldDescriptorDictionary(sections.First());

				var draftStatusLink = StatusSectionDefinitions.GetTouchstreamSectionDefinitionLinkDraft(sections.First(), fieldsList);
				var readyStatusLink = StatusSectionDefinitions.GetTouchstreamSectionDefinitionLink(sections.First(), fieldsList, "ready");
				var inprogressStatusLink = StatusSectionDefinitions.GetTouchstreamSectionDefinitionLink(sections.First(), fieldsList, "in_progress");
				var activeStatusLink = StatusSectionDefinitions.GetTouchstreamSectionDefinitionLink(sections.First(), fieldsList, "active");
				var reprovisionStatusLink = StatusSectionDefinitions.GetTouchstreamSectionDefinitionLink(sections.First(), fieldsList, "reprovision");
				var deactivateStatusLink = StatusSectionDefinitions.GetTouchstreamSectionDefinitionLink(sections.First(), fieldsList, "deactivate");
				var deactivatingStatusLink = StatusSectionDefinitions.GetTouchstreamSectionDefinitionLink(sections.First(), fieldsList, "deactivating");
				var completeStatusLink = StatusSectionDefinitions.GetTouchstreamSectionDefinitionLink(sections.First(), fieldsList, "complete");

				return new List<DomStatusSectionDefinitionLink> { draftStatusLink, readyStatusLink, inprogressStatusLink, activeStatusLink, reprovisionStatusLink, deactivateStatusLink, deactivatingStatusLink, completeStatusLink };
			}

			private static List<DomStatusSectionDefinitionLink> GetMediaTailorBehaviorLinks(List<SectionDefinition> sections)
			{
				Dictionary<string, FieldDescriptorID> fieldsList = GetFieldDescriptorDictionary(sections.First());

				var readyStatusLinks = StatusSectionDefinitions.GetMediaTailorDefinitionLink(sections.First(), fieldsList, "ready");
				var completeStatusLinks = StatusSectionDefinitions.GetMediaTailorDefinitionLink(sections.First(), fieldsList, "complete");

				return new List<DomStatusSectionDefinitionLink> { readyStatusLinks, completeStatusLinks };
			}

			private static Dictionary<string, FieldDescriptorID> GetFieldDescriptorDictionary(SectionDefinition section)
			{
				Dictionary<string, FieldDescriptorID> fieldsList = new Dictionary<string, FieldDescriptorID>();

				var fields = section.GetAllFieldDescriptors();
				foreach (var field in fields)
				{
					var fieldName = field.Name;

					fieldsList[fieldName] = field.ID;
				}

				return fieldsList;
			}

			private static List<IDomButtonDefinition> GetBehaviorButtons()
			{
				DomInstanceButtonDefinition provisionButton = new DomInstanceButtonDefinition("provision")
				{
					VisibilityCondition = new StatusCondition(new List<string> { "draft" }),
					ActionDefinitionIds = new List<string> { "provision" },
					Layout = new DomButtonDefinitionLayout { Text = "Provision" },
				};

				DomInstanceButtonDefinition deactivateButton = new DomInstanceButtonDefinition("deactivate")
				{
					VisibilityCondition = new StatusCondition(new List<string> { "active" }),
					ActionDefinitionIds = new List<string> { "deactivate" },
					Layout = new DomButtonDefinitionLayout { Text = "Deactivate" },
				};

				DomInstanceButtonDefinition reprovisionButton = new DomInstanceButtonDefinition("reprovision")
				{
					VisibilityCondition = new StatusCondition(new List<string> { "active" }),
					ActionDefinitionIds = new List<string> { "reprovision" },
					Layout = new DomButtonDefinitionLayout { Text = "Reprovision" },
				};

				DomInstanceButtonDefinition completeProvision = new DomInstanceButtonDefinition("complete-provision")
				{
					VisibilityCondition = new StatusCondition(new List<string> { "complete" }),
					ActionDefinitionIds = new List<string> { "complete-provision" },
					Layout = new DomButtonDefinitionLayout { Text = "Provision" },
				};

				List<IDomButtonDefinition> domButtons = new List<IDomButtonDefinition> { provisionButton, deactivateButton, reprovisionButton, completeProvision };
				return domButtons;
			}

			private static List<IDomActionDefinition> GetBehaviorActions(string processName, string businessKeyField)
			{
				var provisionAction = new ExecuteScriptDomActionDefinition("provision")
				{
					Script = "start_process",
					IsInteractive = false,
					ScriptOptions = new List<string>
					{
						$"PARAMETER:1:{processName}",
						"PARAMETER:2:draft_to_ready",
						$"PARAMETER:3:{businessKeyField}",
						"PARAMETER:4:provision",
					},
				};

				var deactivateAction = new ExecuteScriptDomActionDefinition("deactivate")
				{
					Script = "start_process",
					IsInteractive = false,
					ScriptOptions = new List<string>
					{
						$"PARAMETER:1:{processName}",
						"PARAMETER:2:active_to_deactivate",
						$"PARAMETER:3:{businessKeyField}",
						"PARAMETER:4:deactivate",
					},
				};

				var reprovisionAction = new ExecuteScriptDomActionDefinition("reprovision")
				{
					Script = "start_process",
					IsInteractive = false,
					ScriptOptions = new List<string>
					{
						$"PARAMETER:1:{processName}",
						"PARAMETER:2:active_to_reprovision",
						$"PARAMETER:3:{businessKeyField}",
						"PARAMETER:4:reprovision",
					},
				};

				var completeProvisionAction = new ExecuteScriptDomActionDefinition("complete-provision")
				{
					Script = "start_process",
					IsInteractive = false,
					ScriptOptions = new List<string>
					{
						$"PARAMETER:1:{processName}",
						"PARAMETER:2:complete_to_ready",
						$"PARAMETER:3:{businessKeyField}",
						"PARAMETER:4:complete-provision",
					},
				};

				var behaviorActions = new List<IDomActionDefinition> { provisionAction, deactivateAction, reprovisionAction, completeProvisionAction, };
				return behaviorActions;
			}

			public class StatusSectionDefinitions
			{
				public static DomStatusSectionDefinitionLink GetMediaTailorDefinitionLink(SectionDefinition section, Dictionary<string, FieldDescriptorID> fieldsList, string status)
				{
					var sectionStatusLinkId = new DomStatusSectionDefinitionLinkId(status, section.GetID());

					var activeDefinitionLink = new DomStatusSectionDefinitionLink(sectionStatusLinkId)
					{
						FieldDescriptorLinks = new List<DomStatusFieldDescriptorLink>
						{
							new DomStatusFieldDescriptorLink(fieldsList["MediaTailor Element (MediaTailor)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["CDN (MediaTailor)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event ID (MediaTailor)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Format (MediaTailor)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Domain URL (MediaTailor)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["URL (MediaTailor)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Product (MediaTailor)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Payload (MediaTailor)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Result URL (MediaTailor)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
						},
					};

					return activeDefinitionLink;
				}

				public static DomStatusSectionDefinitionLink GetTouchstreamSectionDefinitionLinkDraft(SectionDefinition section, Dictionary<string, FieldDescriptorID> fieldsList)
				{
					var sectionStatusLinkId = new DomStatusSectionDefinitionLinkId("draft", section.GetID());

					DomStatusSectionDefinitionLink draftStatusLinkDomInstance = new DomStatusSectionDefinitionLink(sectionStatusLinkId)
					{
						FieldDescriptorLinks = new List<DomStatusFieldDescriptorLink>
						{
							new DomStatusFieldDescriptorLink(fieldsList["Source Element (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Source ID (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Touchstream Element (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Asset ID (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event ID (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event Label (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event Name (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Template Name (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["YoSpace Stream ID HLS (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["YoSpace Stream ID MPD (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Reduced Template (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event Start Date (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event End Date (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Forced Update (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["MediaTailor (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["InstanceId (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Dynamic Group (Touchstream)"])
							{
								Visible = true,
								ReadOnly = false,
								RequiredForStatus = false,
							},
						},
					};

					return draftStatusLinkDomInstance;
				}

				public static DomStatusSectionDefinitionLink GetTouchstreamSectionDefinitionLink(SectionDefinition section, Dictionary<string, FieldDescriptorID> fieldsList, string status)
				{
					var sectionStatusLinkId = new DomStatusSectionDefinitionLinkId(status, section.GetID());
					DomStatusSectionDefinitionLink draftStatusLinkDomInstance = new DomStatusSectionDefinitionLink(sectionStatusLinkId)
					{
						FieldDescriptorLinks = new List<DomStatusFieldDescriptorLink>
						{
							new DomStatusFieldDescriptorLink(fieldsList["Source Element (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Source ID (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Touchstream Element (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Asset ID (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event ID (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event Label (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event Name (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Template Name (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["YoSpace Stream ID HLS (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["YoSpace Stream ID MPD (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Reduced Template (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event Start Date (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Event End Date (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Forced Update (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["MediaTailor (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = false,
							},
							new DomStatusFieldDescriptorLink(fieldsList["InstanceId (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = true,
							},
							new DomStatusFieldDescriptorLink(fieldsList["Dynamic Group (Touchstream)"])
							{
								Visible = true,
								ReadOnly = true,
								RequiredForStatus = false,
							},
						},
					};

					return draftStatusLinkDomInstance;
				}
			}
		}
	}
}