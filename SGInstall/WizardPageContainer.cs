using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.ObjectModel;
using Sidebar;
namespace SGInstall
{
	public partial class WizardPageContainer: UserControl, IWizardPageContainer
	{
		public WizardPageContainer ()
		{
			InitializeComponent ();
			pages.CollectionChanged += Pages_CollectionChanged;
			this.Font = FontHelper.GetFirstAvailableFont (FontHelper.preferredFonts);
		}
		private void Pages_CollectionChanged (object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
				case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
					foreach (WizardPage i in e.NewItems)
					{
						i.PageContainer = this;
					}
					RefreshIndex (); break;
				case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
				case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
				case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
					foreach (WizardPage i in e.OldItems)
					{
						i.PageContainer = null;
						i.IsLoaded = false;
					}
					RefreshIndex ();
					if (index < 0) RefreshCurrentPage ();
					break;
			}
		}
		private ObservableCollection<WizardPage> pages = new ObservableCollection<WizardPage> ();
		public ObservableCollection<WizardPage> Pages => pages;
		private string currId = null;
		public string CurrentId
		{
			get { return currId; }
			set
			{
				if (!InvokeWillUnload () || !InvokeAlreadyUnload ())
					return;
				currId = value;
				RefreshIndex ();
				RefreshCurrentPage ();
				InvokeWillLoad ();
				InvokeAlreadyLoad ();
			}
		}
		private void RefreshIndex ()
		{
			var i = 0;
			index = -1;
			foreach (var p in pages)
			{
				if (p?.Id?.NEquals (currId) ?? false)
					index = i;
				else i++;
			}
		}
		private int index = -1;
		public int CurrentIndex
		{
			get { return index; }
			set
			{
				try
				{
					if (!InvokeWillUnload () || !InvokeAlreadyUnload ())
						return;
					index = value;
					var p = pages [value];
					currId = p.Id;
					InvokeWillLoad ();
					InvokeAlreadyLoad ();
				}
				catch
				{
					index = -1;
					currId = null;
				}
				finally
				{
					RefreshCurrentPage ();
				}
			}
		} 
		public WizardPage CurrentPage
		{
			get
			{
				foreach (var p in pages)
				{
					if (p?.Id?.NEquals (currId) ?? false)
						return p;
				}
				return null;
			}
		}
		public bool IsPageFirst
		{
			get
			{
				try
				{
					var p = pages [0];
					return p?.Id?.NEquals (currId) ?? false;
				}
				catch { return true; }
			}
		}
		public bool IsPageLast
		{
			get
			{
				try
				{
					var p = pages [pages.Count - 1];
					return p?.Id?.NEquals (currId) ?? false;
				}
				catch { return true; }
			}
		}
		private bool InvokeWillLoad ()
		{
			var cp = CurrentPage;
			if (cp == null) return true;
			try
			{
				return cp.OnWillLoad ();
			}
			catch (Exception e)
			{
				MessageBox.Show (e.Message, e.GetType ().ToString (), MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}
		private bool InvokeWillUnload ()
		{
			var cp = CurrentPage;
			if (cp == null) return true;
			try
			{
				return cp.OnWillUnload ();
			}
			catch (Exception e)
			{
				MessageBox.Show (e.Message, e.GetType ().ToString (), MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}
		private bool InvokeAlreadyLoad ()
		{
			var cp = CurrentPage;
			if (cp == null) return true;
			try
			{
				cp.OnAlreadyLoad ();
				cp.IsLoaded = true;
				return true;
			}
			catch (Exception e)
			{
				MessageBox.Show (e.Message, e.GetType ().ToString (), MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}
		private bool InvokeAlreadyUnload ()
		{
			var cp = CurrentPage;
			if (cp == null) return true;
			try
			{
				cp.OnAlreadyUnload ();
				cp.IsLoaded = false;
				return true;
			}
			catch (Exception e)
			{
				MessageBox.Show (e.Message, e.GetType ().ToString (), MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}
		public void RefreshCurrentPage ()
		{
			{
				buttonBack.Visible = true;
				buttonBack.Enabled = !IsPageFirst;
				buttonBack.Text = Program.StringResources.SuitableResource ("INSTALLER_BTN_BACK", "< Back");
				buttonNext.Visible = true;
				buttonNext.Enabled = !IsPageLast;
				buttonNext.Text = Program.StringResources.SuitableResource ("INSTALLER_BTN_NEXT", "Next >");
				buttonCancel.Visible = true;
				buttonCancel.Enabled = true;
				buttonCancel.Text = Program.StringResources.SuitableResource ("INSTALLER_BTN_CANCEL", "Cancel");
			}
			panel1?.Controls?.Clear ();
			var page = pages.Where (e => e?.Id?.NEquals (currId) ?? false);
			if (page.Count () > 0)
			{
				var p = page.ElementAt (0);
				panel1?.Controls?.Add (p);
				p.Dock = DockStyle.Fill;
				buttonBack.Visible = p.ShowBack;
				buttonBack.Enabled = p.CanBack;
				if (IsPageFirst) buttonBack.Enabled = false;
				buttonBack.Text = p.BackButtonTitle;
				buttonNext.Visible = p.ShowNext;
				buttonNext.Enabled = p.CanNext;
				if (IsPageLast) buttonNext.Enabled = false;
				buttonNext.Text = p.NextButtonTitle;
				buttonCancel.Visible = p.ShowCancel;
				buttonCancel.Enabled = p.CanCancel;
				buttonCancel.Text = p.CancelButtonTitle;
			}
			Program.ReleaseLargeResourcesAsync ();
		}
		private void buttonBack_Click (object sender, EventArgs e)
		{
			try
			{
				if (CurrentPage?.OnBackButtonClick (sender, e) == false)
					return;
				Back ();
			}
			catch (Exception ex)
			{
				MessageBox.Show (ex.Message, e.GetType ().ToString (), MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		private void buttonNext_Click (object sender, EventArgs e)
		{
			try
			{
				if (CurrentPage?.OnNextButtonClick (sender, e) == false)
					return;
				Next ();
			}
			catch (Exception ex)
			{
				MessageBox.Show (ex.Message, e.GetType ().ToString (), MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		private void buttonCancel_Click (object sender, EventArgs e)
		{
			try
			{
				if (CurrentPage?.OnCancelButtonClick (sender, e) == false)
					return;
				Cancel ();
			}
			catch (Exception ex)
			{
				MessageBox.Show (ex.Message, e.GetType ().ToString (), MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		public void Next ()
		{
			if (IsPageLast) return;
			CurrentIndex++;
		}
		public void Back ()
		{
			if (IsPageFirst) return;
			CurrentIndex--;
		}
		public void Jump (string id)
		{
			CurrentId = id;
		}
		public void Jump (int index)
		{
			CurrentIndex = index;
		}
		public void Cancel ()
		{
			var parent = this.Parent as Form;
			parent.Close ();
		}
		public bool Mail (string srcId, string tgId, string name, object data, Type dataType)
		{
			foreach (var p in pages)
			{
				if (p?.Id?.NEquals (tgId) ?? false)
				{
					return p.Receive (srcId, name, data, dataType);
				}
			}
			return false;
		}
		public bool Mail (string srcId, string tgId, string name, object data) =>
			Mail (srcId, tgId, name, data, data?.GetType ());
	}
}
