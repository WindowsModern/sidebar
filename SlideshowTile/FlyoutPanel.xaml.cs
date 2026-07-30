using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Sidebar;

namespace WindowsModern.SlideshowTile
{
	public partial class FlyoutPanel: UserControl
	{
		private int _currentIndex = -1;
		private string _currentFile = null;

		public FlyoutPanel ()
		{
			InitializeComponent ();
			InitStrings ();
		}

		private void FlyoutPanel_Loaded (object sender, RoutedEventArgs e)
		{
			Tile.ImageFiles.CollectionChanged += ImageFiles_CollectionChanged;
		}

		private void FlyoutPanel_Unloaded (object sender, RoutedEventArgs e)
		{
			if (Image != null) Image.Source = null;
			Tile.ImageFiles.CollectionChanged -= ImageFiles_CollectionChanged;
		}

		public void InitStrings ()
		{
			var sr = Tile.StringResources;
			BtnBack.ToolTip = sr?.SuitableResource ("FLYOUT_BTN_BACK");
			BtnNext.ToolTip = sr?.SuitableResource ("FLYOUT_BTN_NEXT");
			BtnOpenFile.ToolTip = sr?.SuitableResource ("FLYOUT_BTN_OPENFILE");
		}

		public void SetFile (string file)
		{
			_currentFile = file;
			_currentIndex = FindImageIndex (file);

			if (_currentIndex >= 0)
			{
				UpdateDisplay ();
			}
			else if (Tile.ImageFiles.Count > 0)
			{
				_currentIndex = 0;
				_currentFile = Tile.ImageFiles [0];
				UpdateDisplay ();
			}
			else
			{
				ClearDisplay ();
			}
		}

		private int FindImageIndex (string path)
		{
			if (string.IsNullOrEmpty (path) || Tile.ImageFiles.Count == 0)
				return -1;

			for (int i = 0; i < Tile.ImageFiles.Count; i++)
			{
				if (string.Equals (Tile.ImageFiles [i], path, StringComparison.OrdinalIgnoreCase))
					return i;
			}
			return -1;
		}

		private void UpdateDisplay ()
		{
			if (_currentIndex < 0 || _currentIndex >= Tile.ImageFiles.Count)
			{
				ClearDisplay ();
				return;
			}

			_currentFile = Tile.ImageFiles [_currentIndex];
			FileName.Text = System.IO.Path.GetFileName (_currentFile);
			FileName.ToolTip = _currentFile;

			try
			{
				var bitmap = new BitmapImage ();
				bitmap.BeginInit ();
				bitmap.UriSource = new Uri (_currentFile, UriKind.Absolute);
				bitmap.CacheOption = BitmapCacheOption.None;
				bitmap.EndInit ();
				var whratio = bitmap.Width / bitmap.Height;
				var flyoutWidth = Tile.TileInstance.Manifest.VisualElements.RailStyle.FlyoutWidth;
				var flyoutHeight = Tile.TileInstance.Manifest.VisualElements.RailStyle.FlyoutHeight;
				if (whratio > 1)
				{
					if (bitmap.Width > flyoutWidth)
						bitmap.DecodePixelWidth = (int)(flyoutWidth * UITheme.DPIDouble);
				}
				else if (whratio <= 1)
				{
					if (bitmap.Height > flyoutHeight)
						bitmap.DecodePixelWidth = (int)(flyoutHeight * UITheme.DPIDouble);
				}
				bitmap.Freeze ();
				Image.Source = bitmap;
			}
			catch (Exception ex)
			{
				Debug.WriteLine ($"Flyout load image error: {ex.Message}");
				Application.Current.Dispatcher.BeginInvoke (new Action (() => {
					if (_currentIndex >= 0 && _currentIndex < Tile.ImageFiles.Count)
						Tile.ImageFiles.RemoveAt (_currentIndex);
				}));
				ClearDisplay ();
			}
		}

		private void ClearDisplay ()
		{
			FileName.Text = "";
			FileName.ToolTip = null;
			Image.Source = null;
			_currentIndex = -1;
			_currentFile = null;
		}

		private void BtnBack_Click (object sender, RoutedEventArgs e)
		{
			if (Tile.ImageFiles.Count == 0) return;

			_currentIndex--;
			if (_currentIndex < 0)
				_currentIndex = Tile.ImageFiles.Count - 1;

			UpdateDisplay ();
		}

		private void BtnNext_Click (object sender, RoutedEventArgs e)
		{
			if (Tile.ImageFiles.Count == 0) return;

			_currentIndex++;
			if (_currentIndex >= Tile.ImageFiles.Count)
				_currentIndex = 0;

			UpdateDisplay ();
		}

		private void BtnOpenFile_Click (object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty (_currentFile)) return;
			try
			{
				Process.Start (new ProcessStartInfo (_currentFile) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				Debug.WriteLine ($"Open file error: {ex.Message}");
			}
		}

		private void ImageFiles_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			if (Tile.ImageFiles.Count == 0)
			{
				Dispatcher.BeginInvoke (new Action (ClearDisplay));
				return;
			}

			if (!string.IsNullOrEmpty (_currentFile))
			{
				int newIndex = FindImageIndex (_currentFile);
				if (newIndex >= 0)
				{
					_currentIndex = newIndex;
					Dispatcher.BeginInvoke (new Action (UpdateDisplay));
					return;
				}
			}

			_currentIndex = 0;
			_currentFile = Tile.ImageFiles [0];
			Dispatcher.BeginInvoke (new Action (UpdateDisplay));
		}
	}
}