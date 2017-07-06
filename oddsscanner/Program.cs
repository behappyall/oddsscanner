using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CefSharp.OffScreen;
using CefSharp;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HtmlAgilityPack;
using OddsAnalyzer;
using BookmakerParser;
using BetsLibrary;
using System.Linq;

namespace oddsscanner
{
    public class Program
    {

        public static void Main(string[] args)
        {
            ArbitrageFinder finder = new ArbitrageFinder();

            OlimpBookmaker olimp = new OlimpBookmaker();
            Marathonbet marathon = new Marathonbet();
            LeonBets leon = new LeonBets();
            TitanBet titan = new TitanBet();
            TenBet ten = new TenBet();
            // finder.AddBookmaker(marathon);
            //  finder.AddBookmaker(leon);
            //   finder.AddBookmaker(olimp);
            finder.AddBookmaker(titan);
            //finder.AddBookmaker(ten);

            var result = finder.GetForks();

         //   Console.WriteLine("here");

            for (int i = 0; i < 10; i++)
            {
               result = finder.GetForks();

               var marathon_matches = marathon.GetMatchList();
               var leon_matches = leon.GetMatchList();
                var olimp_matches = olimp.GetMatchList();
                var titan_matches = titan.GetMatchList();
                var ten_matches = ten.GetMatchList();
                var forks = result.Where(e => e.Profit > 0);
                Thread.Sleep(10000);
            }

                List<Bet> list = new List<Bet>();
            /*
            foreach(var bet in leon.GetBetList())
            {
                if (!list.Contains(bet)) list.Add(bet);
                else
                {
                    Console.WriteLine("error");
                    Console.WriteLine();
                    Console.Write("{0} vs {1}   ", bet.MatchName.FirstTeam, bet.MatchName.SecondTeam);
                    Console.Write("Sport : {0}  ", bet.Sport);
                    try
                    {
                        var T = bet as ResultBet;
                        if (T != null)
                        {
                            Console.Write("{0}", ((ResultBet)bet).ToString());
                            Console.Write("coef: {0}", T.Odds);
                        }
                    }
                    catch (Exception ex) { Console.WriteLine(ex.Message); }
                    try
                    {
                        var T = bet as TotalBet;
                        if (T != null)
                        {
                            Console.Write("{0}", ((TotalBet)bet).ToString());
                            Console.Write("coef: {0}", T.Odds);
                        }
                    }
                    catch { }
                    try
                    {
                        var h = bet as HandicapBet;
                        if (h != null)
                        {
                            Console.Write("{0}", ((HandicapBet)bet).ToString());
                            Console.Write("coef: {0}", h.Odds);
                        }
                    }
                    catch { }
                }
            }
            */

           //   result = finder.GetArbitrageBets();

              Console.WriteLine(finder);

          //  MatchName name1 = new MatchName("Ukraine Dinamo (U-21)", "Russia");
          //  MatchName name2 = new MatchName("D/Ukr U21", "Russia A");

          //  Console.WriteLine(name1.Equals(name2));

            Console.ReadLine();

        }

    }
}
