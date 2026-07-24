using System;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Sidebar
{
	public static class WebDownload
	{
		/// <summary>
		/// 异步下载文件，支持取消
		/// </summary>
		/// <param name="downloadFileUri">下载地址</param>
		/// <param name="saveFilePath">本地保存路径</param>
		/// <param name="cancellationToken">取消令牌，用于中止下载</param>
		/// <param name="progressCallback">
		/// 进度回调：参数依次为 progress (0~1)、速度文本、已下载字节数、总字节数（未知时为0）
		/// </param>
		/// <returns>Task，可等待下载完成或被取消</returns>
		public static Task DownloadFileAsync (
			Uri downloadFileUri,
			string saveFilePath,
			CancellationToken cancellationToken,
			Action<double, string, ulong, ulong> progressCallback = null)
		{
			var tcs = new TaskCompletionSource<object> ();
			if (cancellationToken.IsCancellationRequested)
			{
				tcs.SetCanceled ();
				return tcs.Task;
			}
			var client = new WebClient ();
			client.Headers.Add ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
			var sw = new Stopwatch ();
			long lastBytes = 0;
			bool isFirstProgress = true;
			client.DownloadProgressChanged += (sender, e) => {
				if (isFirstProgress)
				{
					sw.Start ();
					lastBytes = e.BytesReceived;
					isFirstProgress = false;
					return;
				}
				long deltaBytes = e.BytesReceived - lastBytes;
				double deltaSeconds = sw.ElapsedMilliseconds / 1000.0;
				if (deltaSeconds > 0)
				{
					double speedBytesPerSec = deltaBytes / deltaSeconds;
					string speedText = FormatSpeed (speedBytesPerSec);

					double progress = (e.TotalBytesToReceive > 0)
						? (double)e.BytesReceived / e.TotalBytesToReceive
						: 0;

					ulong curr = (ulong)e.BytesReceived;
					ulong total = (e.TotalBytesToReceive > 0)
						? (ulong)e.TotalBytesToReceive
						: 0;

					progressCallback?.Invoke (progress, speedText, curr, total);
				}
				sw.Restart ();
				lastBytes = e.BytesReceived;
			};
			client.DownloadFileCompleted += (sender, e) => {
				sw.Stop ();
				client.Dispose ();
				if (e.Error != null)
					tcs.SetException (e.Error);
				else if (e.Cancelled)
					tcs.SetCanceled ();
				else
					tcs.SetResult (null);
			};
			using (cancellationToken.Register (() => {
				try
				{
					client.CancelAsync ();
				}
				catch (ObjectDisposedException)
				{
				}
				catch (Exception ex)
				{
					tcs.SetException (ex);
				}
			}))
			{
				client.DownloadFileAsync (downloadFileUri, saveFilePath);
			}

			return tcs.Task;
		}
		/// <summary>
		/// 将字节/秒格式化为易读的速度文本
		/// </summary>
		private static string FormatSpeed (double bytesPerSecond)
		{
			const double kb = 1024;
			const double mb = kb * 1024;
			const double gb = mb * 1024;

			if (bytesPerSecond >= gb)
				return $"{bytesPerSecond / gb:F2} GB/s";
			if (bytesPerSecond >= mb)
				return $"{bytesPerSecond / mb:F2} MB/s";
			if (bytesPerSecond >= kb)
				return $"{bytesPerSecond / kb:F2} KB/s";
			return $"{bytesPerSecond:F2} B/s";
		}
	}
}