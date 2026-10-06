using System.ComponentModel;
using Sidebar;

namespace SGPkgMgr.Models
{
	/// <summary>
	/// 一个 Application 的编辑模型。所有属性都是可绑定的。
	/// </summary>
	public class ApplicationEntry: INotifyPropertyChanged
	{
		private string _id = "App";
		private string _executable = "Tile.dll";
		private string _entryPoint = "";
		private bool _hasFlyout = true;
		private bool _hasProperties = false;
		private string _displayName = "";
		private string _logo = "Icon.ico";
		private double _minHeight = 28;
		private double _maxHeight = 300;
		private double _defaultHeight = 97;
		private bool _canPinBottom = true;
		private TileOverflow _overflow = TileOverflow.Hidden;
		private double _flyoutWidth = 378;
		private double _flyoutHeight = 721;
		private bool _flyoutCanResize = false;

		public string Id { get { return _id; } set { _id = value; Raise ("Id"); } }
		public string Executable { get { return _executable; } set { _executable = value; Raise ("Executable"); } }
		public string EntryPoint { get { return _entryPoint; } set { _entryPoint = value; Raise ("EntryPoint"); } }
		public bool HasFlyout { get { return _hasFlyout; } set { _hasFlyout = value; Raise ("HasFlyout"); } }
		public bool HasProperties { get { return _hasProperties; } set { _hasProperties = value; Raise ("HasProperties"); } }
		public string DisplayName { get { return _displayName; } set { _displayName = value; Raise ("DisplayName"); } }
		public string Logo { get { return _logo; } set { _logo = value; Raise ("Logo"); } }
		public double MinHeight { get { return _minHeight; } set { _minHeight = value; Raise ("MinHeight"); } }
		public double MaxHeight { get { return _maxHeight; } set { _maxHeight = value; Raise ("MaxHeight"); } }
		public double DefaultHeight { get { return _defaultHeight; } set { _defaultHeight = value; Raise ("DefaultHeight"); } }
		public bool CanPinBottom { get { return _canPinBottom; } set { _canPinBottom = value; Raise ("CanPinBottom"); } }
		public TileOverflow Overflow { get { return _overflow; } set { _overflow = value; Raise ("Overflow"); } }
		public double FlyoutWidth { get { return _flyoutWidth; } set { _flyoutWidth = value; Raise ("FlyoutWidth"); } }
		public double FlyoutHeight { get { return _flyoutHeight; } set { _flyoutHeight = value; Raise ("FlyoutHeight"); } }
		public bool FlyoutCanResize { get { return _flyoutCanResize; } set { _flyoutCanResize = value; Raise ("FlyoutCanResize"); } }

		public static ApplicationEntry FromModel (TileApplication app)
		{
			var entry = new ApplicationEntry ();
			if (app == null) return entry;
			entry.Id = app.Id ?? "App";
			entry.Executable = app.Executable ?? "";
			entry.EntryPoint = app.EntryPoint ?? "";
			entry.HasFlyout = app.HasFlyout;
			entry.HasProperties = app.HasProperties;
			var ve = app.VisualElements;
			if (ve != null)
			{
				entry.DisplayName = ve.DisplayName ?? "";
				entry.Logo = ve.Logo ?? "";
				if (ve.Tile != null)
				{
					entry.MinHeight = ve.Tile.MinHeight;
					entry.MaxHeight = ve.Tile.MaxHeight;
					entry.DefaultHeight = ve.Tile.DefaultHeight;
					entry.CanPinBottom = ve.Tile.CanPinBottom;
					entry.Overflow = ve.Tile.Overflow;
				}
				if (ve.Flyout != null)
				{
					entry.FlyoutWidth = ve.Flyout.FlyoutWidth;
					entry.FlyoutHeight = ve.Flyout.FlyoutHeight;
					entry.FlyoutCanResize = ve.Flyout.FlyoutCanResize;
				}
			}
			return entry;
		}

		public TileApplication ToModel ()
		{
			var ve = new TileVisualElements (
				DisplayName,
				Logo,
				new TileVisualElementTile (MinHeight, MaxHeight, DefaultHeight, CanPinBottom, Overflow),
				new TileVisualElementFlyout (FlyoutWidth, FlyoutHeight, FlyoutCanResize));
			return new TileApplication (Id, Executable, EntryPoint, HasFlyout, HasProperties, ve);
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void Raise (string name)
		{
			var h = PropertyChanged;
			if (h != null) h (this, new PropertyChangedEventArgs (name));
		}
	}
}