using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;

namespace LockWorkStationService
{
	public class Logger: IDisposable
	{
		private readonly EventLog _eventLog;

		public Logger(string logname)
		{
			_eventLog = new EventLog("Application") {Source = logname};
		}

		public static void Install(string logname)
		{
			try
			{
				if (!EventLog.SourceExists(logname))
					EventLog.CreateEventSource(logname, "Application");
			}
			catch (SecurityException e)
			{
				Console.WriteLine($"Don't have permission to access registry: {e.Message}");
				throw;
			}
		}

		public static bool IsInstalled(string logname)
		{
			return EventLog.SourceExists(logname);
		}

		public void Write(string message, EventLogEntryType type = EventLogEntryType.Information)
		{
			_eventLog.WriteEntry(message, type);
		}

		public void Dispose()
		{
			_eventLog?.Dispose();
		}
	}
}