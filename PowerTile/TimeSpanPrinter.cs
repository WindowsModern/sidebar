using System;
namespace WindowsModern.PowerTile
{
	public static class TimeSpanPrinter
	{
		private static string R (string id)
		{
			return Tile.TileFolder.StringResources.SuitableResource (id);
		}
		public static string LongTimeSpanString (int seconds)
		{
			return FormatTimespan (seconds,
				R ("TilePowerTimeWeeksFormat"), R ("TilePowerTimeOneWeek"),
				R ("TilePowerTimeDaysFormat"), R ("TilePowerTimeHoursFormat"),
				R ("TilePowerTimeMinutesFormat"), R ("TilePowerTimeOneMinute"),
				R ("TilePowerTimeSecondsFormat"));
		}
		public static string ShortTimeSpanString (int seconds)
		{
			return FormatTimespan (seconds,
				R ("TilePowerTimeWeeksFormat"), R ("TilePowerTimeOneWeek"),
				R ("TilePowerTimeShortDaysFormat"), R ("TilePowerTimeShortHoursFormat"),
				R ("TilePowerTimeShortMinutesFormat"), R ("TilePowerTimeShortMinutesFormat"),
				R ("TilePowerTimeShortSecondsFormat"));
		}
		public static string FormatTimespan (int seconds, string formatWeeks, string formatWeek, string formatDays, string formatHours, string formatMinutes, string formatMinute, string formatSeconds)
		{
			var timeSpan = TimeSpan.FromSeconds (seconds);
			float weeks = (float)(timeSpan.TotalDays / 7.0);
			float days = (float)(timeSpan.TotalDays - 7f * weeks);
			string format;
			if (weeks >= 2.0) format = formatWeeks;
			else if (weeks >= 1.0) format = formatWeek;
			else if (days >= 1.0) format = formatDays;
			else if (timeSpan.TotalHours >= 1.0) format = formatHours;
			else if (timeSpan.TotalMinutes >= 1.0) format = timeSpan.TotalMinutes != 1.0 ? formatMinutes : formatMinute;
			else format = formatSeconds;
			return string.Format (format, weeks, days, timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
		}
	}
}