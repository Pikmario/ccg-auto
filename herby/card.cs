using System.Collections.Generic;
using Newtonsoft.Json;

namespace Herby
{
	public class card
	{
		public struct card_tags
		{
			public bool taunt,
						stealth,
						cant_be_targeted_by_abilities,
						charge,
						exhausted,
						divine_shield,
						frozen,
						immune,
						windfury,
						silenced,
						battlecry,
						cant_attack,
						powered_up,
						poisonous;

			public card_tags(bool nothing = true)
			{
				this.taunt = false;
				this.stealth = false;
				this.cant_be_targeted_by_abilities = false;
				this.charge = false;
				this.exhausted = true;
				this.divine_shield = false;
				this.frozen = false;
				this.immune = false;
				this.windfury = false;
				this.silenced = false;
				this.battlecry = false;
				this.cant_attack = false;
				this.powered_up = false;
				this.poisonous = false;
			}
		}

		public string local_id; //local id, changes between games
		public string card_uid; //uuid, unique to each card
		public string name; //name of card
		public string card_type; //minion, hero, etc

		public int max_health; //max health of minion
		public int armor; //armor of hero
		public int durability; //durability of weapon
		public int damage; //current damage of minion
		public int atk; //attack of minion
		public int temp_atk = 0; //attack gained from effects like abusive
		public int mana_cost = 0; //mana cost of card

		public int base_health = -1;	//how much health the minion started with
		public int base_atk = -1;	//how much attack the minion started with

		public string family = "";

		public string zone_name = "";
		public string prev_zone_name = "";
		public int zone_position;

		public card_tags tags;

		public card(string id)
		{
			this.local_id = id;
		}

		public card()
		{

		}

		public card(card cloned_card)
		{
			this.local_id = cloned_card.local_id;
			this.card_uid = cloned_card.card_uid;
			this.name = cloned_card.name;
			this.card_type = cloned_card.card_type;

			this.max_health = cloned_card.max_health;
			this.armor = cloned_card.armor;
			this.durability = cloned_card.durability;
			this.damage = cloned_card.get_damage();
			this.atk = cloned_card.atk;
			this.temp_atk = cloned_card.temp_atk;
			this.mana_cost = cloned_card.mana_cost;

			this.base_health = cloned_card.base_health;
			this.base_atk = cloned_card.base_atk;

			this.family = cloned_card.family;

			this.zone_name = cloned_card.zone_name;
			this.prev_zone_name = cloned_card.prev_zone_name;
			this.zone_position = cloned_card.zone_position;

			this.tags = cloned_card.tags;
		}

		public bool set_damage(int damage)
		{
			this.damage = damage;
			if (this.max_health - this.damage <= 0)
			{
				//this kills the minion
				this.damage = this.max_health;
				return true;
			}
			return false;
		}

		public bool deal_damage(int damage, bool is_poisonous = false)
		{
			if (this.tags.immune)
			{
				return false;
			}
			if (this.armor >= damage)
			{
				this.armor -= damage;
				return false;
			}
			damage -= this.armor;
			this.armor = 0;
			if (this.tags.divine_shield == true)
			{
				this.tags.divine_shield = false;
				return false;
			}

			if (this.card_type == "MINION" && is_poisonous)
			{
				return this.set_damage(this.get_cur_health());
			}
			
			return this.set_damage(this.get_damage() + damage);
		}

		public int get_damage()
		{
			return this.damage;
		}

		public int get_cur_health()
		{
			return this.max_health + this.armor - this.get_damage();
		}

		public void silence(board_state board_state)
		{
			this.tags.taunt = false;
			this.tags.stealth = false;
			this.tags.cant_be_targeted_by_abilities = false;
			this.tags.charge = false;
			this.tags.divine_shield = false;
			this.tags.frozen = false;
			this.tags.immune = false;
			this.tags.windfury = false;
			this.tags.silenced = true;
			this.tags.cant_attack = false;
			this.tags.poisonous = false;

			this.max_health = this.base_health;
			this.atk = this.base_atk;

			Dictionary<string, deck_card> herby_deck = build_herby_deck.herby_deck();
			if (herby_deck.ContainsKey(this.name))
			{
				if (herby_deck[this.name].lose_aura != null)
				{
					herby_deck[this.name].lose_aura(this, board_state);
				}
			}
		}

		public override string ToString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}
