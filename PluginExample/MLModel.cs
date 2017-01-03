using System.Collections.Generic;
using Hearthstone_Deck_Tracker.Hearthstone;
using System;
using Hearthstone_Deck_Tracker.Utility.Logging;
using System.Linq;
using System.Windows;

namespace MLStone
{
    abstract class MLModel
    {
        protected Dictionary<string, int> card_indices;
        protected List<string> all_cards;
        protected Dictionary<string, string> class_lookup;
        protected int num_cards = -1;

        protected double[,] frequencies = null;

        protected Known_Deck first = null;

        public abstract List<Card> Predict(List<Card> deck_cards, int count = 35);
        public abstract void Train(List<Known_Deck> train_data);

        protected List<HashSet<string>> decks;

        protected Card card_with_name (string name)
        {
            List<Card> found = HearthDb.Cards.Collectible.Values
                .Where(c => c.Name == name)
                .Select(c => new Card(c))
                .OrderBy(c => c.Rarity)
                .ToList<Card>();

            if (found.Count == 0)
            {
                Log.WriteLine("No card found with name: " + name, LogType.Error);
                return null;
            }

            Card card = found[0];
            return card;
        }

        protected void _init (List<Known_Deck> train_data)
        {
            Log.WriteLine("Starting initialization", LogType.Debug);
            FindAllCards(train_data);
            Log.WriteLine("Found all cards", LogType.Debug);
            num_cards = all_cards.Count;

            Log.WriteLine("Found frequencies", 0);

            first = train_data[0];

            //List<string> freq_buffer = new List<string>();

            decks = decks_to_sets(train_data);

            Log.WriteLine("Found card count=" + num_cards.ToString(), LogType.Debug);
            frequencies = new double[num_cards, num_cards];
            for (int i = 0; i < num_cards; i++)
            {
                for (int j = 0; j < num_cards; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    string c_i = all_cards[i];
                    string c_j = all_cards[j];

                    frequencies[i, j] = calc_freq(c_i, c_j, decks);

                    //freq_buffer.Add(frequencies[i, j].ToString());
                }
            }
        }

        protected class Prob : IComparable<Prob>
        {
            public string known_card_name;
            public string predicted_card_name;
            public double predicted_card_probability;

            public Prob(string a, string b, double c)
            {
                known_card_name = a;
                predicted_card_name = b;
                predicted_card_probability = c;
            }

            public int CompareTo(Prob other)
            {
                return this.predicted_card_probability.CompareTo(other.predicted_card_probability);
            }
        }

        protected double calc_freq(string c_i, string c_j, List<HashSet<string>> decks)
        {
            // Calculate the number of times c_i and c_j appear together, divided by the number of times c_i appears at all
            double both_counts = 0;
            double i_counts = 0;

            foreach (HashSet<string> deck in decks)
            {
                if (deck.Contains(c_i))
                {
                    if (deck.Contains(c_j))
                    {
                        both_counts += 1;
                    }
                    i_counts += 1;
                }
            }
            //Random r = new Random();
            //if (r.NextDouble() < 0.001) {
            //    Log.WriteLine((both_counts / i_counts).ToString(), 0);
            //}
            return both_counts / i_counts;
        }

        protected List<HashSet<string>> decks_to_sets(List<Known_Deck> train_data)
        {
            List<HashSet<string>> sets = new List<HashSet<string>>();
            foreach (Known_Deck k in train_data)
            {
                HashSet<string> set = new HashSet<string>();
                foreach (Card c in k.Get_Deck())
                {
                    set.Add(c.Name);
                }
                sets.Add(set);
            }
            return sets;
        }

        protected void FindAllCards(List<Known_Deck> train_data)
        {
            HashSet<string> known_cards = new HashSet<string>();

            class_lookup = new Dictionary<string, string>();

            foreach (Known_Deck k in train_data)
            {
                foreach (Card c in k.Get_Deck())
                {
                    if (!class_lookup.ContainsKey(c.Name))
                    {
                        class_lookup.Add(c.Name, c.PlayerClass);
                    }
                    known_cards.Add(c.Name);
                }
            }

            all_cards = new List<string>(known_cards);

            card_indices = new Dictionary<string, int>();

            for (int i = 0; i < all_cards.Count; i++)
            {
                card_indices.Add(all_cards[i], i);
            }
        }
    }
}