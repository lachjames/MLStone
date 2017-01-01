using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Plugins;

namespace MLStone
{
	public class CurvyPlugin : IPlugin
	{
		private CurvyList _list;

		public string Author
		{
			get { return "Lachlan O'Neill"; }
		}

		public string ButtonText
		{
			get { return "Settings"; }
		}

		public string Description
		{
			get { return "A plugin which finds the deck closest to the one your opponent seems to be playing (looking at the HearthPwn top 100 decks) and displays it for you."; }
		}

		public MenuItem MenuItem
		{
			get { return null; }
		}

		public string Name
		{
			get { return "Curvy"; }
		}

		public void OnButtonPress()
		{
		}

		public void OnLoad()
		{
			_list = new CurvyList();
			Core.OverlayCanvas.Children.Add(_list);
			Predictor curvy = new Predictor(_list);
            
			GameEvents.OnGameStart.Add(curvy.GameStart);
			GameEvents.OnInMenu.Add(curvy.InMenu);
			//GameEvents.OnTurnStart.Add(curvy.TurnStart);
            GameEvents.OnOpponentPlay.Add(curvy.OpponentPlayed);
            GameEvents.OnTurnStart.Add(curvy.TurnStart);
		}

		public void OnUnload()
		{
			Core.OverlayCanvas.Children.Remove(_list);
		}

		public void OnUpdate()
		{
		}

		public Version Version
		{
			get { return new Version(0, 1, 1); }
		}
	}
}