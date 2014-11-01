using System;
using System.Collections.Generic;
using System.Text;
using RoboWorker3.TO;

namespace RoboWorker3
{
    public interface IClient
    {
        IList<MarketBook> listMarketBook(IList<string> marketIds, PriceProjection priceProjection, OrderProjection? orderProjection, MatchProjection? matchProjection, string currencyCode, string locale);
        IList<MarketBook> listMarketBook(string req);
    }
}
