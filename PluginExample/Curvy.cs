using System;
using System.Collections.Generic;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using CoreAPI = Hearthstone_Deck_Tracker.API.Core;

using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace MLStone
{
	internal class Predictor
	{
		private CurvyList _list = null;

        List<Known_Deck> known_decks = new List<Known_Deck>();

        internal List<Entity> Entities =>
            Helper.DeepClone<Dictionary<int, Entity>>(CoreAPI.Game.Entities).Values.ToList<Entity>();

        internal Entity Opponent => Entities?.FirstOrDefault(x => x.IsOpponent);

        List<Card> played = new List<Card>();
        
        public Predictor(CurvyList list)
        {
            _list = list;

            MessageBoxResult dialogResult = MessageBox.Show("Should I download new decks from Hearthpwn (if not I'll just use the ones I saved from last time...)", "Update Decks?", MessageBoxButton.YesNo);
            if (dialogResult == MessageBoxResult.Yes)
            {
                // Download new results
                Start(true);
            }
            else if (dialogResult == MessageBoxResult.No)
            {
                Start(false);
            }
        }

        private async Task Start(bool update)
        {
            // Hide in menu, if necessary
            //if (Config.Instance.HideInMenu && CoreAPI.Game.IsInMenu)
            //    _list.Hide();

            if (update)
            {
                try
                {
                    known_decks = await Known_Deck.Top_DecksAsync();
                    Save_Top_Decks();
                    MessageBox.Show("Saved top decks");
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            }
            else
            {
                try
                {
                    known_decks = Import_Top_Decks();
                    MessageBox.Show("Imported top decks successfully");
                } catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            }

            //foreach (Known_Deck kd in known_decks)
            //{
            //    MessageBox.Show(kd.Get_Deck().Count.ToString());
            //}

            _list.Update(known_decks[0].Get_Deck());
        }

        //http://stackoverflow.com/questions/16352879/write-list-of-objects-to-a-file

        private void Save_Top_Decks()
        {
            string loc = Save_Load_Loc();

            List<string> out_strings = new List<string> ();
            foreach (Known_Deck kd in known_decks)
            {
                out_strings.Add(kd.serialize());
            }

            File.WriteAllLines(loc, out_strings);
        }

        private List<Known_Deck> Import_Top_Decks()
        {
            string loc = Save_Load_Loc();
            List<Known_Deck> kds = new List<Known_Deck>();

            List<string> lines = File.ReadLines(loc).ToList();

            foreach (string s_raw in lines) {
                string s = s_raw.Trim();
                if (s == "")
                {
                    continue;
                }
                Known_Deck kd = new Known_Deck();
                kd.instantiate(s);
                kds.Add(kd);
            }

            return kds;
        }

        internal void OpponentPlayed(Card c)
        {
            // Opponent just played card c
            played.Add(c);
            Update_Viewer();
        }

        internal void Update_Viewer()
        {
            _list.Show();
            _list.Update(subtract(Probable_Deck(), played));
        }

        public void TurnStart (ActivePlayer p)
        {
            Update_Viewer();
        }

        List<Card> subtract (List<Card> A, List<Card> B)
        {
            // Subtracts deck B from deck A and returns the result
            List<Card> A_cp = copy(A);

            // For each card in deck B, we find it if it's in deck A and, if so, reduce its count by one       
            foreach (Card c in B)
            {
                for (int i = 0; i < A_cp.Count; i++)
                {
                    // If we've found a copy of the card
                    if (A_cp[i].Name == c.Name)
                    {
                        // This card has already been played
                        A_cp[i].Count = Math.Max(A_cp[i].Count - 1, 0); // Can't have a negative number of cards!
                        break;
                    }
                }
            }

            return A_cp;
        }

        List<Card> copy (List<Card> X)
        {
            List<Card> y = new List<Card>();
            foreach (Card c in X)
            {
                y.Add((Card)c.Clone());
            }
            return y;
        }

        private List<Card> Probable_Deck()
        {
            //MessageBox.Show(known_decks[0].Get_Class());
            //MessageBox.Show(CoreAPI.Game.Opponent.Class);
            Known_Deck best = known_decks[0];
            int best_num_matches = -1;
            foreach (Known_Deck kd in known_decks)
            {
                int this_matches = Matches(kd);
                //MessageBox.Show(this_matches.ToString());
                if (this_matches > best_num_matches)
                {
                    best_num_matches = this_matches;
                    best = kd;
                }
            }
            return best.Get_Deck();
        }

        private int Matches (Known_Deck kd)
        {
            if (CoreAPI.Game.Opponent.Class != "" && kd.Get_Class() != CoreAPI.Game.Opponent.Class)
            {
                return -1;
            }
            // Find the number of cards in kd which have been played by our opponent so far
            int m = 0;
            foreach (Card c1 in kd.Get_Deck())
            {
                foreach (Card c2 in played)
                {
                    if (c1 == null || c2 == null || c1.Name == "" || c2.Name == "")
                    {
                        continue;
                    }
                    if (c1.Name == c2.Name)
                    {
                        m += Math.Min(c1.Count, c2.Count);
                    }
                }
            }

            //MessageBox.Show(m.ToString());

            return m;
        }

        // Reset on when a new game starts
        internal void GameStart()
		{
            played.Clear();
            Update_Viewer();
		}

		// Need to handle hiding the element when in the game menu
		internal void InMenu()
		{
			//if (Config.Instance.HideInMenu)
			//{
			//	_list.Hide();
			//}
		}

		//// Update the card list on player's turn
		//internal void TurnStart(ActivePlayer player)
		//{
  //          Update_Viewer();
  //      }

        internal string Save_Load_Loc ()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path += "/HearthstoneDeckTracker/top_decks.dk";
            return path;
        }

        internal class Known_Deck
        {
            Deck this_deck = null;

            string deck_class = "";

            public string Get_Class ()
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

            public string serialize ()
            {
                string s = deck_class + "?";
                foreach (Card c in this_deck.Cards)
                {
                    s += c.Name + "|" + c.Count.ToString() + ";";
                }
                return s;
            }

            public void instantiate (string all_data)
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
}