using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SGPkgMgr.Helpers
{
	/// <summary>
	/// 后台执行耗时任务，自动管理进度条与"忙"状态。
	/// 任务通过进度回调报告 (curr, total, p)，不报告时进度条显示为不确定状态。
	/// </summary>
	public static class UiTask
	{
		public static void Run<TResult> (
			ProgressBar progress,
			Action<bool> setBusy,
			Func<Action<int, int, double>, TResult> work,
			Action<TResult, Exception> onCompleted)
		{
			var dispatcher = Application.Current != null ? Application.Current.Dispatcher : null;
			var scheduler = TaskScheduler.FromCurrentSynchronizationContext ();

			if (setBusy != null) setBusy (true);
			if (progress != null)
			{
				progress.Visibility = Visibility.Visible;
				progress.IsIndeterminate = true;
				progress.Value = 0;
				progress.Maximum = 100;
			}

			Task.Factory.StartNew (() => {
				Action<int, int, double> report = (curr, total, p) => {
					if (progress == null || dispatcher == null) return;
					if (dispatcher.CheckAccess ())
						ApplyProgress (progress, curr, total);
					else
						dispatcher.BeginInvoke (new Action (() => ApplyProgress (progress, curr, total)));
				};
				return work (report);
			})
			.ContinueWith (t => {
				if (progress != null)
				{
					progress.IsIndeterminate = false;
					if (!t.IsFaulted && !t.IsCanceled)
						progress.Value = progress.Maximum;
				}
				if (setBusy != null) setBusy (false);

				Exception error = null;
				TResult result = default (TResult);
				if (t.IsFaulted)
				{
					error = t.Exception != null
						? (t.Exception.InnerException ?? t.Exception)
						: new Exception ("Unknown error");
				}
				else if (t.IsCanceled)
				{
					error = new OperationCanceledException ();
				}
				else
				{
					result = t.Result;
				}
				onCompleted?.Invoke (result, error);
			}, scheduler);
		}

		private static void ApplyProgress (ProgressBar bar, int curr, int total)
		{
			if (bar.IsIndeterminate) bar.IsIndeterminate = false;
			if (total > 0) bar.Maximum = total;
			if (curr < 0) curr = 0;
			if (curr > bar.Maximum) curr = (int)bar.Maximum;
			bar.Value = curr;
		}
	}
}