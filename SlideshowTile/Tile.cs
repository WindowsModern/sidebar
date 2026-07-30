using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Sidebar;

namespace WindowsModern.SlideshowTile
{
	public class Tile: TileBase
	{
		public static Config TileOptions { get; private set; }
		public static Tile TileInstance { get; private set; }
		private TileBaseEventRouter router = null;
		public static ILocaleResources StringResources => TileInstance?.Region?.StringResources;
		private static ObservableCollection<string> _imageFiles = new ObservableCollection<string> ();
		public static ObservableCollection<string> ImageFiles => _imageFiles;
		private TilePanel tilePanel = null;
		private bool _isRefreshing = false;
		private readonly object _refreshLock = new object ();
		private CancellationTokenSource _refreshCts = null;
		private Timer _debounceTimer = null;
		private const int DEBOUNCE_DELAY_MS = 500;
		private static readonly HashSet<string> SupportedExtensions = new HashSet<string> (StringComparer.OrdinalIgnoreCase)
		{
			".bmp", ".jpg", ".jpe", ".jpeg", ".png", ".wdp", ".tiff"
		};
		public override void OnInitialize ()
		{
			TileInstance = this;
			Region?.StringResources?.CleanRedundantValues ();
			UserRegion?.StringResources?.CleanRedundantValues ();
			TileOptions = new Config (Config);
			tilePanel = new TilePanel ();
			var panel = TileUI as Panel;
			panel.Children.Add (tilePanel);
			TileOptions.PropertyChanged += TileOptions_PropertyChanged;
			TileOptions.PictureSource.CollectionChanged += PictureSource_CollectionChanged;
			RefreshImageList ();
			router = new TileRouter (this, tilePanel);
		}
		private void TileOptions_PropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof (TileOptions.IncludeSubFolder):
				case nameof (TileOptions.PictureSource):
					DebounceRefreshImageList ();
					break;
			}
		}
		private void PictureSource_CollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
		{
			DebounceRefreshImageList ();
		}
		private void DebounceRefreshImageList ()
		{
			lock (_refreshLock)
			{
				if (_debounceTimer != null)
				{
					_debounceTimer.Dispose ();
					_debounceTimer = null;
				}

				_debounceTimer = new Timer (
					_ => RefreshImageList (),
					null,
					DEBOUNCE_DELAY_MS,
					Timeout.Infinite
				);
			}
		}
		private void RefreshImageList ()
		{
			CancellationToken cancellationToken;

			lock (_refreshLock)
			{
				if (_isRefreshing)
				{
					_refreshCts?.Cancel ();
					_refreshCts?.Dispose ();
				}

				_isRefreshing = true;
				_refreshCts = new CancellationTokenSource ();
				cancellationToken = _refreshCts.Token;
			}

			System.Threading.ThreadPool.QueueUserWorkItem (_ => {
				try
				{
					if (cancellationToken.IsCancellationRequested) return;

					// 使用 HashSet 替代 List，自动去重，避免多次 Distinct() 调用
					var imageSet = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
					var folders = TileOptions.PictureSource.Distinct ().ToList ();
					bool includeSub = TileOptions.IncludeSubFolder;

					foreach (var folder in folders)
					{
						if (cancellationToken.IsCancellationRequested) return;
						if (!Directory.Exists (folder)) continue;
						CollectImages (folder, imageSet, includeSub, cancellationToken);
					}

					if (cancellationToken.IsCancellationRequested) return;

					// UI线程更新：先排序后批量更新
					System.Windows.Application.Current?.Dispatcher.Invoke (new Action (() => {
						if (cancellationToken.IsCancellationRequested) return;

						var orderedList = imageSet.OrderBy (x => x).ToList ();

						// 检查是否有实际改变，减少不必要的UI更新
						if (_imageFiles.Count == orderedList.Count &&
							_imageFiles.SequenceEqual (orderedList))
						{
							return;
						}

						_imageFiles.Clear ();
						foreach (var img in orderedList)
							_imageFiles.Add (img);
					}));
				}
				catch (OperationCanceledException) { }
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine ($"RefreshImageList error: {ex.Message}");
				}
				finally
				{
					lock (_refreshLock)
					{
						_isRefreshing = false;
						_refreshCts?.Dispose ();
						_refreshCts = null;
					}
				}
			});
		}
		private void CollectImages (string directory, HashSet<string> output, bool includeSubdirectories, CancellationToken cancellationToken)
		{
			try
			{
				// 改进：一次遍历，使用文件过滤而非多次扩展名查询
				var files = Directory.GetFiles (directory);
				foreach (var file in files)
				{
					if (cancellationToken.IsCancellationRequested) return;

					// 检查扩展名是否支持
					var ext = Path.GetExtension (file);
					if (SupportedExtensions.Contains (ext))
					{
						output.Add (file);  // HashSet 自动处理重复
					}
				}

				if (includeSubdirectories)
				{
					var subDirs = Directory.GetDirectories (directory);
					foreach (var subDir in subDirs)
					{
						if (cancellationToken.IsCancellationRequested) return;
						CollectImages (subDir, output, true, cancellationToken);
					}
				}
			}
			catch (UnauthorizedAccessException) { }
			catch (PathTooLongException) { }
		}
		public override void OnDestroy ()
		{
			lock (_refreshLock)
			{
				_debounceTimer?.Dispose ();
				_debounceTimer = null;
				_refreshCts?.Cancel ();
				_refreshCts?.Dispose ();
				_refreshCts = null;
			}

			router?.Dispose ();
			router = null;
			if (tilePanel != null)
			{
				(tilePanel.Parent as Panel)?.Children?.Clear ();
				tilePanel?.Dispose ();
				tilePanel = null;
			}
			if (TileOptions != null)
			{
				TileOptions.PropertyChanged -= TileOptions_PropertyChanged;
				TileOptions.PictureSource.CollectionChanged -= PictureSource_CollectionChanged;
				TileOptions.Dispose ();
				TileOptions = null;
			}
			TileInstance = null;
		}
		class TileRouter: TileBaseEventRouter
		{
			protected TilePanel TileContent = null;
			public TileRouter (TileBase tileBase, TilePanel tp) : base (tileBase)
			{
				TileContent = tp;
			}
			ConfigPanel confPanel = null;
			public override void PropertiesForm_Init (object sender, PropertiesAboutEventArgs e)
			{
				confPanel = new ConfigPanel ();
				e.ClientArea.Children.Add (confPanel);
				e.Window.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
			}
			public override void PropertiesForm_ClickOK (object sender, PropertiesAboutEventArgs e)
			{
				confPanel?.OnClickOK ();
			}
			public override void PropertiesForm_Closed (object sender, EventArgs e)
			{
				(confPanel?.Parent as Panel)?.Children?.Clear ();
				confPanel = null;
			}
			public override void FlyoutForm_Init (object sender, FlyoutAboutEventArgs e)
			{
				TileContent?.OnFlyoutInit (e);
			}
			public override void FlyoutForm_Closed (object sender, EventArgs e)
			{
				TileContent?.OnFlyoutClosed ();
			}
			public override void Router_WillDestroy ()
			{
				TileContent = null;
			}
		}
	}
}