using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Herby
{
	public partial class Herby : Form
	{
		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		[DllImport("user32.dll")]
		static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
		[DllImport("user32.dll")]
		static extern bool SetForegroundWindow(IntPtr hWnd);
		[DllImport("user32.dll")]
		static extern IntPtr GetForegroundWindow();
		[DllImport("user32.dll")]
		static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[DllImport("user32.dll")]
		static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

		static int WM_LBUTTONDOWN = 0x201;	//Left mousebutton down
		static int WM_LBUTTONUP = 0x202;	//Left mousebutton up
		static int WM_RBUTTONDOWN = 0x204;	//Right mousebutton down
		static int WM_RBUTTONUP = 0x205;	//Right mousebutton up

		public struct board_position
		{
			public int x, y;

			public board_position(int x, int y = 899)
			{
				this.x = x;
				this.y = y;
			}
		}

		public struct board_position_box
		{
			public int left_x, right_x, top_y, bottom_y;

			public board_position_box(int left_x, int right_x, int top_y, int bottom_y)
			{
				this.left_x = left_x;
				this.right_x = right_x;
				this.top_y = top_y;
				this.bottom_y = bottom_y;
			}
		}

		public class card_play
		{
			public List<string> moves;
			public double score;
		}

		public Dictionary<string, dynamic> board_position_boxes = new Dictionary<string, dynamic>();
		public static Dictionary<string, deck_card> herby_deck = new Dictionary<string, deck_card>();

		enum KeyModifier
		{
			None = 0,
			Alt = 1,
			Ctrl = 2,
			Shift = 4,
			WinKey = 8
		};

		public bool running = false;
		public bool opening_packs = false;
		public BackgroundWorker game_player = new BackgroundWorker();
		public BackgroundWorker pack_opener = new BackgroundWorker();
		public BackgroundWorker log_reader = new BackgroundWorker();

		IntPtr hs;
		public string my_name = "";

		public const int move_delay_default = 200;

		public static double MY_MINIONS_WEIGHT;
		public static double MY_MINIONS_HAND_WEIGHT;
		public static double MY_HERO_WEIGHT;
		public static double ENEMY_MINIONS_WEIGHT;
		public static double ENEMY_MINIONS_LETHAL_WEIGHT;
		public static double ENEMY_HERO_WEIGHT;
		public static double ATK_WEIGHT;
		public static double TAUNT_WEIGHT;
		public static double WINDFURY_WEIGHT;
		public static double FROZEN_WEIGHT;
		public static double STEALTH_WEIGHT;
		public static double STEALTH_WEIGHT_ABS;
		public static double DIVINE_SHIELD_WEIGHT;
		public static double LIFESTEAL_WEIGHT;
		public static double HIGH_PRIO_WEIGHT;

		public Random rand = new Random();

		board_state cur_board;
		//redundant since it's in the board state, but useful for seeing when it changes
		public bool is_my_turn = false;
		//also redundant, used for double checking
		public int my_hand_size = 0;
		public string last_summoned_minion;

		public int num_wins = 0;
		public int num_losses = 0;

		public string winner = "";

		public bool wipe_log = false;
		string output_log = "";

		public card_play cur_best_move;
		public bool lethal_detected = false;

		public string log_location;

		FileStream hslog_filestream;

		StreamReader hs_log_file;

		public string db_path;

		public List<string> high_prio_targets;

		public Dictionary<string, bool> hashes;

		bool debug = false;

		public List<Button> calced_move_buttons = new List<Button>();
		int move_counter = 0;

		public List<string> player_names = new List<string>();
		public string player2_name = "";

		public bool dynamic_board_positions = true;

		public bool force_focus = false;

		public Herby()
		{
			string json = File.ReadAllText("herby.json");
			Dictionary<string, dynamic> config = new Dictionary<string, dynamic>();
			try
			{
				config = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json);
			}
			catch (Exception e)
			{
				MessageBox.Show("JSON is bad:\r\n\r\n" + e.Message, "Herby Error");
				System.Environment.Exit(0);
			}
			
			get_config_settings(config);

			if (this.debug)
			{
				this.BackColor = Color.Yellow;
			}

			Control.CheckForIllegalCrossThreadCalls = false;

			InitializeComponent();

			RegisterHotKey(this.Handle, 0, (int)KeyModifier.Shift + (int)KeyModifier.Alt, Keys.Space.GetHashCode());
			RegisterHotKey(this.Handle, 1, (int)KeyModifier.Shift + (int)KeyModifier.Ctrl, Keys.Space.GetHashCode());

			if (config.ContainsKey("positions"))
			{
				this.dynamic_board_positions = false;
				init_board_positions(config["positions"]);
			}
			build_deck_options();

			this.cur_board = new board_state();

			this.log_reader.DoWork += new DoWorkEventHandler(
			delegate(object o, DoWorkEventArgs args)
			{
				while (true)
				{
					main_log_read_loop();
					Thread.Sleep(1);
				}
			});

			this.log_reader.RunWorkerAsync();

			checkBox1_CheckedChanged(new object(), new EventArgs());
		}

		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);

			if (m.Msg == 0x0312)
			{
				int id = m.WParam.ToInt32();
				if (id == 0)
				{
					//alt + shift + space
					if (running == false)
					{
						stop_opening_packs();
						start_watching();
					}
					else
					{
						stop_watching();
					}
				}
				else if (id == 1)
				{
					//ctrl + shift + space
					if (opening_packs == false)
					{
						stop_watching();
						start_opening_packs();
					}
					else
					{
						stop_opening_packs();
					}
				}
			}
		}

		void main_log_read_loop()
		{
			build_game_state();
		}

		void main_loop()
		{
			if (this.db_path.Length > 0 && File.Exists(this.db_path + "stop.flag") && !this.cur_board.game_active)
			{
				//telling herby to stop playing remotely, just stop doing stuff
				set_action_text("No action: stop.flag present");
				return;
			}

			this.cur_best_move = new card_play{moves = new List<string>{""}, score = -1000};
			
			if (!this.cur_board.game_active)
			{
				//game isn't active, just hammer the play button until a game starts
				focus_game();
				click_location(this.board_position_boxes["NEAR OPTIONS MENU"], true);
				click_location(this.board_position_boxes["PLAY BUTTON"], true);
				click_location(this.board_position_boxes["PLAY BUTTON BRAWL"], true);
				set_action_text("Waiting for game to start");
				Thread.Sleep(3000);
				return;
			}

			if (this.cur_board.game_state == "BEGIN_MULLIGAN")
			{
				if (this.cur_board.mulligan_complete == true)
				{
					//we already mulligan'd cards, just wait for the opponent
					set_action_text("Waiting for opponent mulligan");
					return;
				}
				else
				{
					//we haven't mulligan'd cards yet, mulligan them
					mulligan_card_step();
					return;
				}
			}
			
			if (this.cur_board.cur_active_player != this.cur_board.my_name)
			{
				//not currently my turn, don't bother calculating moves, the board is going to change before my turn
				set_action_text("Waiting for my turn");
				Thread.Sleep(200);
				this.is_my_turn = false;
				return;
			}
			else if (this.is_my_turn == false)
			{
				//it just became our turn, wait a few seconds before clawing at cards we can't use yet
				set_action_text("Waiting for start of turn draw");
				if (this.debug == false)
				{
					Thread.Sleep(7000);
				}
				this.is_my_turn = true;
				return;
			}
			
			if (this.my_hand_size != this.cur_board.count_cards_in_hand())
			{
				//we just played a card, or drew one, and the numbers don't match up
				//wait a short while, then recalculate
				set_action_text("Waiting for card movement");
				this.my_hand_size = this.cur_board.count_cards_in_hand();
				Thread.Sleep(1000);
				return;
			}
			
			//calculate best move based on game state and stat weights
			this.lethal_detected = false;
			
			move_counter = 0;
			
			this.hashes = new Dictionary<string, bool>();
			
			this.cur_best_move = calculate_best_move(this.cur_board, 1, 3);
			card_play best_move = this.cur_best_move;

			set_action_text("Running best move");
			if (this.debug == false)
			{
				run_best_move(best_move);
			}

			try
			{
				Console.Write("Best move: ");
				if (best_move.moves.Count() == 1)
				{
					if (this.cur_board.cards.ContainsKey(best_move.moves[0]))
					{
						Console.WriteLine(this.cur_board.cards[best_move.moves[0]].name);
					}
					else
					{
						Console.WriteLine(best_move.moves[0]);
					}
				}
				else if (best_move.moves.Count() == 2)
				{
					Console.Write(this.cur_board.cards[best_move.moves[0]].name + " > ");
					if (this.cur_board.cards.ContainsKey(best_move.moves[1]))
					{
						Console.WriteLine(this.cur_board.cards[best_move.moves[1]].name);
					}
					else
					{
						Console.WriteLine(best_move.moves[1]);
					}
				}
				else if (best_move.moves.Count() == 3)
				{
					Console.Write(this.cur_board.cards[best_move.moves[0]].name + " > ");
					Console.WriteLine(this.cur_board.cards[best_move.moves[1]].name);
				}
			}
			catch (Exception e)
			{
				Console.WriteLine("Tried to write best move but couldn't (" + e.Message + ")");
				for (int j = 0; j < 3; j++)
				{
					if (best_move.moves.Count() > j)
					{
						Console.WriteLine("Best move " + (j + 1) + ": " + best_move.moves[j]);
					}
				}
			}
						
			//wait a short while for animation to play and interactivity to return
			set_action_text("Waiting for animation");
			int wait_time = this.rand.Next(1200, 1400);
			if (best_move.moves.Count() == 1 || (best_move.moves.Count() == 2 && best_move.moves[1].All(char.IsDigit) && this.last_summoned_minion != best_move.moves[0]))
			{
				wait_time -= 600;
			}

			foreach (string card_id in this.cur_board.cards_minions)
			{
				card cur_card = this.cur_board.cards[card_id];
				if (cur_card.name == "Lorewalker Cho" && ((best_move.moves.Count() == 2 && best_move.moves[1] == "spell") || (best_move.moves.Count() == 3 && best_move.moves[2] == "spell")))
				{
					set_action_text("Waiting for Cho's animation (fucker)");
					wait_time += 3000;
				}
			}

			Thread.Sleep(wait_time);
		}

		public void set_status_text(string text)
		{
			if (status.Text != text)
			{
				status.Text = text;
			}
		}

		public void set_action_text(string text)
		{
			//if (action_display.Text != text)
			{
				action_display.Text = text;
			}
		}

		public void focus_game()
		{
			if (GetForegroundWindow() != this.hs)
			{
				SetForegroundWindow(this.hs);
			}
		}

		void mulligan_card_step()
		{
			//but also we might not even be in the game yet
			//wait a while seconds before doing anything
			if (this.cur_board.wait_for_mulligan)
			{
				set_action_text("Game started, waiting for mulligan");
				Thread.Sleep(18000);
			}
			int mulligan_board = (this.cur_board.count_cards_in_hand() == 3 ? 0 : 1);
			set_action_text("Mulliganing cards");
			foreach (string card_id in this.cur_board.cards_hand)
			{
				//find all cards in hand with a mana cost above 4 or below 1, toss them
				card cur_card = this.cur_board.cards[card_id];
				if (cur_card.name != "The Coin" && (cur_card.mana_cost > 4 || cur_card.mana_cost < 1) && cur_card.name != "Corridor Creeper" && cur_card.name != "Lesser Emerald Spellstone")
				{
					try
					{
						click_location(this.board_position_boxes["MULLIGAN"][mulligan_board][cur_card.zone_position - 1], true, 250);
					}
					catch
					{
						this.cur_board.wait_for_mulligan = false;
						return;
					}
				}
			}

			//also throw away any 1 drops that wouldn't be played on turn 1
			List<card_play> possible_plays = get_possible_plays(this.cur_board);

			for (int i = 0; i < possible_plays.Count(); i++)
			{
				//simulate this play
				List<board_state> simmed_boards = simulate_board_state(new board_state(this.cur_board), possible_plays[i]);

				board_state simmed_board = simmed_boards[0];

				//get this simulated play's score
				double score_before = calculate_board_value(this.cur_board);
				double score_after = calculate_board_value(simmed_board);
				double total_score = score_after - score_before;

				if (total_score < 0)
				{
					try
					{
						click_location(this.board_position_boxes["MULLIGAN"][mulligan_board][this.cur_board.cards[possible_plays[i].moves[0]].zone_position - 1], true, 250);
					}
					catch
					{

					}
				}
			}

			//now get rid of 1 drops that can't be played (no targets)
			foreach (string card_id in this.cur_board.cards_hand)
			{
				if (this.cur_board.cards[card_id].mana_cost == 1)
				{
					bool can_play = false;
					for (int i = 0; i < possible_plays.Count(); i++)
					{
						if (possible_plays[i].moves[0] == card_id)
						{
							can_play = true;
							break;
						}
					}

					if (!can_play)
					{
						try
						{
							click_location(this.board_position_boxes["MULLIGAN"][mulligan_board][this.cur_board.cards[card_id].zone_position - 1], true, 250);
						}
						catch
						{

						}
					}
				}
			}

			//click confirm then wait 2 seconds
			focus_game();
			click_location(this.board_position_boxes["CONFIRM MULLIGAN"], true, 250);
			Thread.Sleep(2000);
		}

		/// <summary>
		/// Takes a board state, parses the log file (global),
		/// and readjusts the board state based on the data inside
		/// </summary>
		/// <returns>Adjusted board state</returns>
		void build_game_state()
		{
			try
			{
				bool creating_card = false;
				bool updating_card = false;
				string line = "";
				string cur_id = "0";
				string cur_option_id = "";
				string cur_target_id = "";

				this.hslog_filestream = new FileStream(
					this.log_location,
					FileMode.Open,
					FileAccess.ReadWrite,
					FileShare.ReadWrite);
				
				this.hs_log_file = new StreamReader(hslog_filestream);

				if (chk_copy_log.Checked && this.cur_board.my_name != "" && this.cur_board.enemy_name != "")
				{
					File.Copy(log_location, log_location.Replace("output_log.txt", "output_log_" + this.cur_board.my_name + "_" + this.cur_board.enemy_name + ".txt"), true);
				}

				StringBuilder modded_log_contents = new StringBuilder();
				
				board_state log_state = new board_state();

				this.player_names = new List<string>();
				this.player2_name = "";

				while ((line = this.hs_log_file.ReadLine()) != null)
				{
					if (line.Trim() == "")
					{
						continue;
					}
					if (!line.StartsWith("["))
					{
						continue;
					}

					modded_log_contents.AppendLine(line);
					
					if (line.Contains("FINAL_GAMEOVER"))
					{
						log_state.game_active = false;
					}
					if (line.Contains("CREATE_GAME") && log_state.game_active == false)
					{
						//game is starting
						log_state = new board_state();
						log_state.game_active = true;
						//log_state.my_name = this.my_name;
						continue;
					}
					
					if (line.Contains("RegisterScreenEndOfGame"))
					{
						//game is ending
						if (this.cur_board.game_active == false)
						{
							//actual game isn't currently active, wipe out the log file
							this.wipe_log = true;
						}

						log_state.game_active = false;
						if (this.cur_board.my_name == this.winner)
						{
							this.num_wins++;
							this.num_wins_text.Text = num_wins.ToString();
						}
						else
						{
							this.num_losses++;
							this.num_losses_text.Text = num_losses.ToString();
						}

						this.log_games("win");
						this.log_games("lose");
						if (this.kill_losses_spinner.Value > 0 && num_losses >= this.kill_losses_spinner.Value)
						{
							quit_game();
							quit_herby();
						}
						
						if (this.kill_wins_spinner.Value > 0 && num_wins >= this.kill_wins_spinner.Value)
						{
							quit_game();
							quit_herby();
						}
						
						continue;
					}

					if (line.Contains("goldRewardState = ALREADY_CAPPED") && this.running && this.kill_wins_spinner.Value == 0)
					{
						//we hit the gold cap, wee
						//quit the game, we've got nothing to gain by continuing
						quit_game();
						quit_herby();
					}

					if (log_state.game_active == false)
					{
						continue;
					}
					if (line.Contains("PrepareHistoryForCurrentTaskList") || !line.Contains("tag=") || line.Contains("TAG_CHANGE"))
					{
						creating_card = false;
						updating_card = false;
					}
					if (line.Contains("FULL_ENTITY - Creating"))
					{
						//making a new card
						//multistep process, keep track of which we're modifying
						string id_string = get_line_value(line, "ID");
						cur_id = id_string;
						creating_card = true;
						log_state.add_card(cur_id);
						string card_uid = get_line_value(line, "CardID");
						if (card_uid.Length > 0)
						{
							log_state.change_card_prop(cur_id, "card_uid", card_uid);
						}
						continue;
					}
					if (line.Contains("SHOW_ENTITY - Updating") || line.Contains("FULL_ENTITY - Updating") || line.Contains("CHANGE_ENTITY"))
					{
						//updating a card that was previously created without knowing what it was
						//for cards that fell into the deck or brand new created cards
						updating_card = true;
						string id_string = get_line_value(line, "id");
						if (id_string.Length == 0)
						{
							//card was just created, following the format of Entity={id}
							id_string = get_line_value(line, "Entity");
						}
						cur_id = id_string;
						string card_uid = get_line_value(line, "CardID");
						if (card_uid.Length > 0)
						{
							log_state.change_card_prop(cur_id, "card_uid", card_uid);
						}

						string card_name = get_card_name(line);
						if (card_name.Length > 0)
						{
							log_state.cards[cur_id].name = card_name;
						}
						continue;
					}
					if (line.Contains("tag=") && (creating_card || updating_card))
					{
						//lines following a FULL_ENTITY line
						//setting the props and tags for the card just created
						string cur_tag = get_line_value(line, "tag");
						string cur_value = get_line_value(line, "value");
						if (log_state.change_card_prop(cur_id, cur_tag, cur_value) == false)
						{
							log_state.change_card_tag(cur_id, cur_tag, cur_value == "1");
						}

						if (log_state.cards[cur_id].name != null)
						{
							if (herby_deck.ContainsKey(log_state.cards[cur_id].name))
							{
								if (herby_deck[log_state.cards[cur_id].name].family != null)
								{
									log_state.cards[cur_id].family = herby_deck[log_state.cards[cur_id].name].family;
								}
							}
						}

						continue;
					}

					if (line.Contains("tag="))
					{
						if (line.Contains("PowerTaskList") && get_line_value(line, "tag") == "PLAYSTATE")
						{
							//keep track of the player names to figure out which player is which later
							string player_name = get_line_value(line, "Entity");

							if (this.player_names.Count() < 2)
							{
								//player 1 at [0], player 2 at [1]
								this.player_names.Add(player_name);
							}
						}

						if (get_line_value(line, "tag") == "NUM_CARDS_DRAWN_THIS_TURN" && log_state.my_name == "" && log_state.enemy_name == "")
						{
							//looking at the first instances of card draw this game
							//keep track of which player is going second, by virtue of having drawn 4 cards to start
							string player_name = get_line_value(line, "Entity");

							if (!this.player_names.Contains(player_name))
							{
								//it's not either name, skip this line
								continue;
							}

							if (get_line_value(line, "value") == "4")
							{
								player2_name = player_name;
							}
						}
					}

					if (line.Contains("DebugPrintPower") && line.Contains(" TAG_CHANGE"))
					{
						string card_id = get_line_value(line, "id");
						if (card_id.Length == 0)
						{
							card_id = get_line_value(line, "Entity");
						}

						if (card_id.Length == 0)
						{
							//can't find a card in this tag_change line, skip it
							continue;
						}
					
						//get the tag the line is editing and its value
						string tag = get_line_value(line, "tag");
						string tag_value = get_line_value(line, "value");
						//first check to see if it's damage
						if (tag == "IGNORE_DAMAGE" && tag_value == "1")
						{
							//using something to set their hp to 1, need to set damage to 0
							log_state.cards[card_id].set_damage(0);
						}
						else if (tag == "DAMAGE")
						{
							//damage requires a different method call than other properties
							log_state.cards[card_id].set_damage(Int32.Parse(tag_value));
							if (get_line_value(line, "id") == log_state.enemy_hero_id)
							{
								log_state.enemy_health = log_state.cards[card_id].max_health - Int32.Parse(tag_value);
							}
							if (get_line_value(line, "id") == log_state.my_hero_id)
							{
								log_state.my_health = log_state.cards[card_id].max_health - Int32.Parse(tag_value);
							}
						}
						else
						{
							if (log_state.change_card_prop(card_id, tag, tag_value) == false)
							{
								log_state.change_card_tag(card_id, tag, tag_value == "1");
							}
						}

						string player_name = get_line_value(line, "Entity");

						if (tag == "RESOURCES")
						{
							if (player_name == log_state.my_name)
							{
								log_state.max_mana = Int32.Parse(get_line_value(line, "value"));
								log_state.cur_mana = log_state.max_mana - log_state.mana_used;
							}
						}
						else if (tag == "RESOURCES_USED")
						{
							if (player_name == log_state.my_name)
							{
								log_state.mana_used = Int32.Parse(get_line_value(line, "value"));
								log_state.cur_mana = log_state.max_mana - log_state.mana_used;
							}
						}
						else if (tag == "TEMP_RESOURCES")
						{
							if (player_name == log_state.my_name)
							{
								log_state.cur_mana = log_state.max_mana - log_state.mana_used + Int32.Parse(get_line_value(line, "value"));
							}
						}
						else if (tag == "CURRENT_PLAYER")
						{
							if (get_line_value(line, "value") == "1")
							{
								log_state.cur_active_player = player_name;
							}
						}
						else if (tag == "STEP")
						{
							string value = get_line_value(line, "value");
							log_state.game_state = value;

							if (value == "BEGIN_MULLIGAN" && log_state.count_cards_in_hand() > 0)
							{
								log_state.name_ready = true;
							}
						}
						else if (tag == "MULLIGAN_STATE")
						{
							if (player_name == log_state.my_name && get_line_value(line, "value") == "DONE")
							{
								log_state.mulligan_complete = true;
							}
						}
						else if (tag == "PLAYSTATE")
						{
							if (get_line_value(line, "value") == "WON")
							{
								this.winner = player_name;
							}
						}
						else if (tag == "HERO_ENTITY")
						{
							//replacing the hero with a new one (jaraxxus or ragnaros)
							//just set its zone as (Hero), the old hero will be moved automatically
							string new_hero_id = get_line_value(line, "value");
							if (player_name == log_state.my_name)
							{
								log_state.cards[new_hero_id].zone_name = "FRIENDLY PLAY (Hero)";
								log_state.my_hero_id = new_hero_id;
							}
							else
							{
								log_state.cards[new_hero_id].zone_name = "OPPOSING PLAY (Hero)";
								log_state.enemy_hero_id = new_hero_id;
							}
						}

						continue;
					}
					if (line.Contains("TRANSITIONING card"))
					{
						string[] zone_split = line.Split(new string[] {" to "}, 0);
						
						cur_id = get_line_value(line, "id");

						string card_name = get_card_name(line);
						if (card_name.Length > 0)
						{
							log_state.cards[cur_id].name = card_name;
						}
						log_state.add_card_to_zone(cur_id, zone_split[1]);

						if (zone_split[1] == "FRIENDLY PLAY (Hero)")
						{
							log_state.my_hero_id = get_line_value(line, "id");
						}
						else if (zone_split[1] == "OPPOSING PLAY (Hero)")
						{
							log_state.enemy_hero_id = get_line_value(line, "id");
						}
						else if (zone_split[1] == "FRIENDLY PLAY (Hero Power)")
						{
							log_state.hero_power_id = get_line_value(line, "id");
						}
					}

					if (line.Contains("option") || line.Contains("target"))
					{
						if (line.Contains("option 0"))
						{
							//fresh list of options, wipe out the old list
							log_state.legal_moves = new Dictionary<string, List<string>>();
						}
						if (line.Contains("option"))
						{
							cur_option_id = get_line_value(line, "id");
							if (get_line_value(line, "error") == "NONE")
							{
								log_state.legal_moves[cur_option_id] = new List<string>();
							}
						}
						else if (line.Contains("target"))
						{
							cur_target_id = get_line_value(line, "id");
							if (get_line_value(line, "error") == "NONE")
							{
								log_state.legal_moves[cur_option_id].Add(cur_target_id);
							}
						}
					}

					if (log_state.cur_mana == 1 && log_state.my_name == "" && log_state.enemy_name == "" && log_state.name_ready == true)
					{
						if (log_state.count_cards_in_hand() > 4)
						{
							log_state.my_name = player2_name;

							foreach (string player_name in this.player_names)
							{
								if (player_name != log_state.my_name)
								{
									log_state.enemy_name = player_name;
								}
							}
						}
						else if (log_state.count_cards_in_hand() < 5)
						{
							log_state.enemy_name = player2_name;

							foreach (string player_name in this.player_names)
							{
								if (player_name != log_state.enemy_name)
								{
									log_state.my_name = player_name;
								}
							}
						}
					}
				}

				if (this.wipe_log && this.cur_board.game_active == false)
				{
					hs_log_file.BaseStream.SetLength(0);
					this.wipe_log = false;
					log_state.my_name = "";
					log_state.enemy_name = "";

					this.player_names = new List<string>();
					this.player2_name = "";
				}

				if (chk_view_log.Checked)
				{
					if (this.output_log != modded_log_contents.ToString())
					{
						this.output_log = modded_log_contents.ToString();
						log_output.Text = output_log;
						log_output.SelectionStart = log_output.TextLength;
						log_output.ScrollToCaret();
					}
				}

				this.cur_board = log_state;
				
				if (this.cur_board.game_active && this.db_path.Length > 0 && !File.Exists(this.db_path + "running"))
				{
					File.Create(this.db_path + "running");
				}
				else if (!this.cur_board.game_active && this.db_path.Length > 0 && File.Exists(this.db_path + "running"))
				{
					File.Delete(this.db_path + "running");
				}

				this.hs_log_file.BaseStream.Position = 0;

				this.x_coord.Text = Cursor.Position.X.ToString();
				this.y_coord.Text = Cursor.Position.Y.ToString();
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				return;
			}
		}

		card_play calculate_best_move(board_state board, int cur_depth = 1, int max_depth = 3)
		{
			//first get all possible plays for this board state
			board_state plays_board_state;
			if (cur_depth == 1)
			{
				plays_board_state = board;
			}
			else
			{
				plays_board_state = new board_state(board);
			}
			List<card_play> possible_plays = get_possible_plays(plays_board_state);
			
			if (cur_depth == 1)
			{
				max_depth = (int)Math.Ceiling(Math.Log(200/possible_plays.Count(), 2));
			}

			//possible_plays = ShuffleList(possible_plays);
			
			card_play best_play = new card_play{moves = new List<string>{""}, score = -1000};

			//then for each possible play, see which yields the best board state
			//recurse until max_depth has been reached

			move_counter += possible_plays.Count;
			double score_before = calculate_board_value(board);
			for (int i = 0; i < possible_plays.Count; i++)
			{
				List<board_state> simulated_board_states = simulate_board_state(new board_state(board), possible_plays[i]);
				for (int j = 0; j < simulated_board_states.Count; j++)
				{
					board_state simulated_board_state = simulated_board_states[j];
					string hashed_board_state = md5(simulated_board_state.convert_cards_to_string());
					if (this.hashes.ContainsKey(hashed_board_state))
					{
						//we've already calculated this identical board state, throw it out
						goto possible_plays_loop;
					}

					double move_counter_display;

					if (move_counter > 100)
					{
						move_counter_display = Math.Round(move_counter / 100d) * 100;
					}
					else
					{
						move_counter_display = move_counter;
					}

					set_action_text("Calculating best move\r\n" + move_counter_display + " total moves found");
					
					hashes[hashed_board_state] = true;
					double score_after = calculate_board_value(simulated_board_state);
					double board_score = score_after - score_before;

					possible_plays[i].score += board_score;
				
					if (cur_depth < max_depth && possible_plays[i].moves[0] != "END TURN")
					{
						if (this.lethal_detected == false)
						{
							card_play best_lower_action = calculate_best_move(new board_state(simulated_board_state), cur_depth + 1, max_depth);
					
							possible_plays[i].score += best_lower_action.score;
						}
					}
				}

				possible_plays[i].score /= simulated_board_states.Count;

				if (possible_plays[i].score > best_play.score ||	//if calc'd score has a higher score than current
					(possible_plays[i].score == best_play.score && possible_plays[i].moves[0] == "END TURN"))	//all things equal, just pass turn
				{
					best_play = possible_plays[i];
				}
				
				//there should be a chance to to another move which scores the same
				//only if the best scoring move isn't end turn, of course
				if (possible_plays[i].score == best_play.score && best_play.moves[0].Count() != 1)
				{
					if (this.rand.Next(0, 2) == 1)
					{
						best_play = possible_plays[i];
					}
				}
				if (best_play.score > 8000)
				{
					//i have lethal, don't bother calculating any other options
					this.lethal_detected = true;
					return best_play;
				}
				
				possible_plays_loop: ;
			}
			
			return best_play;
		}

		List<card_play> get_possible_plays(board_state board)
		{
			List<card_play> possible_plays = new List<card_play>();

			//look at all the cards in hand that we can play
			foreach (string card_id in board.cards_hand)
			{
				card cur_card = board.cards[card_id];
				if (cur_card.mana_cost > board.cur_mana)
				{
					//card costs more mana than we have
					continue;
				}

				//only cards we know about can be used, else unpredictable things will occur
				if (cur_card.card_type == "MINION" && board.count_minions_on_field() < 7)
				{
					//if it's a minion, just add a thing for playing it
					if (cur_card.tags.battlecry == false)
					{
						//minion has no battlecry, most likely safe to play
						possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, "minion" } });
					}
					else
					{
						//minion has a battlecry, need to make sure we have logic for it
						if (herby_deck.ContainsKey(cur_card.name))
						{
							if (herby_deck[cur_card.name].target == "none")
							{
								//targetless battlecry, just add it in
								possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, "minion" } });
							}
							else
							{
								List<string> search_list = new List<string>();
								switch (herby_deck[cur_card.name].target)
								{
									case "minion":
										search_list = board.cards_minions;
										break;
									case "enemy":
										search_list = board.cards_opposing;
										break;
									case "enemy_minion":
										search_list = board.cards_opposing_minions;
										break;
									case "friendly":
										search_list = board.cards_friendly;
										break;
									case "friendly_minion":
										search_list = board.cards_friendly_minions;
										break;
									default:
										break;
								}

								foreach (string target_id in search_list)
								{
									card cur_target = board.cards[target_id];
									if (cur_target.tags.stealth == false || cur_target.zone_name.Contains("FRIENDLY"))
									{
										possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_target.local_id, "minion" } });
									}
								}
							}
						}
						else
						{
							//this card isn't defined, log it
							log_missing_card(cur_card.name);
						}
					}
				}
				else if (cur_card.card_type == "ABILITY" || cur_card.card_type == "SPELL")
				{
					//spell, make sure we have it defined
					if (herby_deck.ContainsKey(cur_card.name))
					{
						//card is a spell, figure out where to throw it
						if (herby_deck[cur_card.name].target != "none")
						{
							//spell has an explicit target, figure out who
							List<string> search_list = new List<string>();
							switch (herby_deck[cur_card.name].target)
							{
								case "minion":
									search_list = board.cards_minions;
									break;
								case "enemy":
									search_list = board.cards_opposing;
									break;
								case "enemy_minion":
									search_list = board.cards_opposing_minions;
									break;
								case "friendly":
									search_list = board.cards_friendly;
									break;
								case "friendly_minion":
									search_list = board.cards_friendly_minions;
									break;
								default:
									break;
							}

							foreach (string target_id in search_list)
							{
								card cur_target = board.cards[target_id];
								if (((cur_target.tags.stealth == false && cur_target.tags.immune == false) || cur_target.zone_name.Contains("FRIENDLY"))&& cur_target.tags.cant_be_targeted_by_abilities == false)
								{
									possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_target.local_id, "spell" } });
								}
							}
						}
						else
						{
							//spell has no target, either does a random target, goes for face, or summons something
							possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, "spell" } });
						}
					}
					else if (cur_card.tags.secret)
					{
						//card is a secret, mark it as playable if another one of the same name isn't in play
						if (board.check_if_secret_in_play(cur_card.name) == true || board.cards_secrets.Count() >= 5)
						{
							//this secret is already in play or we're full on secrets, can't use it
							continue;
						}

						possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, "secret" } });
					}
					else
					{
						//this card isn't defined, log it
						log_missing_card(cur_card.name);
					}
				}
				else if (cur_card.card_type == "WEAPON")
				{
					//playing a weapon, make sure we don't already have one we're about to destroy
					if (board.check_if_weapon_in_play() == true)
					{
						//already have a weapon, don't play this weapon
						continue;
					}

					possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, "weapon" } });
				}
			}

			//get every combination of attacks, from my minions to their minions and their hero
			//but before that, check if their field has taunts
			bool enemy_has_taunt = board.check_if_taunt_in_play(true);

			int total_atk = board.check_attack_on_board(true);
			int my_atk = board.check_attack_on_board(false);
			
			//now loop through all of our cards that can attack
			foreach (string card_id in board.cards_friendly)
			{
				card cur_card = board.cards[card_id];
				if (cur_card.tags.exhausted == false && cur_card.tags.frozen == false && cur_card.tags.cant_attack == false)
				{
					//check if the current card even has attack
					if (cur_card.atk <= 0)
					{
						//no attack on this dude, skip him
						continue;
					}

					//this is our minion, and it can attack
					//now loop through all the enemy minions, and the enemy hero
					//first, the enemy hero
					if (!enemy_has_taunt && board.cards[board.enemy_hero_id].tags.immune == false && !(cur_card.tags.rush == true && cur_card.tags.just_played == true))
					{
						possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, board.enemy_hero_id } });
					}

					//then all the minions
					foreach (string target_id in board.cards_opposing_minions)
					{
						card cur_enemy = board.cards[target_id];
						if (enemy_has_taunt)
						{
							//one or more taunt minions, only look for them
							if (cur_enemy.tags.taunt == true && cur_enemy.tags.stealth == false && cur_enemy.tags.immune == false)
							{
								possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_enemy.local_id } });
							}
						}
						else
						{
							//no taunts, target everything
							//no don't target everything, this is way too fucking slow, and unnecessary (do me trade? NOPE)
							//instead target everything in our high priority list or with a really high attack or that can kill our deathrattle minion
							//also target everything if enemy has lethal
							//but only target enemy face if i have lethal

							if (my_atk < board.enemy_health)
							{
								if
								(
								cur_enemy.tags.stealth == false
								&& cur_enemy.tags.immune == false
								&& (
									(this.high_prio_targets.Contains(cur_enemy.name) && cur_enemy.tags.silenced == false)
									|| cur_enemy.atk >= 4
									|| (herby_deck.ContainsKey(cur_card.name) && cur_enemy.atk >= cur_card.get_cur_health() && herby_deck[cur_card.name].deathrattle != null)
									|| total_atk >= board.my_health
									)
								)
								{
									possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_enemy.local_id } });
								}
							}
						}
					}
				}
			}

			//finally, end turn and hero power
			if (board.hero_power_id != null)
			{
				card hero_power = board.cards[board.hero_power_id];
				if (hero_power.mana_cost <= board.cur_mana && !hero_power.tags.exhausted && board.enemy_hero_id != null && board.cards[board.enemy_hero_id].tags.immune == false)
				{
					possible_plays.Add(new card_play { moves = new List<string> { board.hero_power_id } });
				}
			}

			//compare my list of possible plays with the game's list of allowable moves
			if (board.legal_moves.Count > 0)
			{
				for (int i = 0 ; i < possible_plays.Count(); i++)
				{
					if (!board.legal_moves.ContainsKey(possible_plays[i].moves[0]))
					{
						//play isn't in list of allowed moves
						possible_plays.RemoveAt(i--);
						continue;
					}

					if (possible_plays[i].moves.Count() == 1)
					{
						//hero power
					}
					else if (possible_plays[i].moves.Count() == 2)
					{
						//minion trade, minion summon, or targetless spell
						if (!board.cards.ContainsKey(possible_plays[i].moves[1]))
						{
							//target isn't a minion or hero, it's an untargeted spell or summoning a minion
							//this was already handled above
							continue;
						}
						else
						{
							//target is a minion or hero
							if (!board.legal_moves[possible_plays[i].moves[0]].Contains(possible_plays[i].moves[1]))
							{
								//target isn't in list of allowed targets
								possible_plays.RemoveAt(i--);
							}
						}
					}
					else if (possible_plays[i].moves.Count() == 3)
					{
						//targeted spell or battlecry
						if (!board.legal_moves[possible_plays[i].moves[0]].Contains(possible_plays[i].moves[1]))
						{
							//target isn't in list of allowed targets
							possible_plays.RemoveAt(i--);
						}
					}
				}
			}

			possible_plays.Add(new card_play { moves = new List<string> { "END TURN" } });

			//now remove all the duplicates
			for (int i = possible_plays.Count - 1; i >= 0; i--)
			{
				if (possible_plays.Count <= i)
				{
					//a bunch have been removed, skip this iteration
					continue;
				}

				card_play play = possible_plays[i];
				if (play.moves.Count == 1)
				{
					//hero power or end turn, nothing to compare this to
					continue;
				}
				
				var this_card = board.cards[play.moves[0]];

				for (int j = possible_plays.Count - 1; j >= 0; j--)
				{
					card_play comp_play = possible_plays[j];
					if (comp_play.moves.Count == 1)
					{
						//also hero power or end turn, gtfo
						continue;
					}

					if (comp_play.moves[0] == play.moves[0])
					{
						//we're looking at the same move, this would be silly to remove
						continue;
					}

					var compare_card = board.cards[comp_play.moves[0]];
					
					if (this_card.name == compare_card.name)
					{
						//looking at a card with the same name
						//now check attack and health
						if (herby_deck.ContainsKey(this_card.name))
						{
							if (herby_deck[this_card.name].type == "spell" || herby_deck[this_card.name].type == "secret" || herby_deck[this_card.name].type == "weapon" || (herby_deck[this_card.name].type == "minion" && this_card.atk == compare_card.atk && this_card.get_cur_health() == compare_card.get_cur_health()))
							{
								//same health and attack, or we're using a spell/secret/weapon
								//now check their target
								if (play.moves[1] == comp_play.moves[1])
								{
									//targets are the same, remove the compared play
									possible_plays.Remove(comp_play);
								}
							}
						}
					}
				}
			}
			
			return possible_plays;
		}

		List<board_state> simulate_board_state(board_state simmed_board, card_play action)
		{
			List<board_state> simmed_boards = new List<board_state> { simmed_board };
			if (action.moves.Count() == 1)
			{
				//super simple. either end turn or hero power
				if (action.moves[0] == "END TURN")
				{
					//NOTHIIIIIIING
					//do something stupid that won't affect anything to make sure the hash for END TURN is different
					if (simmed_board.my_hero_id != null)
					{
						simmed_board.cards[simmed_board.my_hero_id].tags.powered_up = true;
					}
				}
				else
				{
					//only other action that has a single play is the hero power
					simmed_board.deal_damage_to_hero(2);
					simmed_board.cur_mana -= simmed_board.cards[action.moves[0]].mana_cost;
					simmed_board.cards[action.moves[0]].tags.exhausted = true;

					//run the inspire code for each minion on my field that has it
					foreach (string card_id in simmed_board.cards_friendly_minions)
					{
						card cur_card = simmed_board.cards[card_id];
						if (herby_deck.ContainsKey(cur_card.name) && herby_deck[cur_card.name].inspire != null && !cur_card.tags.silenced)
						{
							herby_deck[cur_card.name].inspire(cur_card, simmed_boards);
						}
					}
				}
			}
			else if (action.moves.Count() == 2)
			{
				//two stage action
				//this is attacking with a minion, summoning one without a battlecry, or a targetless spell
				if (action.moves[1].All(char.IsDigit))
				{
					//from card to card, this is a minion attacking
					if (simmed_board.cards[action.moves[1]].zone_name == "OPPOSING PLAY")
					{
						//minion attacking another minion
						simmed_board.minion_trade(action.moves[0], action.moves[1]);
						if (simmed_board.cards[action.moves[0]].get_cur_health() <= 0 && herby_deck.ContainsKey(simmed_board.cards[action.moves[0]].name) && herby_deck[simmed_board.cards[action.moves[0]].name].deathrattle != null && !simmed_board.cards[action.moves[0]].tags.silenced)
						{
							//this card has a deathrattle and it's not silenced, run it
							herby_deck[simmed_board.cards[action.moves[0]].name].deathrattle(simmed_board.cards[action.moves[0]], simmed_boards);
						}

						if (simmed_board.my_hero_id == action.moves[0])
						{
							//my hero is attacking, lower the durability of his weapon
							card card = simmed_board.cards[simmed_board.weapon_id];
							{
								if (card.zone_name == "FRIENDLY PLAY (Weapon)")
								{
									if (--card.durability == 0)
									{
										simmed_board.remove_card_from_zone(card.local_id);
									}
								}
							}
						}
					}
					else if (simmed_board.cards[action.moves[1]].zone_name == "OPPOSING PLAY (Hero)")
					{
						//me hit face? (YUP)
						simmed_board.deal_damage_to_hero(simmed_board.cards[action.moves[0]].atk + simmed_board.cards[action.moves[0]].temp_atk);
					}
					simmed_board.cards[action.moves[0]].tags.exhausted = true;
					simmed_board.cards[action.moves[0]].tags.stealth = false;
				}
				else
				{
					//summoning a minion or casting a targetless spell
					if (action.moves[1] == "minion")
					{
						//summoning a simple minion
						simmed_board.add_card_to_zone(action.moves[0], "FRIENDLY PLAY");
						if (simmed_board.cards[action.moves[0]].tags.charge == false && simmed_board.cards[action.moves[0]].tags.rush == false)
						{
							simmed_board.cards[action.moves[0]].tags.exhausted = true;
						}
						else
						{
							simmed_board.cards[action.moves[0]].tags.exhausted = false;
						}
						
						if (herby_deck.ContainsKey(simmed_board.cards[action.moves[0]].name))
						{
							//check if minion's got a battlecry, and run it
							if (herby_deck[simmed_board.cards[action.moves[0]].name].battlecry != null)
							{
								herby_deck[simmed_board.cards[action.moves[0]].name].battlecry(simmed_board.cards[action.moves[0]], simmed_boards, new card());
							}
						}

						if (simmed_board.cards[action.moves[0]].tags.echo == true)
						{
							//echo card, put a copy of this card into the hand
							card echo_card = new card(simmed_board.cards[action.moves[0]]);
							
							echo_card.local_id = "ECHO_" + echo_card.local_id;
							simmed_board.add_card(echo_card.local_id, echo_card);
							simmed_board.add_card_to_zone(echo_card.local_id, "FRIENDLY HAND");
						}

						simmed_board.cards[action.moves[0]].tags.just_played = true;
					}
					else if (action.moves[1] == "spell")
					{
						//casting a spell without a target
						herby_deck[simmed_board.cards[action.moves[0]].name].effect(simmed_board.cards[action.moves[0]], simmed_boards, new card());
						simmed_board.remove_card_from_zone(action.moves[0]);
					}
					else if (action.moves[1] == "secret")
					{
						//screw trying to figure out secrets, just play them if there's nothing else to play
						simmed_board.add_card_to_zone(action.moves[0], "FRIENDLY SECRET");
					}
					else if (action.moves[1] == "weapon")
					{
						simmed_board.cards[simmed_board.my_hero_id].atk = simmed_board.cards[action.moves[0]].atk;
						simmed_board.add_card_to_zone(action.moves[0], "FRIENDLY PLAY (Weapon)");

						if (herby_deck.ContainsKey(simmed_board.cards[action.moves[0]].name))
						{
							if (herby_deck[simmed_board.cards[action.moves[0]].name].battlecry != null)
							{
								herby_deck[simmed_board.cards[action.moves[0]].name].battlecry(simmed_board.cards[action.moves[0]], simmed_boards, new card());
							}
						}
					}

					for (int i = 0; i < simmed_boards.Count; i++)
					{
						simmed_boards[i].cur_mana -= simmed_boards[i].cards[action.moves[0]].mana_cost;
					}
				}
			}
			else if (action.moves.Count() == 3)
			{
				//this is a spell with a target, or a minion with a targetted battlecry
				if (action.moves[2] == "minion")
				{
					//summoning a minion
					simmed_board.add_card_to_zone(action.moves[0], "FRIENDLY PLAY");
					if (simmed_board.cards[action.moves[0]].tags.charge == false && simmed_board.cards[action.moves[0]].tags.rush == false)
					{
						simmed_board.cards[action.moves[0]].tags.exhausted = true;
					}
					else
					{
						simmed_board.cards[action.moves[0]].tags.exhausted = false;
					}

					herby_deck[simmed_board.cards[action.moves[0]].name].battlecry(simmed_board.cards[action.moves[0]], simmed_boards, simmed_board.cards[action.moves[1]]);

					simmed_board.cards[action.moves[0]].tags.just_played = true;
				}
				else if (action.moves[2] == "spell")
				{
					//casting a spell
					simmed_board.remove_card_from_zone(action.moves[0]);
					herby_deck[simmed_board.cards[action.moves[0]].name].effect(simmed_board.cards[action.moves[0]], simmed_boards, simmed_board.cards[action.moves[1]]);
				}

				for (int i = 0; i < simmed_boards.Count; i++)
				{
					simmed_boards[i].cur_mana -= simmed_boards[i].cards[action.moves[0]].mana_cost;
				}
			}

			if ((action.moves.Count() == 2 || action.moves.Count() == 3) && action.moves[1].All(char.IsDigit))
			{
				//remove enemy minions from play if the thing i did just killed them
				if (simmed_board.cards[action.moves[1]].get_cur_health() <= 0)
				{
					simmed_board.remove_card_from_zone(action.moves[1]);
				}

				//check for enemy deathrattles
				if (simmed_board.cards[action.moves[1]].get_cur_health() <= 0 && herby_deck.ContainsKey(simmed_board.cards[action.moves[1]].name) && herby_deck[simmed_board.cards[action.moves[1]].name].deathrattle != null && !simmed_board.cards[action.moves[1]].tags.silenced)
				{
					herby_deck[simmed_board.cards[action.moves[1]].name].deathrattle(simmed_board.cards[action.moves[1]], simmed_boards);
				}
			}

			return simmed_boards;
		}

		public void log_missing_card(string card_name)
		{
			string filepath = "missing_deck.txt";
			if (!File.Exists(filepath) || !File.ReadAllText(filepath).Contains(card_name))
			{
				using (StreamWriter w = File.AppendText(filepath))
				{
					w.WriteLine(card_name);
				}
			}
		}

		double calculate_board_value(board_state board)
		{
			double score = 0;
			int total_atk = board.check_attack_on_board(true);

			foreach (var cur_card in board.cards.Values)
			{
				double cur_card_value = 0;

				if (cur_card.zone_name == "DECK")
				{
					continue;
				}
				
				if ((cur_card.zone_name.Contains("PLAY") || cur_card.zone_name.Contains("HAND") || cur_card.zone_name.Contains("SECRET")) && !cur_card.zone_name.Contains("(Hero)"))
				{
					cur_card_value += ((cur_card.atk * ATK_WEIGHT) + cur_card.get_cur_health());
					if (cur_card.tags.taunt)
					{
						cur_card_value *= TAUNT_WEIGHT;
					}
					if (cur_card.tags.windfury)
					{
						cur_card_value *= WINDFURY_WEIGHT;
					}
					if (cur_card.tags.frozen)
					{
						cur_card_value *= FROZEN_WEIGHT;
					}
					if (cur_card.tags.stealth && cur_card.tags.exhausted == true)
					{
						cur_card_value *= STEALTH_WEIGHT;
						cur_card_value += STEALTH_WEIGHT_ABS;
					}
					if (cur_card.tags.divine_shield)
					{
						cur_card_value *= DIVINE_SHIELD_WEIGHT;
					}
					if (cur_card.tags.lifesteal)
					{
						cur_card_value *= LIFESTEAL_WEIGHT;
					}
					if (cur_card.zone_name == "FRIENDLY PLAY" && cur_card.card_type == "MINION")
					{
						cur_card_value *= MY_MINIONS_WEIGHT;
					}
					else if (cur_card.zone_name == "FRIENDLY HAND" && cur_card.card_type == "MINION")
					{
						cur_card_value *= MY_MINIONS_HAND_WEIGHT;
					}
					else if (cur_card.zone_name.Contains("OPPOSING"))
					{
						//if enemy has lethal, rank their minions way higher
						if (total_atk >= board.my_health)
						{
							cur_card_value *= ENEMY_MINIONS_LETHAL_WEIGHT * -1;
						}
						else
						{
							cur_card_value *= ENEMY_MINIONS_WEIGHT * -1;
						}
					}
					else if (cur_card.zone_name == "FRIENDLY SECRET")
					{
						cur_card_value += ENEMY_HERO_WEIGHT * 2 + 1;
					}
				}
				else if (cur_card.zone_name == "OPPOSING PLAY (Hero)" || cur_card.local_id == board.enemy_hero_id)
				{
					if (cur_card.get_cur_health() > 0)
					{
						cur_card_value += cur_card.get_cur_health() * ENEMY_HERO_WEIGHT * -1;
						if (cur_card.get_cur_health() <= 5)
						{
							//enemy hero is getting low on health, rank him increasingly higher
							cur_card_value += 100 / (Math.Max(.1, cur_card.get_cur_health()));
						}
					}
					else
					{
						//enemy hero is dead in this board state. rank this super high
						//but only if they don't have a secret in play (might be ice block, get down, etc)
						if (cur_board.cards_enemy_secrets.Count() == 0)
						{
							cur_card_value += 9999;
						}
						else
						{
							cur_card_value += 1000;
						}
					}
				}
				else if (cur_card.zone_name == "FRIENDLY PLAY (Hero)" || cur_card.local_id == board.my_hero_id)
				{
					if (cur_card.get_cur_health() <= 0)
					{
						//i'm dead in this scenario. mark this super low
						cur_card_value -= 9999;
					}
					else if (this.cur_board.check_attack_on_board(true) >= cur_card.get_cur_health() && !board.check_if_taunt_in_play() && board.cards_secrets.Count() == 0)
					{
						//enemy has lethal, mark this kinda low
						cur_card_value -= 100;
					}
					else if (cur_card.get_cur_health() > 0)
					{
						cur_card_value += cur_card.get_cur_health() * MY_HERO_WEIGHT;
					}
				}

				if (cur_card.zone_name == "FRIENDLY HAND")
				{
					if (herby_deck.ContainsKey(cur_card.name) && board.count_cards_in_hand() < 10)
					{
						cur_card_value += herby_deck[cur_card.name].value_in_hand;
					}
				}

				if (this.high_prio_targets.Contains(cur_card.name) && cur_card.tags.silenced == false)
				{
					//current minion is a high priority target. kill it with extreme prejudice
					//this forces kills on mal'ganis, among other shit that will 100% make us lose.
					cur_card_value *= HIGH_PRIO_WEIGHT;
				}

				if (cur_card_value == 0)
				{
					continue;
				}
				
				score += cur_card_value;
			}
			
			return score;
		}

		void run_best_move(card_play play)
		{
			/*
			list of options:
			1 move
				either hero power id or END TURN
			2 moves
				summon minion
					number -> "minion"
				cast a targetless spell
					number -> "spell"
				attacking
					number -> number
			3 moves
				summon targeted minion
					number -> number -> "minion"
				cast targeted spell
					number -> number -> "spell"
			
			*/
			try
			{
				this.last_summoned_minion = "";
				if (play.moves.Count() == 1)
				{
					//hero power id or END TURN, either way it's a single
					if (play.moves[0] == "END TURN")
					{
						//if enemy has lethal and we're ending turn, just concede
						int total_atk = 0;
						total_atk = this.cur_board.check_attack_on_board(true);
						
						if (total_atk >= this.cur_board.my_health && !this.cur_board.check_if_taunt_in_play() && this.cur_board.enemy_health > 0 && this.cur_board.cards_secrets.Count() == 0)
						{
							//enemy has more damage than i have health, and i have no taunts. I CHOOSE DEATH
							concede();
						}
						else
						{
							click_location(board_position_boxes[play.moves[0]], true);
						}
					}
					else
					{
						click_location(board_position_boxes["HERO POWER"], true);
					}
				}
				else if (play.moves.Count() == 2)
				{
					if (play.moves[1].All(char.IsDigit))
					{
						//swinging from one minion (or hero) to another
						int my_position = 7 + this.cur_board.cards[play.moves[0]].zone_position - 2 + (this.cur_board.cards[play.moves[0]].zone_position - this.cur_board.count_minions_in_zone("FRIENDLY PLAY"));
						int enemy_position = 7 + this.cur_board.cards[play.moves[1]].zone_position - 2 + (this.cur_board.cards[play.moves[1]].zone_position - this.cur_board.count_minions_in_zone("OPPOSING PLAY"));

						board_position_box target_position;
						board_position_box source_position;
						if (this.cur_board.cards[play.moves[1]].zone_name == "OPPOSING PLAY (Hero)")
						{
							target_position = this.board_position_boxes["OPPOSING HERO"];
						}
						else
						{
							target_position = this.board_position_boxes["OPPOSING PLAY"][enemy_position];
						}

						if (this.cur_board.cards[play.moves[0]].zone_name == "FRIENDLY PLAY (Hero)")
						{
							source_position = this.board_position_boxes["FRIENDLY HERO"];
						}
						else
						{
							source_position = this.board_position_boxes["FRIENDLY PLAY"][my_position];
						}

						make_play(source_position, target_position);
					}
					else
					{
						//casting a targetless spell or summoning a minion sans targeted battlecry
						board_position_box src_pos = this.board_position_boxes["FRIENDLY HAND"][this.cur_board.count_cards_in_hand()][this.cur_board.cards[play.moves[0]].zone_position - 1];
						board_position_box target_pos = this.board_position_boxes["FRIENDLY PLAY"][12];
						make_play(src_pos, target_pos);
						if (play.moves[1] == "minion")
						{
							this.last_summoned_minion = play.moves[0];
						}
					}
				}
				else if (play.moves.Count() == 3)
				{
					//casting a targeted spell or summoning a minion with a battlecry
					int my_position = 7 + this.cur_board.cards[play.moves[1]].zone_position - 2 + (this.cur_board.cards[play.moves[1]].zone_position - this.cur_board.count_minions_in_zone("FRIENDLY PLAY"));
					int enemy_position = 7 + this.cur_board.cards[play.moves[1]].zone_position - 2 + (this.cur_board.cards[play.moves[1]].zone_position - this.cur_board.count_minions_in_zone("OPPOSING PLAY"));
					board_position_box target_position;

					if (this.cur_board.cards[play.moves[1]].zone_name == "OPPOSING PLAY (Hero)")
					{
						target_position = this.board_position_boxes["OPPOSING HERO"];
					}
					else if (this.cur_board.cards[play.moves[1]].zone_name == "OPPOSING PLAY")
					{
						target_position = this.board_position_boxes["OPPOSING PLAY"][enemy_position];
					}
					else// if (this.cur_board.cards[play.moves[1]].zone_name == "FRIENDLY PLAY")
					{
						if (play.moves[2] == "minion")
						{
							target_position = this.board_position_boxes["FRIENDLY PLAY"][my_position - 1];
						}
						else
						{
							target_position = this.board_position_boxes["FRIENDLY PLAY"][my_position];
						}
					}

					if (play.moves[2] == "minion")
					{
						//summoning a minion with a target
						board_position_box src_pos = this.board_position_boxes["FRIENDLY HAND"][this.cur_board.count_cards_in_hand()][this.cur_board.cards[play.moves[0]].zone_position - 1];
						board_position_box play_pos = this.board_position_boxes["FRIENDLY PLAY"][12];
						make_play(src_pos, play_pos, target_position);
						this.last_summoned_minion = play.moves[0];
					}
					else if (play.moves[2] == "spell")
					{
						//targeted spell
						board_position_box src_pos = this.board_position_boxes["FRIENDLY HAND"][this.cur_board.count_cards_in_hand()][this.cur_board.cards[play.moves[0]].zone_position - 1];
						make_play(src_pos, target_position);
					}
				}
			}
			catch
			{
				Console.WriteLine("can't make play");
				return;
			}
		}

		string get_line_value(string line, string attr)
		{
			string[] attr_array;
			string attr_string;
			attr_array = line.Split(new string[] { " " + attr + "=" }, 0);
			if (attr_array.Length == 1)
			{
				attr_array = line.Split(new string[] { "[" + attr + "=" }, 0);
			}
			if (attr_array.Length == 1)
			{
				return "";
			}
			attr_string = attr_array[1].Split(new string[] { " " }, 0)[0];
			return attr_string;
		}

		string get_card_name(string line)
		{
			string[] attr_array;
			string attr_string;
			attr_array = line.Split(new string[] { "entityName=" }, 0);
			if (attr_array.Length == 1)
			{
				return "";
			}
			attr_array = attr_array[1].Split(new string[] { " id=" }, 0);
			attr_string = attr_array[0];
			return attr_string;
		}

		private void start_watching()
		{
			this.hs = FindWindow(null, "Hearthstone");

			if (this.dynamic_board_positions)
			{
				init_board_positions();
			}

			this.game_player.DoWork += new DoWorkEventHandler(
			delegate(object o, DoWorkEventArgs args)
			{
				while (this.running == true)
				{
					main_loop();
					Thread.Sleep(1);
				}
				this.calc_first_move_button.Show();
				set_status_text("Inactive");
				set_action_text("");
				status.BackColor = Color.FromArgb(255, 170, 170);
			});

			if (this.chk_view_log.Checked == false)
			{
				this.calc_first_move_button.Hide();
				this.hide_moves_button_Click(new object(), new EventArgs());
			}
			set_status_text("Herby playing");
			this.Text = "Herby (Playing)";
			status.BackColor = Color.FromArgb(136, 204, 136);
			this.running = true;

			try
			{
				this.game_player.RunWorkerAsync();
			}
			catch
			{
				stop_watching();
			}
		}

		private void stop_watching()
		{
			this.Text = "Herby";
			set_status_text("Stopping");
			status.BackColor = Color.FromArgb(255, 255, 170);
			this.running = false;
		}

		private void start_opening_packs()
		{
			this.hs = FindWindow(null, "Hearthstone");
			
			this.pack_opener.DoWork += new DoWorkEventHandler(
			delegate(object o, DoWorkEventArgs args)
			{
				while (this.opening_packs == true)
				{
					open_packs();
					Thread.Sleep(1);
				}
				set_status_text("Inactive");
				set_action_text("");
			});

			set_status_text("Opening card packs");
			set_action_text("Opening");
			this.Text = "Herby (Packs)";
			status.BackColor = Color.FromArgb(136, 204, 136);
			this.opening_packs = true;

			try
			{
				this.pack_opener.RunWorkerAsync();
			}
			catch
			{
				stop_opening_packs();
			}
		}

		private void stop_opening_packs()
		{
			set_status_text("Stopping");
			this.Text = "Herby";
			status.BackColor = Color.FromArgb(255, 170, 170);
			this.opening_packs = false;
		}

		public void open_packs()
		{
			click_and_drag(400, 550, 1100, 500, 100, 100);
			click_location(1100, 300, true, 50);
			click_location(1400, 400, true, 50);
			click_location(1270, 800, true, 50);
			click_location(950, 800, true, 50);
			click_location(840, 400, true, 50);
			click_location(1100, 550, true, 50);
		}

		void concede()
		{
			open_options_menu();
			click_location(this.board_position_boxes["CONCEDE BUTTON"], true, 1000);
		}

		void quit_game()
		{
			Thread.Sleep(5000);
			foreach (var process in System.Diagnostics.Process.GetProcessesByName("Hearthstone"))
			{
				process.Kill();
			}
		}

		void quit_herby()
		{
			Application.Exit();
		}

		void log_games(string type)
		{
			if (this.db_path.Length == 0 || !Directory.Exists(this.db_path))
			{
				return;
			}
			int count = 0;
			if (type == "win")
			{
				count = this.num_wins;
			}
			else
			{
				count = this.num_losses;
			}
			var dir = new DirectoryInfo(this.db_path);

			try
			{
				foreach (var file in dir.EnumerateFiles(type + "*"))
				{
					file.Delete();
				}
			
				File.Create(this.db_path + type + "_" + count).Close();
			}
			catch
			{

			}

			this.rand = new Random();
		}

		void open_options_menu()
		{
			//click a bunch of times near options make sure the options window isn't already open
			for (int i = 0; i < 5; i++)
			{
				click_location(this.board_position_boxes["NEAR OPTIONS MENU"], true);
			}
			//then click the gear
			click_location(this.board_position_boxes["OPTIONS MENU"], true, 1000, 500);
		}

		void make_play(params board_position_box[] positions)
		{
			//click at each position to make the calculated play
			for (int i = 0; i < positions.Count(); i++)
			{
				click_location(positions[i], true);
			}

			//then right click just in case we've got a card stuck to our hand
			down_click(true);
			Thread.Sleep(50);
			up_click(true);
			Thread.Sleep(50);
		}

		void make_play(params board_position[] positions)
		{
			for (int i = 0; i < positions.Count(); i++)
			{
				click_location(positions[i], true);
			}

			down_click(true);
			Thread.Sleep(50);
			up_click(true);
			Thread.Sleep(50);
		}

		void click_location(board_position_box pos, bool drag = false, int movedelay = move_delay_default, int click_delay = 100)
		{
			int x = rand.Next(pos.left_x, pos.right_x);
			int y = rand.Next(pos.top_y, pos.bottom_y);

			click_location(x, y, drag, movedelay, click_delay);
		}

		void click_location(board_position pos, bool drag = false, int movedelay = move_delay_default, int click_delay = 100)
		{
			click_location(pos.x, pos.y, drag, movedelay, click_delay);
		}

		void click_location(int x, int y, bool drag = false, int movedelay = move_delay_default, int click_delay = 100)
		{
			if (this.force_focus)
			{
				focus_game();
			}
			move_cursor(x, y, drag, movedelay);
			int split_delay = click_delay / 3;
			Thread.Sleep(split_delay + this.rand.Next(-15, 15));
			down_click();
			Thread.Sleep(split_delay + this.rand.Next(-15, 15));
			up_click();
			Thread.Sleep(split_delay + this.rand.Next(-15, 15));
		}

		void click_and_drag(board_position from, board_position to, int move1delay = move_delay_default, int move2delay = move_delay_default)
		{
			click_and_drag(from.x, from.y, to.x, to.y, move1delay, move2delay);
		}

		void click_and_drag(int from_x, int from_y, int to_x, int to_y, int move1delay = move_delay_default, int move2delay = move_delay_default)
		{
			move_cursor(from_x, from_y, true, move1delay);
			down_click();
			move_cursor(to_x, to_y, true, move2delay);
			up_click();
		}

		void move_cursor(board_position pos, bool drag = false, int delay = move_delay_default, int time_slice = 1)
		{
			move_cursor(pos.x, pos.y, drag, delay, time_slice);
		}

		void move_cursor(int x, int y, bool drag = false, int delay = move_delay_default, int time_slice = 1)
		{
			float x_jitter;
			float y_jitter;
			delay += this.rand.Next(-50, 50);
			float steps = delay / time_slice;

			if (drag == false)
			{
				Cursor.Position = new Point(x, y);
			}
			else
			{
				float cur_x = Cursor.Position.X;
				float cur_y = Cursor.Position.Y;

				float diff_x = cur_x - x;
				float diff_y = cur_y - y;

				for (float i = 0; i < steps; i++)
				{
					x_jitter = rand.Next((int)(-50 * (1 - i / steps)), (int)(50 * (1 - i / steps))) * Math.Min(1, Math.Abs(diff_x / 100));
					y_jitter = rand.Next((int)(-50 * (1 - i / steps)), (int)(50 * (1 - i / steps))) * Math.Min(1, Math.Abs(diff_y / 100));
					cur_x -= (diff_x / steps);
					cur_y -= (diff_y / steps);
					Cursor.Position = new Point((int)(cur_x + x_jitter), (int)(cur_y + y_jitter));
					Thread.Sleep(time_slice);
				}
			}
		}

		void down_click(bool right = false)
		{
			int key;
			if (right)
			{
				key = WM_RBUTTONDOWN;
			}
			else
			{
				key = WM_LBUTTONDOWN;
			}
			SendMessage(hs, key, 0, 0);
		}

		void up_click(bool right = false)
		{
			int key;
			if (right)
			{
				key = WM_RBUTTONUP;
			}
			else
			{
				key = WM_LBUTTONUP;
			}
			SendMessage(hs, key, 0, 0);
		}

		void init_board_positions()
		{
			//figure out board positions dynamically
			RECT hs_dimensions = new RECT();
			GetWindowRect(this.hs, out hs_dimensions);

			int screen_width = hs_dimensions.Right - hs_dimensions.Left;
			int screen_height = hs_dimensions.Bottom - hs_dimensions.Top;

			int max_screen_width = 1920;
			int max_screen_height = 1080;

			if (screen_width == 0)
			{
				screen_width = max_screen_width;
				screen_height = max_screen_height;
			}

			int x_offset = hs_dimensions.Left;
			int y_offset = hs_dimensions.Top;

			bool is_maximized = new Rectangle(hs_dimensions.Left, hs_dimensions.Top, screen_width, screen_height).Contains(Screen.PrimaryScreen.Bounds);

			if (!is_maximized)
			{
				//add the window border pixels to the y offset
				y_offset += 30;
			}

			Dictionary<string, dynamic> max_positions_dict = new Dictionary<string, dynamic>();
			max_positions_dict = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(max_positions.get_max_positions());

			//positions given are for a 1920x1080 display
			//shrink them down to fit the dimensions in screen_width and screen_height
			//height is consistent: ratio of distance from top vs bottom remains the same at all resolutions
			//width is inconsistent: center X is kept consistent, whitespace is added to left and right to allow for widescreen
			//new x_pos = (old x_pos - max_width / 2) * (screen_height / max_height) + (screen_width / 2)
			float height_ratio = (float)(screen_height - (!is_maximized ? 38 : 0)) / max_screen_height;

			this.board_position_boxes["FRIENDLY HAND"] = new board_position_box[11][];

			this.board_position_boxes["FRIENDLY PLAY"] = new board_position_box[13];
			this.board_position_boxes["OPPOSING PLAY"] = new board_position_box[13];
			this.board_position_boxes["MULLIGAN"] = new board_position_box[2][];

			foreach (var position in max_positions_dict)
			{
				string key = position.Key;
				if (key == "FRIENDLY HAND")
				{
					foreach (var hand_position in position.Value)
					{
						int hand_size = Int32.Parse(hand_position.Name.Replace("hand_size_", ""));
						this.board_position_boxes["FRIENDLY HAND"][hand_size] = new board_position_box[hand_size];

						foreach (var hand_size_position in hand_position.Value)
						{
							int card_num = Int32.Parse(hand_size_position.Name.Replace("card_", ""));
							int left = get_new_x_position((int)hand_size_position.Value["left"], max_screen_width, screen_width, height_ratio) + x_offset;
							int right = get_new_x_position((int)hand_size_position.Value["right"], max_screen_width, screen_width, height_ratio) + x_offset;
							int top = (int)Math.Ceiling((int)hand_size_position.Value["top"] * height_ratio) + y_offset;
							int bottom = (int)Math.Ceiling((int)hand_size_position.Value["bottom"] * height_ratio) + y_offset;
							this.board_position_boxes["FRIENDLY HAND"][hand_size][card_num] = new board_position_box(left, right, top, bottom);
						}
					}
				}
				else if (key == "FIELD")
				{
					int friendly_top = (int)Math.Ceiling((int)position.Value["FRIENDLY"]["top"] * height_ratio) + y_offset;
					int friendly_bottom = (int)Math.Ceiling((int)position.Value["FRIENDLY"]["bottom"] * height_ratio) + y_offset;
					int enemy_top = (int)Math.Ceiling((int)position.Value["OPPOSING"]["top"] * height_ratio) + y_offset;
					int enemy_bottom = (int)Math.Ceiling((int)position.Value["OPPOSING"]["bottom"] * height_ratio) + y_offset;

					int footprint = (int)Math.Ceiling((int)position.Value["minion_footprint"] * height_ratio);

					int far_left = get_new_x_position((int)position.Value["far_left"]["left"], max_screen_width, screen_width, height_ratio) + x_offset;
					int far_right = get_new_x_position((int)position.Value["far_left"]["right"], max_screen_width, screen_width, height_ratio) + x_offset;

					for (int i = 0; i < 13; i++)
					{
						this.board_position_boxes["FRIENDLY PLAY"][i] = new board_position_box(far_left + footprint * i, far_right + footprint * i, friendly_top, friendly_bottom);
						this.board_position_boxes["OPPOSING PLAY"][i] = new board_position_box(far_left + footprint * i, far_right + footprint * i, enemy_top, enemy_bottom);
					}
				}
				else if (key == "MULLIGAN")
				{
					this.board_position_boxes["MULLIGAN"][0] = new board_position_box[3];
					this.board_position_boxes["MULLIGAN"][1] = new board_position_box[4];

					int card_num;

					foreach (var going_first in position.Value["going_first"])
					{
						card_num = Int32.Parse(going_first.Name.Replace("card_", ""));
						int left = get_new_x_position((int)going_first.Value["left"], max_screen_width, screen_width, height_ratio) + x_offset;
						int right = get_new_x_position((int)going_first.Value["right"], max_screen_width, screen_width, height_ratio) + x_offset;
						int top = (int)Math.Ceiling((int)going_first.Value["top"] * height_ratio) + y_offset;
						int bottom = (int)Math.Ceiling((int)going_first.Value["bottom"] * height_ratio) + y_offset;
						this.board_position_boxes["MULLIGAN"][0][card_num] = new board_position_box(left, right, top, bottom);
					}

					foreach (var going_second in position.Value["going_second"])
					{
						card_num = Int32.Parse(going_second.Name.Replace("card_", ""));
						int left = get_new_x_position((int)going_second.Value["left"], max_screen_width, screen_width, height_ratio) + x_offset;
						int right = get_new_x_position((int)going_second.Value["right"], max_screen_width, screen_width, height_ratio) + x_offset;
						int top = (int)Math.Ceiling((int)going_second.Value["top"] * height_ratio) + y_offset;
						int bottom = (int)Math.Ceiling((int)going_second.Value["bottom"] * height_ratio) + y_offset;
						this.board_position_boxes["MULLIGAN"][1][card_num] = new board_position_box(left, right, top, bottom);
					}
				}
				else if (key == "NEAR OPTIONS MENU" || key == "OPTIONS MENU")
				{
					//the options button acts like 0% of any other UI element, and both scales with screen size and positions itself near the right edge of the screen
					//full size distance from edges: 4 from bottom, 12 from right
					int max_button_width = 63;
					int max_button_height = 35;
					int max_distance_right = 12;
					int max_distance_bottom = 6;

					int new_button_width = (int)Math.Ceiling(max_button_width * height_ratio);
					int new_button_height = (int)Math.Ceiling(max_button_height * height_ratio);
					int new_distance_right = (int)Math.Ceiling(max_distance_right * height_ratio);
					int new_distance_bottom = (int)Math.Ceiling(max_distance_bottom * height_ratio);

					int left = 0, right = 0, top = 0, bottom = 0;

					if (key == "OPTIONS MENU")
					{
						//need to somehow account for the x and y offsets mattering more
						left = screen_width - new_distance_right - new_button_width + x_offset;
						right = screen_width - new_distance_right + x_offset;
						top = screen_height - new_distance_bottom - new_button_height + y_offset;
						bottom = screen_height - new_distance_bottom + y_offset;
					}
					else if (key == "NEAR OPTIONS MENU")
					{
						left = screen_width - new_distance_right - new_button_width + x_offset;
						right = screen_width - new_distance_right + x_offset;
						top = screen_height - new_distance_bottom - new_button_height + y_offset - (new_button_height * 2);
						bottom = screen_height - new_distance_bottom + y_offset - (new_button_height * 2);
					}

					if (!is_maximized)
					{
						//if we're not fullscreen, we need to remove the relevant borders, because those are accounted for in the screen width and height
						left -= 8;
						right -= 8;
						top -= 38;
						bottom -= 38;
					}

					this.board_position_boxes[key] = new board_position_box(left, right, top, bottom);
				}
				else
				{
					int left = get_new_x_position((int)position.Value["left"], max_screen_width, screen_width, height_ratio) + x_offset;
					int right = get_new_x_position((int)position.Value["right"], max_screen_width, screen_width, height_ratio) + x_offset;
					int top = (int)Math.Ceiling((int)position.Value["top"] * height_ratio) + y_offset;
					int bottom = (int)Math.Ceiling((int)position.Value["bottom"] * height_ratio) + y_offset;

					this.board_position_boxes[key] = new board_position_box(left, right, top, bottom);
				}
			}
		}

		public int get_new_x_position(int old_position, int old_width, int new_width, double height_ratio)
		{
			int old_distance_from_center = old_position - old_width / 2;

			int new_distance_from_center = (int)Math.Ceiling(old_distance_from_center * height_ratio);

			int new_position = new_distance_from_center + new_width / 2;

			return new_position;
		}

		void init_board_positions(dynamic config_positions)
		{
			this.board_position_boxes["FRIENDLY HAND"] = new board_position_box[11][];

			this.board_position_boxes["FRIENDLY PLAY"] = new board_position_box[13];
			this.board_position_boxes["OPPOSING PLAY"] = new board_position_box[13];
			this.board_position_boxes["MULLIGAN"] = new board_position_box[2][];

			foreach (var position in config_positions)
			{
				string key = position.Name;
				if (key == "FRIENDLY HAND")
				{
					foreach (var hand_position in position.Value)
					{
						int hand_size = Int32.Parse(hand_position.Name.Replace("hand_size_", ""));
						this.board_position_boxes["FRIENDLY HAND"][hand_size] = new board_position_box[hand_size];
						
						foreach (var hand_size_position in hand_position.Value)
						{
							int card_num = Int32.Parse(hand_size_position.Name.Replace("card_", ""));
							int left = (int)hand_size_position.Value["left"];
							int right = (int)hand_size_position.Value["right"];
							int top = (int)hand_size_position.Value["top"];
							int bottom = (int)hand_size_position.Value["bottom"];
							this.board_position_boxes["FRIENDLY HAND"][hand_size][card_num] = new board_position_box(left, right, top, bottom);
						}
					}
				}
				else if (key == "FIELD")
				{
					int friendly_top = (int)position.Value["FRIENDLY"]["top"];
					int friendly_bottom = (int)position.Value["FRIENDLY"]["bottom"];
					int enemy_top = (int)position.Value["OPPOSING"]["top"];
					int enemy_bottom = (int)position.Value["OPPOSING"]["bottom"];

					int footprint = (int)position.Value["minion_footprint"];

					int far_left = (int)position.Value["far_left"]["left"];
					int far_right = (int)position.Value["far_left"]["right"];

					for (int i = 0; i < 13; i++)
					{
						this.board_position_boxes["FRIENDLY PLAY"][i] = new board_position_box(far_left + footprint * i, far_right + footprint * i, friendly_top, friendly_bottom);
						this.board_position_boxes["OPPOSING PLAY"][i] = new board_position_box(far_left + footprint * i, far_right + footprint * i, enemy_top, enemy_bottom);
					}
				}
				else if (key == "MULLIGAN")
				{
					this.board_position_boxes["MULLIGAN"][0] = new board_position_box[3];
					this.board_position_boxes["MULLIGAN"][1] = new board_position_box[4];

					int card_num;

					foreach (var going_first in position.Value["going_first"])
					{
						card_num = Int32.Parse(going_first.Name.Replace("card_", ""));
						int left = (int)going_first.Value["left"];
						int right = (int)going_first.Value["right"];
						int top = (int)going_first.Value["top"];
						int bottom = (int)going_first.Value["bottom"];
						this.board_position_boxes["MULLIGAN"][0][card_num] = new board_position_box(left, right, top, bottom);
					}

					foreach (var going_second in position.Value["going_second"])
					{
						card_num = Int32.Parse(going_second.Name.Replace("card_", ""));
						int left = (int)going_second.Value["left"];
						int right = (int)going_second.Value["right"];
						int top = (int)going_second.Value["top"];
						int bottom = (int)going_second.Value["bottom"];
						this.board_position_boxes["MULLIGAN"][1][card_num] = new board_position_box(left, right, top, bottom);
					}
				}
				else
				{
					int left = (int)position.Value["left"];
					int right = (int)position.Value["right"];
					int top = (int)position.Value["top"];
					int bottom = (int)position.Value["bottom"];

					this.board_position_boxes[key] = new board_position_box(left, right, top, bottom);
				}
			}
		}

		public void get_config_settings(Dictionary<string, dynamic> config)
		{
			this.log_location = config["general"]["log_location"].ToString();
			this.db_path = config["general"]["dropbox_location"].ToString();
			//this.my_name = config["general"]["player_name"].ToString();

			if (config["general"]["force_focus"] != null)
			{
				this.force_focus = Convert.ToBoolean((int)config["general"]["force_focus"]);
			}

			dynamic weights = config["general"]["weights"];

			MY_MINIONS_WEIGHT = (double)weights["MY_MINIONS_WEIGHT"];
			MY_MINIONS_HAND_WEIGHT = (double)weights["MY_MINIONS_HAND_WEIGHT"];
			MY_HERO_WEIGHT = (double)weights["MY_HERO_WEIGHT"];
			ENEMY_MINIONS_WEIGHT = (double)weights["ENEMY_MINIONS_WEIGHT"];
			ENEMY_MINIONS_LETHAL_WEIGHT = (double)weights["ENEMY_MINIONS_LETHAL_WEIGHT"];
			ENEMY_HERO_WEIGHT = (double)weights["ENEMY_HERO_WEIGHT"];
			ATK_WEIGHT = (double)weights["ATK_WEIGHT"];
			TAUNT_WEIGHT = (double)weights["TAUNT_WEIGHT"];
			WINDFURY_WEIGHT = (double)weights["WINDFURY_WEIGHT"];
			FROZEN_WEIGHT = (double)weights["FROZEN_WEIGHT"];
			STEALTH_WEIGHT = (double)weights["STEALTH_WEIGHT"];
			STEALTH_WEIGHT_ABS = (double)weights["STEALTH_WEIGHT_ABS"];
			DIVINE_SHIELD_WEIGHT = (double)weights["DIVINE_SHIELD_WEIGHT"];
			LIFESTEAL_WEIGHT = (double)weights["LIFESTEAL_WEIGHT"];
			HIGH_PRIO_WEIGHT = (double)weights["HIGH_PRIO_WEIGHT"];

			this.high_prio_targets = config["general"]["high_prio_targets"].ToObject<List<string>>();
		}

		void build_deck_options()
		{
			herby_deck = build_herby_deck.herby_deck();
		}

		private void checkBox1_CheckedChanged(object sender, EventArgs e)
		{
			if (this.chk_view_log.Checked)
			{
				this.log_output.Show();
				this.Width = 1000;
				this.Height = 600;
				this.hide_moves_button.Hide();
			}
			else
			{
				this.log_output.Hide();
				this.Width = 300;
				this.Height = 220;
			}
		}

		private void calc_first_move_button_Click(object sender, EventArgs e)
		{
			if (this.cur_board.game_active == true)
			{
				this.chk_view_log.Checked = false;
				this.checkBox1_CheckedChanged(sender, e);
				this.Width = 1000;
				this.Height = 600;
				this.hide_moves_button.Show();

				this.output_moves(this.cur_board);
			}
		}

		private void output_moves(board_state board)
		{
			List<card_play> possible_plays = get_possible_plays(board);
			List<board_state> possible_boards = new List<board_state>();

			for (int i = 0; i < this.calced_move_buttons.Count(); i++)
			{
				this.Controls.Remove(this.calced_move_buttons[i]);
			}
			this.calced_move_buttons = new List<Button>();

			int x = 0;
			int y = 0;
			for (int i = 0; i < possible_plays.Count(); i++)
			{
				//simulate this play
				List<board_state> simmed_boards = simulate_board_state(new board_state(board), possible_plays[i]);

				board_state simmed_board = simmed_boards[0];

				//get this simulated play's score
				double score_before = calculate_board_value(board);
				double score_after = calculate_board_value(simmed_board);
				double total_score = score_after - score_before;

				//save this board state for later use
				possible_boards.Insert(i, simmed_board);

				//make a button for this possible play using the saved board state
				this.calced_move_buttons.Insert(i, new Button());
				this.calced_move_buttons[i].Font = new System.Drawing.Font("Microsoft Sans Serif", 6.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
				this.calced_move_buttons[i].Name = "calced_move_" + i;
				this.calced_move_buttons[i].Size = new System.Drawing.Size(186, 24);
				this.calced_move_buttons[i].UseVisualStyleBackColor = true;
				this.calced_move_buttons[i].TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

				this.calced_move_buttons[i].Location = new System.Drawing.Point(12 + (x * 192), 180 + (y * 30));

				y++;
				if (y > 11)
				{
					y = 0;
					x++;
				}

				string button_name = total_score + ": ";
				if (possible_plays[i].moves.Count() == 1)
				{
					if (this.cur_board.cards.ContainsKey(possible_plays[i].moves[0]))
					{
						button_name += simmed_board.cards[possible_plays[i].moves[0]].name;
					}
					else
					{
						button_name += possible_plays[i].moves[0];
					}
				}
				else if (possible_plays[i].moves.Count() == 2)
				{
					button_name += simmed_board.cards[possible_plays[i].moves[0]].name + " > ";
					if (simmed_board.cards.ContainsKey(possible_plays[i].moves[1]))
					{
						button_name += simmed_board.cards[possible_plays[i].moves[1]].name;
					}
					else
					{
						button_name += possible_plays[i].moves[1];
					}
				}
				else if (possible_plays[i].moves.Count() == 3)
				{
					button_name += simmed_board.cards[possible_plays[i].moves[0]].name + " > ";
					button_name += simmed_board.cards[possible_plays[i].moves[1]].name;
				}

				this.calced_move_buttons[i].Text = button_name;

				int cur_button = i;
				this.calced_move_buttons[i].Click += (sender, args) => { output_moves(possible_boards[cur_button]); };

				this.Controls.Add(this.calced_move_buttons[i]);
			}
		}

		private void hide_moves_button_Click(object sender, EventArgs e)
		{
			this.Width = 300;
			this.Height = 220;
			this.hide_moves_button.Hide();

			for (int i = 0; i < this.calced_move_buttons.Count(); i++)
			{
				this.Controls.Remove(this.calced_move_buttons[i]);
			}
		}

		public static string md5(string input)
		{
			// Use input string to calculate MD5 hash
			MD5 md5 = MD5.Create();
			byte[] inputBytes = Encoding.ASCII.GetBytes(input);
			byte[] hashBytes = md5.ComputeHash(inputBytes);

			// Convert the byte array to hexadecimal string
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hashBytes.Length; i++)
			{
				sb.Append(hashBytes[i].ToString("X2"));
			}
			return sb.ToString();
		}

		public List<E> ShuffleList<E>(List<E> inputList)
		{
			List<E> randomList = new List<E>();

			int randomIndex = 0;
			while (inputList.Count > 0)
			{
				randomIndex = this.rand.Next(0, inputList.Count); //Choose a random object in the list
				randomList.Add(inputList[randomIndex]); //add it to the new, random list
				inputList.RemoveAt(randomIndex); //remove to avoid duplicates
			}

			return randomList; //return the new random list
		}
	}
}
