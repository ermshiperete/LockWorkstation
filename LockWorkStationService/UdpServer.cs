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
		private readonly string _address;
		private readonly int    _port;
		private readonly string _allowedRemoteAddress;
		private          Thread _serverThread;
		private          Action _action;
		private readonly Logger _logger;
		private          bool   _started;

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

		internal static bool IsInSubnet(IPAddress address, string subnet)
		{
			var addressBytes = address.GetAddressBytes();
			IPAddress subnetAddress;
			var parts = subnet.Split('/');
			if (parts.Length > 2)
			{
				// ReSharper disable once LocalizableElement
				throw new ArgumentException($"Invalid subnet '{subnet}'", nameof(subnet));
			}

			if (parts.Length > 1)
			{
				subnetAddress = IPAddress.Parse(parts[0]);
				var subnetLength = int.Parse(parts[1]);
				if (subnetLength > 32)
					// ReSharper disable once LocalizableElement
					throw new ArgumentException("Invalid subnet length", nameof(subnet));

				var subnetBytes = subnetAddress.GetAddressBytes();
				if (addressBytes.Length != subnetBytes.Length)
					throw new ArgumentException("Lengths of IP address and subnet do not match");

				for (var i = 0; i < subnetLength / 8; i++)
				{
					if (addressBytes[i] != subnetBytes[i])
						return false;
				}

				if (subnetLength % 8 == 0)
					return true;

				var index = subnetLength / 8;
				var mask = 0;
				for (var i = 0; i < subnetLength % 8; i++)
				{
					mask |= 1 << (7 - i);
				}

				return (addressBytes[index] & mask) == (subnetBytes[index] & mask);
			}
			else
			{
				subnetAddress = IPAddress.Parse(subnet);
				var subnetBytes = subnetAddress.GetAddressBytes();
				if (addressBytes.Length != subnetBytes.Length)
					throw new ArgumentException("Lengths of IP address and subnet do not match");

				for (var i = 0; i < addressBytes.Length; i++)
				{
					if (addressBytes[i] != subnetBytes[i])
						return false;
				}

				return true;
			}
		}

		private void Receive()
		{
			var count = 0;
			while (count < 10)
			{
				count++;
				_logger.Write("Server starting");
				try
				{
					var endPoint = new IPEndPoint(IPAddress.Parse(_address), _port);
					using var udpClient = new UdpClient();
					udpClient.ExclusiveAddressUse = false;
					var socket = udpClient.Client;
					socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
					socket.Bind(endPoint);

					var remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
					var responseOk = Encoding.ASCII.GetBytes("OK");
					var responseFailed = Encoding.ASCII.GetBytes("FAILED");
					var logEntryType = EventLogEntryType.Information;
					_logger.Write("Server ready");
					while (true)
					{
						var bytes = udpClient.Receive(ref remoteEndpoint);
						var data = Encoding.ASCII.GetString(bytes);

						// ReSharper disable once StringLiteralTypo
						_logger.Write($"RECV: {remoteEndpoint}: {bytes.Length} bytes: {data}", logEntryType);
						var isOk = data == "lock" && IsInSubnet(remoteEndpoint.Address, _allowedRemoteAddress);
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

						// ReSharper disable once StringLiteralTypo
						_logger.Write($"RECV: Result: {responseString}", logEntryType);
						var bytesToSend = isOk ? responseOk : responseFailed;
						udpClient.Send(bytesToSend, bytesToSend.Length, remoteEndpoint);
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
