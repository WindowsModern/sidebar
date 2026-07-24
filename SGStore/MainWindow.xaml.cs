using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfAnimatedGif;

namespace Sidebar
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow: Window
	{
		private ImageSource itemImage = null;
		private List<TilePackageItem> _allItems;               // 缓存所有项
		private System.Windows.Threading.DispatcherTimer _searchDebounceTimer; // 防抖定时器
		private bool _isSearching = false;                     // 标记是否正在搜索状态
		public MainWindow ()
		{
			InitializeComponent ();
			InitStrings ();
			InitImages ();
			_searchDebounceTimer = new System.Windows.Threading.DispatcherTimer ();
			_searchDebounceTimer.Interval = TimeSpan.FromMilliseconds (300);
			_searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
			_searchDebounceTimer.Stop ();
		}
		private void SearchDebounceTimer_Tick (object sender, EventArgs e)
		{
			_searchDebounceTimer.Stop ();
			ApplyFilter ();
		}
		private void InitImages ()
		{
			itemImage = new BitmapImage ();
			var bm = itemImage as BitmapImage;
			bm.BeginInit ();
			bm.UriSource = new Uri (System.IO.Path.Combine (App.AppRoot.FolderPath, "Images\\Items.png"));
			bm.EndInit ();
			if (bm.CanFreeze) bm.Freeze ();
			var image = new BitmapImage ();
			image.BeginInit ();
			image.UriSource = new Uri (System.IO.Path.Combine (App.AppRoot.FolderPath, "Images\\Ring.gif"));
			image.EndInit ();
			CurrentItemIcon.Source = itemImage;
			ImageBehavior.SetAnimatedSource (LoadingImage, image);
			var searchImage = new BitmapImage ();
			searchImage.BeginInit ();
			searchImage.UriSource = new Uri (System.IO.Path.Combine (App.AppRoot.FolderPath, "Images\\SearchButtonIcon.ico"));
			searchImage.EndInit ();
			SearchIcon.Source = searchImage;
			var refreshImg = new BitmapImage ();
			refreshImg.BeginInit ();
			refreshImg.UriSource = new Uri (System.IO.Path.Combine (App.AppRoot.FolderPath, "Images\\Refresh.ico"));
			refreshImg.EndInit ();
			var downloadImg = new BitmapImage ();
			downloadImg.BeginInit ();
			downloadImg.UriSource = new Uri (System.IO.Path.Combine (App.AppRoot.FolderPath, "Images\\Download.ico"));
			downloadImg.EndInit ();
			var settingsImg = new BitmapImage ();
			settingsImg.BeginInit ();
			settingsImg.UriSource = new Uri (System.IO.Path.Combine (App.AppRoot.FolderPath, "Images\\Settings.ico"));
			settingsImg.EndInit ();
			var wndImage = new BitmapImage ();
			wndImage.BeginInit ();
			wndImage.UriSource = new Uri (System.IO.Path.Combine (App.AppRoot.FolderPath, "Images\\Store.ico"));
			wndImage.EndInit ();
			RefreshImage.Source = refreshImg;
			DownloadImage.Source = downloadImg;
			SettingsImage.Source = settingsImg;
			Icon = wndImage;
		}
		private void Window_Loaded (object sender, RoutedEventArgs e)
		{
			VisibilityAutoController.Register (LoadingStatusGrid);
			VisibilityAutoController.Register (ErrorStatus);
			VisibilityAutoController.Register (DownloadStatus);
			LoadList ();
		}
		private void Window_Unloaded (object sender, RoutedEventArgs e)
		{
			VisibilityAutoController.Unregister (LoadingStatusGrid);
			VisibilityAutoController.Unregister (ErrorStatus);
			VisibilityAutoController.Unregister (DownloadStatus);
		}
		private void InitStrings ()
		{
			var sr = App.AppRoot.StringResources;
			Title = sr.SuitableResource ("STORE_APPTITLE");
			SearchField.Tag = sr.SuitableResource ("STORE_SEARCHPLACEHOLDER");
			DownTilesCaption.Text = String.Format (sr.SuitableResource ("STORE_DOWNLOADABLETILES"), 0);
			ItemsCount.Text = String.Format (sr.SuitableResource ("STORE_ITEMSCOUNT"), 0);
			LabelLoadingText.Text = sr.SuitableResource ("STORE_PLEASEWAIT");
			LabelErrorTitle.Text = sr.SuitableResource ("STORE_ERRORTITLE");
			LabelDowloadTitle.Text = sr.SuitableResource ("STORE_DWINGTITLE");
			ErrorStatusCancelButton.Content = sr.SuitableResource ("STORE_CANCEL");
			LabelAuthor.Text = sr.SuitableResource ("STORE_PUBLISHER");
			LabelVersion.Text = sr.SuitableResource ("STORE_VERSION");
			ButtonRefresh.ToolTip = sr.SuitableResource ("STORE_BTN_REFRESH");
			ButtonDownload.ToolTip = sr.SuitableResource ("STORE_BTN_DOWNLOAD");
			ButtonSettings.ToolTip = sr.SuitableResource ("STORE_BTN_SETTINGS");
		}
		/// <summary>
		/// 淡入显示元素，并支持完成回调
		/// </summary>
		private void ShowElement (UIElement element, double durationSeconds = 0.4, Action onCompleted = null)
		{
			if (element == null) return;
			if (element.Visibility == Visibility.Visible && element.Opacity >= 1.0)
			{
				onCompleted?.Invoke ();
				return;
			}
			if (element.Visibility != Visibility.Visible)
			{
				element.Visibility = Visibility.Visible;
				element.Opacity = 0;
			}
			var storyboard = new Storyboard ();
			var animation = new DoubleAnimation {
				From = element.Opacity,
				To = 1.0,
				Duration = TimeSpan.FromSeconds (durationSeconds),
				FillBehavior = FillBehavior.Stop
			};
			animation.Completed += (s, e) => {
				element.Opacity = 1.0;
				onCompleted?.Invoke ();
			};
			Storyboard.SetTarget (animation, element);
			Storyboard.SetTargetProperty (animation, new PropertyPath (UIElement.OpacityProperty));
			storyboard.Children.Add (animation);
			storyboard.Begin ();
		}
		/// <summary>
		/// 淡出隐藏元素，并支持完成回调
		/// </summary>
		private void HideElement (UIElement element, double durationSeconds = 0.4, Action onCompleted = null)
		{
			if (element == null) return;
			if (element.Visibility == Visibility.Collapsed && element.Opacity <= 0.0)
			{
				onCompleted?.Invoke ();
				return;
			}
			var storyboard = new Storyboard ();
			var animation = new DoubleAnimation {
				From = element.Opacity,
				To = 0.0,
				Duration = TimeSpan.FromSeconds (durationSeconds),
				FillBehavior = FillBehavior.Stop
			};
			animation.Completed += (s, e) => {
				element.Opacity = 0.0;
				element.Visibility = Visibility.Collapsed;
				onCompleted?.Invoke ();
			};
			Storyboard.SetTarget (animation, element);
			Storyboard.SetTargetProperty (animation, new PropertyPath (UIElement.OpacityProperty));
			storyboard.Children.Add (animation);
			storyboard.Begin ();
		}
		/// <summary>
		/// 异步加载数据列表，并更新界面
		/// </summary>
		private void LoadList ()
		{
			ShowElement (LoadingStatusGrid);
			HideElement (ErrorStatus);
			HideElement (DownloadStatus);
			WebAPI.GetListAsync ()
				.ContinueWith (task => {
					Dispatcher.Invoke (new Action (() => {
						try
						{
							if (task.IsFaulted)
							{
								HideElement (LoadingStatusGrid);
								ShowElement (ErrorStatus);
								var extlines = task.Exception?.InnerExceptions?.Select (iex => {
									return $"{iex.GetType ().ToString ()}: {iex.Message}";
								});
								var extstr = string.Join ("\n", extlines);
								extstr = extstr.Trim ();
								if (string.IsNullOrWhiteSpace (extstr)) extstr = task.Exception?.Message;
								LabelReasonText.Text = extstr;
								return;
							}
							var result = task.Result; // TileList
							_allItems = result?.List ?? new List<TilePackageItem> ();
							if (result?.List == null)
							{
								HideElement (LoadingStatusGrid);
								ShowElement (ErrorStatus);
								LabelReasonText.Text = "There is no any item.";
								return;
							}
							foreach (StoreItem i in DownTilesPanel?.Children)
							{
								if (i != null)
								{
									i.Click -= StoreItem_Click;
									i.MouseDoubleClick -= StoreItem_MouseDoubleClick;
								}
							}
							DownTilesPanel.Children.Clear ();
							foreach (var item in result.List)
							{
								var storeItem = new StoreItem {
									ItemData = item
								};
								storeItem.Click += StoreItem_Click;
								storeItem.MouseDoubleClick += StoreItem_MouseDoubleClick;
								DownTilesPanel.Children.Add (storeItem);
							}
							CurrentSelected = null;
							var sr = App.AppRoot.StringResources;
							DownTilesCaption.Text = String.Format (sr.SuitableResource ("STORE_DOWNLOADABLETILES"), result.List.Count);
							ItemsCount.Text = String.Format (sr.SuitableResource ("STORE_ITEMSCOUNT"), result.List.Count);
							HideElement (LoadingStatusGrid);
							WrapPanel1.Visibility = Visibility.Collapsed;
							WrapPanel2.Visibility = Visibility.Collapsed;
							ItemsCount.Visibility = Visibility.Visible;
							CurrentItemIcon.Source = itemImage;
						}
						catch (Exception ex)
						{
							HideElement (LoadingStatusGrid);
							ShowElement (ErrorStatus);
							LabelReasonText.Text = ex.Message;
							return;
						}
						finally
						{
						}
					}));
				});
		}
		private void StoreItem_MouseDoubleClick (object sender, MouseButtonEventArgs e)
		{
			var item = sender as StoreItem;
			var i = item.ItemData;
			CurrentSelected = i;
			WrapPanel1.Visibility = Visibility.Visible;
			WrapPanel2.Visibility = Visibility.Visible;
			ItemsCount.Visibility = Visibility.Collapsed;
			CurrentItemTitle.Text = i.DisplayName;
			CurrentItemTitle.ToolTip = i.DisplayName;
			CurrentItemDescription.Text = i.Description;
			CurrentItemDescription.ToolTip = i.Description;
			CurrentItemAuthor.Text = i.Publisher;
			CurrentItemAuthor.ToolTip = i.Publisher;
			CurrentItemVersion.Text = i.SupportedNewestVersion;
			CurrentItemVersion.Text = i.SupportedNewestVersion;
			CurrentItemIcon.Source = item.Image;
			DownloadGadget ();
		}
		private void ErrorStatusCancelButton_Click (object sender, RoutedEventArgs e)
		{
			HideElement (ErrorStatus);
		}
		private TilePackageItem CurrentSelected = null;
		private void StoreItem_Click (object sender, RoutedEventArgs e)
		{
			var item = sender as StoreItem;
			var i = item.ItemData;
			CurrentSelected = i;
			WrapPanel1.Visibility = Visibility.Visible;
			WrapPanel2.Visibility = Visibility.Visible;
			ItemsCount.Visibility = Visibility.Collapsed;
			CurrentItemTitle.Text = i.DisplayName;
			CurrentItemTitle.ToolTip = i.DisplayName;
			CurrentItemDescription.Text = i.Description;
			CurrentItemDescription.ToolTip = i.Description;
			CurrentItemAuthor.Text = i.Publisher;
			CurrentItemAuthor.ToolTip = i.Publisher;
			CurrentItemVersion.Text = i.SupportedNewestVersion;
			CurrentItemVersion.Text = i.SupportedNewestVersion;
			CurrentItemIcon.Source = item.Image;
		}
		private void ButtonRefresh_Click (object sender, RoutedEventArgs e)
		{
			LoadList ();
		}
		private void ButtonDownload_Click (object sender, RoutedEventArgs e)
		{
			DownloadGadget ();
		}
		private CancellationTokenSource _cts;
		private void DownloadGadget ()
		{
			ShowElement (DownloadStatus);
			DownloadProgressBar.IsIndeterminate = true;
			ProgressStatusCancelButton.IsEnabled = true;
			_cts = new CancellationTokenSource ();
			var sr = App.AppRoot.StringResources;
			if (CurrentSelected == null)
			{
				ShowError (sr.SuitableResource ("STORE_EMPTYSELECT"));
				return;
			}
			string downloadFolder = System.IO.Path.Combine (App.AppData.FolderPath, "Temp\\Download");
			if (!Directory.Exists (downloadFolder)) Directory.CreateDirectory (downloadFolder);
			string downloadName = GetUniqueFilePath (
				downloadFolder,
				CurrentSelected.SupportedNewestFileName,
				CurrentSelected.SupportedNewestFileNameWithoutExtension,
				CurrentSelected.SupportedNewestFileExtension
			);
			Dispatcher.BeginInvoke ((Action)(() => {
				LabelDownloadItem.Text = string.Format (
					sr.SuitableResource ("STORE_DOWNLOADITEM"),
					CurrentSelected.DisplayName,
					CurrentSelected.SupportedNewestVersion
				);
				DownloadProgressBar.Value = 0;
				DownloadProgressBar.Maximum = 100;
				DownloadProgressBar.Minimum = 0;
				DownloadProgressBar.IsIndeterminate = false;
				LabelDownloadProgress.Text = string.Format (sr.SuitableResource ("STORE_PROGRESS"), 0, "");
			}));
			var downloadTask = WebDownload.DownloadFileAsync (
				CurrentSelected.SupportedNewestFileUri,
				downloadName,
				_cts.Token,
				(progress, speedText, curr, total) => {
					Dispatcher.BeginInvoke ((Action)(() => {
						DownloadProgressBar.Value = progress * 100;
						LabelDownloadProgress.Text = string.Format (
							sr.SuitableResource ("STORE_PROGRESS"),
							(int)(progress * 100),
							speedText
						);
					}));
				}
			);
			downloadTask.ContinueWith (downloadT => {
				if (downloadT.IsCanceled)
				{
					ShowError (sr.SuitableResource ("STORE_TASKCANCELED"));
					return;
				}
				if (downloadT.IsFaulted)
				{
					ShowError (downloadT.Exception);
					return;
				}
				Dispatcher.BeginInvoke ((Action)(() => {
					LabelDownloadProgress.Text = string.Format (
						sr.SuitableResource ("STORE_PROGRESS"),
						100,
						""
					);
					DownloadProgressBar.Value = 100;
					DownloadProgressBar.IsIndeterminate = true;
					LabelDownloadProgress.Text = sr.SuitableResource ("STORE_CHECKING");
				}));
				Task<bool> verifyTask = Task.Factory.StartNew (() => {
					return FileHash.VerifyDigest (downloadName, CurrentSelected.SupportedNewestFileDigest);
				});
				verifyTask.ContinueWith (verifyT => {
					if (verifyT.IsFaulted)
					{
						ShowError (verifyT.Exception);
						return;
					}
					if (verifyT.IsCanceled)
					{
						ShowError (sr.SuitableResource ("STORE_TASKCANCELED"));
						return;
					}
					bool isValid = verifyT.Result;
					if (!isValid)
					{
						ShowError (sr.SuitableResource ("STORE_VERIFYFAILED"));
						return;
					}
					string installerPath = System.IO.Path.Combine (App.AppRoot.FolderPath, "SGInstall.exe");
					Process.Start (installerPath, $"\"{downloadName}\"");

					Dispatcher.BeginInvoke ((Action)(() => {
						HideElement (DownloadStatus);
					}));

				})
				.ContinueWith (_ => {
					Dispatcher.BeginInvoke ((Action)(() => {
						ProgressStatusCancelButton.IsEnabled = false;
						DownloadProgressBar.IsIndeterminate = false;
						_cts?.Dispose ();
						_cts = null;
					}));
				});

			});
		}
		/// <summary>
		/// 生成不重复的文件路径（处理重名）
		/// </summary>
		private string GetUniqueFilePath (string folder, string baseName, string baseNameWithoutExt, string extension)
		{
			string path = System.IO.Path.Combine (folder, baseName);
			if (!File.Exists (path))
				return path;
			int count = 1;
			while (true)
			{
				string newName = $"{baseNameWithoutExt} ({count}){extension}";
				string newPath = System.IO.Path.Combine (folder, newName);
				if (!File.Exists (newPath))
					return newPath;
				count++;
			}
		}
		/// <summary>
		/// 显示错误信息（从异常对象中提取）
		/// </summary>
		private void ShowError (Exception ex)
		{
			string message;
			AggregateException aggEx = ex as AggregateException;
			if (aggEx != null)
			{
				var lines = aggEx.InnerExceptions.Select (iex => $"{iex.GetType ()}: {iex.Message}");
				message = string.Join ("\n", lines);
				if (string.IsNullOrWhiteSpace (message))
					message = aggEx.Message;
			}
			else
			{
				message = ex.Message;
			}
			ShowError (message);
		}
		/// <summary>
		/// 显示错误信息（纯文本）
		/// </summary>
		private void ShowError (string text)
		{
			Dispatcher.BeginInvoke ((Action)(() => {
				HideElement (DownloadStatus);
				ShowElement (ErrorStatus);
				LabelReasonText.Text = text;
			}));
		}
		private void ButtonSettings_Click (object sender, RoutedEventArgs e)
		{
			new SettingsWnd ().ShowDialog ();
		}
		private void ProgressStatusCancelButton_Click (object sender, RoutedEventArgs e)
		{
			if (_cts != null && !_cts.IsCancellationRequested)
			{
				_cts.Cancel ();
				HideElement (DownloadStatus);
			}
		}
		private void SearchField_TextChanged (object sender, TextChangedEventArgs e)
		{
			_searchDebounceTimer.Stop ();
			_searchDebounceTimer.Start ();
		}
		/// <summary>
		/// 根据搜索框内容过滤并刷新面板（支持多关键词 AND 匹配）
		/// </summary>
		private void ApplyFilter ()
		{
			if (_allItems == null || _allItems.Count == 0)
				return;
			string searchText = SearchField.Text?.Trim () ?? "";
			bool hasSearch = !string.IsNullOrWhiteSpace (searchText);
			var sr = App.AppRoot.StringResources;
			if (hasSearch)
			{
				string [] keywords = searchText.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				var filtered = _allItems.Where (item =>
					 keywords.All (kw =>
						  (item.DisplayName?.IndexOf (kw, StringComparison.OrdinalIgnoreCase) >= 0) ||
						  (item.Publisher?.IndexOf (kw, StringComparison.OrdinalIgnoreCase) >= 0)
					 )
				).ToList ();
				DownTiles.Visibility = Visibility.Collapsed;
				SearchTiles.Visibility = Visibility.Visible;
				SearchTilesCaption.Text = string.Format (
					sr.SuitableResource ("STORE_FIND") ?? "Found: {0}",
					filtered.Count
				);
				SearchTilesPanel.Children.Clear ();
				foreach (var item in filtered)
				{
					var storeItem = new StoreItem { ItemData = item };
					storeItem.Click += StoreItem_Click;
					storeItem.MouseDoubleClick += StoreItem_MouseDoubleClick;
					SearchTilesPanel.Children.Add (storeItem);
				}
			}
			else
			{
				DownTiles.Visibility = Visibility.Visible;
				SearchTiles.Visibility = Visibility.Collapsed;
				SearchTilesPanel.Children.Clear (); 
				DownTilesCaption.Text = string.Format (
					sr.SuitableResource ("STORE_DOWNLOADABLETILES") ?? "Downloadable Tiles ({0})",
					_allItems.Count
				);
			}
		}
	}
}
