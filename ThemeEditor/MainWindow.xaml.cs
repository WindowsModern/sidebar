using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using Sidebar;
namespace ThemeEditor
{
	/// <summary>
	/// MainWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MainWindow: Window
	{
		public ObservableCollection<NamespaceNode> ControlTreeData { get; set; } = new ObservableCollection<NamespaceNode> ();
		public MainWindow ()
		{
			InitializeComponent ();
			CodeEditor.Text = @"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                    xmlns:local=""clr-namespace:Sidebar;assembly=Sidebar""
                    xmlns:s=""clr-namespace:System;assembly=mscorlib"">
    <!-- Options -->
    <s:Boolean x:Key=""EnableBlur"">false</s:Boolean>
    <s:Boolean x:Key=""EnableBlurForFlyout"">false</s:Boolean>
    <s:Boolean x:Key=""EnableBlurForDrag"">false</s:Boolean>
    <!-- Other Styles -->
</ResourceDictionary>";
			LoadControlTree ();
			ControlTreeList.ItemsSource = ControlTreeData;
			InitPreviewInteractions ();
			LoadBackgroundColors ();
			InitSelection ();
			_codeFoldingManager = SetupFoldingForEditor (CodeEditor);
			_xamlDisplayFoldingManager = SetupFoldingForEditor (XamlDisplay);
			_previewDebounceTimer = new DispatcherTimer ();
			_previewDebounceTimer.Interval = TimeSpan.FromMilliseconds (750);
			_previewDebounceTimer.Tick += PreviewDebounceTimer_Tick;
		}
		private void LoadBackgroundColors ()
		{
			var colorItems = new List<KeyValuePair<string, Color>> ();
			foreach (var prop in typeof (Colors).GetProperties ())
			{
				var color = (Color)prop.GetValue (null, null);
				if (color == Colors.Transparent)
					continue; 
				colorItems.Add (new KeyValuePair<string, Color> (prop.Name, color));
			}
			colorItems = colorItems.OrderBy (kv => kv.Key).ToList ();
			BackgroundColorCombo.Items.Add (new ComboBoxItem { Content = "Transparent", Tag = null });
			foreach (var item in colorItems)
			{
				BackgroundColorCombo.Items.Add (new ComboBoxItem {
					Content = item.Key,
					Tag = new SolidColorBrush (item.Value)
				});
			}
			BackgroundColorCombo.SelectedIndex = 0;
		}
		private void BackgroundColorCombo_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			var selected = BackgroundColorCombo.SelectedItem as ComboBoxItem;
			if (selected == null) return;
			if (selected.Tag == null)
			{
				BackgroundLayer.Background = this.Resources ["ChessboardBrush"] as DrawingBrush;
			}
			else
			{
				var brush = selected.Tag as SolidColorBrush;
				if (brush != null)
				{
					BackgroundLayer.Background = brush;
				}
			}
		}
		private void LoadControlTree ()
		{
			var controlTypes = Assembly.GetExecutingAssembly ()
				.GetTypes ()
				.Where (t => t.IsClass && !t.IsAbstract && t.IsSubclassOf (typeof (UserControl)))
				.ToList ();
			var groups = controlTypes
				.Where (t => t.Namespace != null && (t.Namespace.StartsWith ("ThemeEditor.Sidebar") || t.Namespace.StartsWith ("ThemeEditor.Tiles")))
				.GroupBy (t => t.Namespace)
				.OrderBy (g => g.Key);
			foreach (var group in groups)
			{
				var nsNode = new NamespaceNode { Namespace = group.Key };
				foreach (var type in group.OrderBy (t => t.Name))
				{
					nsNode.Controls.Add (new ControlTypeNode {
						Name = type.Name,
						ControlType = type
					});
				}
				ControlTreeData.Add (nsNode);
			}
		}
		private void ControlTreeList_SelectedItemChanged (object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			ControlTypeNode selected = e.NewValue as ControlTypeNode;
			if (selected == null) return;
			try
			{
				ClearSelection ();
				var control = (UserControl)Activator.CreateInstance (selected.ControlType);
				control.Margin = new Thickness (5);
				if (double.IsNaN (control.Width)) control.Width = 150;
				if (double.IsNaN (control.Height)) control.Height = 150;
				DesignCanvas.Children.Clear ();
				DesignCanvas.Children.Add (control);
				Canvas.SetLeft (control, 50);
				Canvas.SetTop (control, 50);
				CurrSelectedItem.Content = $"Current Selected: {selected.Name}";
				PreviewButton.IsEnabled = control is INeedWindowControl;
				try
				{
					string xaml = System.Windows.Markup.XamlWriter.Save (control);
					XamlDisplay.Text = FormatXaml (xaml);
				}
				catch (Exception xamlEx)
				{
					XamlDisplay.Text = $"<!-- Unable to serialize XAML for this control -->\n<!-- Error: {xamlEx.Message} -->";
				}
			}
			catch (Exception ex)
			{
				XamlDisplay.Text = $"<!-- Failed to create instance: {ex.Message} -->";
			}
			finally
			{
				Utilities.ReleaseLargeResourcesAsync ();
			}
		}
		private string FormatXaml (string xaml)
		{
			try
			{
				var doc = System.Xml.Linq.XDocument.Parse (xaml);
				return doc.ToString ();
			}
			catch
			{
				return xaml; // 如果解析失败，返回原始字符串
			}
		}
		Window previewWnd;
		private void PreviewButton_Click (object sender, RoutedEventArgs e)
		{
			if (previewWnd != null)
			{
				previewWnd?.Close ();
				previewWnd = null;
				PreviewButton.Content = "Preview";
				return;
			}
			if (!(DesignCanvas?.Children?.Count > 0)) return;
			var chi = DesignCanvas.Children [0];
			var selected = ControlTreeList.SelectedValue as ControlTypeNode;
			if (chi is INeedWindowControl)
			{
				chi = (UserControl)Activator.CreateInstance (selected.ControlType);
				var inwc = chi as INeedWindowControl;
				previewWnd?.Close ();
				previewWnd = null;
				previewWnd = new Window ();
				previewWnd.ResizeMode = ResizeMode.NoResize;
				previewWnd.AllowsTransparency = inwc.AllowTransparency;
				previewWnd.WindowStyle = inwc.WindowStyle;
				previewWnd.Background = new SolidColorBrush (Colors.Transparent);
				previewWnd.SourceInitialized += inwc.Window_SourceInitialized;
				previewWnd.Loaded += inwc.Window_Loaded;
				previewWnd.Unloaded += inwc.Window_Unloaded;
				IntPtr prWndHandle = IntPtr.Zero;
				EventHandler sourceInitializedHandler = null;
				sourceInitializedHandler = (snd, ee) =>
				{
					prWndHandle = new WindowInteropHelper (previewWnd).Handle;
					previewWnd.SourceInitialized -= sourceInitializedHandler; // 用完即删
				};
				previewWnd.SourceInitialized += sourceInitializedHandler;
				MouseEventHandler mouseMoveHandler = null;
				mouseMoveHandler = (snd, ee) =>
				{
					Window win = snd as Window;
					if (win == null) return;
					Point pos = ee.GetPosition (win);
					double x = pos.X, y = pos.Y;
					double w = win.ActualWidth, h = win.ActualHeight;
					int border = 5; // 边缘敏感区宽度
					bool onLeft = x <= border;
					bool onRight = x >= w - border;
					bool onTop = y <= border;
					bool onBottom = y >= h - border;
					if (onTop && onLeft)
						win.Cursor = Cursors.SizeNWSE;
					else if (onTop && onRight)
						win.Cursor = Cursors.SizeNESW;
					else if (onBottom && onLeft)
						win.Cursor = Cursors.SizeNESW;
					else if (onBottom && onRight)
						win.Cursor = Cursors.SizeNWSE;
					else if (onLeft || onRight)
						win.Cursor = Cursors.SizeWE;
					else if (onTop || onBottom)
						win.Cursor = Cursors.SizeNS;
					else
						win.Cursor = Cursors.Arrow;
				};
				MouseButtonEventHandler mouseLeftButtonDownHandler = null;
				mouseLeftButtonDownHandler = (snd, ee) =>
				{
					if (prWndHandle == IntPtr.Zero) return; // 句柄尚未就绪
					Window win = snd as Window;
					if (win == null) return;
					Point pos = ee.GetPosition (win);
					double x = pos.X, y = pos.Y;
					double w = win.ActualWidth, h = win.ActualHeight;
					int border = 5;

					bool onLeft = x <= border;
					bool onRight = x >= w - border;
					bool onTop = y <= border;
					bool onBottom = y >= h - border;
					int cmd = 0;
					if (onTop && onLeft) cmd = 0xF004;      // 左上
					else if (onTop && onRight) cmd = 0xF005; // 右上
					else if (onBottom && onLeft) cmd = 0xF007; // 左下
					else if (onBottom && onRight) cmd = 0xF008; // 右下
					else if (onLeft) cmd = 0xF001;          // 左
					else if (onRight) cmd = 0xF002;         // 右
					else if (onTop) cmd = 0xF003;           // 上
					else if (onBottom) cmd = 0xF006;        // 下
					if (cmd != 0)
					{
						Win32WindowNative.SendMessageW (prWndHandle, 274, (IntPtr)cmd, IntPtr.Zero);
						ee.Handled = true;
					}
				};
				MouseEventHandler mouseLeaveHandler = null;
				mouseLeaveHandler = (snd, ee) =>
				{
					Window win = snd as Window;
					if (win != null) win.Cursor = Cursors.Arrow;
				};
				previewWnd.MouseMove += mouseMoveHandler;
				previewWnd.PreviewMouseLeftButtonDown += mouseLeftButtonDownHandler;
				previewWnd.MouseLeave += mouseLeaveHandler;
				EventHandler closeHandle = null;
				closeHandle = (snd, ee) =>
				{
					previewWnd.SourceInitialized -= sourceInitializedHandler;
					previewWnd.Loaded -= inwc.Window_Loaded;
					previewWnd.Unloaded -= inwc.Window_Unloaded;
					previewWnd.MouseMove -= mouseMoveHandler;
					previewWnd.PreviewMouseLeftButtonDown -= mouseLeftButtonDownHandler;
					previewWnd.MouseLeave -= mouseLeaveHandler;
					previewWnd.Closed -= closeHandle;
					previewWnd = null;
					PreviewButton.Content = "Preview";
					Utilities.ReleaseLargeResourcesAsync ();
				};
				previewWnd.Closed += closeHandle;
				previewWnd.Title = selected.GetType ().ToString ();
				previewWnd.Content = chi;
				previewWnd.Show ();
				PreviewButton.Content = "Close Window";
				bool isMoving = false;
				Point moveStartPoint = new Point ();

				previewWnd.PreviewMouseLeftButtonDown += (snd, ee) =>
				{
					// 只有按下左键且不在边缘区域时才允许移动（避免与边缘调整大小冲突）
					Window win = snd as Window;
					if (win == null) return;
					Point pos = ee.GetPosition (win);
					double w = win.ActualWidth, h = win.ActualHeight;
					int border = 5;
					bool onEdge = pos.X <= border || pos.X >= w - border || pos.Y <= border || pos.Y >= h - border;
					if (!onEdge)
					{
						isMoving = true;
						moveStartPoint = ee.GetPosition (win);
						win.CaptureMouse ();
						ee.Handled = true;
					}
				};

				previewWnd.PreviewMouseMove += (snd, ee) =>
				{
					if (isMoving)
					{
						Window win = snd as Window;
						if (win == null) return;
						Point current = ee.GetPosition (win);
						Vector delta = current - moveStartPoint;
						win.Left += delta.X;
						win.Top += delta.Y;
						ee.Handled = true;
					}
				};

				previewWnd.PreviewMouseLeftButtonUp += (snd, ee) =>
				{
					if (isMoving)
					{
						Window win = snd as Window;
						win?.ReleaseMouseCapture ();
						isMoving = false;
						ee.Handled = true;
					}
				};
				var contextMenu = new ContextMenu ();
				var cancelItem = new MenuItem { Header = "Cancel" };
				cancelItem.Click += (snd, ee) =>
				{
					previewWnd?.Close ();
				};
				contextMenu.Items.Add (cancelItem);
				previewWnd.ContextMenu = contextMenu;
			}
		}
		private void SidebarPreviewCheckBox_Checked (object sender, RoutedEventArgs e)
		{
			SidebarConfig.Global.Direction = SidebarDirection.Left;
		}
		private void SidebarPreviewCheckBox_Unchecked (object sender, RoutedEventArgs e)
		{
			SidebarConfig.Global.Direction = SidebarDirection.Right;
		}
		private bool isPanning = false;
		private Point panStartPoint;
		private double panStartTranslateX, panStartTranslateY;
		private void InitPreviewInteractions ()
		{
			PreviewClipBorder.PreviewMouseWheel += PreviewClipBorder_MouseWheel;
			PreviewClipBorder.PreviewMouseLeftButtonDown += PreviewClipBorder_MouseLeftButtonDown;
			PreviewClipBorder.PreviewMouseMove += PreviewClipBorder_MouseMove;
			PreviewClipBorder.PreviewMouseLeftButtonUp += PreviewClipBorder_MouseLeftButtonUp;
		}
		private void PreviewClipBorder_MouseWheel (object sender, MouseWheelEventArgs e)
		{
			double zoomDelta = e.Delta > 0 ? 1.1 : 0.9;
			double newScale = ZoomTransform.ScaleX * zoomDelta;
			if (newScale < 0.2) newScale = 0.2;
			if (newScale > 5) newScale = 5;
			Point mousePos = e.GetPosition (PreviewContainer);
			double offsetX = (mousePos.X - PanTransform.X) / ZoomTransform.ScaleX;
			double offsetY = (mousePos.Y - PanTransform.Y) / ZoomTransform.ScaleY;
			ZoomTransform.ScaleX = newScale;
			ZoomTransform.ScaleY = newScale;
			double newTranslateX = mousePos.X - offsetX * newScale;
			double newTranslateY = mousePos.Y - offsetY * newScale;
			PanTransform.X = newTranslateX;
			PanTransform.Y = newTranslateY;

			e.Handled = true;
		}
		private void PreviewClipBorder_MouseLeftButtonDown (object sender, MouseButtonEventArgs e)
		{
			isPanning = true;
			panStartPoint = e.GetPosition (PreviewClipBorder);
			panStartTranslateX = PanTransform.X;
			panStartTranslateY = PanTransform.Y;
			PreviewClipBorder.Cursor = Cursors.SizeAll;
			PreviewClipBorder.CaptureMouse ();
			e.Handled = true;
		}
		private void PreviewClipBorder_MouseMove (object sender, MouseEventArgs e)
		{
			if (isPanning)
			{
				Point currentPoint = e.GetPosition (PreviewClipBorder);
				Vector delta = currentPoint - panStartPoint;

				PanTransform.X = panStartTranslateX + delta.X;
				PanTransform.Y = panStartTranslateY + delta.Y;

				e.Handled = true;
			}
		}
		private void PreviewClipBorder_MouseLeftButtonUp (object sender, MouseButtonEventArgs e)
		{
			isPanning = false;
			PreviewClipBorder.Cursor = null;
			PreviewClipBorder.ReleaseMouseCapture ();
			e.Handled = true;
		}
		private AdornerLayer _adornerLayer;
		private ResizeAdorner _currentAdorner;
		private UIElement _selectedElement;
		private void InitSelection ()
		{
			DesignCanvas.PreviewMouseLeftButtonDown += DesignCanvas_PreviewMouseLeftButtonDown;
		}
		private void DesignCanvas_PreviewMouseLeftButtonDown (object sender, MouseButtonEventArgs e)
		{
			DependencyObject source = e.OriginalSource as DependencyObject;
			while (source != null)
			{
				if (source is Thumb)
					return;
				source = VisualTreeHelper.GetParent (source);
			}

			Point hitPoint = e.GetPosition (DesignCanvas);
			DependencyObject hitElement = DesignCanvas.InputHitTest (hitPoint) as DependencyObject;
			UserControl userControl = FindVisualParent<UserControl> (hitElement);

			if (userControl != null)
			{
				SetSelectedElement (userControl);
			}
			else
			{
				ClearSelection ();
			}
			e.Handled = true;
		}
		private void SetSelectedElement (UIElement element)
		{
			if (_selectedElement == element) return;
			ClearSelection ();
			_selectedElement = element;
			_adornerLayer = AdornerLayer.GetAdornerLayer (DesignCanvas);
			if (_adornerLayer != null)
			{
				_currentAdorner = new ResizeAdorner (element, DesignCanvas);
				_adornerLayer.Add (_currentAdorner);
			}
		}
		private void ClearSelection ()
		{
			if (_currentAdorner != null)
			{
				_adornerLayer?.Remove (_currentAdorner);
				_currentAdorner = null;
			}
			_selectedElement = null;
		}
		private T FindVisualParent<T> (DependencyObject child) where T : DependencyObject
		{
			while (child != null)
			{
				if (child is T) return (T)child;
				child = VisualTreeHelper.GetParent (child);
			}
			return null;
		}
		private FoldingManager _codeFoldingManager;
		private FoldingManager _xamlDisplayFoldingManager;
		private void BrowseButton_Click (object sender, RoutedEventArgs e)
		{
			var dialog = new Microsoft.Win32.OpenFileDialog ();
			dialog.Filter = "WPF Resource Dictionary (*.xaml)|*.xaml|All Files (*.*)|*.*";
			dialog.FilterIndex = 1;
			dialog.DefaultExt = ".xaml";
			bool? result = dialog.ShowDialog ();
			if (result == true)
			{
				FilePath.Text = dialog.FileName;
			}
		}
		private void Window_Closed (object sender, EventArgs e)
		{
			try { previewWnd?.Close (); } catch { }
		}
		private FoldingManager SetupFoldingForEditor (TextEditor editor)
		{
			var manager = FoldingManager.Install (editor.TextArea);
			var strategy = new XmlFoldingStrategy ();
			strategy.UpdateFoldings (manager, editor.Document);
			editor.TextChanged += (s, e) => strategy.UpdateFoldings (manager, editor.Document);
			return manager;
		}
		public static void Apply (ResourceDictionary newTheme)
		{
			var appResources = Application.Current.Resources;
			var mergedDictionaries = appResources.MergedDictionaries;
			if (newTheme == null)
			{
				if (mergedDictionaries.Count > 1)
					mergedDictionaries.RemoveAt (1);
				return;
			}
			if (mergedDictionaries.Count > 1) mergedDictionaries [1] = newTheme;
			else mergedDictionaries.Add (newTheme);
		}
		private void CodeEditor_TextChanged (object sender, EventArgs e)
		{
			if (_previewDebounceTimer == null) return; 
			_previewDebounceTimer?.Stop ();
			_previewDebounceTimer?.Start ();
		}
		private void PreviewDebounceTimer_Tick (object sender, EventArgs e)
		{
			_previewDebounceTimer?.Stop ();
			LoadXamlToDesigner (CodeEditor.Text, XamlWorkDir.Text);
		}
		private void XamlWorkDir_TextChanged (object sender, TextChangedEventArgs e)
		{
			if (_previewDebounceTimer == null) return;
			RefreshPreview ();
		}
		private void RefreshPreview ()
		{
			_previewDebounceTimer.Stop ();
			LoadXamlToDesigner (CodeEditor.Text, XamlWorkDir.Text);
		}
		private void LoadXamlToDesigner (string xamlString, string baseDirectory)
		{
			if (string.IsNullOrWhiteSpace (xamlString))
			{
				XamlStatusItem.Content = "Empty XAML";
				XamlStatusItem.ToolTip = null;
				return;
			}
			try
			{
				byte [] bytes = Encoding.UTF8.GetBytes (xamlString);
				using (MemoryStream stream = new MemoryStream (bytes))
				{
					ParserContext context = new ParserContext ();
					if (!string.IsNullOrEmpty (baseDirectory) && Directory.Exists (baseDirectory))
					{
						string baseUri = new Uri (baseDirectory).AbsoluteUri;
						if (!baseUri.EndsWith ("/")) baseUri += "/";
						context.BaseUri = new Uri (baseUri);
					}
					object obj = System.Windows.Markup.XamlReader.Load (stream, context);
					if (obj is ResourceDictionary)
					{
						XamlStatusItem.Content = "ResourceDictionary is valid";
						XamlStatusItem.ToolTip = null;
						if (obj != null) Apply (obj as ResourceDictionary);
					}
					else
					{
						XamlStatusItem.Content = "Invalid: not a ResourceDictionary";
						XamlStatusItem.ToolTip = $"Loaded type: {obj.GetType ().FullName}";
					}
				}
			}
			catch (Exception ex)
			{
				XamlStatusItem.Content = "XAML error";
				XamlStatusItem.ToolTip = ex.Message;
			}
		}
		private void LoadButton_Click (object sender, RoutedEventArgs e)
		{
			if (!File.Exists (FilePath.Text ?? ""))
			{
				BrowseButton_Click (BrowseButton, e);
			}
			if (!File.Exists (FilePath.Text ?? ""))
			{
				MessageBox.Show ("Cannot load: file is not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			var res = MessageBox.Show ($"Ensure to load file \"{FilePath.Text ?? ""}\". the content and the work directory will be replaced.", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (res != MessageBoxResult.Yes) return;
			try
			{
				string content = File.ReadAllText (FilePath.Text);
				CodeEditor.Text = content;
				string dir = System.IO.Path.GetDirectoryName (FilePath.Text);
				if (!string.IsNullOrEmpty (dir))
					XamlWorkDir.Text = dir;
				XamlStatusItem.Content = "File loaded.";
				XamlStatusItem.ToolTip = null;
			}
			catch (Exception ex)
			{
				MessageBox.Show (ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				Utilities.ReleaseLargeResourcesAsync ();
			}
		}
		private void SaveButton_Click (object sender, RoutedEventArgs e)
		{
			string filePath = FilePath.Text?.Trim ();
			if (string.IsNullOrWhiteSpace (filePath))
			{
				var saveDialog = new Microsoft.Win32.SaveFileDialog ();
				saveDialog.Filter = "WPF Resource Dictionary (*.xaml)|*.xaml|All Files (*.*)|*.*";
				saveDialog.DefaultExt = ".xaml";
				if (saveDialog.ShowDialog () == true)
				{
					filePath = saveDialog.FileName;
					FilePath.Text = filePath;
				}
				else
				{
					return; 
				}
			}
			string directory = System.IO.Path.GetDirectoryName (filePath);
			if (!string.IsNullOrEmpty (directory) && !Directory.Exists (directory))
			{
				var dirResult = MessageBox.Show ($"Directory does not exist:\n{directory}\n\nCreate it?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (dirResult == MessageBoxResult.Yes)
				{
					try
					{
						Directory.CreateDirectory (directory);
					}
					catch (Exception ex)
					{
						MessageBox.Show ($"Failed to create directory:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
				}
				else
				{
					return; 
				}
			}
			if (File.Exists (filePath))
			{
				var overwriteResult = MessageBox.Show ($"File already exists:\n{filePath}\n\nOverwrite?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (overwriteResult != MessageBoxResult.Yes)
				{
					return;
				}
			}
			var confirmResult = MessageBox.Show ($"Save content to:\n{filePath}", "Confirm Save", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (confirmResult != MessageBoxResult.Yes)
			{
				return;
			}
			try
			{
				File.WriteAllText (filePath, CodeEditor.Text, Encoding.UTF8);
				MessageBox.Show ("File saved successfully.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
				XamlStatusItem.Content = "File saved.";
				XamlStatusItem.ToolTip = null;
			}
			catch (Exception ex)
			{
				MessageBox.Show ($"Failed to save file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally
			{
				Utilities.ReleaseLargeResourcesAsync ();
			}
		}
		private void Window_Closing (object sender, System.ComponentModel.CancelEventArgs e)
		{
			var result = MessageBox.Show ("Are you sure you want to close the window? Please make sure all changes are saved before closing.", "Confirm Exit",
								 MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result == MessageBoxResult.No)
			{
				e.Cancel = true;
			}
		}
		private DispatcherTimer _previewDebounceTimer;
	}
}
