using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Threading;
using NLog;
namespace GTWNAV_SERVER_SERVICE
{

    class WS_Client
    {
        WebSocket wsClient;
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public string whichTV { get; set; }
        private string macAddress { get; set; }
        private string IP { get; set; }
        public bool isPaired { get; set; } = false;
        public string wsClientKey { get; set; } = "none";
        private int msgCount { get; set; } = 0;
        public bool isConnected { get; set; } = false;
        public int connectionStatus { get; set; } = 0;
        public WS_Client(string _macAddress, string _IP, string _whichTV)
        {
            this.whichTV = _whichTV;
            this.macAddress = _macAddress;
            this.IP = _IP;
            _logger.Debug("Established the property values of websockets client");
        }
        public void setupClient()
        {

            _logger.Debug("Begining the setupClient function");

            WOL.WakeOnLan(macAddress);

            _logger.Debug("Sending the WOL Packet over to " + this.whichTV);

            wsClient = new WebSocket(this.IP);

            wsClient.OnOpen += (o, e) =>
            {
                _logger.Info("Successfully Connected to " + this.whichTV);
                //wsClient.Send("{\"type\":\"register\",\"id\":\"register_0\",\"payload\":{\"forcePairing\":false,\"pairingType\":\"PROMPT\",\"manifest\":{\"manifestVersion\":1,\"appVersion\":\"1.1\",\"signed\":{\"created\":\"20140509\",\"appId\":\"com.lge.test\",\"vendorId\":\"com.lge\",\"localizedAppNames\":{\"\":\"LG Remote App\",\"ko-KR\":\"??? ?\",\"zxx-XX\":\"?? R??ot? A??\"},\"localizedVendorNames\":{\"\":\"LG Electronics\"},\"permissions\":[\"TEST_SECURE\",\"CONTROL_INPUT_TEXT\",\"CONTROL_MOUSE_AND_KEYBOARD\",\"READ_INSTALLED_APPS\",\"READ_LGE_SDX\",\"READ_NOTIFICATIONS\",\"SEARCH\",\"WRITE_SETTINGS\",\"WRITE_NOTIFICATION_ALERT\",\"CONTROL_POWER\",\"READ_CURRENT_CHANNEL\",\"READ_RUNNING_APPS\",\"READ_UPDATE_INFO\",\"UPDATE_FROM_REMOTE_APP\",\"READ_LGE_TV_INPUT_EVENTS\",\"READ_TV_CURRENT_TIME\"],\"serial\":\"2f930e2d2cfe083771f68e4fe7bb07\"},\"permissions\":[\"LAUNCH\",\"LAUNCH_WEBAPP\",\"APP_TO_APP\",\"CLOSE\",\"TEST_OPEN\",\"TEST_PROTECTED\",\"CONTROL_AUDIO\",\"CONTROL_DISPLAY\",\"CONTROL_INPUT_JOYSTICK\",\"CONTROL_INPUT_MEDIA_RECORDING\",\"CONTROL_INPUT_MEDIA_PLAYBACK\",\"CONTROL_INPUT_TV\",\"CONTROL_POWER\",\"READ_APP_STATUS\",\"READ_CURRENT_CHANNEL\",\"READ_INPUT_DEVICE_LIST\",\"READ_NETWORK_STATE\",\"READ_RUNNING_APPS\",\"READ_TV_CHANNEL_LIST\",\"WRITE_NOTIFICATION_TOAST\",\"READ_POWER_STATE\",\"READ_COUNTRY_INFO\"],\"signatures\":[{\"signatureVersion\":1,\"signature\":\"eyJhbGdvcml0aG0iOiJSU0EtU0hBMjU2Iiwia2V5SWQiOiJ0ZXN0LXNpZ25pbmctY2VydCIsInNpZ25hdHVyZVZlcnNpb24iOjF9.hrVRgjCwXVvE2OOSpDZ58hR+59aFNwYDyjQgKk3auukd7pcegmE2CzPCa0bJ0ZsRAcKkCTJrWo5iDzNhMBWRyaMOv5zWSrthlf7G128qvIlpMT0YNY+n/FaOHE73uLrS/g7swl3/qH/BGFG2Hu4RlL48eb3lLKqTt2xKHdCs6Cd4RMfJPYnzgvI4BNrFUKsjkcu+WD4OO2A27Pq1n50cMchmcaXadJhGrOqH5YmHdOCj5NSHzJYrsW0HPlpuAx/ECMeIZYDh6RMqaFM2DXzdKX9NmmyqzJ3o/0lkk/N97gfVRLW5hA29yeAwaCViZNCP8iC9aO0q9fQojoa7NQnAtw==\"}]}}}");
                isConnected = true;
                connectionStatus = 2;
            };

            wsClient.OnMessage += (o, e) =>
            {
                _logger.Info("Received a message from the TV Webscokets server. The message is " + e.Data.ToString());
                dynamic recMsg = JsonConvert.DeserializeObject(e.Data.ToString().Replace("-", ""));
                //check if this is the TV passing back the client code
                if (recMsg.type == "registered")
                {
                    this.wsClientKey = recMsg.payload.clientkey;
                    this.isPaired = true;
                    
                }
                if (recMsg.type == "error")
                {
                    //Error Messages that could be received from the TVs

                    //We are not paired because the TV has prompted the user to accept the pairing.
                    if (recMsg.error == "409 register already in progress")
                    {

                    }

                    if (recMsg.error == "403 cancelled")
                    {
                        this.isPaired = false;
                    }
                }
            };

            wsClient.OnError += (o, e) =>
            {
                Console.WriteLine("There is an error connecting and the message is " + e.Message.ToString());
                _logger.Error("There is an error connecting and the message is " + e.Message.ToString());
                //errorRecvd = true;  
            };

            wsClient.OnClose += (sender, e) =>
            {
                //Console.WriteLine("This stupid disconnected");
                _logger.Warn("The websocket connection with " + this.whichTV + " with an ip address of has been lost");
                this.connectionStatus = 0;
            };

            _logger.Info("Trying to Connect");
            try
            {
                this.connectionStatus = 1;
                wsClient.Connect();
            }
            catch (Exception e)
            {
                _logger.Error("There was a failure to connect and the message is " + e.ToString());
                
            }
        }
        public void tvCommands()
        {
            WOL.WakeOnLan(macAddress);
            if (wsClientKey != "none")
            {
                wsClient.Send("{\"type\":\"request\",\"id\":\"switchinput_" + msgCount + "\",\"uri\":\"ssap://tv/switchInput\",\"payload\":{\"inputId\":\"HDMI_1\"}}");
                _logger.Debug("Requesting Device switch to HDMI1 inside tvcommands Function");
                msgCount++;
            }
            else
            {
                _logger.Debug("wsClientKey is none so sleeping 5 seconds and trying again");
                Thread.Sleep(5000);
                if (wsClientKey != "none")
                {
                    _logger.Debug("Requesting Device switch to HDMI1 inside tvcommands Function after pausing for 5 seconds.");
                    wsClient.Send("{\"type\":\"request\",\"id\":\"switchinput_" + msgCount + "\",\"uri\":\"ssap://tv/switchInput\",\"payload\":{\"inputId\":\"HDMI_1\"}}");
                    msgCount++;
                }
                else
                {
                    Thread.Sleep(5000);
                }
            }
        }

        public void muteVolume()
        {

        }

        public bool checkPairing()
        {
            //Console.WriteLine("Checking to see how the pairing is");
            wsClient.Send("{\"type\":\"register\",\"id\":\"register_" + msgCount + "\",\"payload\":{\"forcePairing\":false,\"pairingType\":\"PROMPT\",\"client-key\":\"" + wsClientKey + "\",\"manifest\":{\"manifestVersion\":1,\"appVersion\":\"1.1\",\"signed\":{\"created\":\"20140509\",\"appId\":\"com.lge.test\",\"vendorId\":\"com.lge\",\"localizedAppNames\":{\"\":\"LG Remote App\",\"ko-KR\":\"리모컨 앱\",\"zxx-XX\":\"ЛГ Rэмotэ AПП\"},\"localizedVendorNames\":{\"\":\"LG Electronics\"},\"permissions\":[\"TEST_SECURE\",\"CONTROL_INPUT_TEXT\",\"CONTROL_MOUSE_AND_KEYBOARD\",\"READ_INSTALLED_APPS\",\"READ_LGE_SDX\",\"READ_NOTIFICATIONS\",\"SEARCH\",\"WRITE_SETTINGS\",\"WRITE_NOTIFICATION_ALERT\",\"CONTROL_POWER\",\"READ_CURRENT_CHANNEL\",\"READ_RUNNING_APPS\",\"READ_UPDATE_INFO\",\"UPDATE_FROM_REMOTE_APP\",\"READ_LGE_TV_INPUT_EVENTS\",\"READ_TV_CURRENT_TIME\"],\"serial\":\"2f930e2d2cfe083771f68e4fe7bb07\"},\"permissions\":[\"LAUNCH\",\"LAUNCH_WEBAPP\",\"APP_TO_APP\",\"CLOSE\",\"TEST_OPEN\",\"TEST_PROTECTED\",\"CONTROL_AUDIO\",\"CONTROL_DISPLAY\",\"CONTROL_INPUT_JOYSTICK\",\"CONTROL_INPUT_MEDIA_RECORDING\",\"CONTROL_INPUT_MEDIA_PLAYBACK\",\"CONTROL_INPUT_TV\",\"CONTROL_POWER\",\"READ_APP_STATUS\",\"READ_CURRENT_CHANNEL\",\"READ_INPUT_DEVICE_LIST\",\"READ_NETWORK_STATE\",\"READ_RUNNING_APPS\",\"READ_TV_CHANNEL_LIST\",\"WRITE_NOTIFICATION_TOAST\",\"READ_POWER_STATE\",\"READ_COUNTRY_INFO\"],\"signatures\":[{\"signatureVersion\":1,\"signature\":\"eyJhbGdvcml0aG0iOiJSU0EtU0hBMjU2Iiwia2V5SWQiOiJ0ZXN0LXNpZ25pbmctY2VydCIsInNpZ25hdHVyZVZlcnNpb24iOjF9.hrVRgjCwXVvE2OOSpDZ58hR+59aFNwYDyjQgKk3auukd7pcegmE2CzPCa0bJ0ZsRAcKkCTJrWo5iDzNhMBWRyaMOv5zWSrthlf7G128qvIlpMT0YNY+n/FaOHE73uLrS/g7swl3/qH/BGFG2Hu4RlL48eb3lLKqTt2xKHdCs6Cd4RMfJPYnzgvI4BNrFUKsjkcu+WD4OO2A27Pq1n50cMchmcaXadJhGrOqH5YmHdOCj5NSHzJYrsW0HPlpuAx/ECMeIZYDh6RMqaFM2DXzdKX9NmmyqzJ3o/0lkk/N97gfVRLW5hA29yeAwaCViZNCP8iC9aO0q9fQojoa7NQnAtw==\"}]}}}");
            msgCount++;
            return false;
        }
    }

}
