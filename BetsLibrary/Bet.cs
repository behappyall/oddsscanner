using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetsLibrary
{
    public abstract class Bet
    {
        public double Odds { get; protected set; }
        public Bookmaker Bookmaker { get; protected set; }
        public MatchName MatchName { get; protected set; }
        public Time Time { get; protected set; }
        public string BetUrl { get; protected set; }
        public string JavaScriptSelectorCode { get; protected set; }
        public Sport Sport { get; protected set; }

        public void ChangeOdds(double newOdds)
        {
            this.Odds = newOdds;
        }

        public abstract List<Bet> GetForkBets();
        

    }

    public enum ResultBetType
    {
        First,
        Draw,
        Second,
        FirstOrDraw,
        FirstOrSecond,
        SecondOrDraw,
        P1,
        P2
    }

    public class ResultBet : Bet
    {
        public ResultBetType ResultBetType { get; private set; }


        public ResultBet(ResultBetType ResultBetType, Time Time, double Odds, MatchName MatchName, string BetUrl, string JavaScriptSelectorCode, Sport Sport, Bookmaker Bookmaker)
        {
            this.ResultBetType = ResultBetType;
            this.Time = Time;
            this.Odds = Odds;
            this.MatchName = MatchName;
            this.BetUrl = BetUrl;
            this.JavaScriptSelectorCode = JavaScriptSelectorCode;
            this.Sport = Sport;
            this.Bookmaker = Bookmaker;
        }

        public override List<Bet> GetForkBets()
        {
            List<Bet> result = new List<Bet>();

            switch (ResultBetType)
            {
                case ResultBetType.First:
                    if (Sport == Sport.Football)
                    {
                        result.Add(new ResultBet(ResultBetType.SecondOrDraw, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                        result.Add(new HandicapBet(HandicapBetType.F2, 0.5, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport,Bookmaker));
                    }
                    else
                    {
                        result.Add(new ResultBet(ResultBetType.Second, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                        result.Add(new HandicapBet(HandicapBetType.F2, 0, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    }
                    break;
                case ResultBetType.Draw:
                        result.Add(new ResultBet(ResultBetType.FirstOrSecond, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    break;
                case ResultBetType.Second:
                    if (Sport == Sport.Football)
                    {
                        result.Add(new ResultBet(ResultBetType.FirstOrDraw, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                        result.Add(new HandicapBet(HandicapBetType.F1, 0.5, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    }
                    else
                    {
                        result.Add(new ResultBet(ResultBetType.First, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                        result.Add(new HandicapBet(HandicapBetType.F1, 0, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    }
                    break;
                case ResultBetType.FirstOrDraw:
                    result.Add(new ResultBet(ResultBetType.Second, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    result.Add(new HandicapBet(HandicapBetType.F2, -0.5, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    result.Add(new HandicapBet(HandicapBetType.F2, 0, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    break;
                case ResultBetType.FirstOrSecond:
                    result.Add(new ResultBet(ResultBetType.Draw, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    break;
                case ResultBetType.SecondOrDraw:
                    result.Add(new ResultBet(ResultBetType.First, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    result.Add(new HandicapBet(HandicapBetType.F1, -0.5, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    result.Add(new HandicapBet(HandicapBetType.F1, 0, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                    break;
            }
            
            return result;
        }

        public override int GetHashCode()
        {
            return ResultBetType.GetHashCode() ^ Time.GetHashCode() ^ MatchName.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is ResultBet bet)
            {
                return Equals(bet);
            }
            else return false;
        }

        public bool Equals(ResultBet bet)
        {
            return bet.Time.Equals(Time) && bet.ResultBetType == ResultBetType && bet.MatchName.Equals(MatchName) && bet.Sport == Sport;
        }

        public override string ToString()
        {
            return String.Format("{0} {1}", Time, ResultBetType);
        }

    }

    public enum HandicapBetType
    {
        F1,
        F2
    }

    public class HandicapBet : Bet
    {
        public HandicapBetType HandicapBetType { get; private set; }
        public double HandicapValue { get; private set; }

        public HandicapBet(HandicapBetType handicapBetType, double HandicapValue, Time Time, double Odds, MatchName MatchName, string BetUrl, string JavaScriptSelectorCode, Sport Sport, Bookmaker Bookmaker)
        {
            this.Odds = Odds;
            this.HandicapBetType = handicapBetType;
            this.HandicapValue = HandicapValue;
            this.Time = Time;
            this.MatchName = MatchName;
            this.BetUrl = BetUrl;
            this.JavaScriptSelectorCode = JavaScriptSelectorCode;
            this.Sport = Sport;
            this.Bookmaker = Bookmaker;
        }

        public override List<Bet> GetForkBets()
        {
            List<Bet> result = new List<Bet>();

            result.Add(new HandicapBet(HandicapBetType == HandicapBetType.F1 ? HandicapBetType.F2 : HandicapBetType.F1, HandicapValue * (-1), Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
            
            if(HandicapValue == 0)
            {
                if (Sport == Sport.Football) result.Add(new ResultBet(HandicapBetType == HandicapBetType.F1 ? ResultBetType.SecondOrDraw : ResultBetType.FirstOrDraw, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
                else result.Add(new ResultBet(HandicapBetType == HandicapBetType.F2 ? ResultBetType.Second : ResultBetType.First, Time, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
            }

            return result;
        }

        public override int GetHashCode()
        {
            return HandicapBetType.GetHashCode() ^ Time.GetHashCode() ^ MatchName.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is HandicapBet bet)
            {
                return Equals(bet);
            }
            else return false;
        }

        public bool Equals(HandicapBet bet)
        {
            return bet.Time.Equals(Time) && bet.HandicapBetType == HandicapBetType && bet.HandicapValue == HandicapValue && bet.MatchName.Equals(MatchName) && bet.Sport == Sport;
        }

        public override string ToString()
        {
            return String.Format("{0} {1} ({2})", Time, HandicapBetType, HandicapValue);
        }
    }

    public enum TotalBetType
    {
        Over,
        Under
    }

    public class TotalBet : Bet
    {
        public TotalBetType TotalBetType { get; private set; }
        public double TotalValue { get; private set; }
        public Team Team { get; private set; }

        public TotalBet(TotalBetType TotalBetType, double TotalValue, Time Time, Team Team, double Odds, MatchName MatchName, string BetUrl, string JavaScriptSelectorCode, Sport Sport, Bookmaker Bookmaker)
        {
            this.Odds = Odds;
            this.TotalBetType = TotalBetType;
            this.TotalValue = TotalValue;
            this.Time = Time;
            this.Team = Team;
            this.MatchName = MatchName;
            this.BetUrl = BetUrl;
            this.JavaScriptSelectorCode = JavaScriptSelectorCode;
            this.Sport = Sport;
            this.Bookmaker = Bookmaker;
        }

        public override List<Bet> GetForkBets()
        {
            List<Bet> result = new List<Bet>();

            if (TotalBetType == TotalBetType.Over) result.Add(new TotalBet(TotalBetType.Under, TotalValue, Time, Team, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));
            else result.Add(new TotalBet(TotalBetType.Over, TotalValue, Time, Team, Odds, MatchName, BetUrl, JavaScriptSelectorCode, Sport, Bookmaker));

            return result;
        }

        public override int GetHashCode()
        {
            return TotalBetType.GetHashCode() ^ Time.GetHashCode() ^ Team.GetHashCode() ^ MatchName.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is TotalBet bet)
            {
                return Equals(bet);
            }
            else return false;
        }

        public bool Equals(TotalBet bet)
        {
            return bet.Time.Equals(Time) && bet.TotalBetType == TotalBetType && bet.TotalValue == TotalValue && bet.Team == Team && bet.MatchName.Equals(MatchName) && bet.Sport == Sport;
        }

        public override string ToString()
        {
            if (Team == Team.All)
                return String.Format("{0} {1} ({2})", Time, TotalBetType, TotalValue);
            else
            if (Team == Team.First)
                return String.Format("{0} {3} {1} ({2})", Time, TotalBetType, TotalValue, MatchName.FirstTeam);
            else
            if (Team == Team.Second)
                return String.Format("{0} {3} {1} ({2})", Time, TotalBetType, TotalValue, MatchName.SecondTeam);
            else
                return null;

        }

      
    }


    public enum Team
    {
        First,
        Second,
        All
    }
}
