using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace RoboWorker3.TO
{
    public class MarketCatalogue
    {
        [JsonProperty(PropertyName = "marketId")]
        public string MarketId { get; set; }

        [JsonProperty(PropertyName = "marketName")]
        public string MarketName { get; set; }

        [JsonProperty(PropertyName = "isMarketDataDelayed")]
        public bool IsMarketDataDelayed { get; set; }

        
    }
}
