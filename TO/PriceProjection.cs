using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace RoboWorker3.TO
{
    public class PriceProjection
    {
        [JsonProperty(PropertyName = "priceData")]
        public IList<PriceData> PriceData { get; set; }

        [JsonProperty(PropertyName = "exBestOffersOverrides")]
        public ExBestOffersOverrides ExBestOffersOverrides { get; set; }

        [JsonProperty(PropertyName = "virtualise")]
        public bool? Virtualise { get; set; }

        [JsonProperty(PropertyName = "rolloverStakes")]
        public bool? RolloverStakes { get; set; }


    }
}
