using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace YaguTalkCrawler
{
    public class Program
    {
        const string ID = "khirai2";
        const string PW = "wonseok2";
        const string NICKNAME = "VSCode";

        static void LogIn(IWebDriver driver, string id, string pw)
        {
            var wait = new WebDriverWait(driver, new TimeSpan(0, 0, 15));
            wait.Until((driver) => driver.FindElements(By.Id("ol_id")).Count > 0);
            
            IWebElement idElem = driver.FindElement(By.Id("ol_id"));
            idElem.Click();
            idElem.SendKeys(id);

            IWebElement pwElem = driver.FindElement(By.Id("ol_pw"));
            pwElem.Click();
            pwElem.SendKeys(pw);

            driver.FindElement(By.Id("ol_submit")).Click();

            return;
        }

        static int[] GetInput()
        {
            do
            {
                Console.Write("Input your article IDs of bidding items, separated by comma(,): ");
                string[] words = Console.ReadLine().Replace(" ", "").Split(',');
                try
                {
                    return Array.ConvertAll(words, w => int.Parse(w));
                }
                catch (Exception)
                {
                    Console.WriteLine("Non-valid input.");
                }
            }
            while (true);
        }

        static Article[] GetData(IWebDriver driver, int[] items)
        {
            Article[] articles = new Article[items.Length];
            var cnt = 0;

            foreach (int item in items)
            {
                // Fetch article
                string url = $"https://yagutalk.com/auction/{item}";
                driver.Navigate().GoToUrl(url);
                string title = driver.FindElement(By.XPath("//*[@id=\"bo_v_title\"]/span[2]")).Text;
                string[] content = driver.FindElement(By.XPath("//*[@id=\"bo_v_con\"]")).Text.Split(new[] { '\r', '\n' });
                content = content.Where(x => !string.IsNullOrEmpty(x)).ToArray();

                // Parse text to get information.
                string parsedTitle = Regex.Replace(title, @"\D", " ").TrimStart().TrimEnd();
                int[] splitTitle = Array.ConvertAll(Regex.Split(parsedTitle, @"\s+"), s => int.Parse(s));

                Article article = new Article();
                article.DueTime = new DateTime(splitTitle[1], splitTitle[2], splitTitle[3], splitTitle[4], splitTitle[5], 0, DateTimeKind.Local);

                // Throw exception for past bidding.
                if (article.DueTime < DateTime.Now)
                {
                    Console.WriteLine($"Bidding for item #{item} is already over!");
                    throw new ArgumentOutOfRangeException("TimeOver");
                }

                foreach (string line in content)
                {
                    if (Regex.Matches(line, "입찰 시작").Count > 0)
                    {
                        article.MinVal = int.Parse(Regex.Replace(line, @"\D", ""));

                        if (Regex.Match(line, "포인트").Success)
                        {
                            article.BidType = BiddingType.Point;
                        }
                        else
                        {
                            article.BidType = BiddingType.Cash;
                        }
                        break;
                    }

                }
                foreach (string line in content)
                {
                    if (Regex.Matches(line, "입찰 단위").Count > 0 && Regex.Matches(line, ":").Count > 0)
                    {
                        article.UnitVal = int.Parse(Regex.Replace(line, @"\D", ""));
                        break;
                    }

                }

                article.Id = item;


                Console.WriteLine("===========================================");
                Console.WriteLine($"Summary for item {item}:");
                Console.WriteLine($"Due time: {article.DueTime.ToShortDateString()} {article.DueTime.ToShortTimeString()}");
                Console.WriteLine($"Bidding type: {article.BidType}");
                Console.WriteLine($"Minimum value: {article.MinVal}");
                Console.WriteLine($"Unit value: {article.UnitVal}");
                Console.WriteLine($"===========================================");


                Console.Write("Write out the maximum bidding value: ");
                while (true)
                {
                    if (int.TryParse(Console.ReadLine(), out int maxVal))
                    {
                        if (maxVal >= article.MinVal)
                        {
                            article.MaxVal = maxVal;
                            break;
                        }
                        else
                        {
                            Console.Write("Maximum bidding must be equal or greater than minimum bidding. Try Again: ");
                        }
                    }
                    else
                    {
                        Console.Write("Invalid input. Try again: ");
                    }
                }

                articles[cnt++] = article;
            }

            return articles;
        }

        static void ScheduleTasks(IWebDriver _driver, List<Article> articles)
        {
            List<DateTime> times = articles.ConvertAll(x => x.DueTime.AddSeconds(-3));

            // For debugging only
            //times[0] = DateTime.Now.AddSeconds(3);
            //times[1] = DateTime.Now.AddSeconds(10);


            // Timer[] timers = new Timer[times.Count];
            int cnt = 0;

            foreach (var t in times)
            {
                DateTime current = DateTime.Now;
                TimeSpan interval = t - current;
                if (interval < TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException("Invalid due time!");
                }

                Console.WriteLine($"[{DateTime.Now}] {interval} remaining until {articles[cnt].Id}...");

                System.Timers.Timer timer = new System.Timers.Timer(interval.TotalMilliseconds);
                timer.AutoReset = false;
                timer.Elapsed += (sender, e) => Timer_Elapsed(sender, e, _driver, articles[cnt++]);
                
                timer.Start();
                Thread.Sleep((int)interval.TotalMilliseconds + 1000);
                timer.Stop();

                //var autoEvent = new AutoResetEvent(false);
                //var statusChecker = new StatusChecker(10);

                //timers[cnt] = new Timer(x =>
                //{
                //    Console.WriteLine("Fired!");
                //}, null, interval, Timeout.InfiniteTimeSpan);

                
            }

            Thread.Sleep(360000000);
        }

        private static void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e, IWebDriver _driver, Article article)
        {
            _driver.Navigate().GoToUrl($"https://yagutalk.com/auction/{article.Id}");
            var elems = _driver.FindElements(By.ClassName("cm_wrap"));

            List<Bid> bids = new List<Bid>();

            for (int i = 0; i < elems.Count; i++)
            {
                string[] lines = elems[i].Text.Split(new[] { '\r', '\n' });
                string nickName = lines[0].TrimStart().TrimEnd();
                string child = elems[i].FindElement(By.ClassName("cmt_contents")).Text;
                if (!Int32.TryParse(Regex.Replace(child, @"\D", ""), out int bidValue))
                {
                    continue;
                }
                bids.Add(new Bid { NickName = nickName, BidValue = bidValue });
            }

            bids = bids.OrderByDescending(b => b.BidValue).ToList();

            if (bids[0]?.BidValue == bids[1]?.BidValue || bids[0]?.NickName != NICKNAME || bids.Count == 0)
            {
                int valueToBid = article.MinVal;
                if (bids.Count != 0) 
                {
                    int topBid = bids[0].BidValue;
                    valueToBid = topBid % article.UnitVal == 0 ?
                        topBid + article.UnitVal :
                        (int)(Math.Ceiling((double)(topBid / article.UnitVal)) * article.UnitVal);
                }
                if (valueToBid > article.MaxVal){
                    Console.WriteLine($"[{DateTime.Now}] Maximum bidding value exceeded for {article.Id}");
                    return;
                }
                IWebElement elem = _driver.FindElement(By.Id("wr_content"));
                elem.Click();
                elem.SendKeys($"{valueToBid}");
                Thread.Sleep(100);
                _driver.FindElement(By.XPath("//*[@id=\"btn_submit\"]")).Click();
                Console.WriteLine($"[{DateTime.Now}] Commented {valueToBid} {article.BidType} for {article.Id}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now}] Did not commented");
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("YTCrawler 1.3.1 by Gluon. 2021");


            int[] items = GetInput();

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("headless");
            options.AddArgument("window-size=1920,1080");
            IWebDriver _driver = new ChromeDriver(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), options);
            _driver.Navigate().GoToUrl(@"https://yagutalk.com/");

            LogIn(_driver, ID, PW);
            List<Article> articles = GetData(_driver, items).ToList().OrderBy(a => a.DueTime).ToList();

            Console.WriteLine("All input validated. Waiting for bidding...");

            ScheduleTasks(_driver, articles);


        }
    }
}
