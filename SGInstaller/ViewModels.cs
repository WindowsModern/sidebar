using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Sidebar;

namespace SGInstaller
{
	public abstract class ViewModelBase: INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged (string name)
		{
			var h = PropertyChanged;
			if (h != null) h (this, new PropertyChangedEventArgs (name));
		}
		protected bool SetField<T> (ref T field, T value, string name)
		{
			if (object.Equals (field, value)) return false;
			field = value;
			OnPropertyChanged (name);
			return true;
		}
		public virtual string Title { get { return ""; } }
		public virtual ImageSource Logo
		{
			get { return InstallerContext.Current.GetLogoImageSource (); }
		}
		public virtual string DisplayName
		{
			get { return InstallerContext.Current.GetDisplayName (); }
		}
		public virtual string PublisherText
		{
			get { return LocalizationManager.Format ("INSTALLER_INFO_PUBLISHER", InstallerContext.Current.GetPublisher ()); }
		}
		public virtual string VersionText
		{
			get { return LocalizationManager.Format ("INSTALLER_INFO_VERSION", InstallerContext.Current.GetVersion ()); }
		}
		public virtual bool ShowPrimaryButton { get { return true; } }
		public virtual bool ShowSecondaryButton { get { return true; } }
		public virtual string PrimaryButtonTitle { get { return LocalizationManager.Get ("INSTALLER_BTN_NEXT", "Next"); } }
		public virtual string SecondaryButtonTitle { get { return LocalizationManager.Get ("INSTALLER_BTN_CANCEL", "Cancel"); } }
		public virtual ICommand PrimaryCommand { get { return null; } }
		public virtual ICommand SecondaryCommand { get { return null; } }
		public virtual bool OnWillLeave () { return true; }
		public virtual void OnEnter () { }
		public virtual void OnLeave () { }
	}

	public sealed class MainViewModel: ViewModelBase
	{
		private ViewModelBase current;
		public MainViewModel ()
		{
			var opts = CommandLineOptions.Parse (Environment.GetCommandLineArgs ());
			if (!string.IsNullOrEmpty (opts.PackagePath))
			{
				Navigate (new LoadingViewModel (opts.PackagePath));
			}
			else
			{
				Navigate (new FileSelectViewModel ());
			}
		}
		public ViewModelBase CurrentPage
		{
			get { return current; }
			private set { SetField (ref current, value, "CurrentPage"); }
		}
		public void Navigate (ViewModelBase next)
		{
			if (next == null) return;
			if (current != null && !current.OnWillLeave ()) return;
			if (current != null) current.OnLeave ();
			CurrentPage = next;
			next.OnEnter ();
		}
		private static MainViewModel GetMain ()
		{
			var wnd = Application.Current == null ? null : Application.Current.MainWindow;
			return wnd == null ? null : wnd.DataContext as MainViewModel;
		}
		public static void Go (ViewModelBase next)
		{
			var m = GetMain ();
			if (m != null) m.Navigate (next);
		}
	}

	public sealed class FileSelectViewModel: ViewModelBase
	{
		public override string Title
		{
			get { return LocalizationManager.Get ("INSTALLER_SELECTFILE", "Please select a package to install."); }
		}
		public override string PrimaryButtonTitle
		{
			get { return LocalizationManager.Get ("INSTALLER_BTN_SELECTFILE", "Open File"); }
		}
		public override string SecondaryButtonTitle
		{
			get { return LocalizationManager.Get ("INSTALLER_BTN_CANCEL", "Cancel"); }
		}
		public override ICommand PrimaryCommand
		{
			get
			{
				return new RelayCommand (() => {
					var dlg = new OpenFileDialog ();
					dlg.Filter = LocalizationManager.Format (
						"{0}|*.sgpkg;*.sgpkgbundle|{1}|*.*",
						LocalizationManager.Get ("INSTALLER_FILETYPE_PKG", "Sidebar Gadget Package (*.sgpkg, *.sgpkgbundle)"),
						LocalizationManager.Get ("INSTALLER_FILETYPE_ALL", "All Files (*.*)")
					);
					if (dlg.ShowDialog () == true) MainViewModel.Go (new LoadingViewModel (dlg.FileName));
				});
			}
		}
		public override ICommand SecondaryCommand
		{
			get
			{
				return new RelayCommand (() => Application.Current.Shutdown ());
			}
		}
	}

	public sealed class LoadingViewModel: ViewModelBase
	{
		private readonly string filePath;
		private string statusText;
		public LoadingViewModel (string filePath)
		{
			this.filePath = filePath;
			this.statusText = LocalizationManager.Get ("INSTALLER_LOADING", "Please wait...");
		}
		public override string Title
		{
			get { return LocalizationManager.Get ("INSTALLER_LOADING", "Please wait..."); }
		}
		public override bool ShowPrimaryButton { get { return false; } }
		public override bool ShowSecondaryButton { get { return false; } }
		public string StatusText
		{
			get { return statusText; }
			private set { SetField (ref statusText, value, "StatusText"); }
		}
		public override void OnEnter ()
		{
			if (string.IsNullOrEmpty (filePath))
			{
				MainViewModel.Go (new FileSelectViewModel ());
				return;
			}
			Task<TilePackageBase> task = Task.Factory.StartNew (() => {
				return PackageService.Instance.LoadPackage (filePath);
			});
			task.ContinueWith (t => {
				if (t.Exception != null)
				{
					var inner = t.Exception.InnerException == null ? t.Exception : t.Exception.InnerException;
					MessageBox.Show (inner.Message, inner.GetType ().ToString (), MessageBoxButton.OK, MessageBoxImage.Error);
					MainViewModel.Go (new FileSelectViewModel ());
				}
				else
				{
					InstallerContext.Current.CurrentPackage = t.Result;
					MainViewModel.Go (new PreinstallViewModel ());
				}
			}, TaskScheduler.FromCurrentSynchronizationContext ());
		}
	}

	public sealed class PreinstallViewModel: ViewModelBase
	{
		public override string Title { get { return GetTitle (); } }
		public override string PrimaryButtonTitle { get { return GetPrimaryTitle (); } }
		public override string SecondaryButtonTitle
		{
			get { return LocalizationManager.Get ("INSTALLER_BTN_CANCEL", "Cancel"); }
		}
		public string Description
		{
			get { return InstallerContext.Current.GetDescription (); }
		}
		public override ICommand PrimaryCommand
		{
			get
			{
				return new RelayCommand (() => MainViewModel.Go (new InstallingViewModel ()));
			}
		}
		public override ICommand SecondaryCommand
		{
			get
			{
				return new RelayCommand (() => Application.Current.Shutdown ());
			}
		}
		public override void OnEnter ()
		{
			if (InstallerContext.Current.IsSilent || InstallerContext.Current.IsVerySilent)
			{
				MainViewModel.Go (new InstallingViewModel ());
			}
		}
		private static string GetTitle ()
		{
			switch (InstallerContext.Current.GetNextOperation ())
			{
				case GadgetStatus.NeedUpdate:
					return LocalizationManager.Get ("INSTALLER_PRE_TITLE_UPDATE", "Do you want to update this gadget?");
				case GadgetStatus.Reinstall:
					return LocalizationManager.Get ("INSTALLER_PRE_TITLE_REINSTALL", "Do you want to reinstall this gadget?");
				default:
					return LocalizationManager.Get ("INSTALLER_PRE_TITLE_INSTALL", "Do you want to install this gadget?");
			}
		}
		private static string GetPrimaryTitle ()
		{
			switch (InstallerContext.Current.GetNextOperation ())
			{
				case GadgetStatus.NeedUpdate:
					return LocalizationManager.Get ("INSTALLER_BTN_UPDATE", "Update");
				case GadgetStatus.Reinstall:
					return LocalizationManager.Get ("INSTALLER_BTN_REINSTALL", "Reinstall");
				default:
					return LocalizationManager.Get ("INSTALLER_BTN_INSTALL", "Install");
			}
		}
	}

	public sealed class InstallingViewModel: ViewModelBase
	{
		private double progress;
		private string statusText;

		// 后台线程读取当前安装阶段
		private volatile int progressBase;
		private volatile int progressScale;

		// 用于防止页面离开后，已经排队的 UI 更新继续生效
		private volatile bool progressUpdatesEnabled;

		public InstallingViewModel ()
		{
			progress = 0;
			statusText = LocalizationManager.Get (
				"INSTALLER_ING_TITLE",
				"Installing..."
			);

			progressBase = 0;
			progressScale = 100;
			progressUpdatesEnabled = false;
		}

		public override string Title
		{
			get
			{
				return LocalizationManager.Get (
					"INSTALLER_ING_TITLE",
					"Installing..."
				);
			}
		}

		public override bool ShowPrimaryButton
		{
			get { return false; }
		}

		public override bool ShowSecondaryButton
		{
			get { return false; }
		}

		public double Progress
		{
			get { return progress; }
			private set
			{
				if (SetField (ref progress, value, "Progress"))
				{
					// SetField 已经负责通知属性变化
				}
			}
		}

		public string StatusText
		{
			get { return statusText; }
			private set
			{
				SetField (ref statusText, value, "StatusText");
			}
		}

		public override void OnEnter ()
		{
			progressUpdatesEnabled = true;

			PackageService.Instance.ProgressChanged += OnProgressChanged;

			// 确保安装过程在 UI 消息循环启动后执行
			Application.Current.Dispatcher.BeginInvoke (
				new Action (RunInstall)
			);
		}

		public override void OnLeave ()
		{
			progressUpdatesEnabled = false;

			PackageService.Instance.ProgressChanged -= OnProgressChanged;
		}

		// 安装线程调用。
		// 不在后台线程直接修改 WPF 属性，而是每次回调都投递到 UI 线程。
		private void OnProgressChanged (
			int current,
			int total,
			double percent
		)
		{
			int basePercent = progressBase;
			int scalePercent = progressScale;

			int totalPercent = (int)Math.Round (
				basePercent + percent * scalePercent
			);

			if (totalPercent < 0)
				totalPercent = 0;

			if (totalPercent > 100)
				totalPercent = 100;

			if (!progressUpdatesEnabled)
				return;

			Application.Current.Dispatcher.BeginInvoke (
				new Action (() => ApplyProgress (totalPercent))
			);
		}

		// UI 线程执行。
		// 不检查上一次的值，因此不做防抖和去重。
		private void ApplyProgress (int value)
		{
			if (!progressUpdatesEnabled)
				return;

			Progress = value;
			StatusText = LocalizationManager.Format (
				"INSTALLER_ING_PROGRESS",
				value
			);
		}

		private void RunInstall ()
		{
			InstallerContext ctx = InstallerContext.Current;
			GadgetStatus operation = ctx.GetNextOperation ();
			string familyName = ctx.GetFamilyName ();

			if (operation == GadgetStatus.NeedUpdate ||
				operation == GadgetStatus.Reinstall)
			{
				// 卸载阶段：0-10%
				EnterUninstallPhase ();

				Task.Factory.StartNew (() =>
				{
					NotificationService.NotifyUnpinTile (familyName);
					Thread.Sleep (500);
				})
				.ContinueWith (
					_ => {
					// 更新阶段：10-100%
					EnterInstallPhase (10, 90);

						Task<bool> updateTask = Task.Factory.StartNew (() => {
							try
							{
								PackageService.Instance.Update (
									ctx.CurrentPackage.FileName
								);

								return true;
							}
							catch
							{
								return false;
							}
						});

						updateTask.ContinueWith (
							t => {
								if (t.Result)
									ToSuccess ();
								else
									PerformInstall ();
							},
							TaskScheduler.FromCurrentSynchronizationContext ()
						);
					},
					TaskScheduler.FromCurrentSynchronizationContext ()
				);
			}
			else
			{
				// 普通安装：0-100%
				EnterInstallPhase (0, 100);
				PerformInstall ();
			}
		}

		private void EnterUninstallPhase ()
		{
			progressBase = 0;
			progressScale = 0;

			Progress = 5;
			StatusText = LocalizationManager.Get (
				"INSTALLER_ING_UNINSTALLING",
				"Uninstalling old version..."
			);
		}

		private void EnterInstallPhase (
			int basePercent,
			int scalePercent
		)
		{
			progressBase = basePercent;
			progressScale = scalePercent;

			Progress = basePercent;
			StatusText = LocalizationManager.Format (
				"INSTALLER_ING_PROGRESS",
				basePercent
			);
		}

		private void PerformInstall ()
		{
			Task.Factory.StartNew (() =>
			{
				PackageService.Instance.Install (
					InstallerContext.Current.CurrentPackage.FileName
				);
			})
			.ContinueWith (
				task => {
					if (task.Exception != null)
					{
						Exception inner =
							task.Exception.InnerException == null
								? task.Exception
								: task.Exception.InnerException;

						ToFailed (inner);
					}
					else
					{
						ToSuccess ();
					}
				},
				TaskScheduler.FromCurrentSynchronizationContext ()
			);
		}

		private static void ToSuccess ()
		{
			MainViewModel.Go (
				new InstallSuccessViewModel ()
			);
		}

		private static void ToFailed (Exception exception)
		{
			MainViewModel.Go (
				new InstallFailedViewModel (exception)
			);
		}
	}

	public sealed class InstallSuccessViewModel: ViewModelBase
	{
		public override string Title
		{
			get { return LocalizationManager.Get ("INSTALLER_SUCCESS_TITLE", "Successfully installed!"); }
		}
		public override string PrimaryButtonTitle
		{
			get { return LocalizationManager.Get ("INSTALLER_BTN_CLOSE", "Close"); }
		}
		public override bool ShowSecondaryButton { get { return false; } }
		public override ICommand PrimaryCommand
		{
			get
			{
				return new RelayCommand (() => Application.Current.Shutdown ());
			}
		}
		public override void OnEnter ()
		{
			StartAutoClose (5);
		}
		private static void StartAutoClose (int seconds)
		{
			var timer = new System.Windows.Threading.DispatcherTimer ();
			timer.Interval = TimeSpan.FromSeconds (seconds);
			timer.Tick += (s, e) => {
				timer.Stop ();
				Application.Current.Shutdown ();
			};
			timer.Start ();
		}
	}

	public sealed class InstallFailedViewModel: ViewModelBase
	{
		private readonly Exception exception;
		public InstallFailedViewModel (Exception ex)
		{
			this.exception = ex;
		}
		public override string Title
		{
			get { return LocalizationManager.Get ("INSTALLER_FAILED_TITLE", "Failed to install"); }
		}
		public override string PrimaryButtonTitle
		{
			get { return LocalizationManager.Get ("INSTALLER_BTN_CLOSE", "Close"); }
		}
		public override bool ShowSecondaryButton { get { return false; } }
		public string ReasonLabel
		{
			get { return LocalizationManager.Get ("INSTALLER_FAILED_REASON", "Reason:"); }
		}
		public string ReasonText
		{
			get
			{
				if (exception == null) return "Unknown error";
				var inner = exception.InnerException;
				return (inner == null ? exception.Message : inner.Message) ?? "Unknown error";
			}
		}
		public override ICommand PrimaryCommand
		{
			get
			{
				return new RelayCommand (() => Application.Current.Shutdown ());
			}
		}
		public override void OnEnter ()
		{
			if (InstallerContext.Current.IsSilent)
			{
				var timer = new System.Windows.Threading.DispatcherTimer ();
				timer.Interval = TimeSpan.FromSeconds (10);
				timer.Tick += (s, e) => {
					timer.Stop ();
					Application.Current.Shutdown ();
				};
				timer.Start ();
			}
		}
	}
}