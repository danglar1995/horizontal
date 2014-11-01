using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace RoboWorker3.TO
{
    [JsonObject(MemberSerialization.OptOut)]
    public class MarketIds
    {
        public string marketId1;
    }
}
