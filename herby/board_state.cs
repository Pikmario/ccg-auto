using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Herby
{
	public class board_state
	{
		public Dictionary<string, card> cards = new Dictionary<string, card>();

		public bool my_turn = false;
		public int max_mana = 1;
		public int cur_mana = 1;
		public int mana_used = 0;

		public int my_health = 30;
		public int enemy_health = 30;

		public string my_name = "";
		public string enemy_name = "";

		public string my_hero_id;
		public string enemy_hero_id;

		public string hero_power_id;
		public string weapon_id;

		public bool game_active;

		public string game_state;
		public bool mulligan_complete = false;
		public bool wait_for_mulligan = true;

		public List<string> cards_board = new List<string>();
		public List<string> cards_hand = new List<string>();
		public List<string> cards_minions = new List<string>();
		public List<string> cards_friendly = new List<string>();
		public List<string> cards_opposing = new List<string>();
		public List<string> cards_friendly_minions = new List<string>();
		public List<string> cards_opposing_minions = new List<string>();

		public board_state()
		{

		}

		public board_state(board_state cloned_board)
		{
			//pass in a board_state to a new object, and it will create a copy of it
			this.my_turn = cloned_board.my_turn;
			this.max_mana = cloned_board.max_mana;
			this.cur_mana = cloned_board.cur_mana;
			this.mana_used = cloned_board.mana_used;

			this.my_health = cloned_board.my_health;
			this.enemy_health = cloned_board.enemy_health;

			this.my_name = cloned_board.my_name;
			this.enemy_name = cloned_board.enemy_name;

			this.my_hero_id = cloned_board.my_hero_id;
			this.enemy_hero_id = cloned_board.enemy_hero_id;

			this.hero_power_id = cloned_board.hero_power_id;
			this.weapon_id = cloned_board.weapon_id;

			this.game_active = cloned_board.game_active;

			this.game_state = cloned_board.game_state;
			this.mulligan_complete = cloned_board.mulligan_complete;
			this.wait_for_mulligan = cloned_board.wait_for_mulligan;

			this.cards_board = new List<string>(cloned_board.cards_board);
			this.cards_hand = new List<string>(cloned_board.cards_hand);
			this.cards_minions = new List<string>(cloned_board.cards_minions);
			this.cards_friendly = new List<string>(cloned_board.cards_friendly);
			this.cards_opposing = new List<string>(cloned_board.cards_opposing);
			this.cards_friendly_minions = new List<string>(cloned_board.cards_friendly_minions);
			this.cards_opposing_minions = new List<string>(cloned_board.cards_opposing_minions);

			foreach (KeyValuePair<string, card> entry in cloned_board.cards)
			{
				this.cards[entry.Key] = new card(entry.Value);
			}
		}

		public void add_card(string id)
		{
			if (!this.cards.ContainsKey(id))
			{
				this.cards.Add(id, new card(id));
			}
		}

		public void add_card(string id, card added_card)
		{
			if (!this.cards.ContainsKey(id))
			{
				this.cards.Add(id, added_card);
			}
		}

		public bool change_card_prop(string id, string prop_name, dynamic prop_value)
		{
			if (!cards.ContainsKey(id))
			{
				//current card doesn't exist
				return false;
			}
			prop_name = prop_name.ToLower();

			if (prop_value.Length == 0)
			{
				//prop value is no good
				return false;
			}

			switch (prop_name)
			{
				case "name":
					cards[id].name = prop_value;
					break;
				case "card_id":
					cards[id].card_uid = prop_value;
					break;
				case "cardtype":
					cards[id].card_type = prop_value;
					break;
				case "health":
					cards[id].max_health = Int32.Parse(prop_value);
					if (cards[id].base_health == -1)
					{
						cards[id].base_health = Int32.Parse(prop_value);
					}
					break;
				case "armor":
					cards[id].armor = Int32.Parse(prop_value);
					break;
				case "durability":
					cards[id].durability = Int32.Parse(prop_value);
					break;
				case "atk":
					cards[id].atk = Int32.Parse(prop_value);
					if (cards[id].base_atk == -1)
					{
						cards[id].base_atk = Int32.Parse(prop_value);
					}
					break;
				case "cost":
					cards[id].mana_cost = Int32.Parse(prop_value);
					break;
				case "zone":
					if (prop_value != "PLAY" && prop_value != "DECK" && prop_value != "HAND")
					{
						add_card_to_zone(id, prop_value);
					}
					break;
				case "zone_position":
					cards[id].zone_position = Int32.Parse(prop_value);
					break;
				default:
					return false;
			}

			return true;
		}

		public bool change_card_tag(string id, string tag_name, bool value)
		{
			tag_name = tag_name.ToLower();

			switch (tag_name)
			{
				case "taunt":
					cards[id].tags.taunt = value;
					break;
				case "stealth":
					cards[id].tags.stealth = value;
					break;
				case "cant_be_targeted_by_abilities":
					cards[id].tags.cant_be_targeted_by_abilities = value;
					break;
				case "charge":
					cards[id].tags.charge = value;
					if (value)
					{
						cards[id].tags.exhausted = !value;
					}
					break;
				case "exhausted":
					cards[id].tags.exhausted = value;
					break;
				case "divine_shield":
					cards[id].tags.divine_shield = value;
					break;
				case "frozen":
					cards[id].tags.frozen = value;
					break;
				case "immune":
				case "cant_be_damaged":
					cards[id].tags.immune = value;
					break;
				case "silenced":
					cards[id].tags.silenced = value;
					break;
				case "battlecry":
					cards[id].tags.battlecry = value;
					break;
				case "cant_attack":
					cards[id].tags.cant_attack = value;
					break;
				case "powered_up":
					cards[id].tags.powered_up = value;
					break;
				default:
					return false;
			}

			return true;
		}

		public void add_card_to_zone(string card_id, string zone_name)
		{
			this.remove_card_from_zone(card_id);
			this.cards[card_id].zone_name = zone_name;

			if (zone_name == "FRIENDLY PLAY")
			{
				this.cards_board.Add(card_id);
				this.cards_minions.Add(card_id);
				this.cards_friendly.Add(card_id);
				this.cards_friendly_minions.Add(card_id);
			}
			if (zone_name == "FRIENDLY PLAY (Hero)")
			{
				this.cards_board.Add(card_id);
				this.cards_friendly.Add(card_id);
			}
			if (zone_name == "FRIENDLY HAND")
			{
				this.cards_hand.Add(card_id);
			}
			if (zone_name == "OPPOSING PLAY")
			{
				this.cards_board.Add(card_id);
				this.cards_minions.Add(card_id);
				this.cards_opposing.Add(card_id);
				this.cards_opposing_minions.Add(card_id);
			}
			if (zone_name == "OPPOSING PLAY (Hero)")
			{
				this.cards_board.Add(card_id);
				this.cards_opposing.Add(card_id);
			}
			if (zone_name == "FRIENDLY PLAY (Hero Power")
			{
				this.hero_power_id = card_id;
			}
			if (zone_name == "FRIENDLY PLAY (Weapon)")
			{
				this.weapon_id = card_id;
			}

			this.cards_board = this.cards_board.Distinct().ToList();
			this.cards_hand = this.cards_hand.Distinct().ToList();
			this.cards_minions = this.cards_minions.Distinct().ToList();
			this.cards_friendly = this.cards_friendly.Distinct().ToList();
			this.cards_opposing = this.cards_opposing.Distinct().ToList();
			this.cards_friendly_minions = this.cards_friendly_minions.Distinct().ToList();
			this.cards_opposing_minions = this.cards_opposing_minions.Distinct().ToList();
			
			if (zone_name.Contains("FRIENDLY PLAY"))
			{
				if (this.cards[card_id].tags.charge == false && !zone_name.Contains("Hero"))
				{
					this.cards[card_id].tags.exhausted = true;
				}
				Dictionary<string, deck_card> herby_deck = build_herby_deck.herby_deck();
				if (herby_deck.ContainsKey(this.cards[card_id].name))
				{
					if (herby_deck[this.cards[card_id].name].gain_aura != null)
					{
						herby_deck[this.cards[card_id].name].gain_aura(this.cards[card_id], this);
					}
				}

				foreach (string board_card_id in this.cards_friendly_minions)
				{
					card cur_card = this.cards[board_card_id];
					if (cur_card.local_id != card_id && cur_card.name != null && herby_deck.ContainsKey(cur_card.name) && herby_deck[cur_card.name].enter_aura != null)
					{
						herby_deck[cur_card.name].enter_aura(this.cards[card_id]);
					}
				}
			}
		}

		public void remove_card_from_zone(string card_id)
		{
			//temp wipe out their zone name, it'll get put back later (probably)
			if (this.cards[card_id].zone_name.Contains(" PLAY"))
			{
				if (this.cards[card_id].name != null)
				{
					Dictionary<string, deck_card> herby_deck = build_herby_deck.herby_deck();
					if (herby_deck.ContainsKey(this.cards[card_id].name))
					{
						if (herby_deck[this.cards[card_id].name].lose_aura != null)
						{
							herby_deck[this.cards[card_id].name].lose_aura(this.cards[card_id], this);
						}
					}
				}
			}

			this.cards[card_id].prev_zone_name = this.cards[card_id].zone_name;
			this.cards[card_id].zone_name = "";

			this.cards_board.Remove(card_id);
			this.cards_hand.Remove(card_id);
			this.cards_minions.Remove(card_id);
			this.cards_friendly.Remove(card_id);
			this.cards_opposing.Remove(card_id);
			this.cards_friendly_minions.Remove(card_id);
			this.cards_opposing_minions.Remove(card_id);

			/*
			this is unnecessary to keep track of
			foreach (var cur_card in this.cards.Values)
			{
				if (cur_card.zone_name == this.cards[card_id].zone_name				//if the card is in the same zone
					&& cur_card.zone_position > this.cards[card_id].zone_position	//and it's at a higher position
					&& (cur_card.zone_name.Contains("PLAY")		//and it's on the field
					|| cur_card.zone_name.Contains("HAND")))	//or in the hand
				{
					this.cards[cur_card.local_id].zone_position--;
				}
			}
			*/


		}

		public int count_cards_in_hand()
		{
			return this.cards_hand.Count();
		}

		public void minion_trade(string attacker_id, string defender_id)
		{
			if (this.cards[defender_id].deal_damage(this.cards[attacker_id].atk + this.cards[attacker_id].temp_atk))
			{
				this.remove_card_from_zone(defender_id);
			}

			if (this.cards[attacker_id].deal_damage(this.cards[defender_id].atk))
			{
				this.remove_card_from_zone(attacker_id);
			}
		}

		public void deal_damage_to_hero(int damage, bool enemy = true)
		{
			string zone = enemy ? "OPPOSING" : "FRIENDLY";
			zone += " PLAY (Hero)";

			string card_id = enemy ? this.enemy_hero_id : this.my_hero_id;

			card cur_card = this.cards[card_id];

			cur_card.deal_damage(damage);
			if (enemy)
			{
				this.enemy_health = cur_card.get_cur_health();
			}
			else
			{
				this.my_health = cur_card.get_cur_health();
			}

			return;
		}

		public int count_minions_on_field(bool enemy = false)
		{
			if (enemy)
			{
				return this.cards_opposing_minions.Count();
			}
			else
			{
				return this.cards_friendly_minions.Count();
			}
		}

		public int count_minions_in_zone(string zone)
		{
			return this.count_minions_on_field(zone == "OPPOSING PLAY");
		}

		public bool check_if_beast_in_play()
		{
			foreach (string card_id in this.cards_friendly_minions)
			{
				card cur_card = this.cards[card_id];
				if (cur_card.family == "beast")
				{
					return true;
				}
			}
			return false;
		}

		public bool check_if_secret_in_play(string secret_name)
		{
			foreach (var secret_check in this.cards.Values)
			{
				if (secret_check.zone_name == "FRIENDLY SECRET" && secret_check.name == secret_name)
				{
					//this secret is already in play, can't use it
					return true;
				}
			}
			return false;
		}

		public bool check_if_taunt_in_play(bool enemy_field = false)
		{
			List<string> check_list = (enemy_field ? this.cards_opposing_minions : this.cards_friendly_minions);

			foreach (string card_id in check_list)
			{
				card cur_card = this.cards[card_id];
				if (cur_card.tags.taunt == true && cur_card.tags.stealth == false)
				{
					return true;
				}
			}
			return false;
		}

		public bool check_if_weapon_in_play()
		{
			card weapon_check = this.cards[this.weapon_id];
			if (weapon_check.zone_name == "FRIENDLY PLAY (Weapon)")
			{
				//we already have a weapon equipped
				return true;
			}

			return false;
		}

		public int check_attack_on_board(bool enemy_field = false)
		{
			int total_atk = 0;
			int cur_atk;

			List<string> check_list = (enemy_field ? this.cards_opposing_minions : this.cards_friendly_minions);

			foreach (string card_id in check_list)
			{
				card cur_card = this.cards[card_id];
				cur_atk = cur_card.atk;
				if (cur_card.tags.windfury)
				{
					cur_atk *= 2;
				}
				if (cur_card.tags.frozen || (cur_card.tags.exhausted && !enemy_field))
				{
					cur_atk *= 0;
				}
				total_atk += cur_atk;
			}
			
			return total_atk;
		}

		public string convert_cards_to_string()
		{
			StringBuilder sb = new System.Text.StringBuilder();
			
			foreach (var cur_card in this.cards.Values)
			{
				sb.Append(cur_card.ToString());
			}
			return sb.ToString();
		}
	}
}