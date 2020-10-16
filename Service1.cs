using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using MinimalisticTelnet;
using WebSocketSharp;
using System.Threading;
using Newtonsoft.Json;
using Json.Net;
using NLog;
using Fleck;
using Websocket_Server;
using System.Xml.Linq;

namespace GTWNAV_SERVER_SERVICE
{
    public partial class Service1 : ServiceBase
    {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private static WS_Server piServer;
        XDocument xDoc = XDocument.Load("C:\\Campbell Clinic Software\\GTWNAV_WS_SERVER_SERVICE\\Settings.xml");

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _logger.Info("GTWNAV_SERVER_SERVICE Starting v1.5");
            _logger.Info("Started a Task to begin Pi Server");

            string tmpIp = xDoc.Element("Settings").Element("serverAddress").Value;
            string tmpPort = xDoc.Element("Settings").Element("serverPort").Value;

            Task startServer = new Task(() => piServer = new WS_Server(false, tmpIp, tmpPort));
            startServer.Start();
        }

        protected override void OnStop()
        {
        }
    }
}
