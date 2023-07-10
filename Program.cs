using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using murrayju.ProcessExtensions;

namespace LockWorkstation
{
	internal class Program
	{
		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool LockWorkStation();
	
		public static void Main(string[] args)
		{
			Console.WriteLine("Starting");

			Process.Start("runas", @"/user:eberhard c:\windows\system32\tsdiscon.exe");
			
			//if (!LockWorkStation())
			//	Console.WriteLine("LockWorkStation failed");

			//ProcessExtensions.StartProcessAsCurrentUser("calc.exe");
		}
	}
}