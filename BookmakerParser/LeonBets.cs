using System;
using System.Collections.Generic;
using System.Linq;
using BetsLibrary;
using CefSharp.OffScreen;
using CefSharp;
using HtmlAgilityPack;


namespace BookmakerParser
{
    public class LeonBets : BetsLibrary.BookmakerParser
    {
        private string MatchListUrl = "https://mobile.leonbets.net/mobile/#liveEvents";
        
        private const int MaximumMatches = 10;

        private Dictionary<string, ChromiumWebBrowser> browserDict = new Dictionary<string, ChromiumWebBrowser>();
        List<string> activeMatchList = new List<string>();
        private ChromiumWebBrowser matchListBrowser;
        private const Bookmaker Maker = Bookmaker.Leon;
        string JavaSelectCode = "Java";
        string[] type_of_sport = { "Soccer", "Basketball", "Tennis", "Volleyball" };
        public LeonBets()
        {

        }

        private void LoadMatchListPages()
        {
            matchListBrowser = new ChromiumWebBrowser(MatchListUrl);
        }

        public override void Parse()
        {
            if (matchListBrowser == null)
                LoadMatchListPages();

            if (!matchListBrowser.IsBrowserInitialized || matchListBrowser.IsLoading) return;

            int index = 0;
            activeMatchList = new List<string>();
            while (activeMatchList.Count < MaximumMatches && index < type_of_sport.Length)
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
            string html = matchListBrowser.GetSourceAsync().Result;
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);
            HtmlNodeCollection matchList = doc.DocumentNode.SelectNodes("//li[@class='groupedListItem first' or @class='groupedListItem']");


            if (matchList == null) return;
            foreach (var node in matchList)
            {
                string All = node.InnerHtml;
                HtmlDocument All_Doc = new HtmlDocument();
                All_Doc.LoadHtml(All);
                //
                HtmlNodeCollection h3 = All_Doc.DocumentNode.SelectNodes("//h3");
                string l = h3.First().InnerText;
                if (l == type_of_sport[index])
                {
                    HtmlNodeCollection matchNodes = All_Doc.DocumentNode.SelectNodes("//li[@class='groupedListSubItem first' or @class='groupedListSubItem last' or @class='groupedListSubItem' or @class='groupedListSubItem first last']");
                    if (matchNodes == null) return;
                    foreach (var node2 in matchNodes)
                    {

                        string id = String.Empty;

                        id = node2.Attributes["id"].Value;
                        id = id.Remove(0, 2);
                        activeMatchList.Add(id);


                        if (!browserDict.ContainsKey(id))
                        {
                            string url = "https://mobile.leonbets.net/mobile/#eventDetails/:" + id;
                            Console.WriteLine(url);
                            browserDict.Add(id, new ChromiumWebBrowser(url));
                            System.Threading.Thread.Sleep(5000);
                        }
                        if (activeMatchList.Count == MaximumMatches) break;
                    }
                }
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

            MatchName matchName = GetMatchName(doc);
            if (matchName == null) return;
            Sport sport = GetSport(doc);
            if (sport == Sport.NotSupported) return;


            string BetUrl = browser.Address;


            Bet result = null;

            HtmlNodeCollection maindocument = doc.DocumentNode.SelectNodes("//li[@class='groupedListItem']");

            if (maindocument == null) return;

            foreach (var node in maindocument)
            {
                result = null;
                try
                {
                    string all_main = node.InnerHtml;

                    HtmlDocument document = new HtmlDocument();
                    document.LoadHtml(all_main);

                    string maintype = document.DocumentNode.SelectNodes("//h3").First().InnerText;
                    HtmlNodeCollection betsNodes = document.DocumentNode.SelectNodes("//a[@id]");

                    Team team = GetTeam(maintype);
                    Time time = GetTime(maintype);// зробити час
                    foreach (var node2 in betsNodes)
                    {
                        string value = node2.InnerHtml;
                        if (!value.Contains("class=\"oddHolder\"")) continue;
                        HtmlDocument document2 = new HtmlDocument();
                        document2.LoadHtml(value);
                        HtmlNodeCollection test = document2.DocumentNode.SelectNodes("//div");

                        string type = test.First().InnerText;
                        string coeff = test.Last().InnerText;

                        double Probability = Convert.ToDouble(coeff.Replace(".", ","));
                        if (maintype == "1X2")
                        {
                            if (type == "1")
                            {
                                result = new ResultBet(ResultBetType.First, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                            if (type == "x" || type== "X")
                            {
                                result = new ResultBet(ResultBetType.Draw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                            if (type == "2")
                            {
                                result = new ResultBet(ResultBetType.Second, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                        }
                        if(maintype== "Double Chance")
                        {
                            if (type == "1x" || type == "1X")
                            {
                                result = new ResultBet(ResultBetType.FirstOrDraw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                            if (type == "12")
                            {
                                result = new ResultBet(ResultBetType.FirstOrSecond, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                            if (type == "x2" || type == "X2")
                            {
                                result = new ResultBet(ResultBetType.SecondOrDraw, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                        }
                        if (maintype.Contains("Total") || maintype.Contains("total"))
                        {
                            if (type.Contains("Under"))
                            {
                                try
                                {
                                    double param = Convert.ToDouble(type.Split(new string[] { "Under (", ")" }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","));

                                    result = new TotalBet(TotalBetType.Under, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);

                                }
                                catch { }
                            }
                            if (type.Contains("Over"))
                            {
                                double param = Convert.ToDouble(type.Split(new string[] { "Over (", ")" }, StringSplitOptions.RemoveEmptyEntries)[0].Replace(".", ","));

                                result = new TotalBet(TotalBetType.Over, param, time, team, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                        }
                        if (maintype.Contains("Handicap") && maintype.Contains("Asian"))
                        {
                            string first_or_second_team = type.Split(new string[] { " (" }, StringSplitOptions.RemoveEmptyEntries)[0];
                            if(first_or_second_team=="1")
                            {
                                double param = Convert.ToDouble(type.Split(new string[] { "(", ")" }, StringSplitOptions.RemoveEmptyEntries)[1].Replace(".", ","));

                                result = new HandicapBet(HandicapBetType.F1, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                            if (first_or_second_team == "2")
                            {
                                double param = Convert.ToDouble(type.Split(new string[] { "(", ")" }, StringSplitOptions.RemoveEmptyEntries)[1].Replace(".", ","));

                                result = new HandicapBet(HandicapBetType.F2, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                        }
                        else
                        if (maintype.Contains("Handicap") && !maintype.Contains("Asian"))
                        {
                            string first_or_second_team = type.Split(new string[] { " (" }, StringSplitOptions.RemoveEmptyEntries)[0];
                            if (first_or_second_team == "1")
                            {
                                string initial_score= type.Split(new string[] { "(", ")" }, StringSplitOptions.RemoveEmptyEntries)[1];
                                int first_number = Convert.ToInt32(initial_score.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[0]);
                                int second_number = Convert.ToInt32(initial_score.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[1]);
                                double param = 0;
                                if (first_number!=0)
                                {
                                    param = first_number - 0.5;
                                }
                                if (second_number != 0)
                                {
                                    param = (-1) * (second_number) - 0.5;
                                }
                                result = new HandicapBet(HandicapBetType.F1, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                            else
                            if (first_or_second_team == "2")
                            {
                                string initial_score = type.Split(new string[] { "(", ")" }, StringSplitOptions.RemoveEmptyEntries)[1];
                                int first_number = Convert.ToInt32(initial_score.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[0]);
                                int second_number = Convert.ToInt32(initial_score.Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[1]);
                                double param = 0;
                                if (first_number != 0)
                                {
                                    param = (-1)*first_number - 0.5;
                                }
                                if (second_number != 0)
                                {
                                    param = second_number - 0.5;
                                }
                                result = new HandicapBet(HandicapBetType.F2, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                        }
                        else
                        if(maintype== "Draw No Bet")
                        {

                            double param = 0;
                            if (type.Contains("1"))
                            {
                                result = new HandicapBet(HandicapBetType.F1, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                            else
                            if(type.Contains("2"))
                            {
                                result = new HandicapBet(HandicapBetType.F2, param, time, Probability, matchName, BetUrl, JavaSelectCode, sport, Maker);
                            }
                        }

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

                }
                catch (Exception e)
                {
                    Console.Write(e.Message);
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
                string opp1 = doc.DocumentNode.SelectNodes("//td[@class='nameOne type2']").First().InnerText;
                string opp2 = doc.DocumentNode.SelectNodes("//td[@class='nameTwo type2']").First().InnerText;

                opp1 = opp1.Split(new string[] { "<h1>", "</h1>" }, StringSplitOptions.RemoveEmptyEntries)[0];
                opp2 = opp2.Split(new string[] { "<h1>", "</h1>" }, StringSplitOptions.RemoveEmptyEntries)[0];

                return new MatchName(opp1, opp2);
            }
            catch { return null; }
        }

        Sport GetSport(HtmlDocument doc)
        {
            try
            {
                HtmlNodeCollection node = doc.DocumentNode.SelectNodes("//span[@class='preInfo type2']");
                string sport = node.First().InnerText;


                sport = sport.Split(new string[] { " -" }, StringSplitOptions.RemoveEmptyEntries)[0];

                switch (sport)
                {
                    case "Soccer": return Sport.Football;
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
        Team GetTeam(string team)
        {
            if (!team.Contains("Player 1") && !team.Contains("Player 2") && !team.Contains("Hometeam") && !team.Contains("hometeam") && !team.Contains("Awayteam") && !team.Contains("awayteam"))
                return Team.All;
            else
            if ((team.Contains("Player 1") || team.Contains("hometeam") || team.Contains("Hometeam")) && (!team.Contains("Player 2") && !team.Contains("awayteam") && !team.Contains("Awayteam")))
                return Team.First;
            else 
            if ((team.Contains("Player 2") || team.Contains("awayteam") || team.Contains("Awayteam")) && (!team.Contains("Player 1") && !team.Contains("hometeam") && !team.Contains("Hometeam")))
                return Team.Second;
            else
                // return Team.NotSupported;
                return Team.All;
        }
        Time GetTime(string TotalorHand)
        {
            TimeType type=TimeType.AllGame;
            int value = 0;
            if ((!TotalorHand.Contains("Sets") && !TotalorHand.Contains("sets")) && (TotalorHand.Contains("Set") || TotalorHand.Contains("set")))
            {
                type = TimeType.Set;
                if (TotalorHand.Contains("1") || TotalorHand.Contains("first"))
                    value = 1;
                else
               if (TotalorHand.Contains("2") || TotalorHand.Contains("second"))
                    value = 2;
                else
               if (TotalorHand.Contains("3") || TotalorHand.Contains("third"))
                    value = 3;
                else
               if (TotalorHand.Contains("4") || TotalorHand.Contains("fourth"))
                    value = 4;
                if (TotalorHand.Contains("5") || TotalorHand.Contains("fifth"))
                    value = 5;
                return new Time(type, value);
            }
            if (!TotalorHand.Contains("1st") && !TotalorHand.Contains("2nd") && !TotalorHand.Contains("3rd") && !TotalorHand.Contains("4th") && !TotalorHand.Contains("first") && !TotalorHand.Contains("second") && !TotalorHand.Contains("third") && !TotalorHand.Contains("fourth") && !TotalorHand.Contains("fifth"))
                return new Time(TimeType.AllGame);
            else
            if (TotalorHand.Contains("1st") || TotalorHand.Contains("first"))
                value = 1;
            else
            if (TotalorHand.Contains("2nd") || TotalorHand.Contains("second"))
                value = 2;
            else
            if (TotalorHand.Contains("3rd") || TotalorHand.Contains("third"))
                value = 3;
            else
            if (TotalorHand.Contains("4th") || TotalorHand.Contains("fourth"))
                value = 4;
            if (TotalorHand.Contains("5th") || TotalorHand.Contains("fifth"))
                value = 5;
            if (TotalorHand.Contains("Half") || TotalorHand.Contains("half"))
                type = TimeType.Half;
            if (TotalorHand.Contains("Quarter") || TotalorHand.Contains("quarter"))
                type = TimeType.Quarter;
            return new Time(type, value);
        }
    }

}
