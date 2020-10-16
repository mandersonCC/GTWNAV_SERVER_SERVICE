using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTWNAV_SERVER_SERVICE
{
    class connectedClients
    {
        public connectedClients()
        {
            this.socketId = "none";
            this.location = "none";
            this.deviceType = "none";
            this.ipAddress = "none";
            this.devicename = "none";
        }
        public string socketId { get; set; }
        public string location { get; set; }
        public string deviceType { get; set; }
        public string ipAddress { get; set; }
        public string devicename { get; set; }
    }
}
