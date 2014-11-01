using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace RoboWorker3.TO
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RollUpModel
    {
        STAKE, PAYOUT, MANAGED_LIABILITY, NONE
    }
}
