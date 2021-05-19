using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YaguTalkCrawler
{

    public enum BiddingType { Cash, Point }

    public class Article
    {
        public int Id { get; set; }

        public DateTime DueTime { get; set; }

        public BiddingType BidType { get; set; }

        public int MinVal { get; set; }

        public int UnitVal { get; set; }

        public int MaxVal { get; set; }
            

    }

    public class Bid
    {
        public string NickName { get; set; }

        public int BidValue { get; set; }
    }
}
