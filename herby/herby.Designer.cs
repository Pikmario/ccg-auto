namespace Herby
{
    partial class Herby
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
			this.chk_view_log = new System.Windows.Forms.CheckBox();
			this.status = new System.Windows.Forms.Label();
			this.log_output = new System.Windows.Forms.RichTextBox();
			this.status_label = new System.Windows.Forms.Label();
			this.num_wins_label = new System.Windows.Forms.Label();
			this.num_wins_text = new System.Windows.Forms.Label();
			this.num_losses_label = new System.Windows.Forms.Label();
			this.num_losses_text = new System.Windows.Forms.Label();
			this.kill_game_label = new System.Windows.Forms.Label();
			this.kill_wins_spinner = new System.Windows.Forms.NumericUpDown();
			this.kill_losses_spinner = new System.Windows.Forms.NumericUpDown();
			this.kill_wins_label = new System.Windows.Forms.Label();
			this.kill_losses_label = new System.Windows.Forms.Label();
			this.lbl_x = new System.Windows.Forms.Label();
			this.lbl_y = new System.Windows.Forms.Label();
			this.x_coord = new System.Windows.Forms.Label();
			this.y_coord = new System.Windows.Forms.Label();
			this.action_label = new System.Windows.Forms.Label();
			this.action = new System.Windows.Forms.Label();
			this.chk_copy_log = new System.Windows.Forms.CheckBox();
			((System.ComponentModel.ISupportInitialize)(this.kill_wins_spinner)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.kill_losses_spinner)).BeginInit();
			this.SuspendLayout();
			// 
			// chk_view_log
			// 
			this.chk_view_log.AutoSize = true;
			this.chk_view_log.Location = new System.Drawing.Point(12, 155);
			this.chk_view_log.Name = "chk_view_log";
			this.chk_view_log.Size = new System.Drawing.Size(70, 17);
			this.chk_view_log.TabIndex = 22;
			this.chk_view_log.Text = "View Log";
			this.chk_view_log.UseVisualStyleBackColor = true;
			this.chk_view_log.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
			// 
			// status
			// 
			this.status.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(170)))), ((int)(((byte)(170)))));
			this.status.Location = new System.Drawing.Point(12, 32);
			this.status.Name = "status";
			this.status.Size = new System.Drawing.Size(180, 34);
			this.status.TabIndex = 23;
			this.status.Text = "Inactive";
			this.status.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// log_output
			// 
			this.log_output.Location = new System.Drawing.Point(12, 178);
			this.log_output.Name = "log_output";
			this.log_output.Size = new System.Drawing.Size(960, 372);
			this.log_output.TabIndex = 21;
			this.log_output.Text = "";
			this.log_output.Visible = false;
			// 
			// status_label
			// 
			this.status_label.Location = new System.Drawing.Point(12, 9);
			this.status_label.Name = "status_label";
			this.status_label.Size = new System.Drawing.Size(180, 23);
			this.status_label.TabIndex = 26;
			this.status_label.Text = "Current Status";
			this.status_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// num_wins_label
			// 
			this.num_wins_label.AutoSize = true;
			this.num_wins_label.Location = new System.Drawing.Point(198, 13);
			this.num_wins_label.Name = "num_wins_label";
			this.num_wins_label.Size = new System.Drawing.Size(34, 13);
			this.num_wins_label.TabIndex = 27;
			this.num_wins_label.Text = "Wins:";
			// 
			// num_wins_text
			// 
			this.num_wins_text.AutoSize = true;
			this.num_wins_text.Location = new System.Drawing.Point(259, 13);
			this.num_wins_text.Name = "num_wins_text";
			this.num_wins_text.Size = new System.Drawing.Size(13, 13);
			this.num_wins_text.TabIndex = 28;
			this.num_wins_text.Text = "0";
			this.num_wins_text.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// num_losses_label
			// 
			this.num_losses_label.AutoSize = true;
			this.num_losses_label.Location = new System.Drawing.Point(198, 30);
			this.num_losses_label.Name = "num_losses_label";
			this.num_losses_label.Size = new System.Drawing.Size(43, 13);
			this.num_losses_label.TabIndex = 29;
			this.num_losses_label.Text = "Losses:";
			// 
			// num_losses_text
			// 
			this.num_losses_text.AutoSize = true;
			this.num_losses_text.Location = new System.Drawing.Point(259, 30);
			this.num_losses_text.Name = "num_losses_text";
			this.num_losses_text.Size = new System.Drawing.Size(13, 13);
			this.num_losses_text.TabIndex = 30;
			this.num_losses_text.Text = "0";
			this.num_losses_text.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
			// 
			// kill_game_label
			// 
			this.kill_game_label.AutoSize = true;
			this.kill_game_label.Location = new System.Drawing.Point(201, 67);
			this.kill_game_label.Name = "kill_game_label";
			this.kill_game_label.Size = new System.Drawing.Size(76, 13);
			this.kill_game_label.TabIndex = 31;
			this.kill_game_label.Text = "Kill game after:";
			// 
			// kill_wins_spinner
			// 
			this.kill_wins_spinner.Location = new System.Drawing.Point(201, 88);
			this.kill_wins_spinner.Name = "kill_wins_spinner";
			this.kill_wins_spinner.Size = new System.Drawing.Size(35, 20);
			this.kill_wins_spinner.TabIndex = 32;
			// 
			// kill_losses_spinner
			// 
			this.kill_losses_spinner.Location = new System.Drawing.Point(201, 115);
			this.kill_losses_spinner.Name = "kill_losses_spinner";
			this.kill_losses_spinner.Size = new System.Drawing.Size(35, 20);
			this.kill_losses_spinner.TabIndex = 33;
			// 
			// kill_wins_label
			// 
			this.kill_wins_label.AutoSize = true;
			this.kill_wins_label.Location = new System.Drawing.Point(242, 90);
			this.kill_wins_label.Name = "kill_wins_label";
			this.kill_wins_label.Size = new System.Drawing.Size(28, 13);
			this.kill_wins_label.TabIndex = 34;
			this.kill_wins_label.Text = "wins";
			// 
			// kill_losses_label
			// 
			this.kill_losses_label.AutoSize = true;
			this.kill_losses_label.Location = new System.Drawing.Point(242, 117);
			this.kill_losses_label.Name = "kill_losses_label";
			this.kill_losses_label.Size = new System.Drawing.Size(36, 13);
			this.kill_losses_label.TabIndex = 35;
			this.kill_losses_label.Text = "losses";
			// 
			// lbl_x
			// 
			this.lbl_x.AutoSize = true;
			this.lbl_x.Location = new System.Drawing.Point(161, 156);
			this.lbl_x.Name = "lbl_x";
			this.lbl_x.Size = new System.Drawing.Size(17, 13);
			this.lbl_x.TabIndex = 36;
			this.lbl_x.Text = "X:";
			// 
			// lbl_y
			// 
			this.lbl_y.AutoSize = true;
			this.lbl_y.Location = new System.Drawing.Point(218, 156);
			this.lbl_y.Name = "lbl_y";
			this.lbl_y.Size = new System.Drawing.Size(17, 13);
			this.lbl_y.TabIndex = 37;
			this.lbl_y.Text = "Y:";
			// 
			// x_coord
			// 
			this.x_coord.AutoSize = true;
			this.x_coord.Location = new System.Drawing.Point(181, 156);
			this.x_coord.Name = "x_coord";
			this.x_coord.Size = new System.Drawing.Size(31, 13);
			this.x_coord.TabIndex = 38;
			this.x_coord.Text = "1440";
			// 
			// y_coord
			// 
			this.y_coord.AutoSize = true;
			this.y_coord.Location = new System.Drawing.Point(241, 156);
			this.y_coord.Name = "y_coord";
			this.y_coord.Size = new System.Drawing.Size(31, 13);
			this.y_coord.TabIndex = 39;
			this.y_coord.Text = "1000";
			// 
			// action_label
			// 
			this.action_label.Location = new System.Drawing.Point(12, 67);
			this.action_label.Name = "action_label";
			this.action_label.Size = new System.Drawing.Size(180, 23);
			this.action_label.TabIndex = 26;
			this.action_label.Text = "Current Action";
			this.action_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// action
			// 
			this.action.BackColor = System.Drawing.SystemColors.Control;
			this.action.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.action.Location = new System.Drawing.Point(12, 90);
			this.action.Name = "action";
			this.action.Size = new System.Drawing.Size(180, 34);
			this.action.TabIndex = 23;
			this.action.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// chk_copy_log
			// 
			this.chk_copy_log.AutoSize = true;
			this.chk_copy_log.Location = new System.Drawing.Point(89, 155);
			this.chk_copy_log.Name = "chk_copy_log";
			this.chk_copy_log.Size = new System.Drawing.Size(71, 17);
			this.chk_copy_log.TabIndex = 40;
			this.chk_copy_log.Text = "Copy Log";
			this.chk_copy_log.UseVisualStyleBackColor = true;
			// 
			// Herby
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(406, 232);
			this.Controls.Add(this.chk_copy_log);
			this.Controls.Add(this.y_coord);
			this.Controls.Add(this.x_coord);
			this.Controls.Add(this.lbl_y);
			this.Controls.Add(this.lbl_x);
			this.Controls.Add(this.kill_losses_label);
			this.Controls.Add(this.kill_wins_label);
			this.Controls.Add(this.kill_losses_spinner);
			this.Controls.Add(this.kill_wins_spinner);
			this.Controls.Add(this.kill_game_label);
			this.Controls.Add(this.num_losses_text);
			this.Controls.Add(this.num_losses_label);
			this.Controls.Add(this.num_wins_text);
			this.Controls.Add(this.num_wins_label);
			this.Controls.Add(this.action_label);
			this.Controls.Add(this.status_label);
			this.Controls.Add(this.action);
			this.Controls.Add(this.status);
			this.Controls.Add(this.chk_view_log);
			this.Controls.Add(this.log_output);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.MaximizeBox = false;
			this.Name = "Herby";
			this.Text = "Herby";
			((System.ComponentModel.ISupportInitialize)(this.kill_wins_spinner)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.kill_losses_spinner)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

        }

        #endregion

		private System.Windows.Forms.CheckBox chk_view_log;
		private System.Windows.Forms.Label status;
		private System.Windows.Forms.RichTextBox log_output;
		private System.Windows.Forms.Label status_label;
		private System.Windows.Forms.Label num_wins_label;
		private System.Windows.Forms.Label num_wins_text;
		private System.Windows.Forms.Label num_losses_label;
		private System.Windows.Forms.Label num_losses_text;
		private System.Windows.Forms.Label kill_game_label;
		private System.Windows.Forms.NumericUpDown kill_wins_spinner;
		private System.Windows.Forms.NumericUpDown kill_losses_spinner;
		private System.Windows.Forms.Label kill_wins_label;
		private System.Windows.Forms.Label kill_losses_label;
		private System.Windows.Forms.Label lbl_x;
		private System.Windows.Forms.Label lbl_y;
		private System.Windows.Forms.Label x_coord;
		private System.Windows.Forms.Label y_coord;
		private System.Windows.Forms.Label action_label;
		private System.Windows.Forms.Label action;
		private System.Windows.Forms.CheckBox chk_copy_log;

		


	}
}

