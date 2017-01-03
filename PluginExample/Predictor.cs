using System;
using System.Collections.Generic;
using System.Linq;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using CoreAPI = Hearthstone_Deck_Tracker.API.Core;
using Hearthstone_Deck_Tracker.Utility.Logging;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace MLStone
{
    internal partial class Predictor
    {
        private CurvyList _list = null;

        List<Known_Deck> known_decks = new List<Known_Deck>();

        internal List<Entity> Entities =>
            Helper.DeepClone<Dictionary<int, Entity>>(CoreAPI.Game.Entities).Values.ToList<Entity>();

        internal Entity Opponent => Entities?.FirstOrDefault(x => x.IsOpponent);

        List<Card> played = new List<Card>();

        MLModel model;
        
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
            
            MessageBox.Show("Finished learning!");
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
                    MessageBox.Show(e.StackTrace);
                    throw e;
                }
            }
            else
            {
                try
                {
                    known_decks = Import_Top_Decks();
                    MessageBox.Show("Imported top decks successfully");
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.StackTrace);
                    throw e;
                }
            }

            //foreach (Known_Deck kd in known_decks)
            //{
            //    MessageBox.Show(kd.Get_Deck().Count.ToString());
            //}
            try
            {
                model = new WitnessModel();

                model.Train(known_decks);

                _list.Update(known_decks[0].Get_Deck());
            }
            catch (Exception e)
            {
                MessageBox.Show(e.StackTrace);
                throw e;
            }
        }

        //http://stackoverflow.com/questions/16352879/write-list-of-objects-to-a-file

        private void Save_Top_Decks()
        {
            string loc = Save_Load_Loc();

            List<string> out_strings = new List<string>();
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

            foreach (string s_raw in lines)
            {
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
            played.Add(c);
            try
            {
                // Opponent just played card c
                Update_Viewer();
            } catch (NullReferenceException e)
            {
                played.Remove(c);
                Update_Viewer();
            }
        }

        internal void Update_Viewer()
        {
            _list.Show();
            //_list.Update(subtract(Probable_Deck(), played));
            //try
            //{
                _list.Update(model.Predict(played));
            //}
            //catch (Exception e)
            //{
            //    MessageBox.Show("Error: " + e.Message);
            //    throw e;
            //}
        }

        public void TurnStart(ActivePlayer p)
        {
            Update_Viewer();
        }

        List<Card> subtract(List<Card> A, List<Card> B)
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

        List<Card> copy(List<Card> X)
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

        private int Matches(Known_Deck kd)
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

        internal string Save_Load_Loc()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path += "/HearthstoneDeckTracker/top_decks.dk";
            return path;
        }

        class WitnessModel : MLModel
        {
            int min_count = 3;
            Dictionary<string, List<String>> witnesses = new Dictionary<string, List<string>>();

            public override void Train(List<Known_Deck> train_data)
            {
                // For each card, we find three other cards which it "witnesses" if it's played - the cards which are most commonly played with it
                _init(train_data);
                MessageBox.Show("Initialized with " + this.num_cards.ToString() + " cards");
                for (int i = 0; i < num_cards; i++)
                {
                    string cur_card = all_cards[i];

                    List<Prob> probs = new List<Prob>();
                    for (int j = 0; j < num_cards; j++)
                    {
                        string other_card = all_cards[j];
                        if (i == j)
                        {
                            continue;
                        }

                        double freq = frequencies[i, j];

                        probs.Add(new Prob(cur_card, other_card, freq));
                    }
                    probs.Sort();
                    probs.Reverse();

                    List<string> cur_witnesses = new List<string>();

                    while (cur_witnesses.Count < min_count || probs[0].predicted_card_probability >= 0.8)
                    {
                        if (probs.Count == 0)
                        {
                            break;
                        }
                        cur_witnesses.Add(probs[0].predicted_card_name);
                        Log.WriteLine("Card: " + cur_card + " has witness: " + probs[0].predicted_card_name + " with prob=" + probs[0].predicted_card_probability.ToString(), LogType.Info);
                        probs.RemoveAt(0);
                    }

                    witnesses.Add(cur_card, cur_witnesses);
                }
            }

            public override List<Card> Predict(List<Card> deck_cards, int count = 35)
            {
                Dictionary<string, int> witnessed_counts = new Dictionary<string, int>();

                foreach (Card c in deck_cards)
                {
                    if (!witnesses.ContainsKey(c.Name))
                    {
                        continue;
                    }
                    List<string> wts = witnesses[c.Name];
                    foreach (string w in wts)
                    {
                        if (!class_compatible(card_with_name(w))) {
                            continue;
                        }

                        if (!witnessed_counts.ContainsKey(w))
                        {
                            witnessed_counts.Add(w, 0);
                        }
                        witnessed_counts[w] += 1;
                    }
                }

                var topn = witnessed_counts.OrderByDescending(pair => pair.Value);


                List<Card> predictions = new List<Card>();

                foreach (var x in topn)
                {
                    string name = x.Key;
                    Card c = card_with_name(name);
                    predictions.Add(c);
                }

                return predictions;
            }

            private bool class_compatible(Card card)
            {
                return card.PlayerClass == CoreAPI.Game.Opponent.Class || card.PlayerClass == "Neutral";
            }
        }

        class BruteForceModel : MLModel
        {
            // A model which, given a bunch of transactions (each a list of strings) can predict the rest of a transaction given a component of it
            public override void Train(List<Known_Deck> train_data)
            {
                _init(train_data);
                // We have a list of decks, each of which contains a set of cards
                // From this information, we want to create a set of cards which most often are included with other cards
                //Log.WriteLine(string.Join(";", freq_buffer), LogType.Info);

            }

            public override List<Card> Predict(List<Card> deck_cards, int count = 35)
            {
                if (deck_cards.Count == 0)
                {
                    return new List<Card>();
                }

                Dictionary<string, Prob> probabilities_dict = new Dictionary<string, Prob>();

                List<string> deck_cards_names = new List<string>();

                foreach (Card c in deck_cards)
                {
                    deck_cards_names.Add(c.Name);
                }

                foreach (Card deck_card in deck_cards)
                {
                    string dc_name = deck_card.Name;
                    if (!card_indices.ContainsKey(dc_name))
                    {
                        // If we've never seen this card before (can happen, esp. when playing the innkeeper or basic only decks!)
                        continue;
                    }
                    int i = card_indices[dc_name];
                    foreach (string known_card in all_cards)
                    {
                        int j = card_indices[known_card];
                        if (i == j || deck_cards_names.Contains(known_card) || frequencies[i, j] == 0.0)
                        {
                            continue;
                        } else
                        {
                            //Log.WriteLine(frequencies[i, j].ToString(), LogType.Info);
                        }
                        if (!probabilities_dict.ContainsKey(known_card))
                        {
                            probabilities_dict.Add(known_card, new Prob(dc_name, known_card, 0.0));
                        }
                        // if they have the same class or the possible match is neutral...
                        if (class_lookup[known_card] == class_lookup[dc_name] || class_lookup[known_card] == "Neutral")
                        {
                            probabilities_dict[known_card].predicted_card_probability += frequencies[i, j]; // * (CoreAPI.Game.Opponent.Class == class_lookup[known_card] ? 1.5 : 1.0);
                        } else
                        {
                            probabilities_dict.Remove(known_card);
                        }
                    }
                }

                Log.WriteLine(probabilities_dict.Count.ToString(), LogType.Info);

                if (probabilities_dict.Count == 0)
                {
                    return new List<Card>();
                }

                List<Prob> probabilities = new List<Prob>(probabilities_dict.Values);

                probabilities.Sort();

                List<Card> predictions = new List<Card>();

                for (int i = 0; i < count; i++)
                {
                    if (probabilities.Count == 0)
                    {
                        Log.WriteLine(predictions.Count.ToString(), LogType.Info);
                        return predictions;
                    }
                    Prob cur = probabilities[probabilities.Count - 1];
                    probabilities.RemoveAt(probabilities.Count - 1);


                    predictions.Add(card_with_name(cur.predicted_card_name));
                }

                Log.WriteLine(predictions.Count.ToString(), LogType.Info);
                return predictions;
            }
        }
    }
}