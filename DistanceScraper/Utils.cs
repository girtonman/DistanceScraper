using System;
using System.Diagnostics;

namespace DistanceScraper
{
	public static class Utils
	{
		public static Stopwatch Timer = new Stopwatch();

		public static void Init()
		{
			Timer.Start();
		}

		public static void WriteLine(string message)
		{
			var elapsed = Timer.Elapsed;
			Console.WriteLine($"[{(int) elapsed.TotalMinutes}:{elapsed.Seconds.ToString("00")}]: {message}");
		}

		public static void ClearCurrentConsoleLine()
		{
			Console.SetCursorPosition(0, Console.CursorTop - 1);
			var currentLineCursor = Console.CursorTop;
			Console.SetCursorPosition(0, Console.CursorTop);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, currentLineCursor);
		}
	}
}
