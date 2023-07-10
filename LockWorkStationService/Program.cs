using System.ServiceProcess;

namespace LockWorkStationService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            var servicesToRun = new ServiceBase[]
            {
                new LockWorkStationService()
            };
            if (args.Length > 0 && args[0] == "--debug")
                ((LockWorkStationService)servicesToRun[0]).Debug();
            else
                ServiceBase.Run(servicesToRun);
        }
    }
}
