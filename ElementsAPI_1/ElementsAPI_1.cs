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

06/09/2023	1.0.0.1		TGH, Skyline	Initial version
****************************************************************************
*/

namespace ElementsAPI_1
{
    using System.Linq;
    using System.Collections.Generic;
    using Models;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Net.Apps.UserDefinableApis;
    using Skyline.DataMiner.Net.Apps.UserDefinableApis.Actions;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class Script
    {
        private readonly List<string> _knownAlarmLevels
            = new List<string> { "Warning", "Minor", "Major", "Critical" };

        private readonly JsonSerializerSettings _serializerSettings
            = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            };

        [AutomationEntryPoint(AutomationEntryPointType.Types.OnApiTrigger)]
        public ApiTriggerOutput OnApiTrigger(IEngine engine, ApiTriggerInput requestData)
        {
            // Validate if a body was given
            if (string.IsNullOrWhiteSpace(requestData.RawBody))
            {
                return new ApiTriggerOutput()
                {
                    ResponseBody = "Request body cannot be empty",
                    ResponseCode = (int)StatusCode.BadRequest,
                };
            }

            // Convert the body JSON to a 'Input' object
            Input input;
            try
            {
                input = JsonConvert.DeserializeObject<Input>(
                    requestData.RawBody ?? string.Empty, _serializerSettings);
            }
            catch
            {
                return new ApiTriggerOutput()
                {
                    ResponseBody = "Could not parse request body.",
                    ResponseCode = (int)StatusCode.InternalServerError,
                };
            }

            // Validate if a valid alarm level was given
            if (string.IsNullOrWhiteSpace(input.AlarmLevel) || !_knownAlarmLevels.Contains(input.AlarmLevel))
            {
                return new ApiTriggerOutput()
                {
                    ResponseBody =
                        $"Invalid alarm level passed, possible values are: ${string.Join(",", _knownAlarmLevels)}",
                    ResponseCode = (int)StatusCode.BadRequest,
                };
            }

            // Get the elements according to the alarm level and limit
            List<ElementInfo> elements;
            try
            {
                elements = GetElements(engine, input);
            }
            catch
            {
                return new ApiTriggerOutput()
                {
                    ResponseBody = "Something went wrong fetching the Elements.",
                    ResponseCode = (int)StatusCode.InternalServerError,
                };
            }

            // Return the collection of elements as a JSON
            return new ApiTriggerOutput
            {
                ResponseBody = JsonConvert.SerializeObject(elements, _serializerSettings),
                ResponseCode = (int)StatusCode.Ok,
            };
        }

        private List<ElementInfo> GetElements(IEngine engine, Input input)
        {
            // Build an element filter according to the alarm level input
            var elementFilter = new ElementFilter();
            switch (input.AlarmLevel)
            {
                case "Minor":
                    elementFilter.MinorOnly = true;
                    break;
                case "Warning":
                    elementFilter.WarningOnly = true;
                    break;
                case "Major":
                    elementFilter.MajorOnly = true;
                    break;
                case "Critical":
                    elementFilter.CriticalOnly = true;
                    break;
            }

            // Retrieve the elements
            var rawElements = engine.FindElements(elementFilter);

            // Map the elements to objects of the 'ElementInfo' class
            var elements = new List<ElementInfo>();
            foreach (var rawElement in rawElements)
            {
                elements.Add(new ElementInfo()
                {
                    DataMinerId = rawElement.RawInfo.DataMinerID,
                    ElementId = rawElement.RawInfo.ElementID,
                    Name = rawElement.ElementName,
                    ProtocolName = rawElement.ProtocolName,
                    ProtocolVersion = rawElement.ProtocolVersion,
                });
            }

            // Limit and return the elements list
            return elements.Take(input.Limit).ToList();
        }
    }
}

namespace Models
{
    public class ElementInfo
    {
        public int ElementId { get; set; }

        public int DataMinerId { get; set; }

        public string ProtocolName { get; set; }

        public string ProtocolVersion { get; set; }

        public string Name { get; set; }
    }

    public class Input
    {
        public string AlarmLevel { get; set; }

        public int Limit { get; set; }
    }
}
