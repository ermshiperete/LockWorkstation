using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace LockWorkStationService
{
    public partial class LockWorkStationService : ServiceBase
    {
        private readonly UdpServer _server;
        private readonly Logger _logger;
        public LockWorkStationService()
        {
            InitializeComponent();
            _logger = new Logger(nameof(LockWorkStationService));
            var address = ConfigurationManager.AppSettings["address"];
            var port = ConfigurationManager.AppSettings["port"];
            var remoteAddress = ConfigurationManager.AppSettings["remote"];

            _server = new UdpServer(address, int.Parse(port), remoteAddress, _logger);
        }

        protected override void OnStart(string[] args)
        {
            _server.Start(LockWorkStation);
        }

        protected override void OnStop()
        {
            _server.Stop();
        }

        private void LockWorkStation()
        {
            ProcessExtensions.StartProcessAsCurrentUser(@"c:\windows\system32\tsdiscon.exe");
            // ProcessExtensions.StartProcessAsCurrentUser(@"c:\windows\system32\rundll32.exe",
            //    "user32.dll,LockWorkStation");
        }

        public void Debug()
        {
            OnStart(new string[]{});
        }
    }
}
