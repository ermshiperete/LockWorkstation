using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LockWorkStationService
{
	public sealed class UdpServer
	{
		private const    int     BufSize = 256;
		private readonly string  _address;
		private readonly int     _port;
		private readonly string  _allowedRemoteAddress;
		private          Thread  _serverThread;
		private          Action  _action;
		private          Logger  _logger;
		private          bool    _started;

		public UdpServer(string address, int port, string allowedRemoteAddress, Logger logger)
		{
			_address = address;
			_port = port;
			_allowedRemoteAddress = allowedRemoteAddress;
			_logger = logger;
			_started = false;
		}

		public void Start(Action action = null)
		{
			if (_started)
				return;

			_started = true;
			_action = action;
			_serverThread = new Thread(Receive);
			_serverThread.Start();
		}

		public void Stop()
		{
			_serverThread.Abort();
		}

		private void Receive()
		{
			while (true)
			{
				_logger.Write("Server starting");
				try
				{
					using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
					socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
					socket.Bind(new IPEndPoint(IPAddress.Parse(_address), _port));

					var buffer = new byte[BufSize];
					EndPoint remoteEndpoint = new IPEndPoint(IPAddress.Parse(_allowedRemoteAddress), 0);
					var responseOk = Encoding.ASCII.GetBytes("OK");
					var responseFailed = Encoding.ASCII.GetBytes("FAILED");
					var logEntryType = EventLogEntryType.Information;
					_logger.Write("Server ready");
					while (true)
					{
						var bytes = socket.ReceiveFrom(buffer, ref remoteEndpoint);
						var data = Encoding.ASCII.GetString(buffer, 0, bytes);

						_logger.Write($"RECV: {remoteEndpoint}: {bytes} byte: {data}", logEntryType);
						var isOk = data == "lock" && ((IPEndPoint)remoteEndpoint).Address.Equals(IPAddress.Parse(_allowedRemoteAddress));
						var responseString = isOk ? "OK" : "FAILED";

						try
						{
							_action?.Invoke();
						}
						catch (Exception e)
						{
							responseString = $"Got exception {e.GetType()}: {e.Message}";
							logEntryType = EventLogEntryType.Error;
							isOk = false;
						}

						_logger.Write($"RECV: Result: {responseString}", logEntryType);
						socket.SendTo(isOk ? responseOk : responseFailed, remoteEndpoint);
					}
				}
				catch (ThreadAbortException)
				{
					_logger.Write("Ending server");
					return;
				}
				catch (SocketException ex)
				{
					_logger.Write($"Got exception {ex.GetType().Name}: {ex.Message}. Trying again in 10s");
					Thread.Sleep(new TimeSpan(0, 0, 10));
				}
			}
		}
	}
}
