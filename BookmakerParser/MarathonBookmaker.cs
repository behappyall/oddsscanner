using System;
using System.Collections.Generic;
using System.Linq;
using BetsLibrary;
using CefSharp.OffScreen;
using CefSharp;
using HtmlAgilityPack;

namespace BookmakerParser
{
    public class Marathonbet : BetsLibrary.BookmakerParser
    {
        private string[] MatchListUrl
        {
            get
            {
                string[] result =
                    {
                        "https://www.marathonbet.com/en/live/26418"         // силка на футбол
                          ,"https://www.marathonbet.com/en/live/45356"      // силка на баскетбол
                          , "https://www.marathonbet.com/en/live/22723"     // силка на теніс
                          , "https://www.marathonbet.com/en/live/23690"     // силка на волейбол
                    };
                return result;
            }
        }

        // iceHockey не можу знайти силку в лайві його не має і не буде до 2018 ... 
        //
        private const int MaximumMatches = 10;

        private Dictionary<string, ChromiumWebBrowser> browserDict = new Dictionary<string, ChromiumWebBrowser>();
        List<string> activeMatchList = new List<string>();
        private ChromiumWebBrowser[] matchListBrowser;
        private const Bookmaker Maker = Bookmaker.Marathonbet;
        string JavaSelectCode = "Java";
        public Marathonbet()
        {

        }

        private void LoadMatchListPages()
        {
            matchListBrowser = new ChromiumWebBrowser[MatchListUrl.Length];
            for (int i = 0; i < MatchListUrl.Length; i++)
                matchListBrowser[i] = new ChromiumWebBrowser(MatchListUrl[i]);
        }

        public override void Parse()
        {
            if (matchListBrowser == null)
            {
                LoadMatchListPages();
            }
            for (int i = 0; i < MatchListUrl.Length; i++)
                if (!matchListBrowser[i].IsBrowserInitialized || matchListBrowser[i].IsLoading) return;


            int index = 0;
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

            HtmlNodeCollection matchNodes = doc.DocumentNode.SelectNodes("//div[@class='expand-event-btn']");

            if (matchNodes == null) return;
            foreach (var node in matchNodes)
            {

                string id = String.Empty;

                id = node.Attributes["data-expand-event-btn"].Value;
                activeMatchList.Add(id);


                if (!browserDict.ContainsKey(id))
                {
                    string url = "https://www.marathonbet.com/en/live/" + id;
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

            HtmlNodeCollection betsNodes = doc.DocumentNode.SelectNodes("//td");

            Bet result = null;
            if (betsNodes == null) return;
            foreach (var node in betsNodes)
            {
                result = null;
                if (node.Attributes["data-market-type"] != null) continue;
                HtmlAttribute attribute = node.Attributes["data-sel"];
                if (attribute == null) continue;
                string value = attribute.Value.Replace("&quot;", string.Empty);

                string coeff = value.Split(new string[] { ",epr:", ",prices:{" }, StringSplitOptions.RemoveEmptyEntries)[1];

                double Probability = Convert.ToDouble(coeff.Replace(".", ","));
                string type = value.Split(new string[] { "sn:", ",mn:" }, StringSplitOptions.RemoveEmptyEntries)[1];

                string TotalorHand = value.Split(new string[] { "mn:", ",ewc:" }, StringSplitOptions.RemoveEmptyEntries)[1];
                if (TotalorHand.Contains(" + ")) continue;
                if (TotalorHand.Contains("minutes") || TotalorHand.Contains("Minutes")
                    || type.Contains("minutes") || type.Contains("Minutes")) continue;
                Team team = GetTeam(TotalorHand, matchName);
                Time time = GetTime(TotalorHand);
                #region main bets
                if (TotalorHand.Contains("Match Result") || TotalorHand == "Result")
                {
                    if (type == matchName.FirstTeam + " To Win")
                        result = new ResultBet(ResultBetType.First, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    else
                    if (type == "Draw")
                        result = new ResultBet(ResultBetType.Draw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    else
                    if (type == matchName.SecondTeam + " To Win")
                        result = new ResultBet(ResultBetType.Second, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    else
                    if (type == matchName.FirstTeam + " To Win or Draw")
                        result = new ResultBet(ResultBetType.FirstOrDraw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    else
                    if (type == matchName.FirstTeam + " To Win or " + matchName.SecondTeam + " To Win")
                        result = new ResultBet(ResultBetType.FirstOrSecond, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    else
                    if (type == matchName.SecondTeam + " To Win or Draw")
                        result = new ResultBet(ResultBetType.SecondOrDraw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                }
                #endregion
                else
                #region Totals for All Team
                    // Totals
                    if (TotalorHand.Contains("Total") && !TotalorHand.Contains("Sets") && !TotalorHand.Contains("Innings"))
                {
                    if (type.Contains("Under"))
                    {
                        if (TotalorHand.Contains("Asian"))
                        {
                            try
                            {
                                double first_param = Convert.ToDouble(type.Split(new string[] { "Under ", "," }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","));
                                double second_param = Convert.ToDouble(type.Split(new string[] { "Under ", "," }, StringSplitOptions.RemoveEmptyEntries)[1].Replace(".", ","));

                                double param = (first_param + second_param) / 2;
                                result = new TotalBet(TotalBetType.Under, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);

                            }
                            catch { }
                        }
                        else
                        {
                            try
                            {
                                double param = Convert.ToDouble(type.Split(new string[] { "Under ", "\0" }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","));

                                result = new TotalBet(TotalBetType.Under, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);

                            }
                            catch { }
                        }
                    }
                    if (type.Contains("Over"))
                    {
                        if (TotalorHand.Contains("Asian"))
                        {
                            try
                            {
                                double first_param = Convert.ToDouble(type.Split(new string[] { "Over ", "," }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","));
                                double second_param = Convert.ToDouble(type.Split(new string[] { "Over ", "," }, StringSplitOptions.RemoveEmptyEntries)[1].Replace(".", ","));

                                double param = (first_param + second_param) / 2;
                                result = new TotalBet(TotalBetType.Over, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);

                            }
                            catch { }
                        }
                        else
                        {
                            try
                            {
                                double param = Convert.ToDouble(type.Split(new string[] { "Over ", "\0" }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","));

                                result = new TotalBet(TotalBetType.Over, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                            catch { }
                        }
                    }
                }
                #endregion
                else
                #region All Handicaps
                    if (TotalorHand.Contains("Handicap") || TotalorHand.Contains("Draw No Bet")) // переробити. draw no bet = 0 все інше нормально. з ханд робити
                {
                    if (type.Contains(matchName.FirstTeam))
                    {
                        double param = 0;
                        string test;
                        if (TotalorHand.Contains("Draw No Bet")) param = 0;
                        else
                        {
                            if (TotalorHand.Contains("Asian"))
                            {
                                try
                                {
                                    double first_param = Convert.ToDouble(type.Split(new string[] { matchName.FirstTeam + " (", ")", "," }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","));
                                    double second_param = Convert.ToDouble(type.Split(new string[] { matchName.FirstTeam + " (", ")", "," }, StringSplitOptions.RemoveEmptyEntries)[1].Replace(".", ","));

                                    param = (first_param + second_param) / 2;
                                }
                                catch { }

                            }
                            else
                            {
                                try
                                {
                                    test = type.Split(new string[] { matchName.FirstTeam + " (", ")" }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ",");
                                    param = Convert.ToDouble(test);
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                        }

                        result = new HandicapBet(HandicapBetType.F1, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);

                    }
                    if (type.Contains(matchName.SecondTeam))
                    {

                        double param = 0;
                        string test;
                        if (TotalorHand.Contains("Draw No Bet")) param = 0;
                        else
                        {
                            if (TotalorHand.Contains("Asian"))
                            {
                                try
                                {
                                    double first_param = Convert.ToDouble(type.Split(new string[] { matchName.SecondTeam + " (", ")", "," }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","));
                                    double second_param = Convert.ToDouble(type.Split(new string[] { matchName.SecondTeam + " (", ")", "," }, StringSplitOptions.RemoveEmptyEntries)[1].Replace(".", ","));

                                    param = (first_param + second_param) / 2;
                                }
                                catch { }

                            }
                            else
                            {
                                try
                                {
                                    test = type.Split(new string[] { matchName.SecondTeam + " (", ")" }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ",");
                                    param = Convert.ToDouble(test);
                                }
                                catch
                                {
                                    continue;
                                }
                            }
                        }
                        result = new HandicapBet(HandicapBetType.F2, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    }
                }
                #endregion

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
                string matchName = string.Empty;

                foreach (var node in doc.DocumentNode.SelectNodes("//tbody"))
                    if (node.Attributes["data-event-name"] != null) { matchName = node.Attributes["data-event-name"].Value; break; }

                var matchNameSplit = matchName.Split(new string[] { " vs ", " @ " }, StringSplitOptions.RemoveEmptyEntries);
                if (matchNameSplit[0].Contains("("))
                {
                    matchNameSplit[0] = matchNameSplit[0].Split(new string[] { " (" }, StringSplitOptions.RemoveEmptyEntries)[0];
                }
                if (matchNameSplit[1].Contains("("))
                {
                    matchNameSplit[1] = matchNameSplit[1].Split(new string[] { " (" }, StringSplitOptions.RemoveEmptyEntries)[0];
                }
                return new MatchName(matchNameSplit[0], matchNameSplit[1]);
            }
            catch { return null; }
        }
        Time GetTime(string TotalorHand)
        {
            TimeType type=TimeType.AllGame;
            int value=0;
            if (!TotalorHand.Contains("1st") && !TotalorHand.Contains("2nd") && !TotalorHand.Contains("3rd") && !TotalorHand.Contains("4th"))
                return new Time(TimeType.AllGame);
            else
            if (TotalorHand.Contains("1st"))
                value = 1;
            else
            if (TotalorHand.Contains("2nd"))
                value = 2;
            else
            if (TotalorHand.Contains("3rd"))
                value = 3;
            else
            if (TotalorHand.Contains("4th"))
                value = 4;
            if (TotalorHand.Contains("5th"))
                value = 5;
            if (TotalorHand.Contains("Half"))
                type = TimeType.Half;
            if (TotalorHand.Contains("Quarter"))
                type = TimeType.Quarter;
            else
            if (TotalorHand.Contains("Set"))
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
                string sport = doc.DocumentNode.SelectNodes("//a[@class='sport-category-label']").First().InnerText;

                switch (sport)
                {
                    case "Football": return Sport.Football;
                    case "Basketball": return Sport.Basketball;
                    case "Baseball": return Sport.Baseball;
                    case "Tennis": return Sport.Tennis;
                    case "Ice Hockey": return Sport.IceHockey;
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

