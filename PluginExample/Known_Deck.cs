using System;
using System.Collections.Generic;
using System.Linq;
using Hearthstone_Deck_Tracker.Hearthstone;

using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace MLStone
{
    class Known_Deck
    {
        Deck this_deck = null;

        string deck_class = "";

        public string Get_Class()
        {
            return deck_class;
        }

        public List<Card> Get_Deck()
        {
            List<Card> cards = new List<Card>(this.this_deck.Cards);
            Comparison<Card> comp = (x, y) => x.Cost.CompareTo(y.Cost);
            cards.Sort(comp);
            return cards;
        }

        public async Task<Known_Deck> Import(string url)
        {
            this_deck = await Hearthstone_Deck_Tracker.Importing.Websites.Hearthpwn.Import(url);
            deck_class = this_deck.Class;
            return this;
        }

        public static async Task<List<Known_Deck>> Top_DecksAsync()
        {
            //MessageBox.Show("Starting!");
            List<Known_Deck> decks = new List<Known_Deck>();

            for (int i = 1; i <= 6; i++)
            {
                //MessageBox.Show(i.ToString());
                string cur_page = "http://www.hearthpwn.com/decks?filter-is-forge=2&filter-show-standard=1&filter-deck-tag=1&filter-deck-type-val=10&filter-deck-type-op=3&page=" + i.ToString();
                //MessageBox.Show(cur_page);
                using (WebClient wc = new WebClient())
                {
                    wc.Proxy = WebRequest.DefaultWebProxy;
                    string html = wc.DownloadString(cur_page);
                    //MessageBox.Show(html);

                    List<string> urls = Between(html, "<a href=\"/decks/", "\">");

                    List<Task<Known_Deck>> tasks = new List<Task<Known_Deck>>();
                    foreach (string url in urls)
                    {
                        try
                        {
                            string full_url = "http://www.hearthpwn.com/decks/" + url;
                            //MessageBox.Show(full_url);
                            //await kd.Import(full_url);
                            Known_Deck kd = new Known_Deck();
                            Task<Known_Deck> task = kd.Import(full_url);
                            tasks.Add(task);
                        }
                        catch
                        {
                            MessageBox.Show(url + " has failed to import!");
                        }
                    }

                    foreach (Task<Known_Deck> t in tasks)
                    {
                        decks.Add(await t);
                    }
                    //MessageBox.Show(decks.Count.ToString());
                }
            }

            return decks;
        }

        static List<string> Between(String source, String before, String after)
        {
            // Finds a list of all the strings which are between a 'before' and an 'after'
            List<string> found = new List<string>();

            List<int> befores = new List<int>();
            foreach (Match match in Regex.Matches(source, before))
            {
                befores.Add(match.Index);
            }

            List<int> afters = new List<int>();
            foreach (Match match in Regex.Matches(source, after))
            {
                afters.Add(match.Index);
            }

            int adj = 0;
            for (int i = 0; i < befores.Count; i++)
            {
                int start = befores[i] + before.Length;
                while (afters[i + adj] < befores[i] + before.Length)
                {
                    adj++;
                }
                int end = afters[i + adj];

                //Console.WriteLine(start);
                //Console.WriteLine(end);

                string bet = source.Substring(start, end - start - 1);

                found.Add(bet);
            }

            return found;
        }

        public string serialize()
        {
            string s = deck_class + "?";
            foreach (Card c in this_deck.Cards)
            {
                s += c.Name + "|" + c.Count.ToString() + ";";
            }
            return s;
        }

        public void instantiate(string all_data)
        {
            string[] tokens = all_data.Split('?');

            string d_class = tokens[0];
            string data = tokens[1];

            this.deck_class = d_class;

            string[] cards = data.Split(';');
            Deck d = new Deck();
            foreach (string c_info_raw in cards)
            {
                string c_info = c_info_raw.Trim();
                if (c_info == "")
                {
                    continue;
                }
                try
                {
                    //MessageBox.Show(c_info);
                    string[] info = c_info.Split('|');
                    //MessageBox.Show(info.ToString());
                    string c_name = info[0];
                    int count = int.Parse(info[1]);
                    Card card = HearthDb.Cards.Collectible.Values
                            .Where(c => c.Name == c_name)
                            .Select(c => new Card(c))
                            .OrderBy(c => c.Rarity)
                            .ToList<Card>()[0];
                    card.Count = count;
                    d.Cards.Add(card);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Failed on " + c_info + "; " + e.Message);
                }
            }
            this_deck = d;
        }
    }
}