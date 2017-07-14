using System;
using System.Collections.Generic;
using System.Linq;
using BetsLibrary;
using CefSharp.OffScreen;
using CefSharp;
using HtmlAgilityPack;
using System.Threading.Tasks;
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
        private const int MaximumMatches = 100;

        private const Bookmaker Maker = Bookmaker.Marathonbet;
        string JavaSelectCode = "Java";
        public Marathonbet()
        {

        }

        public override void Parse()
        {
            int index = 0;
            MatchDict = new Dictionary<MatchName, string>();
            while (MatchDict.Count < MaximumMatches && index < MatchListUrl.Length)
            {
                ParseMatchList(index);
                index++;
            }
        }

        public void ParseMatchList(int index)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(MatchListUrl[index]);

            HtmlNodeCollection matchNodes = doc.DocumentNode.SelectNodes("//tbody[@class and @data-event-treeid and @data-expanded-event-treeid and @data-live='true']");

            if (matchNodes == null) return;
            foreach (var node in matchNodes)
            {
                string id = String.Empty;

                id = node.Attributes["data-event-treeid"].Value;
                MatchName Name = GetMatchName(node);

                string url = "https://www.marathonbet.com/en/live/" + id;
               
                MatchDict.Add(Name, url);
                if (MatchDict.Count == MaximumMatches) break;
            }


        }

        private void ParseMatch(string url)
        {
            HtmlWeb web = new HtmlWeb();
            //  var proxy = ProxyList.GetRandomProxy();
            //  Console.WriteLine(DateTime.Now +"s");
            HtmlDocument doc;

            try
            {
                doc = web.Load(url/*, proxy.ip, proxy.port, proxy.login, proxy.password*/);
            }
            catch { return; }

            ParseMatchPageHtml(doc, url);
        }

        MatchName GetMatchName(HtmlNode node)
        {
            try
            {
                string matchName = string.Empty;

                matchName = node.Attributes["data-event-name"].Value;
                var matchNameSplit = matchName.Split(new string[] { " vs ", " @ ", " - " }, StringSplitOptions.RemoveEmptyEntries);
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

        public override void ParseBets(List<MatchName> matches)
        {

            BetList = new List<Bet>();

            var tasks = new List<Task>();
            int taskCount = 0;

            foreach (var match in matches)
            {
                if (!MatchDict.ContainsKey(match)) continue;
                tasks.Add(Task.Factory.StartNew(() => ParseMatch(MatchDict[match])));
                taskCount++;
                if (taskCount > 10) { Task.WaitAll(tasks.ToArray()); taskCount = 0; }
            }

            Task.WaitAll(tasks.ToArray());

            foreach (var output in BetList)
            {
                Console.WriteLine();
                Console.Write("{0} vs {1}   ", output.MatchName.FirstTeam, output.MatchName.SecondTeam);
                Console.Write("{0} ", output);
                Console.Write("coef: {0}", output.Odds);
            }

            Console.WriteLine("Marathon parsed {0} betsv at {1}", BetList.Count, DateTime.Now);
        }

        public override void ParseMatchPageHtml(string html, string url)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            ParseMatchPageHtml(doc, url);
        }

        public override void ParseMatchPageHtml(HtmlDocument doc, string url)
        {
            Sport sport = GetSport(doc);
            if (sport == Sport.NotSupported) return;

            MatchName matchName = GetFullMatchName(doc); // повне ім'я (only for tennis cuz all event names were  writed like (Coppejans K.)  )
            if (matchName == null) return;
            string BetUrl = url;

            HtmlNodeCollection betsNodes = doc.DocumentNode.SelectNodes("//td");

            Bet result = null;
            if (betsNodes == null) return;
            foreach (var node in betsNodes)
            {
                result = null;
                //if (node.Attributes["data-market-type"] != null) continue;
                HtmlAttribute attribute = node.Attributes["data-sel"];
                if (attribute == null) continue;
                string value = attribute.Value.Replace("\"", string.Empty);

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
                if (TotalorHand.Contains("Match Result") || TotalorHand == "Result" || TotalorHand.Contains("Match Winner Including All OT") || (TotalorHand.Contains("Result") && TotalorHand.Contains("Set")) || TotalorHand.Contains("Normal Time Result") || TotalorHand.Contains("To Win Match") && !TotalorHand.Contains("Handicap"))
                {
                    if (TotalorHand.Contains("Match Winner Including All OT"))
                    {
                        if (type == matchName.FirstTeam)
                        {
                            if (node.Attributes["data-market-type"] != null && node.Attributes["data-market-type"].Value == "RESULT_2WAY")
                                result = new ResultBet(ResultBetType.P1, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        }
                        else
                        if (type == matchName.SecondTeam)
                        {
                            if (node.Attributes["data-market-type"] != null && node.Attributes["data-market-type"].Value == "RESULT_2WAY")
                                result = new ResultBet(ResultBetType.P2, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        }
                    }
                    if (TotalorHand.Contains("To Win Match"))
                    {
                        if (type == matchName.FirstTeam)
                        {
                            if (node.Attributes["data-market-type"] != null && node.Attributes["data-market-type"].Value == "RESULT_2WAY")
                                result = new ResultBet(ResultBetType.P1, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        }
                        else
                        if (type == matchName.SecondTeam)
                        {
                            if (node.Attributes["data-market-type"] != null && node.Attributes["data-market-type"].Value == "RESULT_2WAY")
                                result = new ResultBet(ResultBetType.P2, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        }
                    }
                    if (type == matchName.FirstTeam + " To Win")
                    {
                        if (node.Attributes["data-market-type"] != null && node.Attributes["data-market-type"].Value == "RESULT_2WAY")
                            result = new ResultBet(ResultBetType.P1, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        else
                            result = new ResultBet(ResultBetType.First, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    }
                    else
                    if (type == "Draw")
                        result = new ResultBet(ResultBetType.Draw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    else
                    if (type == matchName.SecondTeam + " To Win")
                    {
                        if (node.Attributes["data-market-type"] != null && node.Attributes["data-market-type"].Value == "RESULT_2WAY")
                            result = new ResultBet(ResultBetType.P2, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                        else
                            result = new ResultBet(ResultBetType.Second, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                    }
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
                    if (index != -1)
                    {
                        BetList[index].ChangeOdds(result.Odds);
                    }
                    else
                        BetList.Add(result);
                }

            }

            System.Threading.Thread.Sleep(50);
        }


        MatchName GetFullMatchName(HtmlDocument doc)
        {
            try
            {
                string matchName = string.Empty;

                HtmlNodeCollection Team = doc.DocumentNode.SelectNodes("//div[@class='live-today-member-name nowrap ']");
                HtmlDocument FirstDoc = new HtmlDocument();
                HtmlDocument SecondDoc = new HtmlDocument();

                FirstDoc.LoadHtml(Team.First().InnerHtml);

                SecondDoc.LoadHtml(Team.Last().InnerHtml);

                string firstTeam = FirstDoc.DocumentNode.SelectNodes("//span").First().InnerText;
                string secondTeam = SecondDoc.DocumentNode.SelectNodes("//span").First().InnerText;

                matchName = firstTeam + " vs " + secondTeam;

                var matchNameSplit = matchName.Split(new string[] { " vs ", " @ ", " - " }, StringSplitOptions.RemoveEmptyEntries);
                if (matchNameSplit[0].Contains("("))
                {
                    matchNameSplit[0] = matchNameSplit[0].Split(new string[] { " (" }, StringSplitOptions.RemoveEmptyEntries)[0];
                }
                if (matchNameSplit[1].Contains("("))
                {
                    matchNameSplit[1] = matchNameSplit[1].Split(new string[] { " (" }, StringSplitOptions.RemoveEmptyEntries)[0];
                }
                if(matchNameSplit[0].Contains("/"))
                {
                    matchNameSplit[0] = matchNameSplit[0].Replace(" ", "");
                }
                if (matchNameSplit[1].Contains("/"))
                {
                    matchNameSplit[1] = matchNameSplit[1].Replace(" ", "");
                }
                return new MatchName(matchNameSplit[0], matchNameSplit[1]);
            }
            catch { return null; }
        }
        Time GetTime(string TotalorHand)
        {
            TimeType type = TimeType.AllGame;
            int value = 0;
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
