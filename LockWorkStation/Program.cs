using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LockWorkStation
{
	internal class Program
	{
		public static void Main(string[] args)
		{
			var address = args[0];
			var port = args[1];

			using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
				ProtocolType.Udp))
			{
				var ipAddress = IPAddress.Parse(address);
				socket.Connect(ipAddress, int.Parse(port));

				var data = Encoding.ASCII.GetBytes("lock");
				socket.Send(data);

				var buffer = new byte[256];
				var endPoint = new IPEndPoint(ipAddress, 0) as EndPoint;
				var length = socket.ReceiveFrom(buffer, ref endPoint);
				Console.WriteLine(Encoding.ASCII.GetString(buffer, 0, length));
			}
		}
	}
}