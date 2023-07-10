using System;
using LockWorkStationService;

namespace TestServer
{
	internal class Program
	{
		public static void Main(string[] args)
		{
			var logger = new Logger("TestApp");
			var server =new UdpServer("0.0.0.0", 56341, "10.3.1.1", logger);
			server.Start();
		}
	}
}