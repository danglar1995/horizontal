using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using RoboWorker3.bf.global;
using RoboWorker3.bf.exchange;
using RoboWorker3.TO;

namespace RoboWorker3
{
    public class BetfairExchange
    {
        public BetfairExchange()
        {
            is_logged_in = false;
            session_token = string.Empty;

            transactions = 0;
        }

        private string session_token;
        private bool is_logged_in;
        public int transactions;
        public static DateTime demo_date = DateTime.Now.AddYears(10);
        private DateTime last_login_date;

        public bool checkLoginStatus()
        {
            if (last_login_date == DateTime.MinValue || last_login_date.AddHours(1) < DateTime.Now)
            {
                login();
                last_login_date = DateTime.Now;
            }
            return true;
        }

        private bool login()
        {
            BFGlobalService bfGlobal = new BFGlobalService();
            LoginReq req = new LoginReq();
            req.username = Market.login;
            req.password = Market.password;
            req.productId = 82;
            req.vendorSoftwareId = 82;
            LoginResp resp = bfGlobal.login(req);
            //Process response
            if (resp.header.errorCode != RoboWorker3.bf.global.APIErrorEnum.OK || resp.errorCode != LoginErrorEnum.OK)
            {
                //Show error if login failed
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "The API returned the following error codes\r\n\r\nHeader Error Code: " + resp.header.errorCode.ToString().Replace("_", " ") + "\r\nLogin Error Code:   " + resp.errorCode.ToString().Replace("_", " "));
                Console.WriteLine("The API returned the following error codes\r\n\r\nHeader Error Code: " + resp.header.errorCode.ToString().Replace("_", " ") + "\r\nLogin Error Code:   " + resp.errorCode.ToString().Replace("_", " "));
                return false;
            }
            session_token = resp.header.sessionToken;
            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Free login OK");
            Console.WriteLine("Free login OK");
            is_logged_in = true;
            return true;
        }

        public void GetAllMarkets(DateTime startDate, int[] eventIds, DateTime start, DateTime end, List<MarketInfo> markets, int hours, string marketTypeName)
        {
            checkLoginStatus();

            GetAllMarketsReq req = new GetAllMarketsReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            req.fromDate = startDate;
            req.toDate = end;
            req.locale = "en";
            int?[] topEventIds = new int?[eventIds.Length];
            for (int i = 0; i < topEventIds.Length; i++)
                topEventIds[i] = eventIds[i];
            req.eventTypeIds = topEventIds;
            GetAllMarketsResp resp = new RoboWorker3.bf.exchange.BFExchangeService().getAllMarkets(req);
            ConvertFromGetAllMarketResponse(resp.marketData, eventIds, markets, hours, marketTypeName);
        }

        private void ConvertFromGetAllMarketResponse(string response, int[] topEventIds, List<MarketInfo> markets, int hours, string marketTypeName)
        {
            string[] marketsStrings = response.Split(new string[1] { ":" }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                for (int i = 0; i < marketsStrings.Length; i++)
                {
                    if (marketsStrings[i] != "")
                    {
                        string[] marketString = marketsStrings[i].Split(new string[1] { "~" }, StringSplitOptions.None);
                        if (marketString.Length >= 14 && marketString[3] == "ACTIVE")
                        {
                            if (marketString[9] == "GRC") // Исключаем греческий футбол
                            {
                                continue;
                            }
                            string name = marketString[1];
                            DateTime start_time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(double.Parse(marketString[4])).ToLocalTime();
                            int event_id = int.Parse(marketString[6].Split(new string[1] { "/" }, StringSplitOptions.RemoveEmptyEntries)[0]);

                            if (name.ToLower().Contains(marketTypeName) && start_time > DateTime.Now && start_time <= DateTime.Now.AddHours(hours))
                            {
                                bool isFound = false;
                                int market_id = Convert.ToInt32(marketString[0]);
                                for (int j = 0; j < markets.Count; j++)
                                {
                                    if (markets[j].market_id == market_id)
                                    {
                                        markets[j].close_time = start_time;
                                        isFound = true;
                                        break;
                                    }
                                }
                                if (isFound)
                                {
                                    continue;
                                }
                                MarketInfo m = new MarketInfo();
                                m.market_id = market_id;
                                m.close_time = start_time;
                                markets.Add(m);
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine("Exception" + ex.ToString());
            }
        }

        public bool getMarketTradedVolume(ref MarketInfo market)
        {
            checkLoginStatus();
            
            double total_matched = 0;
            for (int k = 0; k < market.team_ids.Count; k++)
            {
                RoboWorker3.bf.exchange.GetMarketTradedVolumeReq req = new RoboWorker3.bf.exchange.GetMarketTradedVolumeReq();
                req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
                req.header.sessionToken = session_token;
                req.marketId = market.market_id;

                req.selectionId = market.team_ids[k];

                RoboWorker3.bf.exchange.GetMarketTradedVolumeResp resp = new RoboWorker3.bf.exchange.BFExchangeService().getMarketTradedVolume(req);
                if (resp.errorCode == (RoboWorker3.bf.exchange.GetMarketTradedVolumeErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
                {
                    login();
                }
                // Display success or error message depending on result
                if (resp.errorCode != RoboWorker3.bf.exchange.GetMarketTradedVolumeErrorEnum.OK && resp.errorCode != RoboWorker3.bf.exchange.GetMarketTradedVolumeErrorEnum.NO_RESULTS)
                {
                    File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to get TradedVolume. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                    Console.WriteLine("Failed to get TradedVolume. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                    return false;
                }
                double result = 0.0;
                if (resp.errorCode != RoboWorker3.bf.exchange.GetMarketTradedVolumeErrorEnum.NO_RESULTS)
                {
                    for (int i = 0; i < resp.priceItems.Length; i++)
                    {
                        result += resp.priceItems[i].totalMatchedAmount;
                    }
                }
                market.matcheds[market.team_ids[k]] = result;
                total_matched += result;
            }
            market.total_matched = total_matched;

            return true;
        }

        /*
        public bool getPrices(ref MarketInfo market, double min_cash_for_bet)
        {
            try
            {
                string htmlContent = string.Empty;
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://lite.betfair.com/Market.do?s=000114" + market.market_id.ToString() + "x1z");
                    using (StreamReader reader = new StreamReader(request.GetResponse().GetResponseStream()))
                    {
                        htmlContent = reader.ReadToEnd();
                    }
                }

                market.is_suspended = false;
                market.is_closed = false;

                if (htmlContent.Contains("This market is suspended"))
                {
                    market.is_suspended = true;
                    return true;
                }

                if (htmlContent.Contains("This market is closed"))
                {
                    market.is_closed = true;
                    return true;
                }

                // Хак, с целью немедленно прикрыть это безобразие
                if (htmlContent.Contains("[ In-play ]"))
                {
                    market.close_time = DateTime.Now.AddMinutes(3);
                }

                int p1, p2, tm;

                p1 = htmlContent.IndexOf("<strong>Total Matched</strong>: ");
                p1 = p1 + "<strong>Total Matched</strong>: ".Length + 1;
                p2 = htmlContent.IndexOf("<", p1);
                market.total_matched = double.Parse(htmlContent.Substring(p1, p2 - p1).Replace(",", string.Empty));

                p1 = htmlContent.IndexOf("<strong>Back</strong>");
                p1 = p1 + "<strong>Back</strong>".Length + 2;
                p2 = htmlContent.IndexOf("%", p1);
                if (p2 - p1 < 10)
                {
                    //market.back_percent = double.Parse(htmlContent.Substring(p1, p2 - p1), System.Globalization.NumberStyles.AllowDecimalPoint);
                }

                p1 = htmlContent.IndexOf("<strong>Lay</strong>");
                p1 = p1 + "<strong>Lay</strong>".Length + 2;
                p2 = htmlContent.IndexOf("%", p1);
                if (p2 - p1 < 10)
                {
                    //market.lay_percent = double.Parse(htmlContent.Substring(p1, p2 - p1), System.Globalization.NumberStyles.AllowDecimalPoint);
                }

                tm = htmlContent.IndexOf("<tr><td>");
                for (int i = 0; i < market.team_ids.Count; i++)
                {
                    MarketCoefficients kfs = new MarketCoefficients();

                    p1 = htmlContent.IndexOf("<td class=\"back\"> <strong>", tm);
                    p1 = p1 + "<td class=\"back\"> <strong>".Length;
                    p2 = htmlContent.IndexOf("<", p1);
                    if (htmlContent.Substring(p1, p2 - p1).ToLower() == "offer")
                    {
                        kfs.back.Add(1.0);
                        kfs.back_cash.Add(0);
                    }
                    else
                    {
                        kfs.back.Add(double.Parse(htmlContent.Substring(p1, p2 - p1), System.Globalization.NumberStyles.AllowDecimalPoint));

                        p1 = htmlContent.IndexOf("$", p2) + 1;
                        p2 = htmlContent.IndexOf(")", p1);
                        kfs.back_cash.Add(double.Parse(htmlContent.Substring(p1, p2 - p1).Replace(",", string.Empty)));
                    }

                    p1 = htmlContent.IndexOf("<td class=\"lay\"> <strong>", tm);
                    p1 = p1 + "<td class=\"lay\"> <strong>".Length;
                    p2 = htmlContent.IndexOf("<", p1);
                    if (htmlContent.Substring(p1, p2 - p1).ToLower() == "offer")
                    {
                        kfs.lay.Add(1000);
                        kfs.lay_cash.Add(0);
                    }
                    else
                    {
                        kfs.lay.Add(double.Parse(htmlContent.Substring(p1, p2 - p1), System.Globalization.NumberStyles.AllowDecimalPoint));

                        p1 = htmlContent.IndexOf("$", p2) + 1;
                        p2 = htmlContent.IndexOf(")", p1);
                        kfs.lay_cash.Add(double.Parse(htmlContent.Substring(p1, p2 - p1).Replace(",", string.Empty)));
                    }

                    market.coefficients[market.team_ids[i]] = kfs;

                    tm = htmlContent.IndexOf("<tr><td>", tm + 1);
                }

                p1 = htmlContent.IndexOf("<strong>Market Information</strong>: ");
                p1 += "<strong>Market Information</strong>: ".Length;
                p2 = htmlContent.IndexOf(" MSD", p1);
                if (p2 == -1)
                {
                    p2 = htmlContent.IndexOf(" MSK", p1);
                }
                string close_time = htmlContent.Substring(p1, p2 - p1);

                Console.WriteLine("close_time = " + close_time);

                int day = int.Parse(close_time.Substring(0, close_time.IndexOf(" ")));
                p1 = close_time.IndexOf(" ") + 1;
                p2 = close_time.IndexOf(" ", p1);
                string month_str = close_time.Substring(p1, p2 - p1).ToLower();
                p1 = close_time.IndexOf(" - ") + " - ".Length;
                p2 = close_time.IndexOf(":", p1);
                int hour = int.Parse(close_time.Substring(p1, p2 - p1));
                int minute = int.Parse(close_time.Substring(p2 + 1));
                int month = 0;
                switch (month_str)
                {
                    case "jan": 
                        month = 1;
                        break;
                    case "feb": 
                        month = 2;
                        break;
                    case "mar": 
                        month = 3;
                        break;
                    case "apr": 
                        month = 4;
                        break;
                    case "may": 
                        month = 5;
                        break;
                    case "jun": 
                        month = 6;
                        break;
                    case "jul": 
                        month = 7;
                        break;
                    case "aug": 
                        month = 8;
                        break;
                    case "sep": 
                        month = 9;
                        break;
                    case "oct": 
                        month = 10;
                        break;
                    case "nov": 
                        month = 11;
                        break;
                    case "dec": 
                        month = 12;
                        break;
                }

                int year = DateTime.Now.Year;
                if (month < DateTime.Now.Month)
                {
                    year++;
                }
                market.close_time = new DateTime(year, month, day, hour, minute, 0);

                Console.WriteLine("Market " + market.market_id.ToString() + " Total matched " + market.total_matched.ToString());
                for (int i = 0; i < market.team_ids.Count; i++)
                {
                    Console.WriteLine(market.coefficients[market.team_ids[i]].back[0].ToString() + " (" + market.coefficients[market.team_ids[i]].back_cash[0].ToString() + ") \t- " + market.coefficients[market.team_ids[i]].lay[0].ToString() + " (" + market.coefficients[market.team_ids[i]].lay_cash[0].ToString() + ")");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return false;
        }
        */

        public bool getPrices(ref MarketInfo market, double min_cash_for_bet)
        {
            List<MarketInfo> markets = new List<MarketInfo>();
            markets.Add(market);
            return getPrices(markets, min_cash_for_bet);
        }

        private static string Url = "https://api.betfair.com";
        public bool getPrices(List<MarketInfo> markets, double min_cash_for_bet)
        {
            checkLoginStatus();
            for (int index = 0; index < markets.Count; index += 20)
            {
                //IClient client = new JsonRpcClient(Url, "4loI3fZ7ceVUzpZj", session_token);//serg19832
                //IClient client = new JsonRpcClient(Url, "lpLOIKO1Kk2d9MNs", session_token);//vs
                //IClient client = new JsonRpcClient(Url, "JC6O6TlTVbRJTfLr", session_token);//vs3
                //IClient client = new JsonRpcClient(Url, "BVLeM5svoxUxiGHI", session_token);//dimon
                //IClient client = new JsonRpcClient(Url, "b6KvVJjt9iILBbSK", session_token);//artur
                IClient client = new JsonRpcClient(Url, "zuFnXxq9BOMrOepc", session_token);//alex
                //string marketIds = "";
                IList<string> marketIds = new List<string>();
                //marketIds.Add("1.110162587");
                for (int idx = index; idx < index + 20 && idx < markets.Count; idx++)
                {
                    marketIds.Add("1." + markets[idx].market_id.ToString());
                    //marketIds += (marketIds.Length == 0 ? "" : ",") + "\"" + m.market_id.ToString() + "\"";
                }
                //string req = "{\"jsonrpc\":\"2.0\",\"method\":\"SportsAPING/v1.0/listMarketBook\",\"params\":{\"marketIds\":[" + marketIds + "],\"priceProjection\":{\"priceData\":[\"EX_BEST_OFFERS\"]},\"orderProjection\":null,\"matchProjection\":null,\"locale\":null,\"currencyCode\":null},\"id\":1}";
                IList<PriceData> priceData = new List<PriceData>();
                //get all prices from the exchange
                priceData.Add(PriceData.EX_BEST_OFFERS);

                var priceProjection = new PriceProjection();
                priceProjection.PriceData = priceData;

                var marketBook = client.listMarketBook(marketIds, priceProjection, null, null, null, null);
                //var marketBook = client.listMarketBook(req);

                if (marketBook.Count != 0)
                {
                    Console.WriteLine("GetPrices success");
                }
                for (int i = 0; i < marketBook.Count; i++)
                {
                    MarketInfo m = null;
                    foreach (MarketInfo mar in markets)
                    {
                        if ("1." + mar.market_id.ToString() == marketBook[i].MarketId)
                        {
                            m = mar;
                            break;
                        }
                    }
                    if (m == null)
                    {
                        continue;
                    }
                    m.isInplay = marketBook[i].IsInplay;
                    m.total_matched = marketBook[i].TotalMatched;
                    m.team_ids = new List<int>();
                    m.matcheds = new SortedDictionary<int, double>();
                    m.coefficients = new SortedDictionary<int, MarketCoefficients>();
                    foreach (RoboWorker3.TO.Runner runner in marketBook[i].Runners)
                    {
                        m.matcheds[(int)runner.SelectionId] = runner.TotalMatched;
                        m.team_ids.Add((int)runner.SelectionId);
                        m.coefficients[(int)runner.SelectionId] = new MarketCoefficients();
                        MarketCoefficients cfs = m.coefficients[(int)runner.SelectionId];
                        cfs.back = new List<double>();
                        cfs.back_cash = new List<double>();
                        cfs.lay = new List<double>();
                        cfs.lay_cash = new List<double>();
                        for (int j = 0; j < 3; j++)
                        {
                            if (j == 0)
                            {
                                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("marketId {0} ", m.market_id));
                            }
                            if (runner.ExchangePrices.AvailableToBack.Count > j)
                            {
                                cfs.back.Add(runner.ExchangePrices.AvailableToBack[j].Price);
                                cfs.back_cash.Add(runner.ExchangePrices.AvailableToBack[j].Size);
                                if (j == 0)
                                {
                                    File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", string.Format("back {0}", cfs.back[0]));
                                }
                            }
                            if (runner.ExchangePrices.AvailableToLay.Count > j)
                            {
                                cfs.lay.Add(runner.ExchangePrices.AvailableToLay[j].Price);
                                cfs.lay_cash.Add(runner.ExchangePrices.AvailableToLay[j].Size);
                                if (j == 0)
                                {
                                    File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", string.Format(" - lay {0}", cfs.lay[0]));
                                }
                            }
                            if (j == 0)
                            {
                                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n");
                            }
                        }
                    }
                }
                Thread.Sleep(500);
            }
            return true;
        }
        /*
        public bool getPrices(ref MarketInfo market, double min_cash_for_bet)
        {
            checkLoginStatus();
            {
                login();
            }
            RoboWorker3.bf.exchange.GetMarketPricesReq req = new RoboWorker3.bf.exchange.GetMarketPricesReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            req.marketId = market.market_id;

            RoboWorker3.bf.exchange.GetMarketPricesResp resp = new RoboWorker3.bf.exchange.BFExchangeService().getMarketPrices(req);
            if (resp.errorCode != RoboWorker3.bf.exchange.GetMarketPricesErrorEnum.OK)
            {
                if (resp.errorCode == (RoboWorker3.bf.exchange.GetMarketPricesErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
                {
                    login();
                }
                if (resp.header.errorCode == RoboWorker3.bf.exchange.APIErrorEnum.EXCEEDED_THROTTLE)
                {
                    Thread.Sleep(1000);
                }
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to getMarketPrices. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to getMarketPrices. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                return false;
            }
            if (resp.marketPrices.delay != 0)
            {
                market.close_time = DateTime.Now;
                return true;
            }
            market.coefficients.Clear();
            for (int i = 0; i < resp.marketPrices.runnerPrices.Length; i++)
            {
                for (int j = 0; j < market.team_ids.Count; j++)
                {
                    MarketCoefficients kfs = new MarketCoefficients();
                    if (resp.marketPrices.runnerPrices[i].selectionId == market.team_ids[j])
                    {
                        if (resp.marketPrices.runnerPrices[i].bestPricesToBack.Length == 0)
                        {
                            kfs.back.Add(1.0);
                            kfs.back_cash.Add(0.0);
                        }
                        for (int k = 0; k < resp.marketPrices.runnerPrices[i].bestPricesToBack.Length; k++)
                        {
                            if (min_cash_for_bet > resp.marketPrices.runnerPrices[i].bestPricesToBack[k].amountAvailable * (resp.marketPrices.runnerPrices[i].bestPricesToBack[k].price - 1))
                            {
                                continue;
                            }
                            kfs.back.Add(resp.marketPrices.runnerPrices[i].bestPricesToBack[k].price);
                            kfs.back_cash.Add(resp.marketPrices.runnerPrices[i].bestPricesToBack[k].amountAvailable);
                        }
                        if (kfs.back.Count == 0)
                        {
                            kfs.back.Add(resp.marketPrices.runnerPrices[i].bestPricesToBack[0].price);
                            kfs.back_cash.Add(resp.marketPrices.runnerPrices[i].bestPricesToBack[0].amountAvailable);
                        }

                        if (resp.marketPrices.runnerPrices[i].bestPricesToLay.Length == 0)
                        {
                            kfs.lay.Add(1000.0);
                            kfs.lay_cash.Add(0.0);
                        }
                        for (int k = 0; k < resp.marketPrices.runnerPrices[i].bestPricesToLay.Length; k++)
                        {
                            if (min_cash_for_bet > resp.marketPrices.runnerPrices[i].bestPricesToLay[k].amountAvailable * (resp.marketPrices.runnerPrices[i].bestPricesToLay[k].price - 1))
                            {
                                continue;
                            }
                            kfs.lay.Add(resp.marketPrices.runnerPrices[i].bestPricesToLay[k].price);
                            kfs.lay_cash.Add(resp.marketPrices.runnerPrices[i].bestPricesToLay[k].amountAvailable);
                        }
                        if (kfs.lay.Count == 0)
                        {
                            kfs.lay.Add(resp.marketPrices.runnerPrices[i].bestPricesToLay[0].price);
                            kfs.lay_cash.Add(resp.marketPrices.runnerPrices[i].bestPricesToLay[0].amountAvailable);
                        }

                        market.coefficients[market.team_ids[j]] = kfs;

                        break;
                    }
                }
            }

            return true;
        }
         */

        public bool lessBet(Bet bet, double new_cash)
        {
            checkLoginStatus();
            
            RoboWorker3.bf.exchange.UpdateBetsReq ureq = new RoboWorker3.bf.exchange.UpdateBetsReq();
            ureq.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            ureq.header.sessionToken = session_token;
            ureq.bets = new RoboWorker3.bf.exchange.UpdateBets[1];

            RoboWorker3.bf.exchange.UpdateBets u_bet = new RoboWorker3.bf.exchange.UpdateBets();
            u_bet.betId = bet.id;
            u_bet.oldPrice = bet.bet_kf;
            u_bet.oldSize = Math.Round(bet.bet_cash * 100) / 100;
            u_bet.newPrice = bet.bet_kf;
            new_cash = Math.Round(new_cash * 100) / 100;
            u_bet.newSize = new_cash;
            ureq.bets[0] = u_bet;

            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Less bet " + bet.id + " kf " + bet.bet_kf + " (" + bet.bet_cash + ") -> (" + new_cash + ")");
            Console.WriteLine("Less bet " + bet.id + " kf " + bet.bet_kf + " (" + bet.bet_cash + ") -> (" + new_cash + ")");
            
            if (u_bet.oldSize == u_bet.newSize)
            {
                return true;
            }

            RoboWorker3.bf.exchange.UpdateBetsResp uresp = new RoboWorker3.bf.exchange.BFExchangeService().updateBets(ureq);
            // Display success or error message depending on result
            if (uresp.errorCode != RoboWorker3.bf.exchange.UpdateBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to update bet. Error codes: Sys {0}, Svc {1}", uresp.header.errorCode, uresp.errorCode));
                Console.WriteLine("Failed to update bet. Error codes: Sys {0}, Svc {1}", uresp.header.errorCode, uresp.errorCode);
                return false;
            }
            if (!uresp.betResults[0].success)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to update bet. Error codes: Sys {0}", uresp.betResults[0].resultCode));
                Console.WriteLine("Failed to update bet. Error codes: Sys {0}", uresp.betResults[0].resultCode);
                return false;
            }

            return true;
        }

        public List<MarketInfo> getMarketsWithBets()
        {
            checkLoginStatus();
            
            List<MarketInfo> result = new List<MarketInfo>();

            RoboWorker3.bf.exchange.GetCurrentBetsReq req = new RoboWorker3.bf.exchange.GetCurrentBetsReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            req.betStatus = RoboWorker3.bf.exchange.BetStatusEnum.M;
            req.detailed = true;
            req.orderBy = RoboWorker3.bf.exchange.BetsOrderByEnum.NONE;
            req.recordCount = 5000;
            req.startRecord = 0;
            req.noTotalRecordCount = false;

            RoboWorker3.bf.exchange.GetCurrentBetsResp resp = new RoboWorker3.bf.exchange.BFExchangeService().getCurrentBets(req);
            if (resp.errorCode == (RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            if (resp.errorCode == RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "get matched markets ok");
                Console.WriteLine("get matched markets ok");

                for (int i = 0; i < resp.bets.Length; i++)
                {
                    RoboWorker3.bf.exchange.Bet r_bet = resp.bets[i];
                    bool founded = false;
                    foreach (MarketInfo market in result)
                    {
                        if (market.market_id == r_bet.marketId)
                        {
                            founded = true;
                            break;
                        }
                    }
                    if (!founded)
                    {
                        MarketInfo market = new MarketInfo();
                        market.market_id = r_bet.marketId;
                        result.Add(market);
                    }
                }
            }
            else
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to get matched markets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to get matched markets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
            }

            req = new RoboWorker3.bf.exchange.GetCurrentBetsReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            req.betStatus = RoboWorker3.bf.exchange.BetStatusEnum.U;
            req.detailed = true;
            req.orderBy = RoboWorker3.bf.exchange.BetsOrderByEnum.NONE;
            req.recordCount = 50;
            req.startRecord = 0;
            req.noTotalRecordCount = false;

            resp = new RoboWorker3.bf.exchange.BFExchangeService().getCurrentBets(req);
            if (resp.errorCode == (RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            if (resp.errorCode == RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "get markets ok");
                Console.WriteLine("get markets ok");

                for (int i = 0; i < resp.bets.Length; i++)
                {
                    RoboWorker3.bf.exchange.Bet r_bet = resp.bets[i];
                    bool founded = false;
                    foreach (MarketInfo market in result)
                    {
                        if (market.market_id == r_bet.marketId)
                        {
                            founded = true;
                            break;
                        }
                    }
                    if (!founded)
                    {
                        MarketInfo market = new MarketInfo();
                        market.market_id = r_bet.marketId;
                        result.Add(market);
                    }
                }
            }
            else
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to get markets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to get markets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
            }

            return result;
        }

        public bool getMarket(ref MarketInfo market)
        {
            checkLoginStatus();

            try
            {
                RoboWorker3.bf.exchange.GetMarketReq req = new RoboWorker3.bf.exchange.GetMarketReq();
                req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
                req.header.sessionToken = session_token;
                req.marketId = market.market_id;
                RoboWorker3.bf.exchange.GetMarketResp resp = new RoboWorker3.bf.exchange.BFExchangeService().getMarket(req);

                if (resp.errorCode == (RoboWorker3.bf.exchange.GetMarketErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
                {
                    login();
                }
                if (resp.errorCode != RoboWorker3.bf.exchange.GetMarketErrorEnum.OK)
                {
                    File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to get market. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                    Console.WriteLine("Failed to get market. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                    return false;
                }

                market.close_time = resp.market.marketSuspendTime.ToLocalTime();
                if (resp.market.numberOfWinners == 1 && resp.market.runners.Length == 2 && resp.market.marketType == RoboWorker3.bf.exchange.MarketTypeEnum.O && resp.market.marketStatus == RoboWorker3.bf.exchange.MarketStatusEnum.ACTIVE && !resp.market.runnersMayBeAdded)
                {
                    if (market.close_time > DateTime.Now.AddMinutes(60) && market.close_time < DateTime.Now.AddDays(1))
                    {
                        market.is_processing = true;
                        market.need_be_removed = false;
                    }
                    else
                    {
                        market.is_processing = false;
                        market.need_be_removed = true;
                    }
                    market.team_ids.Clear();
                    for (int i = 0; i < resp.market.runners.Length; i++)
                    {
                        market.team_ids.Add(resp.market.runners[i].selectionId);
                    }
                    
                    getMarketTradedVolume(ref market);
                }
                else
                {
                    market.need_be_removed = true;
                }
            }
            catch (System.Exception ex)
            {
                return false;
            }

            //            resp.market.marketSuspendTime = resp.market.marketSuspendTime.AddHours(4);
            //            Console.WriteLine("Name: " + resp.market.name + " Suspend time: " + resp.market.marketSuspendTime + " Number of w's: " + resp.market.numberOfWinners + " count: " + resp.market.runners.Length + " Descr: " + resp.market.marketDescription);
            return true;
        }

        public bool getProfitLoss(ref MarketInfo market)
        {
            checkLoginStatus();            

            RoboWorker3.bf.exchange.GetMarketProfitAndLossReq req = new RoboWorker3.bf.exchange.GetMarketProfitAndLossReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            req.includeSettledBets = true;
            req.netOfCommission = true;
            req.marketID = market.market_id;

            RoboWorker3.bf.exchange.GetMarketProfitAndLossResp resp = new RoboWorker3.bf.exchange.BFExchangeService().getMarketProfitAndLoss(req);
            if (resp.errorCode == (RoboWorker3.bf.exchange.GetMarketProfitAndLossErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            // Display success or error message depending on result
            if (resp.errorCode != RoboWorker3.bf.exchange.GetMarketProfitAndLossErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to get PL. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to get PL. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                return false;
            }

            for (int i = 0; i < resp.annotations.Length; i++)
            {
                market.profits[resp.annotations[i].selectionId] = resp.annotations[i].ifWin;
            }

            return true;
        }

        public long placeBet(int market_id, double kf, double cash, int team_id, bool back_lay)
        {
            checkLoginStatus();
            
            if (cash < 4.0)
            {
                return placeBetLess4(market_id, kf, cash, team_id, back_lay);
            }

            RoboWorker3.bf.exchange.PlaceBetsReq req = new RoboWorker3.bf.exchange.PlaceBetsReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            req.bets = new RoboWorker3.bf.exchange.PlaceBets[1];

            RoboWorker3.bf.exchange.PlaceBets r_bet = new RoboWorker3.bf.exchange.PlaceBets();
            r_bet.asianLineId = 0;
            if (back_lay)
            {
                r_bet.betType = RoboWorker3.bf.exchange.BetTypeEnum.B;
            }
            else
            {
                r_bet.betType = RoboWorker3.bf.exchange.BetTypeEnum.L;
            }
            r_bet.price = kf;
            r_bet.marketId = market_id;
            r_bet.selectionId = team_id;
            r_bet.size = Math.Round(cash * 100.0) / 100.0;

            req.bets[0] = r_bet;
            transactions++;

            RoboWorker3.bf.exchange.PlaceBetsResp resp = new RoboWorker3.bf.exchange.BFExchangeService().placeBets(req);
            if (resp.errorCode == (RoboWorker3.bf.exchange.PlaceBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            // Display success or error message depending on result
            if (resp.errorCode != RoboWorker3.bf.exchange.PlaceBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to place bet. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to place bet. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                return 0;
            }
            if (!resp.betResults[0].success)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to place bet. Error codes: Sys {0}", resp.betResults[0].resultCode));
                Console.WriteLine(string.Format("Failed to place bet. Error codes: Sys {0}", resp.betResults[0].resultCode));
                return 0;
            }

            return resp.betResults[0].betId;
        }

        public long placeBetLess4(int market_id, double kf, double cash, int team_id, bool back_lay)
        {
            checkLoginStatus();

            RoboWorker3.bf.exchange.PlaceBetsReq req = new RoboWorker3.bf.exchange.PlaceBetsReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            req.bets = new RoboWorker3.bf.exchange.PlaceBets[1];

            RoboWorker3.bf.exchange.PlaceBets r_bet = new RoboWorker3.bf.exchange.PlaceBets();
            r_bet.asianLineId = 0;
            double old_price = 0;
            if (back_lay)
            {
                r_bet.betType = RoboWorker3.bf.exchange.BetTypeEnum.B;
                old_price = 1000;
                r_bet.price = 1000;
            }
            else
            {
                r_bet.betType = RoboWorker3.bf.exchange.BetTypeEnum.L;
                old_price = 1.01;
                r_bet.price = 1.01;
            }
            r_bet.marketId = market_id;
            r_bet.selectionId = team_id;
            r_bet.size = 4;

            req.bets[0] = r_bet;
            transactions++;

            RoboWorker3.bf.exchange.PlaceBetsResp resp = new RoboWorker3.bf.exchange.BFExchangeService().placeBets(req);
            if (resp.errorCode == (RoboWorker3.bf.exchange.PlaceBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            // Display success or error message depending on result
            if (resp.errorCode != RoboWorker3.bf.exchange.PlaceBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to place bet. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to place bet. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                return 0;
            }

            long bet_id = resp.betResults[0].betId;
            RoboWorker3.bf.exchange.UpdateBetsReq ureq = new RoboWorker3.bf.exchange.UpdateBetsReq();
            ureq.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            ureq.header.sessionToken = session_token;
            ureq.bets = new RoboWorker3.bf.exchange.UpdateBets[1];

            RoboWorker3.bf.exchange.UpdateBets u_bet = new RoboWorker3.bf.exchange.UpdateBets();
            u_bet.betId = bet_id;
            u_bet.oldPrice = old_price;
            u_bet.oldSize = 4;
            u_bet.newPrice = old_price;
            cash = Math.Round(cash * 100) / 100;
            u_bet.newSize = Math.Round((4 + cash) * 100.0) / 100.0;
            ureq.bets[0] = u_bet;

            RoboWorker3.bf.exchange.UpdateBetsResp uresp = new RoboWorker3.bf.exchange.BFExchangeService().updateBets(ureq);
            // Display success or error message depending on result
            if (uresp.errorCode != RoboWorker3.bf.exchange.UpdateBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to place bet. Error codes: Sys {0}, Svc {1}", uresp.header.errorCode, uresp.errorCode));
                Console.WriteLine("Failed to place bet. Error codes: Sys {0}, Svc {1}", uresp.header.errorCode, uresp.errorCode);
                return 0;
            }
            if (!uresp.betResults[0].success)
            {
                return 0;
            }

            RoboWorker3.bf.exchange.CancelBetsReq creq = new RoboWorker3.bf.exchange.CancelBetsReq();
            creq.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            creq.header.sessionToken = session_token;
            creq.bets = new RoboWorker3.bf.exchange.CancelBets[1];

            RoboWorker3.bf.exchange.CancelBets c_bet = new RoboWorker3.bf.exchange.CancelBets();
            c_bet.betId = bet_id;
            creq.bets[0] = c_bet;
            transactions++;

            RoboWorker3.bf.exchange.CancelBetsResp cresp = new RoboWorker3.bf.exchange.BFExchangeService().cancelBets(creq);
            if (cresp.errorCode == (RoboWorker3.bf.exchange.CancelBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            if (cresp.errorCode != RoboWorker3.bf.exchange.CancelBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to cancel bets by change bets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to cancel bets by change bets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                return 0;
            }

            RoboWorker3.bf.exchange.UpdateBetsReq ureq2 = new RoboWorker3.bf.exchange.UpdateBetsReq();
            ureq2.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            ureq2.header.sessionToken = session_token;
            ureq2.bets = new RoboWorker3.bf.exchange.UpdateBets[1];

            RoboWorker3.bf.exchange.UpdateBets u_bet2 = new RoboWorker3.bf.exchange.UpdateBets();
            u_bet2.betId = (long)uresp.betResults[0].newBetId;
            u_bet2.oldPrice = old_price;
            u_bet2.oldSize = cash;
            u_bet2.newPrice = kf;
            u_bet2.newSize = cash;
            ureq2.bets[0] = u_bet2;

            RoboWorker3.bf.exchange.UpdateBetsResp uresp2 = new RoboWorker3.bf.exchange.BFExchangeService().updateBets(ureq2);
            // Display success or error message depending on result
            if (uresp2.errorCode == (RoboWorker3.bf.exchange.UpdateBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            if (uresp2.errorCode != RoboWorker3.bf.exchange.UpdateBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to place bet. Error codes: Sys {0}, Svc {1}", uresp2.header.errorCode, uresp2.errorCode));
                Console.WriteLine("Failed to place bet. Error codes: Sys {0}, Svc {1}", uresp2.header.errorCode, uresp2.errorCode);
                return 0;
            }

            return (long)uresp2.betResults[0].newBetId;
        }

        public List<int> getActiveEventTypes()
        {
            checkLoginStatus();
            
            List<int> result = new List<int>();
            RoboWorker3.bf.global.GetEventTypesReq req = new GetEventTypesReq();
            req.header = new RoboWorker3.bf.global.APIRequestHeader();
            req.header.sessionToken = session_token;

            GetEventTypesResp resp = new BFGlobalService().getActiveEventTypes(req);
            if (resp.errorCode == (GetEventsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            if (resp.errorCode == GetEventsErrorEnum.OK)
            {
                Console.WriteLine("getActiveEventTypes OK");
                for (int i = 0; i < resp.eventTypeItems.Length; i++)
                {
//                    if (resp.eventTypeItems[i].id != 2 && resp.eventTypeItems[i].id != 6231 && resp.eventTypeItems[i].id != 2378961)
//                    if (resp.eventTypeItems[i].id == 7522)
                    if (resp.eventTypeItems[i].id != 6231)
                    {
                        result.Add(resp.eventTypeItems[i].id);
                        Console.WriteLine("ID: " + resp.eventTypeItems[i].id + " Name: \"" + resp.eventTypeItems[i].name + "\" Next market id: " + resp.eventTypeItems[i].nextMarketId + " Exchange Id: " + resp.eventTypeItems[i].exchangeId);
                    }
                }
            }
            else
            {
                Console.WriteLine("Failed to getActiveEventTypes. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                return result;
            }

            return result;
        }

        public List<int> getEvents(int parent_id)
        {
            checkLoginStatus();

            List<int> result = new List<int>();
            GetEventsReq req1 = new GetEventsReq();
            req1.header = new RoboWorker3.bf.global.APIRequestHeader();
            req1.header.sessionToken = session_token;
            req1.eventParentId = parent_id;
            GetEventsResp resp1 = new BFGlobalService().getEvents(req1);
            if (resp1.errorCode == (GetEventsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            if (resp1.errorCode == GetEventsErrorEnum.OK)
            {
                //                Console.WriteLine("getActiveEventTypes OK");
                for (int i = 0; i < resp1.eventItems.Length; i++)
                {
                    //                    Console.WriteLine("ID: " + resp1.eventItems[i].eventId + " Type Id: " + resp1.eventItems[i].eventTypeId + " Name: \"" + resp1.eventItems[i].eventName + "\" Level:" + resp1.eventItems[i].menuLevel);
                    result.AddRange(getEvents(resp1.eventItems[i].eventId));
                }
                for (int i = 0; i < resp1.marketItems.Length; i++)
                {
                    result.Add(resp1.marketItems[i].marketId);
                    //                    Console.WriteLine("ID: " + resp1.marketItems[i].marketId + " Type: " + resp1.marketItems[i].marketType.ToString() + " Name: " + resp1.marketItems[i].marketName + " Variant: " + resp1.marketItems[i].venue);
                }
            }
            else
            {
                //                Console.WriteLine("Failed to getActiveEventTypes. Error codes: Sys {0}, Svc {1}", resp1.header.errorCode, resp1.errorCode);
                return new List<int>();
            }

            return result;
        }

        public bool checkBetIsReallyMatched(Bet bet)
        {
            checkLoginStatus();

            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "get matched bets bet.matched = " + bet.matched);

            RoboWorker3.bf.exchange.GetCurrentBetsReq req = new RoboWorker3.bf.exchange.GetCurrentBetsReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            //            req.marketId = 0;
            req.betStatus = RoboWorker3.bf.exchange.BetStatusEnum.M;
            req.detailed = true;
            req.orderBy = RoboWorker3.bf.exchange.BetsOrderByEnum.NONE;
            req.recordCount = 5000;
            req.startRecord = 0;
            req.noTotalRecordCount = false;

            RoboWorker3.bf.exchange.GetCurrentBetsResp resp = new RoboWorker3.bf.exchange.BFExchangeService().getCurrentBets(req);
            if (resp.errorCode == (RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }

            if (resp.errorCode == RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "get matched bets ok");
                Console.WriteLine("get matched bets ok");
            }
            else
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to check is bet matched. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to check is bet matched. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                return false;
            }
            for (int i = 0; i < resp.bets.Length; i++)
            {
                RoboWorker3.bf.exchange.Bet r_bet = resp.bets[i];
                if (r_bet.betId == bet.id)
                {
                    if (bet.matched == 0 || (Math.Abs(bet.matched - r_bet.matchedSize) < 0.02 || Math.Abs(bet.matched + bet.bet_cash - r_bet.matchedSize) < 0.02))
                    {
                        File.AppendAllText("out.txt", "\n" + "bet OK ID: " + bet.id);
                        Console.WriteLine("bet OK ID: " + bet.id);
                        return true;
                    }
                }
            }

            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "bet not found!! ID: " + bet.id);
            Console.WriteLine("bet not found!! ID: " + bet.id);
            return false;
        }

        public bool checkBetsAccepted(ref List<Bet> bets, List<MarketInfo> markets)
        {
            checkLoginStatus();
            
            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Transaction count: " + transactions);
            Console.WriteLine("Transaction count: " + transactions);
            if ((DateTime.Now.Minute % 60) == 0)
            {
                transactions = 0;
            }

            #region Получаем все непринятые ставки для этого идентификатора рынка
            RoboWorker3.bf.exchange.GetCurrentBetsReq req = new RoboWorker3.bf.exchange.GetCurrentBetsReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            //            req.marketId = 0;
            req.betStatus = RoboWorker3.bf.exchange.BetStatusEnum.U;
            req.detailed = true;
            req.orderBy = RoboWorker3.bf.exchange.BetsOrderByEnum.NONE;
            req.recordCount = 50;
            req.startRecord = 0;
            req.noTotalRecordCount = false;

            RoboWorker3.bf.exchange.GetCurrentBetsResp resp = new RoboWorker3.bf.exchange.BFExchangeService().getCurrentBets(req);
            if (resp.errorCode == (RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            if (resp.errorCode == RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "get bets ok");
                Console.WriteLine("get bets ok");
            }
            else
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to get bets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to get bets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                if (resp.errorCode == RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum.NO_RESULTS)
                {
                    return true;
                }
            }
            #endregion

            foreach (MarketInfo market in markets)
            {
                market.unmatched_bets.Clear();
            }

            List<Bet> actualBets = new List<Bet>();
            #region Перебираем все ставки для этого идентификатора рынка и смотрим какие приняты
            for (int i = 0; i < resp.bets.Length; i++)
            {
                RoboWorker3.bf.exchange.Bet r_bet = resp.bets[i];
                Bet betFound = null;
                foreach (Bet bet in bets)
                {
                    if (bet.id == r_bet.betId)
                    {
                        actualBets.Add(bet);
                        betFound = bet;
                        break;
                    }
                }
                if (betFound != null)
                {
                    foreach (MarketInfo market in markets)
                    {
                        if (market.market_id == r_bet.marketId)
                        {
                            Bet bet = new Bet();
                            bet.market_id = r_bet.marketId;
                            bet.bet_kf = r_bet.price;
                            bet.bet_cash = r_bet.remainingSize;
                            bet.back_lay = (r_bet.betType == BetTypeEnum.B);
                            bet.id = r_bet.betId;
                            bet.team_id = r_bet.selectionId;
                            bet.magicNumber = betFound.magicNumber;
                            market.unmatched_bets.Add(bet);
                        }
                    }
                }
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Bets: " + r_bet.price + "(" + r_bet.remainingSize + ") ID: " + r_bet.betId);
                Console.WriteLine("Bets: " + r_bet.price + "(" + r_bet.remainingSize + ") ID: " + r_bet.betId);
            }
            foreach (Bet bet in bets)
            {
                if (bet.need_set)
                {
                    actualBets.Add(bet);
                }
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "->My bets: ID: " + bet.id + " bet " + bet.bet_kf + "(" + bet.bet_cash + ") nc: " + bet.need_canceled + " nrfl: " + bet.need_removed_from_list + " ns: " + bet.need_set);
                Console.WriteLine("->My bets: ID: " + bet.id + " bet " + bet.bet_kf + "(" + bet.bet_cash + ") nc: " + bet.need_canceled + " nrfl: " + bet.need_removed_from_list + " ns: " + bet.need_set);
            }
            bets = actualBets;
            #endregion

            return true;
        }

        public bool cancelAllBetsFromMarket(int market_id)
        {
            checkLoginStatus();

            Console.WriteLine("cancelAllBetsFromMarket: " + market_id);
            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "cancelAllBetsFromMarket: " + market_id);

            RoboWorker3.bf.exchange.GetCurrentBetsReq req = new RoboWorker3.bf.exchange.GetCurrentBetsReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;
            req.marketId = market_id;
            req.betStatus = RoboWorker3.bf.exchange.BetStatusEnum.U;
            req.detailed = true;
            req.orderBy = RoboWorker3.bf.exchange.BetsOrderByEnum.NONE;
            req.recordCount = 50;
            req.startRecord = 0;
            req.noTotalRecordCount = false;

            RoboWorker3.bf.exchange.GetCurrentBetsResp resp = new RoboWorker3.bf.exchange.BFExchangeService().getCurrentBets(req);
            if (resp.errorCode == (RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
            {
                login();
            }
            if (resp.errorCode == RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "get bets by market_id ok");
                Console.WriteLine("get bets by market_id ok");
            }
            else
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to get bets by market_id. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                Console.WriteLine("Failed to get bets by market_id. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                if (resp.errorCode == RoboWorker3.bf.exchange.GetCurrentBetsErrorEnum.NO_RESULTS)
                {
                    return true;
                }
            }

            RoboWorker3.bf.exchange.CancelBetsReq req2 = new RoboWorker3.bf.exchange.CancelBetsReq();
            req2.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req2.header.sessionToken = session_token;
            req2.bets = new RoboWorker3.bf.exchange.CancelBets[resp.bets.Length];

            for (int i = 0; i < resp.bets.Length; i++)
            {
                RoboWorker3.bf.exchange.CancelBets r_bet = new RoboWorker3.bf.exchange.CancelBets();
                r_bet.betId = resp.bets[i].betId;
                req2.bets[i] = r_bet;

                transactions++;
            }


            RoboWorker3.bf.exchange.CancelBetsResp resp2 = new RoboWorker3.bf.exchange.BFExchangeService().cancelBets(req2);
            if (resp2.errorCode == RoboWorker3.bf.exchange.CancelBetsErrorEnum.OK)
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "cancelBets by market_id ok");
                Console.WriteLine("cancelBets by market_id ok");
            }
            else
            {
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to cancel bets by market_id ok. Error codes: Sys {0}, Svc {1}", resp2.header.errorCode, resp2.errorCode));
                Console.WriteLine("Failed to cancel bets by market_id. Error codes: Sys {0}, Svc {1}", resp2.header.errorCode, resp2.errorCode);
                return false;
            }


            return true;
        }

        public bool cancelBets(ref List<Bet> bets)
        {
            checkLoginStatus();
            
            RoboWorker3.bf.exchange.CancelBetsReq req = new RoboWorker3.bf.exchange.CancelBetsReq();
            req.header = new RoboWorker3.bf.exchange.APIRequestHeader();
            req.header.sessionToken = session_token;

            int count = 0;
            #region Подсчитываем количество ставок, которые надо отменить
            List<Bet> new_bets = new List<Bet>();
            List<int> market_ids = new List<int>();
            foreach (Bet bet in bets)
            {
                bool new_market_id = true;
                foreach (int m_id in market_ids)
                {
                    if (bet.market_id == m_id)
                    {
                        new_market_id = false;
                        break;
                    }
                }
                if (new_market_id)
                {
                    market_ids.Add((int)bet.market_id);
                }
                if (bet.need_set && bet.need_canceled)
                {
                    bet.need_removed_from_list = true;
                }
                if (!bet.need_removed_from_list)
                {
                    new_bets.Add(bet);
                }
                if (bet.need_canceled && !bet.need_removed_from_list)
                {
                    count++;
                    File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Canceling bets " + bet.bet_kf.ToString() + "(" + bet.bet_cash + ") ID: " + bet.id);
                    Console.WriteLine("Canceling bets " + bet.bet_kf.ToString() + "(" + bet.bet_cash + ") ID: " + bet.id);
                }
            }
            bets.Clear();
            bets.AddRange(new_bets);
            #endregion
            if (count == 0)
            {
                return true;
            }

            bool result = true;
            foreach (int market_id in market_ids)
            {
                count = 0;
                foreach (Bet bet in bets)
                {
                    if (bet.market_id == market_id && bet.need_canceled)
                    {
                        count++;
                    }
                }
                if (count == 0)
                {
                    continue;
                }
                req.bets = new RoboWorker3.bf.exchange.CancelBets[count];
                #region Формируем структуру для отмены этих ставок
                int i = 0;
                foreach (Bet bet in bets)
                {
                    if (bet.market_id == market_id && bet.need_canceled)
                    {
                        RoboWorker3.bf.exchange.CancelBets r_bet = new RoboWorker3.bf.exchange.CancelBets();
                        r_bet.betId = bet.id;
                        req.bets[i++] = r_bet;

                        transactions++;
                    }
                }

                #endregion
                //            bets = new_bets;


                RoboWorker3.bf.exchange.CancelBetsResp resp = new RoboWorker3.bf.exchange.BFExchangeService().cancelBets(req);
                if (resp.errorCode == (RoboWorker3.bf.exchange.CancelBetsErrorEnum)RoboWorker3.bf.exchange.APIErrorEnum.NO_SESSION)
                {
                    login();
                }
                if (resp.errorCode == RoboWorker3.bf.exchange.CancelBetsErrorEnum.OK)
                {
                    File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "cancelBets");
                    Console.WriteLine("cancelBets");
                }
                else
                {
                    File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + string.Format("Failed to cancel bets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode));
                    Console.WriteLine("Failed to cancel bets. Error codes: Sys {0}, Svc {1}", resp.header.errorCode, resp.errorCode);
                    for (i = 0; i < req.bets.Length; i++)
                    {
                        Bet bet = new Bet();
                        foreach (Bet abet in bets)
                        {
                            if (abet.id == req.bets[i].betId)
                            {
                                bet = abet;
                                break;
                            }
                        }
                        if (checkBetIsReallyMatched(bet))
                        {
                            bet.need_canceled = false;
                            bet.setted = true;
                            bet.need_removed_from_list = false;
                        }
                    }
                    result = false;
                }
            }
            new_bets = new List<Bet>();
            foreach (Bet bet in bets)
            {
                if (!bet.need_canceled && bet.bet_cash > 0)
                {
                    new_bets.Add(bet);
                }
            }
            bets.Clear();
            bets.AddRange(new_bets);
            return result;
        }

        public int placeBets(ref List<Bet> bets, List<MarketInfo> markets)
        {
            checkLoginStatus();
            int count = 0;
            for (int j = 0; j < bets.Count; j++)
            {
                Bet bet = bets[j];
                if (bet.need_removed_from_list || bet.need_canceled)
                {
                    continue;
                }
                if (!bet.need_set || !bet.need_wait_set)
                {
                    continue;
                }

                bet.id = placeBet((int)bet.market_id, bet.bet_kf, bet.bet_cash, bet.team_id, bet.back_lay);
                bet.need_set = false;

                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Placing bets for sell " + bet.bet_kf.ToString() + "(" + bet.bet_cash + ") ID: " + bet.id);
                Console.WriteLine("Placing bets for sell " + bet.bet_kf.ToString() + "(" + bet.bet_cash + ") ID: " + bet.id);
                count++;
            }
            
            return count;
        }

        public bool getAllEvents(ref List<MarketInfo> markets)
        {
            //GetAllMarkets(DateTime.Now.AddMinutes(60), new int[] { 1 }, DateTime.Now.AddMinutes(60), DateTime.Now.AddDays(1), markets, 24, "over/under 2.5 goals");
            GetAllMarkets(DateTime.Now.AddMinutes(60), new int[] { 1 }, DateTime.Now.AddMinutes(60), DateTime.Now.AddDays(1), markets, 24, "match odds");
            //GetAllMarkets(DateTime.Now.AddMinutes(60), new int[] { 7522 }, DateTime.Now.AddMinutes(60), DateTime.Now.AddDays(1), markets, 24, "match odds");
            return true;
        }
    }

    public class CloseBet
    {
        public double cash;
        public double kf;
        public int team_id;
    }

    public class Bet
    {
        public Bet()
        {
            id = 0;
            market_id = 0;
            team_id = 0;
            back_lay = true;
            bet_cash = 0;
            bet_kf = 0;
            close_bets = new List<CloseBet>();
            accepted_cash = 0;
            income = 0;
            matched = 0;
            need_set = false;
            need_wait_set = false;
            setted = false;
            need_canceled = false;
            is_first = 0;
            need_removed_from_list = false;
            magicNumber = 0;
        }
        public long id;
        public long market_id;
        public int team_id;
        public bool back_lay;
        public double bet_cash;
        public double accepted_cash;
        public double bet_kf;
        public List<CloseBet> close_bets;
        public double income;
        public double matched;
        public int is_first;

        public bool need_set;
        public bool need_wait_set;
        public bool setted;
        public bool need_canceled;
        public bool need_removed_from_list;
        public int magicNumber;
    }

    public class Market
    {
        public Market() 
        {
            bets = new List<Bet>();
            bf = new BetfairExchange();
        }

        public static double fork_percent = 0.001;
        public static double MAX_BET = 4.0;
        public static string login = "";
        public static string password = "";
        public static double close_bet_difference = 1.0;
        public static int max_markets_count = 10;
        public static int sleep_time = 10;
        public static double stoploss = 0;
        public static double min_cf = 0;
        public static double max_cf = 0;
        public static double min_matched = 0;
        public static double close_time = 0;
        public static int maxUnmatchedCount = 2;
        
        public List<Bet> bets;
        BetfairExchange bf;

        public bool closeMarket(MarketInfo market)
        {
            Console.WriteLine("Close market: " + market.market_id);
            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Close market: " + market.market_id);
            bool isEqualed = false;
            
            bool need_equal = true;
            while (need_equal)
            {
                bf.cancelAllBetsFromMarket(market.market_id);
                bf.getPrices(ref market, 0.0);
                bf.getProfitLoss(ref market);
                need_equal = false;
                for (int i = 0; i < market.profits.Count; i++)
                {
                    for (int j = i; j < market.profits.Count; j++)
                    {
                        if (Math.Abs(market.profits[market.team_ids[i]] - market.profits[market.team_ids[j]]) > 0.1)
                        {
                            need_equal = true;
                            break;
                        }
                    }
                    if (need_equal)
                    {
                        break;
                    }
                }
                if (!need_equal)
                {
                    break;
                }
                isEqualed = true;
                double max_profit = -10000;
                int best_team_to_equal = -1;
                for (int k = 0; k < market.profits.Count; k++)
                {
                    double current_profit = market.profits[market.team_ids[k]];
                    for (int i = 0; i < market.profits.Count; i++)
                    {
                        if (i == k)
                        {
                            continue;
                        }
                        if (Math.Abs(market.profits[market.team_ids[k]] - market.profits[market.team_ids[i]]) > 0.2)
                        {
                            if (market.profits[market.team_ids[i]] > market.profits[market.team_ids[k]])
                            {
                                if (market.coefficients[market.team_ids[i]].lay[0] == 1000 && market.coefficients[market.team_ids[i]].lay_cash[0] == 0)
                                {
                                    current_profit = -1000;
                                    break;
                                }
                                double cash = (market.profits[market.team_ids[i]] - market.profits[market.team_ids[k]]) / (market.coefficients[market.team_ids[i]].lay[0]);
                                current_profit += cash;
                            }
                            else
                            {
                                if (market.coefficients[market.team_ids[i]].back[0] == 1.01 && market.coefficients[market.team_ids[i]].back_cash[0] == 0)
                                {
                                    current_profit = -1000;
                                    break;
                                }
                                double cash = (market.profits[market.team_ids[k]] - market.profits[market.team_ids[i]]) / (market.coefficients[market.team_ids[i]].back[0]);
                                current_profit -= cash;
                            }
                        }
                    }
                    if (current_profit > max_profit)
                    {
                        best_team_to_equal = k;
                        max_profit = current_profit;
                    }
                }
                bool bet_placed = false;
                if (best_team_to_equal != -1)
                {
                    for (int i = 0; i < market.profits.Count; i++)
                    {
                        if (i == best_team_to_equal)
                        {
                            continue;
                        }
                        if (Math.Abs(market.profits[market.team_ids[best_team_to_equal]] - market.profits[market.team_ids[i]]) > 0.2)
                        {
                            bet_placed = true;
                            if (market.profits[market.team_ids[i]] > market.profits[market.team_ids[best_team_to_equal]])
                            {
                                double cash = (market.profits[market.team_ids[i]] - market.profits[market.team_ids[best_team_to_equal]]) / (market.coefficients[market.team_ids[i]].lay[0]);
                                cash = Math.Round(cash * 100) / 100;
                                bf.placeBet(market.market_id, market.coefficients[market.team_ids[i]].lay[0], cash, market.team_ids[i], false);
                            }
                            else
                            {
                                double cash = (market.profits[market.team_ids[best_team_to_equal]] - market.profits[market.team_ids[i]]) / (market.coefficients[market.team_ids[i]].back[0]);
                                cash = Math.Round(cash * 100) / 100;
                                bf.placeBet(market.market_id, market.coefficients[market.team_ids[i]].back[0], cash, market.team_ids[i], true);
                            }
                        }
                    }
                    if (!bet_placed)
                    {
                        break;
                    }
                    File.AppendAllText("market_stats.txt", "\nClose market " + market.market_id + " income: " + max_profit);
                }

            }

            if (isEqualed)
            {
                if (!Protection.checkAccess())
                {
                    //Console.WriteLine(Program.licenseText);
                    //Console.ReadLine();
                    //Environment.Exit(0);
                }
            }
            
            return true;
        }

        public double getNextVal(double value)
        {
            value = Math.Round(value * 100.0) / 100.0;
            if (value < 1.0)
            {
                value = 1.0;
            }
            if (value < 2.0)
            {
                return Math.Round((value + 0.01) * 100.0) / 100.0;
            }
            if (value < 3.0)
            {
                return Math.Round((value + 0.02) * 100.0) / 100.0;
            }
            if (value < 4.0)
            {
                return Math.Round((value + 0.05) * 100.0) / 100.0;
            }
            if (value < 6.0)
            {
                return Math.Round((value + 0.1) * 100.0) / 100.0;
            }
            if (value < 10.0)
            {
                return Math.Round((value + 0.2) * 100.0) / 100.0;
            }
            if (value < 20.0)
            {
                return Math.Round((value + 0.5) * 100.0) / 100.0;
            }
            if (value < 30.0)
            {
                return Math.Round((value + 1.0) * 100.0) / 100.0;
            }
            if (value < 50.0)
            {
                return Math.Round((value + 2.0) * 100.0) / 100.0;
            }
            if (value < 100.0)
            {
                return Math.Round((value + 5.0) * 100.0) / 100.0;
            }
            return Math.Round((value + 10.0) * 100.0) / 100.0;
        }

        public double getPrevVal(double value)
        {
            value = Math.Round(value * 100.0) / 100.0;
            if (value <= 1)
            {
                value = 1.02;
            }
            if (value <= 2.0)
            {
                return Math.Round((value - 0.01) * 100.0) / 100.0;
            }
            if (value <= 3.0)
            {
                return Math.Round((value - 0.02) * 100.0) / 100.0;
            }
            if (value <= 4.0)
            {
                return Math.Round((value - 0.05) * 100.0) / 100.0;
            }
            if (value <= 6.0)
            {
                return Math.Round((value - 0.1) * 100.0) / 100.0;
            }
            if (value <= 10.0)
            {
                return Math.Round((value - 0.2) * 100.0) / 100.0;
            }
            if (value <= 20.0)
            {
                return Math.Round((value - 0.5) * 100.0) / 100.0;
            }
            if (value <= 30.0)
            {
                return Math.Round((value - 1.0) * 100.0) / 100.0;
            }
            if (value <= 50.0)
            {
                return Math.Round((value - 2.0) * 100.0) / 100.0;
            }
            if (value <= 100.0)
            {
                return Math.Round((value - 5.0) * 100.0) / 100.0;
            }
            return Math.Round((value - 10.0) * 100.0) / 100.0;
        }

        private Bet addBet(double cf, int marketId, int teamId, bool backLay, bool needSet, int magicNumber) 
        {
            Bet bet = new Bet();
            bet.bet_kf = cf;
            bet.bet_cash = Market.MAX_BET;
            bet.setted = false;
            bet.need_set = true;
            bet.need_wait_set = needSet;
            bet.need_removed_from_list = false;
            bet.market_id = marketId;
            bet.team_id = teamId;
            bet.back_lay = backLay;
            bet.magicNumber = magicNumber;

            foreach (Bet now_bet in bets)
            {
                if (now_bet.back_lay == bet.back_lay &&
                    now_bet.market_id == bet.market_id &&
                    now_bet.team_id == bet.team_id &&
                    now_bet.bet_kf == bet.bet_kf)
                {
                    if (!now_bet.need_wait_set && bet.need_wait_set)
                    {
                        now_bet.need_wait_set = true;
                    }
                    return null;
                }
            }

            return bet;
        }

        public bool getBets(MarketInfo market)
        {
            if (market.close_time != DateTime.MinValue && market.close_time < DateTime.Now.AddMinutes(Market.close_time + 5))
            {
                return true;
            }
            if (market.coefficients.Count == 0 ||
                !market.coefficients.ContainsKey(market.teamId) ||
                market.coefficients[market.teamId].back.Count == 0 ||
                market.coefficients[market.teamId].lay.Count == 0) 
            {
                return true;
            }
            bool isBetNotClosed = false;
            double cfBack = market.coefficients[market.teamId].back[0];
            double cfLay = market.coefficients[market.teamId].lay[0];
            int backCount = 0;
            int layCount = 0;
            foreach (Bet bet in market.unmatched_bets)
            {
                if (bet.back_lay)
                {
                    backCount++;
                }
                else
                {
                    layCount++;
                }
                if (bet.back_lay == false && bet.bet_kf == cfBack ||
                    bet.back_lay == true && bet.bet_kf == cfLay)
                {
                    isBetNotClosed = true;
                    break;
                }

            }
            if (isBetNotClosed)
            {
                return true;
            }
            int magicNumber = (new Random((int)DateTime.Now.Ticks)).Next();
            Bet layBet = addBet(cfBack, market.market_id, market.teamId, false, backCount <= Market.maxUnmatchedCount, magicNumber);
            Bet backBet = addBet(cfLay, market.market_id, market.teamId, true, layCount <= Market.maxUnmatchedCount, magicNumber);

            if (layBet != null && backBet != null)
            {
                bets.Add(backBet);
                bets.Add(layBet);
            }
            SortedDictionary<int, int> unmatchedBetsCountByMagicNumber = new SortedDictionary<int, int>();
            foreach (Bet bet in bets)
            {
                if (unmatchedBetsCountByMagicNumber.ContainsKey(bet.magicNumber))
                {
                    unmatchedBetsCountByMagicNumber[bet.magicNumber]++;
                }
                else
                {
                    unmatchedBetsCountByMagicNumber[bet.magicNumber] = 1;
                }
            }
            foreach (Bet bet in bets)
            {
                if (bet.market_id == market.market_id && unmatchedBetsCountByMagicNumber.ContainsKey(bet.magicNumber))
                {
                    if (unmatchedBetsCountByMagicNumber[bet.magicNumber] == 1 && !bet.need_wait_set)
                    {
                        bet.need_wait_set = true;
                    }
                }
            }
            
            return true;
        }

        private bool getMarketsAll(ref List<MarketInfo> markets)
        {
            int free_api_limit = 5;
            for (int j = 0; j < markets.Count; j++)
            {
                if (!markets[j].is_processing && !markets[j].need_be_removed)
                {
                    if (free_api_limit-- <= 0)
                    {
                        return true;
                    }
                    MarketInfo market = markets[j];
                    bf.getMarket(ref market);
                    if (market.team_ids.Count > 0 && market.team_ids[0] != 0)
                    {
                        bf.getMarketTradedVolume(ref market);
                    }
                    else
                    {
                        market.need_be_removed = true;
                    }
                    markets[j] = market;
                }
            }

            return true;
        }

        private bool getMarkets(ref List<MarketInfo> markets, List<int> ignoreMarketIds)
        {
            List<MarketInfo> new_markets = new List<MarketInfo>();
            for (int j = 0; j < markets.Count; j++)
            {
                markets[j].is_processing = true;
                if (!markets[j].need_be_removed && !ignoreMarketIds.Contains(markets[j].market_id))
                {
                    new_markets.Add(markets[j]);
                }
            }
            markets = new_markets;
            for (int i = 0; i < markets.Count; i++)
            {
                if (markets[i].close_time == DateTime.MinValue)
                {
                    break;
                }
                for (int j = i + 1; j < markets.Count; j++)
                {
                    if (markets[j].close_time == DateTime.MinValue)
                    {
                        break;
                    }
                    if (markets[i].close_time > markets[j].close_time)
                    {
                        MarketInfo temp = markets[i];
                        markets[i] = markets[j];
                        markets[j] = temp;
                    }
                }
            }
            return true;
        }

        private void closeAndFilter(List<MarketInfo> markets, List<int> ignoreMarkets)
        {
            /////////////////////////////////////////////////////////////////////////
            // Закрываем рынки которые начнутся в ближайшие 7 минут
            for (int i = 0; i < markets.Count; i++)
            {
                try
                {
                    MarketInfo market = markets[i];
                    bool isSuitable = market.coefficients.Count == 0;
                    if (market.coefficients.Count > 0)
                    {
                        foreach (int teamId in market.coefficients.Keys)
                        {
                            MarketCoefficients cf = market.coefficients[teamId];
                            if (cf.back.Count > 0 && cf.lay.Count > 0 && cf.back[0] > Market.min_cf && cf.lay[0] < Market.max_cf)
                            {
                                isSuitable = true;
                                market.teamId = teamId;
                            }
                        }
                    }
                    if (!isSuitable && market.teamId != 0)
                    {
                        bf.cancelBets(ref bets);
                        closeMarket(market);
                        ignoreMarkets.Add(market.market_id);
                    }
                    if (market.close_time != DateTime.MinValue && market.close_time < DateTime.Now.AddMinutes(Market.close_time))
                    {
                        bf.cancelBets(ref bets);
                        closeMarket(market);
                    }
                    if ((market.close_time != DateTime.MinValue && market.close_time < DateTime.Now.AddMinutes(Market.close_time)) || 
                        !isSuitable || market.total_matched < Market.min_matched || market.isInplay)
                    {
                        foreach (Bet bet in bets)
                        {
                            if (bet.market_id == market.market_id)
                            {
                                bet.need_canceled = true;
                            }
                        }
                        market.need_be_removed = true;
                        market.is_processing = false;
                        markets[i] = market;
                        continue;
                    }
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
                Thread.Sleep(500);
            }
            /////////////////////////////////////////////////////////////////////////
        }

        public bool processMarket(List<MarketInfo> markets)
        {
            List<int> ignoreMarketIds = new List<int>();
            
            List<MarketInfo> old_markets = bf.getMarketsWithBets();
            bool have_not_processed = true;
            while (have_not_processed)
            {
                have_not_processed = false;                
                getMarketsAll(ref old_markets);
                foreach (MarketInfo market in old_markets)
                {
                    if (market.close_time == DateTime.MinValue)
                    {
                        have_not_processed = true;
                        Thread.Sleep(60 * 1000);
                        break;
                    }
                }
            }
            foreach (MarketInfo market in old_markets)
            {
                closeMarket(market);
                Console.WriteLine("Market " + market.market_id + " closed");
                File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Market " + market.market_id + " closed");
            }
            
            DateTime last_get_pl = DateTime.Now;

            DateTime last_get_events_call = DateTime.Now;
            Console.WriteLine("Start time get events: " + DateTime.Now + " Markets count: " + markets.Count);
            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Start time get events: " + DateTime.Now + " Markets count: " + markets.Count);
            bf.getAllEvents(ref markets);
            Console.WriteLine("End time get events: " + DateTime.Now + " Markets count: " + markets.Count);
            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "End time get events: " + DateTime.Now + " Markets count: " + markets.Count);

            DateTime last_get_markets_call = DateTime.Now;

            //getMarketsAll(ref markets);
            bf.getPrices(markets, 0);
            getMarkets(ref markets, ignoreMarketIds);
            Console.WriteLine("Markets count: " + markets.Count);
            DateTime last_get_markets_additional_info = DateTime.MinValue;
            while (true)
            {
                try
                {
                    closeAndFilter(markets, ignoreMarketIds);

                    getMarkets(ref markets, ignoreMarketIds);
                    /////////////////////////////////////////////////////////////////////////
                    // Получаем инфу о новых рынках и упорядочеваем
                    //if (DateTime.Now > last_get_markets_call.AddSeconds(60) || DateTime.Now < last_get_markets_call) // на случай если суки опять переведут время вперед
                    //{
                    //    getMarketsAll(ref markets);
                        //getMarkets(ref markets);
                        //last_get_markets_call = DateTime.Now;
                    //}
                    /////////////////////////////////////////////////////////////////////////


                    /////////////////////////////////////////////////////////////////////////
                    // Обновляем список событий
                    if (DateTime.Now > last_get_events_call.AddHours(1) || DateTime.Now < last_get_markets_call) // на случай если суки опять переведут время вперед
                    {
                        Console.WriteLine("Start time get events: " + DateTime.Now + " Markets count: " + markets.Count);
                        File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Start time get events: " + DateTime.Now + " Markets count: " + markets.Count);
                        
                        // Отменяем все ставки чтобы не пролететь во время получения рынков (это занимает 3-5 минут)
                        //for (int i = 0; i < bets.Count; i++)
                        //{
                            //bets[i].need_canceled = true;
                        //}
                        //bf.cancelBets(ref bets);
                        //foreach (MarketInfo market in markets)
                        //{
                            //if (market.close_time < DateTime.Now.AddMinutes(25) && market.close_time != DateTime.MinValue)
                            //{
                                //closeMarket(market);
                                //market.unmatched_bets.Clear();
                            //}
                        //}

                        bf.getAllEvents(ref markets);
                        Console.WriteLine("End time get events: " + DateTime.Now + " Markets count: " + markets.Count);
                        File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "End time get events: " + DateTime.Now + " Markets count: " + markets.Count);
                        //getMarketsAll(ref markets);
                        bf.getPrices(markets, 0);
                        closeAndFilter(markets, ignoreMarketIds);
                        getMarkets(ref markets, ignoreMarketIds);
                        File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "End time get markets: " + DateTime.Now + " Markets count: " + markets.Count);

                        last_get_events_call = DateTime.Now;
                    }
                    /////////////////////////////////////////////////////////////////////////

                    bf.getPrices(markets, 15.0);

                    if (DateTime.Now > last_get_pl.AddMinutes(2))
                    {
                        last_get_pl = DateTime.Now;
                        for (int i = 0; i < max_markets_count && i < markets.Count; i++)
                        {
                            MarketInfo m = markets[i];
                            if (m.teamId == 0)
                            {
                                continue;
                            }
                            int betBackCount = 0;
                            int betLayCount = 0;
                            foreach (Bet bet in m.unmatched_bets)
                            {
                                if (bet.back_lay)
                                {
                                    betBackCount++;
                                }
                                else
                                {
                                    betLayCount++;
                                }
                            }
                            if (Math.Max(betBackCount, betLayCount) > (m.coefficients[m.teamId].back[0] < 1.4 ? 5 : 6))
                            {
                                closeMarket(m);
                                ignoreMarketIds.Add(m.market_id);
                                m.is_processing = false;
                                m.need_be_removed = true;
                            }
                            //bf.getProfitLoss(ref m);
                            //double betSize = 0;
                            //if (m.profits[m.teamId] < 0)
                            //{
                                //betSize = -m.profits[m.teamId] / (m.coefficients[m.teamId].back[0] - 1);
                            //}
                            //else
                            //{
                                //betSize = m.profits[m.teamId] * 1.05263158 / (m.coefficients[m.teamId].lay[0] - 1);
                            //}
                            //if (betSize > 0)
                            //{
                                //foreach (int selId in m.profits.Keys)
                                //{
                                    //if (selId != m.teamId)
                                    //{
                                        //double pl = (m.profits[selId] > 0) ? (m.profits[selId] * 1.05263158 - betSize) : (m.profits[selId] + betSize);
                                        //if (pl < 0)
                                        //{
                                            //pl /= m.coefficients[m.teamId].lay[0];
                                        //}
                                        //if (pl < -Market.stoploss)
                                        //{
                                            //File.AppendAllText("stoploss.txt", "m.coefficients[m.teamId].lay[0]=" + m.coefficients[m.teamId].lay[0].ToString() + " m.profits[m.teamId]=" + m.profits[m.teamId].ToString() + " profit=" + pl.ToString() + " betsize = " + betSize.ToString() + " backCf= " + m.coefficients[selId].back[0].ToString() + " m.profits[selId] = " + m.profits[selId]);
                                            //closeMarket(m);
                                            //ignoreMarketIds.Add(m.market_id);
                                            //m.is_processing = false;
                                            //m.need_be_removed = true;
                                        //}
                                        //break;
                                    //}
                                //}
                            //}
                        }
                    }

                    /////////////////////////////////////////////////////////////////////////
                    // Заполняем заявки на обновление коэффициентов в потоках
                    DateTime start_time = DateTime.Now;
                    int count_temp = 0;
                    for (int i = 0; i < markets.Count; i++)
                    {
                        MarketInfo market = markets[i];
                        if (market.close_time == DateTime.MinValue)
                        {
                            break;
                        }

                        if (market.need_be_removed || !market.is_processing)
                        {
                            continue;
                        }
                        if (count_temp >= max_markets_count)
                        {
                            bool is_betted = false;
                            foreach (Bet m_bet in bets)
                            {
                                if (m_bet.market_id == market.market_id)
                                {
                                    is_betted = true;
                                    break;
                                }
                            }
                            if (is_betted)
                            {
                                foreach (Bet bet in bets)
                                {
                                    if (bet.market_id == market.market_id)
                                    {
                                        bet.need_canceled = true;
                                    }
                                }
                                bf.cancelBets(ref bets);
                                closeMarket(market);
                            }
                            markets[i] = market;
                            continue;
                        }
                        count_temp++;
                    }
                    /////////////////////////////////////////////////////////////////////////


                    

                    count_temp = 0;
                    List<long> market_ids = new List<long>();
                    foreach (Bet bet in bets)
                    {
                        if (!market_ids.Contains(bet.market_id))
                        {
                            market_ids.Add(bet.market_id);
                        }
                    }
                    for (int i = 0; i < max_markets_count && i < markets.Count; i++)
                    {
                        MarketInfo market = markets[i];
                        if (market_ids.Contains(market.market_id))
                        {
                            market_ids.Remove(market.market_id);
                        }
                        if (market.need_be_removed || !market.is_processing)
                        {
                            continue;
                        }
                        int market_id = market.market_id;
                        bool is_continue = false;
                        if (market.total_matched < Market.min_matched)
                        {
                            market.need_be_removed = true;
                            markets[i] = market;
                            is_continue = true;
                            continue;
                        }
                        for (int k = 0; k < market.team_ids.Count; k++)
                        {
                            if (false)//(market.matcheds[market.team_ids[k]] < 1000)
                            {
                                market.need_be_removed = true;
                                markets[i] = market;
                                is_continue = true;
                                break;
                            }
                        }
                        if (market.is_closed || market.is_suspended)
                        {
                            bf.cancelAllBetsFromMarket(market_id);
                            markets[i] = market;
//                            closeMarket(market);
//                            market.need_be_removed = true;
                            continue;
                        }

                        Console.WriteLine("Market: " + market.market_id + " close_time: " + market.close_time + " matched_size: " + market.total_matched);
                        File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Market: " + market.market_id + " close_time: " + market.close_time + " matched_size: " + market.total_matched);

                        if (market.close_time == DateTime.MinValue)
                        {
                            markets[i] = market;
                            break;
                        }
                        getBets(market);
                    }
                    foreach (int market_id in market_ids)
                    {
                        MarketInfo market = new MarketInfo();
                        for (int i = 0; i < markets.Count; i++)
                        {
                            if (markets[i].market_id == market_id)
                            {
                                market = markets[i];
                                break;
                            }
                        }
                        if (market.team_ids.Count > 0)
                        {
                            bf.cancelAllBetsFromMarket(market.market_id);
                            closeMarket(market);
                        }
                    }
                    if (bf.transactions > 950)
                    {
                        try
                        {
                            foreach (Bet bet in bets)
                            {
                                bet.need_canceled = true;
                            }
                            bf.cancelBets(ref bets);
                            foreach (MarketInfo market in markets)
                            {
                                if (market.close_time != DateTime.MinValue)
                                {
                                    closeMarket(market);
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n Transactions too many, close all markets Exception: " + ex.Message);
                        }
                        Thread.Sleep((60 - (DateTime.Now.Minute % 60)) * 60 * 1000);
                        bf.transactions = 0;
                    }
                    bf.checkBetsAccepted(ref bets, markets);
                    foreach (Bet bet in bets)
                    {
                        if (bet.bet_cash == 0 && bet.need_wait_set)
                        {
                            bet.need_removed_from_list = true;
                            bet.need_set = false;
                        }
                    }
                    bf.placeBets(ref bets, markets);
                    foreach (Bet bet in bets)
                    {
                        if (bet.id == 0 && bet.need_wait_set)
                        {
                            bet.need_removed_from_list = true;
                            bet.need_set = false;
                        }
                    }
                    //bf.cancelBets(ref bets);
                    //foreach (MarketInfo market in markets)
                    //{
                    //    if (market.unmatched_bets.Count > 0)
                    //    {
                    //        closeMarket(market);
                            //market.need_be_removed = true;
                            //market.is_processing = false;
                    //    }
                    //}
                    
                    if (bets.Count > 0)
                    {
                        //Thread.Sleep(1200 - (DateTime.Now.Millisecond - start_time));
                        TimeSpan mms = DateTime.Now - start_time;
                        Console.WriteLine("Millisecond interval: " + mms.TotalMilliseconds.ToString());
                        if (mms.TotalMilliseconds < 1200)
                        {
                            Thread.Sleep(1200 - (int)mms.TotalMilliseconds);
                        }
                        File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\n" + "Millisecond interval: " + mms.TotalMilliseconds.ToString());
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
                catch (System.Exception ex)
                {
                    File.AppendAllText("out" + Thread.CurrentThread.ManagedThreadId + ".txt", "\nException: " + ex.Message);
                }
                Thread.Sleep(1000);
            }
        }
    }

    public class MarketCoefficients
    {
        public MarketCoefficients()
        {
            back = new List<double>();
            lay = new List<double>();
            back_cash = new List<double>();
            lay_cash = new List<double>();
        }

        public List<double> back;
        public List<double> lay;
        public List<double> back_cash;
        public List<double> lay_cash;
    }

    public class MarketInfo
    {
        public MarketInfo()
        {
            unmatched_bets = new List<Bet>();
            team_ids = new List<int>();
            coefficients = new SortedDictionary<int, MarketCoefficients>();
            matcheds = new SortedDictionary<int, double>();
            profits = new SortedDictionary<int, double>();
            isInplay = false;
            teamId = 0;
        }
        public MarketInfo(int m_id)
        {
            market_id = m_id;
            team_ids = new List<int>();
            is_closed = false;
            is_suspended = false;

            coefficients = new SortedDictionary<int, MarketCoefficients>();
            matcheds = new SortedDictionary<int, double>();
            profits = new SortedDictionary<int, double>();

            close_time = DateTime.MinValue;
            is_processing = false;
            need_be_removed = false;
            total_matched = 0;

            unmatched_bets = new List<Bet>();

            need_parse_in_thread = false;
            is_parsed_in_thread = false;

            thread_id_which_parse = 0;
            isInplay = false;
            teamId = 0;
        }
        public int market_id;
        public List<int> team_ids;
        public SortedDictionary<int, MarketCoefficients> coefficients;
        public SortedDictionary<int, double> matcheds;
        public SortedDictionary<int, double> profits;
        public bool is_closed;
        public bool is_suspended;
        public bool isInplay;

        public DateTime close_time;
        public bool is_processing;
        public bool need_be_removed;
        public double total_matched;

        public int teamId;

        public List<Bet> unmatched_bets;

        public bool need_parse_in_thread;
        public bool is_parsed_in_thread;
        public int thread_id_which_parse;
    }
    //BlackIce2008
    //betfairalexfdsJ3ssA
    class Program
    {
        public const string licenseText = "Продлите лицензию у разработчика. Пишите на serg.morozov@gmail.com либо в аську 82954868";
        static void Main()
        {
            if (!Protection.checkAccess())
            {
                //Console.WriteLine(licenseText);
                //Console.ReadLine();
                //Environment.Exit(0);
            }
            //Market.password = "serg1983_3";
            //MarketInfo market = new MarketInfo();
            //market.market_id = 20777112;
            //BetfairExchange bf = new BetfairExchange();
            //bf.getPrices(ref market, 0);

            Market.close_bet_difference = 1.0;
            Market.MAX_BET = 10;
            //Market.password = "Serg1983";
            Console.Write("Please enter login: ");
            Market.login = Console.ReadLine();
            //Console.WriteLine(Market.login);
            Console.Write("Please enter password: ");
            Market.password = Console.ReadLine();
            Market mar = new Market();
            //MarketInfo mi = new MarketInfo();
            //Console.Write("Please enter market id: ");
            //mi.market_id = int.Parse(Console.ReadLine());
            Console.Write("Please enter max bet: ");
            Market.MAX_BET = double.Parse(Console.ReadLine());
            Console.Write("Please enter stop loss: ");
            Market.stoploss = double.Parse(Console.ReadLine());
            Console.Write("Please enter min cf (1.1): ");
            Market.min_cf = double.Parse(Console.ReadLine());
            Console.Write("Please enter max cf (1.7): ");
            Market.max_cf = double.Parse(Console.ReadLine());
            Console.Write("Please enter min matched (20000): ");
            Market.min_matched = double.Parse(Console.ReadLine());
            Console.Write("Please enter close time (7): ");
            Market.close_time = double.Parse(Console.ReadLine());
            Console.Write("Please enter max unmatched count (2): ");
            Market.maxUnmatchedCount = int.Parse(Console.ReadLine());
            //Console.Write("Please enter markets count: ");
            //Market.max_markets_count = int.Parse(Console.ReadLine());
            //Console.Write("Please enter fork percent (0.005): ");
            //Market.fork_percent = double.Parse(Console.ReadLine());
            //Market.fork_percent = 0.005;
            //Console.Write("Please enter sleep time: ");
            Market.sleep_time = 30;// int.Parse(Console.ReadLine());
            //BetfairExchange bf = new BetfairExchange();
            //bf.getMarket(ref mi);
            //mar.closeMarket(mi);
            List<MarketInfo> mis = new List<MarketInfo>();
            //MarketInfo m = new MarketInfo();
            //m.market_id = 20907089;
            //mis.Add(m);
            //mis.Add(mi);
            mar.processMarket(mis);
        }
    }
}
