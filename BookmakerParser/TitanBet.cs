using System;
using System.Collections.Generic;
using System.Linq;
using BetsLibrary;
using CefSharp.OffScreen;
using CefSharp;
using HtmlAgilityPack;

namespace BookmakerParser
{
    public class TitanBet : BetsLibrary.BookmakerParser
    {
        private string[] MatchListUrl
        {
            get
            {
                string[] result =
                    {
                        "https://m.titanbet.com/en/inplay/FOOT/Football"            // силка на футбол
                          ,"https://m.titanbet.com/en/inplay/BASK/Basketball"       // силка на баскетбол
                          , "https://m.titanbet.com/en/inplay/TENN/Tennis"          // силка на теніс
                          , "https://m.titanbet.com/en/inplay/VOLL/Volleyball"      // силка на волейбол
                    };
                return result;
            }
        }
        const string switch_mobile = "http://sports.titanbet.com/web_nr?key=do_switch_platform&platform=mobile";
        private const int MaximumMatches = 10;

        private Dictionary<string, ChromiumWebBrowser> browserDict = new Dictionary<string, ChromiumWebBrowser>();
        List<string> activeMatchList = new List<string>();
        private ChromiumWebBrowser[] matchListBrowser;
        private const Bookmaker Maker = Bookmaker.Titanbet;
        string JavaSelectCode = "Java";
        public TitanBet()
        {

        }
        private void LoadMobile()
        {
            matchListBrowser = new ChromiumWebBrowser[MatchListUrl.Length + 1];
            matchListBrowser[0] = new ChromiumWebBrowser(switch_mobile);

        }
        private void LoadMatchListPages()
        {
            for (int i = 1; i < MatchListUrl.Length + 1; i++)
                matchListBrowser[i] = new ChromiumWebBrowser(MatchListUrl[i - 1]);
        }

        public override void Parse()
        {
            if (matchListBrowser == null)
            {
                LoadMobile();
            }

            if (!matchListBrowser[0].IsBrowserInitialized || matchListBrowser[0].IsLoading) return;

            if (matchListBrowser[1] == null)
            {
                LoadMatchListPages();
            }

            for (int i = 0; i < MatchListUrl.Length + 1; i++)
                if (!matchListBrowser[i].IsBrowserInitialized || matchListBrowser[i].IsLoading) return;


            int index = 1;
            activeMatchList = new List<string>();
            while (activeMatchList.Count < MaximumMatches && index < matchListBrowser.Length)
            {
                ParseMatchList(index);
                index++;
            }
            BetList = new List<Bet>();
            DeleteNotActiveMatch();

            foreach (var match in browserDict)
            {
                ParseMatch(match.Value);
            }

        }

        public void DeleteNotActiveMatch()
        {
            var notActiveMatchArray = browserDict.Where(e => !activeMatchList.Contains(e.Key)).Select(e => e.Key).ToArray();

            foreach (var key in notActiveMatchArray)
                browserDict.Remove(key);
        }

        public void ParseMatchList(int index)
        {
            string html = matchListBrowser[index].GetSourceAsync().Result;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            HtmlNodeCollection liveList = doc.DocumentNode.SelectNodes("//div[@class='evs']");

            if (liveList == null) return;


            string MatchList = liveList.First().InnerHtml;


            HtmlDocument MatchListDoc = new HtmlDocument();
            MatchListDoc.LoadHtml(MatchList);

            HtmlNodeCollection matchNodes = MatchListDoc.DocumentNode.SelectNodes("//a[@href]");

            if (matchNodes == null) return;

            foreach (var node in matchNodes)
            {

                string id = String.Empty;

                id = node.Attributes["data-ev_id"].Value;
                activeMatchList.Add(id);


                if (!browserDict.ContainsKey(id))
                {
                    string additional_url = node.Attributes["href"].Value;
                    string url = "https://m.titanbet.com/" + additional_url;
                    Console.WriteLine(url);
                    browserDict.Add(id, new ChromiumWebBrowser(url));
                    System.Threading.Thread.Sleep(5000);
                }
                if (activeMatchList.Count == MaximumMatches) break;
            }

        }

        private void ParseMatch(ChromiumWebBrowser browser)
        {
            if (!browser.IsBrowserInitialized || browser.IsLoading)
                return;
            var task = browser.GetSourceAsync();
            task.Wait(2000);
            if (!task.IsCompleted) return;
            string html = task.Result;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            Sport sport = GetSport(doc);
            if (sport == Sport.NotSupported) return;

            MatchName matchName = GetMatchName(doc);
            if (matchName == null) return;

            string BetUrl = browser.Address;

            HtmlNodeCollection betsNodes = doc.DocumentNode.SelectNodes("//div[@class and @data-mkt_id]");

            Bet result = null;
            if (betsNodes == null) return;
            foreach (var node in betsNodes)
            {
                if (node.Attributes["data-allow_cash_out"] != null) continue;
                string main_type_all = node.InnerHtml;
                HtmlDocument main_doc = new HtmlDocument();
                main_doc.LoadHtml(main_type_all);
                HtmlNode name_of_typenode = main_doc.DocumentNode.SelectSingleNode("//span[@class='mkt-name']");
                if (name_of_typenode == null || main_doc == null) continue;
                string main_type = name_of_typenode.InnerText;

                if (main_type == null) continue;
                Team team = GetTeam(main_type, matchName);

                Time time = GetTime(main_type);
                
                HtmlNodeCollection NodeList = main_doc.DocumentNode.SelectNodes("//div[@class='seln ' or @class='seln seln_sort-D' or @class='seln'] | //li | //span[@class='seln ' or @class='seln seln_sort-D' or @class='seln']");
                if (NodeList == null) continue;
                foreach (var node2 in NodeList)
                {

                    result = null;
                    string node2_string = node2.InnerHtml;
                    HtmlDocument node2_doc = new HtmlDocument();
                    node2_doc.LoadHtml(node2_string);

                    HtmlNodeCollection node2_Collection = node2_doc.DocumentNode.SelectNodes("//span[@class='price dec']");
                    string coef = node2_Collection.First().InnerText;
                    double Probability = Convert.ToDouble(coef.Replace(".", ","));
                    #region main bets
                    if (main_type == "Full Time Result" || main_type == "Match Winner")
                    {
                        string type = node2_doc.DocumentNode.SelectNodes("//span[@class='seln-short-name']").First().InnerText;
                        if (type == "1")
                            result = new ResultBet(ResultBetType.First, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        else
                        if (type == "X")
                            result = new ResultBet(ResultBetType.Draw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        else
                        if (type == "2")
                            result = new ResultBet(ResultBetType.Second, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);

                        if (type == "P1")
                            result = new ResultBet(ResultBetType.First, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        else
                            if (type == "P2")
                            result = new ResultBet(ResultBetType.Second, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    }
                    if (main_type == "Double Chance")
                    {
                        string type = node2_doc.DocumentNode.SelectNodes("//span[@class='seln-name']").First().InnerText;
                        // test this code try-hrd
                        if (type == matchName.FirstTeam + "/Draw")
                            result = new ResultBet(ResultBetType.FirstOrDraw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        else
                        if (type == matchName.FirstTeam + "/" + matchName.SecondTeam)
                            result = new ResultBet(ResultBetType.FirstOrSecond, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        else
                        if (type == matchName.SecondTeam + "/Draw")
                            result = new ResultBet(ResultBetType.SecondOrDraw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);

                    }
                    if (main_type.Contains("Total"))
                    {
                        try
                        {
                            string type = node2_doc.DocumentNode.SelectNodes("//span[@class='seln-name' or @class='seln-short-name']").First().InnerText;
                            string handicap = node2_doc.DocumentNode.SelectNodes("//span[@class='seln-hcap']").First().InnerText;

                            if (type == "Over")
                            {
                                if (handicap.Contains("/"))
                                {
                                    double param = (Convert.ToDouble(handicap.Split(new string[] { " / " }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","))
                                                + Convert.ToDouble(handicap.Split(new string[] { " / " }, StringSplitOptions.RemoveEmptyEntries)[1].Replace(".", ","))) / 2;
                                    result = new TotalBet(TotalBetType.Over, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                                }
                                else
                                {
                                    double param = Convert.ToDouble(handicap.Replace(".", ","));
                                    result = new TotalBet(TotalBetType.Over, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                                }

                            }
                            if (type == "Under")
                            {
                                if (handicap.Contains("/"))
                                {
                                    double param = (Convert.ToDouble(handicap.Split(new string[] { " / " }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","))
                                                + Convert.ToDouble(handicap.Split(new string[] { " / " }, StringSplitOptions.RemoveEmptyEntries)[1].Replace(".", ","))) / 2;
                                    result = new TotalBet(TotalBetType.Under, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                                }
                                else
                                {
                                    double param = Convert.ToDouble(handicap.Replace(".", ","));
                                    result = new TotalBet(TotalBetType.Under, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                                }
                            }
                        }
                        catch { continue; }
                    }
                    if(main_type.Contains("Handicap"))
                    {
                        string type = node2_doc.DocumentNode.SelectNodes("//span[@class='seln-short-name']").First().InnerText;
                        double param = Convert.ToDouble(node2_doc.DocumentNode.SelectNodes("//span[@class='seln-hcap']").First().InnerText.Replace(".", ","));
                        if(type=="1")
                        {
                            result = new HandicapBet(HandicapBetType.F1, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        }
                        if (type=="2")
                        {
                            result = new HandicapBet(HandicapBetType.F2, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        }
                    }
                    if (main_type == "Draw No Bet")
                    {
                        double param = 0;
                        string type = node2_doc.DocumentNode.SelectNodes("//span[@class='seln-short-name']").First().InnerText;
                        if (type == "1")
                        {
                            result = new HandicapBet(HandicapBetType.F1, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        }
                        if (type == "2")
                        {
                            result = new HandicapBet(HandicapBetType.F2, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        }
                    }
                    
                if (result != null)
                {
                    int index = BetList.IndexOf(result);
                    if (index!=-1)
                    {
                        BetList[index].ChangeOdds(result.Odds);
                    }
                    else
                    BetList.Add(result);
                }
                }
                #endregion


            }
                foreach (var output in BetList)
            {
                Console.WriteLine();
                Console.Write("{0} vs {1}   ", output.MatchName.FirstTeam, output.MatchName.SecondTeam);
                Console.Write("{0} ", output);
                Console.Write("coef: {0}", output.Odds);
            }



            System.Threading.Thread.Sleep(500);
        }
        MatchName GetMatchName(HtmlDocument doc)
        {
            try
            {
                string[] matchName;
                HtmlNodeCollection NameNodes = doc.DocumentNode.SelectNodes("//span[@class='current-page']");

                matchName = (NameNodes.First().InnerText).Split(new string[] { " v ", " @ ", " vs. " }, StringSplitOptions.RemoveEmptyEntries);

                return new MatchName(matchName[0], matchName[1]);
            }
            catch { return null; }
        }
        Time GetTime(string main_type)
        {
            TimeType type=TimeType.AllGame;
            int value = 0;
            if (!main_type.Contains("1st") && !main_type.Contains("2nd") && !main_type.Contains("3rd") && !main_type.Contains("4th"))
                return new Time(TimeType.AllGame);
            else
            if (main_type.Contains("1st") || main_type.Contains("First"))
                value = 1;
            else
            if (main_type.Contains("2nd") || main_type.Contains("Second"))
                value = 2;
            else
            if (main_type.Contains("3rd") || main_type.Contains("Third"))
                value = 3;
            else
            if (main_type.Contains("4th") || main_type.Contains("Fourth"))
                value = 4;
            if (main_type.Contains("5th") || main_type.Contains("Fifth"))
                value = 5;
            if (main_type.Contains("Half"))
                type = TimeType.Half;
            if (main_type.Contains("Quarter"))
                type = TimeType.Quarter;
            else
            if (main_type.Contains("Set"))
                type = TimeType.Set;
            return new Time(type, value);
        }
        Team GetTeam(string TotalorHand, MatchName name)
        {
            Team team;
            if (!TotalorHand.Contains(name.FirstTeam) && !TotalorHand.Contains(name.SecondTeam))
                team = Team.All;
            else
            if (TotalorHand.Contains(name.FirstTeam) && !TotalorHand.Contains(name.SecondTeam))
                team = Team.First;
            else
            if (!TotalorHand.Contains(name.FirstTeam) && TotalorHand.Contains(name.SecondTeam))
                team = Team.Second;
            else team = Team.All;
            return team;
        }
        Sport GetSport(HtmlDocument doc)
        {
            try
            {
                string sport = doc.DocumentNode.SelectNodes("//title").First().InnerText;
                sport = sport.Split(new string[] { " - " }, StringSplitOptions.RemoveEmptyEntries)[0];
                switch (sport)
                {
                    case "Football": return Sport.Football;
                    case "Basketball": return Sport.Basketball;
                    case "Tennis": return Sport.Tennis;
                    case "Volleyball": return Sport.Volleyball;
                }

                return Sport.NotSupported;
            }
            catch
            {
                return Sport.NotSupported;
            }
        }

    }

}
