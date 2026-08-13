namespace WindowsModern.PowerTile
{
	partial class PowerHostForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose (bool disposing)
		{
			powerChanged?.Clear ();
			if (disposing && (components != null))
			{
				components.Dispose ();
			}
			base.Dispose (disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent ()
		{
			this.SuspendLayout();
			// 
			// PowerHostForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(483, 271);
			this.MaximizeBox = false;
			this.Name = "PowerHostForm";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.Text = "Power Host Form";
			this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
			//this.Visible = false;
			this.ResumeLayout(false);

		}

		#endregion
	}
}