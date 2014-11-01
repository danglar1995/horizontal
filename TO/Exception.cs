using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace RoboWorker3.TO
{
    public class Exception
    {
        // exception in json-rpc format
        [JsonProperty(PropertyName = "data")]
        public Newtonsoft.Json.Linq.JObject Data { get; set; }		// actual exception details


        // exception in rescript format
        [JsonProperty(PropertyName = "detail")]
        public Newtonsoft.Json.Linq.JObject Detail { get; set; }		// actual exception details

    }
}
