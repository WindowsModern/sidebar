using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Sidebar
{
	public sealed class MultiRootWatcher: IDisposable
	{
		private static readonly TimeSpan InfiniteTimeSpan = TimeSpan.FromMilliseconds (Timeout.Infinite);
		private readonly string [] _roots;
		private readonly TimeSpan _debounce;
		private readonly Action _callback;
		private readonly object _sync = new object ();
		private FileSystemWatcher [] _watchers;
		private Timer _timer;
		private volatile bool _disposed;
		public MultiRootWatcher (IEnumerable<string> roots, TimeSpan debounce, Action callback)
		{
			if (roots == null) throw new ArgumentNullException (nameof (roots));
			if (callback == null) throw new ArgumentNullException (nameof (callback));
			_roots = roots.ToArray ();
			_debounce = debounce > TimeSpan.Zero ? debounce : TimeSpan.FromMilliseconds (500);
			_callback = callback;
		}
		public string [] Roots { get { return _roots; } }
		public void Start ()
		{
			if (_disposed) return;
			lock (_sync)
			{
				if (_disposed || _watchers != null) return;
				var list = new List<FileSystemWatcher> ();
				foreach (var root in _roots)
				{
					if (string.IsNullOrEmpty (root)) continue;
					if (!Directory.Exists (root)) continue;
					try
					{
						var w = new FileSystemWatcher (root) {
							IncludeSubdirectories = false,
							NotifyFilter = NotifyFilters.FileName
										 | NotifyFilters.DirectoryName
										 | NotifyFilters.LastWrite
										 | NotifyFilters.Size,
							InternalBufferSize = 16 * 1024,
						};
						w.Created += OnFsChanged;
						w.Changed += OnFsChanged;
						w.Deleted += OnFsChanged;
						w.Renamed += OnFsRenamed;
						w.Error += OnFsError;
						w.EnableRaisingEvents = true;
						list.Add (w);
					}
					catch { }
				}
				_watchers = list.ToArray ();
			}
		}
		public void Trigger () { Schedule (); }
		private void OnFsChanged (object sender, FileSystemEventArgs e) { Schedule (); }
		private void OnFsRenamed (object sender, RenamedEventArgs e) { Schedule (); }
		private void OnFsError (object sender, ErrorEventArgs e) { Schedule (); }
		private void Schedule ()
		{
			if (_disposed) return;
			lock (_sync)
			{
				if (_disposed) return;
				if (_timer == null)
					_timer = new Timer (OnTimer, null, _debounce, InfiniteTimeSpan);
				else
					_timer.Change (_debounce, InfiniteTimeSpan);
			}
		}
		private void OnTimer (object state)
		{
			if (_disposed) return;
			try { _callback (); }
			catch { }
		}
		public void Dispose ()
		{
			if (_disposed) return;
			_disposed = true;
			FileSystemWatcher [] ws;
			lock (_sync)
			{
				ws = _watchers;
				_watchers = null;
				if (_timer != null) { _timer.Dispose (); _timer = null; }
			}
			if (ws != null)
			{
				foreach (var w in ws)
				{
					try
					{
						w.EnableRaisingEvents = false;
						w.Created -= OnFsChanged;
						w.Changed -= OnFsChanged;
						w.Deleted -= OnFsChanged;
						w.Renamed -= OnFsRenamed;
						w.Error -= OnFsError;
						w.Dispose ();
					}
					catch { }
				}
			}
		}
	}
	internal static class UiDispatch
	{
		public static void Run (Action action)
		{
			if (action == null) return;
			var disp = Application.Current != null ? Application.Current.Dispatcher : null;
			if (disp == null || disp.CheckAccess ()) action ();
			else disp.Invoke (action);
		}
		public static void Post (Action action)
		{
			if (action == null) return;
			var disp = Application.Current != null ? Application.Current.Dispatcher : null;
			if (disp == null || disp.CheckAccess ()) action ();
			else disp.BeginInvoke (action);
		}
	}
	internal static class ObservableSync
	{
		public static void Sync<T, TKey> (
			ObservableCollection<T> target,
			IList<T> desired,
			Func<T, TKey> keySelector,
			IEqualityComparer<TKey> keyComparer = null,
			Func<T, T, bool> contentEquality = null)
		{
			if (target == null) return;
			if (desired == null) desired = new List<T> ();
			if (keySelector == null) throw new ArgumentNullException (nameof (keySelector));
			keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;

			if (target.Count > 0)
			{
				var desiredKeys = new HashSet<TKey> (keyComparer);
				for (int i = 0; i < desired.Count; i++)
					desiredKeys.Add (keySelector (desired [i]));
				for (int i = target.Count - 1; i >= 0; i--)
				{
					if (!desiredKeys.Contains (keySelector (target [i])))
						target.RemoveAt (i);
				}
			}

			for (int i = 0; i < desired.Count; i++)
			{
				var newItem = desired [i];
				var key = keySelector (newItem);

				int existingIndex = -1;
				for (int j = 0; j < target.Count; j++)
				{
					if (keyComparer.Equals (keySelector (target [j]), key))
					{
						existingIndex = j;
						break;
					}
				}

				if (existingIndex >= 0)
				{
					if (contentEquality != null && !contentEquality (target [existingIndex], newItem))
						target [existingIndex] = newItem;
					if (existingIndex != i)
						target.Move (existingIndex, i);
				}
				else
				{
					target.Insert (i, newItem);
				}
			}
		}
	}
}