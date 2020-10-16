using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using NLog;

namespace GTWNAV_SERVER_SERVICE
{
    class Device
    {
        //Setup logger object for nLog logging.
        private static Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public string name { get; set; }
        public string location { get; set; }
        public string ipAddress { get; set; }
        public string macAddress { get; set; }
        public string deviceType { get; set; }
        public bool isConnected { get; set; }
        public bool isActive { get; set; }
        public bool wsConnected { get; set; }
        public bool enabled { get; set; }
        public string htmlId { get; set; }
        public Device(string _name, string _location, string _ipaddress, string _macaddress, string _devicetype,bool _enabled,string _htmlId)
        {
            this.name = _name;
            this.location = _location;
            this.ipAddress = _ipaddress;
            this.macAddress = _macaddress;
            this.deviceType = _devicetype;
            this.isConnected = false;
            this.enabled = _enabled;
            this.htmlId = _htmlId;

        }

        public void testConnection()
        {
            if (this.isActive == true)
            {
                Task.Factory.StartNew(() => PingHost(this.ipAddress));
            }
        }
        public void testConnectionSync()
        {
            PingHost(this.ipAddress);
        }
        private bool PingHost(string nameOrAddress)
        {
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(nameOrAddress,2);
                pingable = reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                // Discard PingExceptions and return false;
            }
            finally
            {
                if (pinger != null)
                {
                    pinger.Dispose();
                }
            }
            this.isConnected = pingable;
            //Console.WriteLine("The IP address of " + this.ipAddress + " is contactable: " + pingable);
            if (pingable == false)
            {
                _logger.Error("Unable to ping " + this.ipAddress);
            }
            return pingable;
        }
    }
}
