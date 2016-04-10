using System;
using System.Collections.Generic;

namespace Herby
{
	public class deck_card
	{
		public string type = "";
		public string target = "none";
		public string family = "";
		public double value_in_hand = 0;
		public Action<card, board_state> deathrattle;
		public Action<card, board_state> inspire;
		public Action<card, board_state, card> battlecry;
		public Action<card, board_state, card> effect;
		public Action<card, board_state> gain_aura;
		public Action<card, board_state> lose_aura;
	}
}