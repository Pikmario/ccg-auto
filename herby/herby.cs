using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using Newtonsoft.Json;

namespace Herby
{
	public partial class Herby : Form
    {
		[DllImport("user32.dll")]
		private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
		[DllImport("user32.dll")]
		private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

		[DllImport("user32.dll", SetLastError = true)]
		static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
		static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

		[DllImport("user32.dll")]
		static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

		[StructLayout(LayoutKind.Sequential)]
		public struct POINT
		{
			public int X;
			public int Y;

			public POINT(int x, int y)
			{
				this.X = x;
				this.Y = y;
			}

			public POINT(System.Drawing.Point pt) : this(pt.X, pt.Y) { }

			public static implicit operator System.Drawing.Point(POINT p)
			{
				return new System.Drawing.Point(p.X, p.Y);
			}

			public static implicit operator POINT(System.Drawing.Point p)
			{
				return new POINT(p.X, p.Y);
			}
		}

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

		public string my_controller_value = "";

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

		public bool i_won = true;

		public bool wipe_log = false;
		string output_log = "";

		public card_play cur_best_move;

		public int num_best_move_workers;

		public string log_location;

		FileStream hslog_filestream;

		StreamReader hs_log_file;

		public string db_path;

		public List<string> high_prio_targets;

		public Herby()
        {
			string json = File.ReadAllText("herby.json");
			Dictionary<string, dynamic> config = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json);
			
			get_config_settings(config);


			Control.CheckForIllegalCrossThreadCalls = false;

            InitializeComponent();

			RegisterHotKey(this.Handle, 0, (int)KeyModifier.Shift + (int)KeyModifier.Alt, Keys.Space.GetHashCode());
			RegisterHotKey(this.Handle, 1, (int)KeyModifier.Shift + (int)KeyModifier.Ctrl, Keys.Space.GetHashCode());

			init_board_positions(config["positions"]);
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

		public static List<List<card_play>> splitList(List<card_play> locations, int nSize = 30)
		{
			List<List<card_play>> list = new List<List<card_play>>();

			for (int i = 0; i < locations.Count(); i += nSize)
			{
				list.Add(locations.GetRange(i, Math.Min(nSize, locations.Count - i)));
			}

			return list;
		}

		void main_loop()
		{
			if (this.db_path.Length > 0 && File.Exists(this.db_path + "stop.flag") && !this.cur_board.game_active)
			{
				//telling herby to stop playing remotely, just stop doing stuff
				set_action_text("No action: stop.flag present");
				return;
			}
			if (this.num_best_move_workers > 0)
			{
				//we've currently got calculations going on for the best move, just get out of here for now
				return;
			}

			this.cur_best_move = new card_play{moves = new List<string>{""}, score = -1000};
			
			if (!this.cur_board.game_active)
			{
				//game isn't active, just hammer the play button until a game starts
				click_location(this.board_position_boxes["PLAY BUTTON"], true);
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

			if (!this.cur_board.my_turn)
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
				Thread.Sleep(7000);
				this.is_my_turn = true;
				return;
			}
			
			if (this.my_hand_size != this.cur_board.count_cards_in_hand())
			{
				//we just played a card, or drew one, and the numbers don't match up
				//wait a short while, then recalculate
				set_action_text("Waiting for cards");
				this.my_hand_size = this.cur_board.count_cards_in_hand();
				Thread.Sleep(1000);
				return;
			}
			
			//calculate best move based on game state and stat weights
			//first we get a list of all the possible plays for the initital game state
			List<card_play> possible_plays = get_possible_plays(new board_state(this.cur_board));
			
			//split these possible plays into a unique bg worker
			List<BackgroundWorker> bm_bgs = new List<BackgroundWorker>();
			List<List<card_play>> split_plays = splitList(possible_plays, 3);
			
			for (int i = 0; i < split_plays.Count(); i++)
			{
				//keep track of how many bg workers we've spawned
				this.num_best_move_workers++;

				bm_bgs.Add(new BackgroundWorker());
				int cur_worker = i;
				bm_bgs[cur_worker].DoWork += new DoWorkEventHandler(
				delegate(object o, DoWorkEventArgs args)
				{
					//calculate the best move for this limited amount of possible plays
					card_play best_move = calculate_best_move(new board_state(this.cur_board), 1, 3, split_plays[cur_worker]);
					args.Result = best_move;
				});

				set_action_text("Calculating best move\r\n" + this.num_best_move_workers + " threads active");

				bm_bgs[cur_worker].RunWorkerCompleted += new RunWorkerCompletedEventHandler(
				delegate(object o, RunWorkerCompletedEventArgs args)
				{
					//get the result from the DoWorkEvent
					card_play calced_move = (card_play)args.Result;
					
					if (calced_move.score > this.cur_best_move.score)
					{
						this.cur_best_move = calced_move;
					}

					if (calced_move.score == this.cur_best_move.score && this.cur_best_move.moves[0].Length != 1)
					{
						if (this.rand.Next(0, 2) == 1 || this.cur_best_move.moves[0].Length == 0)
						{
							this.cur_best_move = calced_move;
						}
					}

					//one worker less is active, make note of it
					set_action_text("Calculating best move\r\n" + (this.num_best_move_workers - 1) + " threads active");

					if (this.num_best_move_workers == 1)
					{
						//we've taken out the last bg worker, run the best move they all calculated

						//play out the calculated best move
						card_play best_move = this.cur_best_move;
						
						set_action_text("Running best move");
						run_best_move(best_move);
						
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
							Console.WriteLine(this.cur_board.cards[best_move.moves[0]].name);
							Console.WriteLine(best_move.moves[1]);
							Console.WriteLine(this.cur_board.cards[best_move.moves[1]].name);
						}
						
						//wait a short while for animation to play and interactivity to return
						set_action_text("Waiting for animation");
						int wait_time = this.rand.Next(1200, 1400);
						if (best_move.moves.Count() == 1 || (best_move.moves.Count() == 2 && best_move.moves[1].All(char.IsDigit) && this.last_summoned_minion != best_move.moves[0]))
						{
							wait_time -= 800;
						}

						foreach (var cur_card in this.cur_board.cards.Values)
						{
							if (cur_card.name == "Lorewalker Cho" && ((best_move.moves.Count() == 2 && best_move.moves[1] == "spell") || (best_move.moves.Count() == 3 && best_move.moves[2] == "spell")))
							{
								set_action_text("Waiting for Cho's animation (fucker)");
								wait_time += 3000;
							}
						}

						Thread.Sleep(wait_time);
					}

					this.num_best_move_workers--;
				});

				bm_bgs[cur_worker].RunWorkerAsync();
			}
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
			if (action.Text != text)
			{
				action.Text = text;
			}
		}

		void mulligan_card_step(bool wait = false)
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
			foreach (var cur_card in this.cur_board.my_hand_cards.Values)
			{
				//find all cards in hand with a mana cost above 4 or below 1, toss them
				if (cur_card.zone_name == "FRIENDLY HAND" && cur_card.name != "The Coin" && (cur_card.mana_cost > 4 || cur_card.mana_cost < 1))
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
			//click confirm then wait 2 seconds
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

				this.hslog_filestream = new FileStream(
					this.log_location,
					FileMode.Open,
					FileAccess.ReadWrite,
					FileShare.ReadWrite);
				
				this.hs_log_file = new StreamReader(hslog_filestream);

				string modded_log_contents = "";
				this.my_controller_value = "";
				
				board_state log_state = new board_state();

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

					if (chk_view_log.Checked)
					{
						modded_log_contents += line + "\r\n";
					}
					
					if (line.Contains("CREATE_GAME") && log_state.game_active == false)
					{
						//game is starting
						log_state = new board_state();
						log_state.game_active = true;
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
						if (this.cur_board.game_active == true)
						{
							if (this.i_won)
							{
								this.num_wins++;
								this.num_wins_text.Text = num_wins.ToString();
							}
							else
							{
								this.num_losses++;
								this.num_losses_text.Text = num_losses.ToString();
							}
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

					if (log_state.game_active == false)
					{
						continue;
					}

					if (line.Contains("goldRewardState = ALREADY_CAPPED") && this.running)
					{
						//we hit the gold cap, wee
						//quit the game, we've got nothing to gain by continuing
						quit_game();
						quit_herby();
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
					if (line.Contains("SHOW_ENTITY - Updating") || line.Contains("FULL_ENTITY - Updating"))
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

						if (this.my_controller_value == "" && cur_tag == "CONTROLLER" && log_state.cards[cur_id].name != null)
						{
							this.my_controller_value = cur_value;
						}
						continue;
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
						if (log_state.my_name == "" && tag == "CONTROLLER" && tag_value == this.my_controller_value)
						{
							log_state.my_name = player_name;
						}
						else if (log_state.enemy_name == "" && tag == "CONTROLLER" && tag_value != this.my_controller_value)
						{
							log_state.enemy_name = player_name;
						}

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
							if (player_name == log_state.my_name)
							{
								log_state.my_turn = get_line_value(line, "value") == "1";
							}
						}
						else if (tag == "STEP")
						{
							log_state.game_state = get_line_value(line, "value");
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
							if (player_name == log_state.my_name)
							{
								if (get_line_value(line, "value") == "LOST")
								{
									this.i_won = false;
								}
								else if (get_line_value(line, "value") == "WON")
								{
									this.i_won = true;
								}
								else
								{
									//assume i lost, it was probably a draw
									this.i_won = false;
								}
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
						log_state.add_card_to_zone(cur_id, zone_split[1]);
						if (zone_split[1] == "FRIENDLY PLAY (Hero)")
						{
							log_state.my_hero_id = get_line_value(line, "id");
						}
						else if (zone_split[1] == "OPPOSING PLAY (Hero)")
						{
							log_state.enemy_hero_id = get_line_value(line, "id");
						}
						string card_name = get_card_name(line);
						if (card_name.Length > 0)
						{
							log_state.cards[cur_id].name = card_name;
						}
					}
				}

				if (this.wipe_log && this.cur_board.game_active == false)
				{
					hs_log_file.BaseStream.SetLength(0);
					this.wipe_log = false;
				}

				if (chk_view_log.Checked)
				{
					if (this.output_log != modded_log_contents)
					{
						this.output_log = modded_log_contents;
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

		card_play calculate_best_move(board_state board, int cur_depth = 1, int max_depth = 3, List<card_play> possible_plays = null)
		{
			//first get all possible plays for this board state
			if (possible_plays == null)
			{
				possible_plays = get_possible_plays(new board_state(board));
			}
			
			card_play best_play = new card_play{moves = new List<string>{""}, score = -1000};

			max_depth = (int)Math.Ceiling(Math.Log(300/possible_plays.Count(), 2));
			
			//then for each possible play, see which yields the best board state
			//recurse until max_depth has been reached
			for (int i = 0; i < possible_plays.Count(); i++)
			{
				double score_before = calculate_board_value(board);
				board_state simulated_board_state = simulate_board_state(new board_state(board), possible_plays[i]);
				double score_after = calculate_board_value(simulated_board_state);

				double board_score = score_after - score_before;

				possible_plays[i].score = board_score;
				
				if (cur_depth < max_depth && possible_plays[i].moves[0] != "END TURN")
				{
					card_play best_lower_action = calculate_best_move(new board_state(simulated_board_state), cur_depth + 1, max_depth);
					
					possible_plays[i].score += best_lower_action.score;
				}
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
				if (best_play.score > 5000)
				{
					//i have lethal, don't bother calculating any other options
					return best_play;
				}
			}
			
			return best_play;
		}

		List<card_play> get_possible_plays(board_state board)
		{
			List<card_play> possible_plays = new List<card_play>();

			//look at all the cards in hand that we can play
			//first look at spells and battlecries we can shoot at the enemy hero
			foreach (var cur_card in board.my_hand_cards.Values)
			{
				if (cur_card.mana_cost <= board.cur_mana)
				{
					if (herby_deck.ContainsKey(cur_card.name))
					{
						if (herby_deck[cur_card.name].type == "spell" || herby_deck[cur_card.name].type == "minion")
						{
							if (herby_deck[cur_card.name].target == "enemy")
							{
								//DAT IF CHAIN THO
								foreach (var enemy_card in board.enemy_field_cards.Values)
								{
									if (enemy_card.zone_name == "OPPOSING PLAY (Hero)" && enemy_card.tags.immune == false)
									{
										//target's the enemy hero, shoot his stupid face
										possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, enemy_card.local_id, herby_deck[cur_card.name].type } });
									}
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

			//then look at summoning minions and targeting non-hero enemies
			foreach (var cur_card in board.my_hand_cards.Values)
			{
				if (cur_card.mana_cost <= board.cur_mana)
				{
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
									string search_zone = "";
									bool contains = false;
									var check_cards = board.cards;
									switch (herby_deck[cur_card.name].target)
									{
										case "minion":
											search_zone = "PLAY";
											contains = true;
											check_cards = board.my_field_cards.Union(board.enemy_field_cards).ToDictionary(k => k.Key, v => v.Value);
											break;
										case "enemy":
											search_zone = "OPPOSING PLAY";
											contains = true;
											check_cards = board.enemy_field_cards;
											break;
										case "enemy_minion":
											search_zone = "OPPOSING PLAY";
											contains = false;
											check_cards = board.enemy_field_cards;
											break;
										case "friendly":
											search_zone = "FRIENDLY PLAY";
											contains = true;
											check_cards = board.my_field_cards;
											break;
										case "friendly_minion":
											search_zone = "FRIENDLY PLAY";
											contains = false;
											check_cards = board.my_field_cards;
											break;
										default:
											break;
									}
									
									foreach (var cur_target in check_cards.Values)
									{
										if (contains)
										{
											if (cur_target.zone_name.Contains(search_zone) && !cur_target.zone_name.Contains("Power") && !cur_target.zone_name.Contains("Weapon") && (cur_target.tags.stealth == false || cur_target.zone_name.Contains("FRIENDLY")))
											{
												if (cur_target.zone_name.Contains("Hero"))
												{
													continue;
												}
												possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_target.local_id, "minion" } });
											}
										}
										else
										{
											if (cur_target.zone_name == search_zone && (cur_target.tags.stealth == false || cur_target.zone_name.Contains("FRIENDLY")))
											{
												possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_target.local_id, "minion" } });
											}
										}
									}
								}
							}
						}
					}
					else if (cur_card.card_type == "ABILITY" || cur_card.card_type == "SPELL")
					{
						//spell or secret, make sure we have it defined
						if (herby_deck.ContainsKey(cur_card.name))
						{
							if (herby_deck[cur_card.name].type == "spell")
							{
								//card is a spell, figure out where to throw it
								if (herby_deck[cur_card.name].target != "none")
								{
									//spell has an explicit target, figure out who
									string search_zone = "";
									bool contains = false;
									var check_cards = board.cards;
									switch (herby_deck[cur_card.name].target)
									{
										case "minion":
											search_zone = "PLAY";
											contains = true;
											check_cards = board.my_field_cards.Union(board.enemy_field_cards).ToDictionary(k => k.Key, v => v.Value);
											break;
										case "enemy":
											search_zone = "OPPOSING PLAY";
											contains = true;
											check_cards = board.enemy_field_cards;
											break;
										case "enemy_minion":
											search_zone = "OPPOSING PLAY";
											contains = false;
											check_cards = board.enemy_field_cards;
											break;
										case "friendly":
											search_zone = "FRIENDLY PLAY";
											contains = true;
											check_cards = board.my_field_cards;
											break;
										case "friendly_minion":
											search_zone = "FRIENDLY PLAY";
											contains = false;
											check_cards = board.my_field_cards;
											break;
										default:
											break;
									}

									foreach (var cur_target in check_cards.Values)
									{
										if (contains)
										{
											if (cur_target.zone_name.Contains(search_zone) && !cur_target.zone_name.Contains("Power") && !cur_target.zone_name.Contains("Weapon") && (cur_target.tags.stealth == false || cur_target.zone_name.Contains("FRIENDLY")) && cur_target.tags.cant_be_targeted_by_abilities == false)
											{
												if (cur_target.zone_name.Contains("Hero"))
												{
													continue;
												}
												possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_target.local_id, "spell" } });
											}
										}
										else
										{
											if (cur_target.zone_name == search_zone && cur_target.tags.stealth == false && cur_target.tags.cant_be_targeted_by_abilities == false)
											{
												possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_target.local_id, "spell" } });
											}
										}
									}
								}
								else
								{
									//spell has no target, either does a random target, goes for face, or summons something
									possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, "spell" } });
								}
							}
							else if (herby_deck[cur_card.name].type == "secret")
							{
								//playing a secret, make sure no other secrets of the same type already exist
								if (board.check_if_secret_in_play() == true)
								{
									//this secret is already in play, can't use it
									continue;
								}

								possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, "secret" } });
							}
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
			}

			//get every combination of attacks, from my minions to their minions and their hero
			//but before that, check if their field has taunts
			bool enemy_has_taunt = board.check_if_taunt_in_play(true);
			
			//now loop through all of our minions that can attack
			foreach (var cur_card in board.my_field_cards.Values)
			{
				if ((cur_card.zone_name == "FRIENDLY PLAY" || cur_card.zone_name == "FRIENDLY PLAY (Hero)") && cur_card.tags.exhausted == false && cur_card.tags.frozen == false && cur_card.tags.cant_attack == false)
				{
					//check if the current card even has attack
					if (cur_card.atk == 0)
					{
						//no attack on this dude, skip him
						continue;
					}

					//this is our minion, and it can attack
					//now loop through all the enemy minions, and the enemy hero
					//first, the enemy hero
					if (!enemy_has_taunt)
					{
						foreach (var cur_enemy in board.enemy_field_cards.Values)
						{
							if (cur_enemy.zone_name == "OPPOSING PLAY (Hero)" && cur_enemy.tags.immune == false)
							{
								possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_enemy.local_id } });
							}
						}
					}

					//then all the minions
					foreach (var cur_enemy in board.enemy_field_cards.Values)
					{
						if (enemy_has_taunt)
						{
							//one or more taunt minions, only look for them
							if (cur_enemy.zone_name == "OPPOSING PLAY" && cur_enemy.tags.taunt == true && cur_enemy.tags.stealth == false && cur_enemy.tags.immune == false)
							{
								possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_enemy.local_id } });
							}
						}
						else
						{
							//no taunts, target everything
							//no don't target everything, this is way too fucking slow, and unnecessary (do me trade? NOPE)
							//instead target everything in our high priority list or with a really high attack
							//also target everything if enemy has lethal
							//but only target enemy face if i have lethal
							int total_atk = 0;
							total_atk = board.check_attack_on_board(true);
							int my_atk = 0;
							my_atk = board.check_attack_on_board(false);

							if (my_atk < board.enemy_health)
							{
								if (cur_enemy.zone_name == "OPPOSING PLAY" && cur_enemy.tags.stealth == false && cur_enemy.tags.immune == false && ((this.high_prio_targets.Contains(cur_enemy.name) && cur_enemy.tags.silenced == false) || cur_enemy.atk > 4 || total_atk >= board.my_health))
								{
									possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id, cur_enemy.local_id } });
								}
							}
						}
					}
				}
			}

			//finally, end turn and hero power
			foreach (var cur_card in board.my_field_cards.Values)
			{
				if (cur_card.zone_name == "FRIENDLY PLAY (Hero Power)" && cur_card.mana_cost <= board.cur_mana && !cur_card.tags.exhausted)
				{
					possible_plays.Add(new card_play { moves = new List<string> { cur_card.local_id } });
					break;
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

		board_state simulate_board_state(board_state simmed_board, card_play action)
		{
			if (action.moves.Count() == 1)
			{
				//super simple. either end turn or hero power
				if (action.moves[0] == "END TURN")
				{
					//NOTHIIIIIIING
					//no really, make no changes to the board
				}
				else
				{
					//only other action that has a single play is the hero power
					simmed_board.deal_damage_to_hero(2);
					simmed_board.cur_mana -= simmed_board.cards[action.moves[0]].mana_cost;
					simmed_board.cards[action.moves[0]].tags.exhausted = true;

					//run the inspire code for each minion on my field that has it
					foreach (var cur_card in simmed_board.my_field_cards.Values)
					{
						if (herby_deck.ContainsKey(cur_card.name) && herby_deck[cur_card.name].inspire != null && !cur_card.tags.silenced)
						{
							herby_deck[cur_card.name].inspire(cur_card, simmed_board);
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
							herby_deck[simmed_board.cards[action.moves[0]].name].deathrattle(simmed_board.cards[action.moves[0]], simmed_board);
						}

						//do the same thing for the minion i just traded with
						if (simmed_board.my_hero_id == action.moves[0])
						{
							//my hero is attacking, lower the durability of his weapon
							foreach (var card in simmed_board.my_field_cards.Values)
							{
								if (card.zone_name == "FRIENDLY PLAY (Weapon)")
								{
									if (--card.durability == 0)
									{
										simmed_board.remove_card_from_zone(card.local_id);
									}
									break;
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
						if (simmed_board.cards[action.moves[0]].tags.charge == false)
						{
							simmed_board.cards[action.moves[0]].tags.exhausted = true;
						}
						
						if (herby_deck.ContainsKey(simmed_board.cards[action.moves[0]].name))
						{
							//check if minion's got a battlecry, and run it
							if (herby_deck[simmed_board.cards[action.moves[0]].name].battlecry != null)
							{
								herby_deck[simmed_board.cards[action.moves[0]].name].battlecry(simmed_board.cards[action.moves[0]], simmed_board, new card());
							}
						}
					}
					else if (action.moves[1] == "spell")
					{
						//casting a spell without a target
						herby_deck[simmed_board.cards[action.moves[0]].name].effect(simmed_board.cards[action.moves[0]], simmed_board, new card());
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
								herby_deck[simmed_board.cards[action.moves[0]].name].battlecry(simmed_board.cards[action.moves[0]], simmed_board, new card());
							}
						}
					}
					simmed_board.cur_mana -= simmed_board.cards[action.moves[0]].mana_cost;
				}
			}
			else if (action.moves.Count() == 3)
			{
				//this is a spell with a target, or a minion with a targetted battlecry
				if (action.moves[2] == "minion")
				{
					//summoning a minion
					simmed_board.add_card_to_zone(action.moves[0], "FRIENDLY PLAY");
					if (simmed_board.cards[action.moves[0]].tags.charge == false)
					{
						simmed_board.cards[action.moves[0]].tags.exhausted = true;
					}
					
					herby_deck[simmed_board.cards[action.moves[0]].name].battlecry(simmed_board.cards[action.moves[0]], simmed_board, simmed_board.cards[action.moves[1]]);
				}
				else if (action.moves[2] == "spell")
				{
					//casting a spell
					simmed_board.remove_card_from_zone(action.moves[0]);
					herby_deck[simmed_board.cards[action.moves[0]].name].effect(simmed_board.cards[action.moves[0]], simmed_board, simmed_board.cards[action.moves[1]]);
				}
				simmed_board.cur_mana -= simmed_board.cards[action.moves[0]].mana_cost;
			}

			if ((action.moves.Count() == 2 || action.moves.Count() == 3) && action.moves[1].All(char.IsDigit))
			{
				if (simmed_board.cards[action.moves[1]].get_cur_health() <= 0 && herby_deck.ContainsKey(simmed_board.cards[action.moves[1]].name) && herby_deck[simmed_board.cards[action.moves[1]].name].deathrattle != null && !simmed_board.cards[action.moves[1]].tags.silenced)
				{
					herby_deck[simmed_board.cards[action.moves[1]].name].deathrattle(simmed_board.cards[action.moves[1]], simmed_board);
				}
			}

			return simmed_board;
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

			//merge all the various card dictionaries into one big one
			//this is super fucked
			var all_cards = board.my_field_cards.Union(board.my_hand_cards).Union(board.enemy_field_cards).ToDictionary(k => k.Key, v => v.Value);
			foreach (var cur_card in all_cards.Values)
			{
				double cur_card_value = 0;
				
				if ((cur_card.zone_name.Contains("PLAY") || cur_card.zone_name.Contains("HAND")) && !cur_card.zone_name.Contains("(Hero)"))
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
						int total_atk = 0;
						total_atk = board.check_attack_on_board(true);

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
						cur_card_value += 1;
					}
				}
				else if (cur_card.zone_name == "OPPOSING PLAY (Hero)")
				{
					if (cur_card.get_cur_health() > 0)
					{
						cur_card_value += cur_card.get_cur_health() * ENEMY_HERO_WEIGHT * -1;
						if (cur_card.get_cur_health() <= 15)
						{
							//enemy hero is getting low on health, rank him increasingly higher
							cur_card_value += 300 / (Math.Max(1, cur_card.get_cur_health()));
						}
					}
					else
					{
						//enemy hero is dead in this board state. rank this super high
						cur_card_value += 9999;
					}
				}
				else if (cur_card.zone_name == "FRIENDLY PLAY (Hero)")
				{
					if (this.cur_board.check_attack_on_board(true) >= cur_card.get_cur_health() && !board.check_if_taunt_in_play())
					{
						//enemy has lethal, mark this kinda low
						cur_card_value -= 100;
					}
					else if (cur_card.get_cur_health() > 0)
					{
						cur_card_value += cur_card.get_cur_health() * MY_HERO_WEIGHT;
					}
					else
					{
						//i'm dead in this scenario. mark this super low
						cur_card_value -= 9999;
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

						if (total_atk >= this.cur_board.my_health && !this.cur_board.check_if_taunt_in_play() && this.cur_board.enemy_health > 0)
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
			attr_array = line.Split(new string[] { "name=" }, 0);
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

			this.game_player.DoWork += new DoWorkEventHandler(
			delegate(object o, DoWorkEventArgs args)
			{
				while (this.running == true)
				{
					main_loop();
					Thread.Sleep(1);
				}
				set_status_text("Inactive");
				set_action_text("");
				status.BackColor = Color.FromArgb(255, 170, 170);
			});

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
			click_and_drag(244, 412, 840, 400, 100, 100);
			click_location(866, 228, true, 50);
			click_location(1160, 340, true, 50);
			click_location(1020, 720, true, 50);
			click_location(700, 700, true, 50);
			click_location(600, 330, true, 50);
			click_location(850, 470, true, 50);
		}

		void concede()
		{
			open_options_menu();
			click_location(this.board_position_boxes["CONCEDE BUTTON"], true, 2000);
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

			foreach (var file in dir.EnumerateFiles(type + "*"))
			{
				file.Delete();
			}
			
			File.Create(this.db_path + type + "_" + count).Close();

			this.rand = new Random();
		}

		void open_options_menu()
		{
			//click a bunch of times near options make sure the options window isn't already open
			for (int i = 0; i < 10; i++)
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

					this.board_position_boxes[position.Name] = new board_position_box(left, right, top, bottom);
				}
			}
		}

		public void get_config_settings(Dictionary<string, dynamic> config)
		{
			this.log_location = config["general"]["log_location"].ToString();
			this.db_path = config["general"]["dropbox_location"].ToString();

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
				this.log_output.Visible = true;
				this.Width = 1000;
				this.Height = 600;
			}
			else
			{
				this.log_output.Visible = false;
				this.Width = 300;
				this.Height = 220;
			}
		}
    }
}
