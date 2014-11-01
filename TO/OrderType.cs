using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace RoboWorker3.TO
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum OrderType
    {
        LIMIT,				// normal exchange limit order for immediate execution
        LIMIT_ON_CLOSE,		// limit order for the auction (SP)
        MARKET_ON_CLOSE		// market order for the auction (SP)
    }
}
