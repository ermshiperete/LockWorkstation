using System;
using LockWorkStationService;

namespace TestServer
{
	internal class Program
	{
		public static void Main(string[] args)
		{
			const string logName = "TestApp";
			if (args.Length >= 1 && args[0] == "install")
			{
				Logger.Install(logName);
				return;
			}
			if (!Logger.IsInstalled(logName))
				Logger.Install(logName);
			var logger = new Logger(logName);
			var server =new UdpServer("0.0.0.0", 56431, "10.3.0.50/23", logger);
			server.Start();
		}
	}
}