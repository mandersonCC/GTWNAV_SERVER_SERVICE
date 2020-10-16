using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Fleck;
using Newtonsoft.Json;
using System.Timers;
using PrimS.Telnet;
using MinimalisticTelnet;
using System.Threading;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.IO;
using GTWNAV_SERVER_SERVICE;
using System.Xml.Linq;
using System.ComponentModel.Design;

namespace Websocket_Server
{

    /// <summary>
    /// Contains all the code to run a websockets server.
    /// Requires the use of a custom Fleck project that can be found in the CC GitLab
    /// Download Fleck, and add the project file into your solution
    /// Go into references and add a reference to the local copy of Fleck 
    /// </summary>
    /// 



    class WS_Server
    {
        //Class ID: 50

        public string currentInput { get; set; }
        public bool isMuted { get; set; }
        public List<string> selectedDisplays { get; set; }
        public List<Communcations> recvdComms = new List<Communcations>();

        public string currentAction { get; set; }

        //Setup logger object for nLog logging.
        private static Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private bool useSSL { get; set; } //Determines if we should use the secure websockets connection
        private string serverAddress { get; set; } //Providers the address of the server
        private string serverPort { get; set; } //Port the WS server will listen on
        private string certLocation { get; set; } //Location and filename of the pfx needed for SSL
        private string certPassword { get; set; } //the private key of the cert file
        private List<connectedClients> _connectedClients = new List<connectedClients>();
        private List<IWebSocketConnection> sockets = new List<IWebSocketConnection>(); //List to hold all of the connections
                                                                                       //private List<IWebSocketConnection> dash_sockets = new List<IWebSocketConnection>(); //List to hold connections of open dashboards since do not want to disconnect
                                                                                       //private List<ScannerClient> scanner_list = new List<ScannerClient>(); //List to hold info of scanner connections
        public List<Device> tvList = new List<Device>();
        XDocument xDoc = XDocument.Load("C:\\Campbell Clinic Software\\GTWNAV_WS_SERVER_SERVICE\\Settings.xml");
        public WS_Server(bool _useSSL, string _serverAddress, string _serverPort)
        {
            //Function ID: 10
            try
            {
                createDeviceList();
                //Set the server properties
                this.useSSL = _useSSL;
                this.serverAddress = _serverAddress;
                this.serverPort = _serverPort;



                _logger.Info("Class properties have been set. They are as follows: useSSL: " + useSSL + " serverAddress: " + serverAddress + " serverPort: " + serverPort);

                _logger.Info("Sending the Start Function");
                this.Start(); //Signal the server to begin operations

                selectedDisplays = new List<string>();
            }
            catch (Exception e)
            {
                _logger.Error("5010-01|The function performed an exception and is unable to continue. Please see the exeception error for more information: " + e.ToString());
            }

        }

        public void Start()
        {
            //Function ID: 20

            //Starts the websocket server and setup the event handlers

            WebSocketServer server;

            try
            {
                //Check to see if we should be using a secure or not
                if (useSSL == true)
                {
                    server = new WebSocketServer("wss://" + serverAddress + ":" + serverPort);
                    server.Certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certLocation, certPassword);
                }
                else
                {
                    server = new WebSocketServer("ws://" + serverAddress + ":" + serverPort);
                }

                _logger.Info("Starting the server on port " + serverPort);
                server.Start(socket =>
                {
                    socket.OnOpen = () =>
                    {
                        //Excutes when a new connection is established
                        try
                        {
                            sockets.Add(socket); //Add the newly connected socket to the list of sockets
                            socket.Send("socketID: " + socket.ConnectionInfo.Id);
                            _logger.Info("Created Connection with " + socket.ConnectionInfo.ClientIpAddress);
                            //_logger.Info("Created Connection with " + socket.ConnectionInfo.ClientIpAddress);
                            connectedClients tmpClient = new connectedClients();
                            tmpClient.socketId = socket.ConnectionInfo.Id.ToString();
                            tmpClient.ipAddress = socket.ConnectionInfo.ClientIpAddress;
                            _connectedClients.Add(tmpClient);
                        }
                        catch (Exception ex) {
                            //System.Diagnostics.Info.Write("trying to add socket");
                            _logger.Error("There is an exception error trying to add socket. The error is " + Environment.NewLine + ex.ToString());
                        }
                    };

                    socket.OnClose = () =>
                    {
                        try
                        {
                            //Executes when an existing connection is closed
                            _logger.Info("Closed Connection with " + socket.ConnectionInfo.ClientIpAddress);
                            //_logger.Info("Closed Connection with " + socket.ConnectionInfo.ClientIpAddress);
                            sockets.Remove(socket); //Remove the closed connection from the list of active connections.
                            foreach (IWebSocketConnection _socket in sockets)
                            {
                                //_logger.Info(_socket.ConnectionInfo.Id + " " + _socket.ConnectionInfo.ClientIpAddress);
                                _logger.Info(_socket.ConnectionInfo.Id + " " + _socket.ConnectionInfo.ClientIpAddress);
                            }
                            List<connectedClients> tmpList = new List<connectedClients>();
                            foreach (connectedClients cc in _connectedClients)
                            {
                                if (cc.socketId != socket.ConnectionInfo.Id.ToString())
                                {
                                    tmpList.Add(cc);
                                }
                            }
                            _connectedClients.Clear();
                            _connectedClients = tmpList;
                            //_logger.Info("Number of Items in _ConnectCliented List is " + _connectedClients.Count());
                            _logger.Info("Number of Items in _ConnectCliented List is " + _connectedClients.Count());
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("There is an exception error trying to remove the socket from the list while closing connect. The error is " + Environment.NewLine + ex.ToString());
                            //_logger.Info(ex.ToString());
                        }
                    };

                    socket.OnMessage = (message) =>
                    {
                        if (message.IndexOf("ping") == -1)
                        {

                            _logger.Info("Recieved Message From " + socket.ConnectionInfo.ClientIpAddress + " The contents are: " + Environment.NewLine + message);
                        }
                        //Executes when a message is recevied from a client
                        string msgback = this.receiveMsg(socket, message);
                        dynamic received = JsonConvert.DeserializeObject(message);

                    };

                    socket.OnPong = (message) =>
                    {
                        //Javascript client API does not support Ping/Pong


                    };

                    socket.OnPing = (message) =>
                    {
                        //Javascript client API does not support Ping/Pong
                        //Actions to take when we receive a ping
                        socket.SendPing(message);
                    };
                });
            }
            catch (Exception e)
            {
                _logger.Info("FAILED to start server");
                _logger.Error("5020-01|The function performed an exception and is unable to continue. Please see the exeception error for more information: " + e.ToString());
            }
        }
        private string receiveMsg(IWebSocketConnection _socket, string _message)
        {
            string recvMsg = "";

            //Deconstruct the json message into a dynamic object since different messages will be constructed differently
            dynamic received = JsonConvert.DeserializeObject(_message);
            try
            {
                _logger.Info("Line 205");
                _logger.Info(received);
                recvMsg = received.Message.ToString().ToLower();
                string msgSender = received.Source.ToString().ToLower();
                _logger.Info("The msgSender is " + msgSender);
                string sourceID = received.SourceID.ToString().ToLower();
                string Which = received.Which.ToString().ToLower();
                string Action = received.Action.ToString().ToLower();
                _logger.Info("Line 2115");
                if (msgSender == "controller")
                {
                    switch (recvMsg)
                    {
                        //Generic commands for all implementations
                        case "ping":
                            this.receivedPing(_socket, received);
                            break;
                        case "volume":
                            this.changeVolume(received);
                            break;
                        case "display":
                            _logger.Info("Received request to add or remove a display");
                            this.changeMatrix(received, _socket);
                            break;
                        case "status":
                            
                            _logger.Info("i have recevied a status request from  " + _socket.ConnectionInfo.ClientIpAddress);
                            getStatus(_socket);
                            break;
                        default:
                            _logger.Info("Line 225");
                            bool isSuccess = changeMatrix(received, _socket);
                            if (isSuccess == true)
                            {
                                _socket.Send("1");
                            }
                            break;
                    }

                }
                _logger.Info("Line 233");
                if (msgSender == "receiver")
                {
                    _logger.Info("Line 236");
                    switch (recvMsg)
                    {
                        case "connected":
                            _logger.Info("Line 239");
                            for (int i = 0; i < _connectedClients.Count; i++)
                            {
                                _logger.Info("Line 242");
                                if (_connectedClients[i].socketId == Which)
                                {
                                    _logger.Info("Line 245");
                                    _connectedClients[i].devicename = sourceID;
                                    _logger.Info("Ok I have a sourceID");
                                }
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("There is an exception error in the receivemsg function and it is " + ex.ToString());
            }
            return recvMsg;
        }
        #region Client Commands Processing

        private void receivedPing(IWebSocketConnection _socket, dynamic msg)
        {
        }
        private void getStatus(IWebSocketConnection _socket)
        {
            _logger.Info("I have been asked to get the status of the matrix");
            _socket.Send("StatusVol|" + this.isMuted);
            //First get the ping status of the TVs. 
            //We assume if we can ping them then they are turned on. 
            foreach (Device tv in tvList)
            {
                tv.testConnectionSync();
                //_socket.Send("StatusUp|{\"htmlId\":\""+tv.htmlId+"\",\"status\":\""+tv.isConnected+"\"}");
                
            }
            //Next we need to pull a list of inputs and outputs from the primary HDMI Matrix in the GTWN Boardroom.
            TelnetConnection tc = new TelnetConnection("172.16.0.33", 5000); //Create connection to GTWN HDMI Matrix
            tc.Write("MT00RD0000NT"); //This command ask for a list of all the inputs and outputs.
            List<Matrix> matrixList = new List<Matrix>();
            string tmpReturnTC = tc.Read().ToString();
            _logger.Info("The returned value from the primary HDMI Matrix is " + tmpReturnTC);
           
            string[] telnetFeedback = tmpReturnTC.Split(';'); //Spilt up the string into an array 
                                                                       //Example of the returned string from the HDMI matrix is: LINK:O1I1;O2I1;O3I1;O4I8;O5I1;O6I8;O7I1;O8I1;END
                                                                       //That means:
                                                                       //Output 1 is using Input 1 as it's source
                                                                       //Output 2 is using Input 1 as it's source
                                                                       //Output 3 is using Input 1 as it's source
                                                                       //Output 4 is using Input 8 as it's source.
                                                                       //etc
            _logger.Info("The number of entries in the telnetFeedback array is  " + telnetFeedback.Count());
            for (int i=0;i<telnetFeedback.Count();i++)
            {
                _logger.Info("The value of telnetFeed number " + i + " is " + telnetFeedback[i]);
            }
            string tmpString;
            Matrix tmpMatrix;
            Device tmpTV;
                //********************************************************************************//
                //************* CHECK FOR OUTPUT 1 and 2 INFO ************************************//
                //************* OUTPUT 1 and 2 are Boardroom Big Side TVs ************************//
                //********************************************************************************//

            //Check for the header word "link" the word linnk is the designation as the begining of the string.
            if (telnetFeedback[0].IndexOf("LINK") > -1)
                {
                    //Remove the LINK out so I can isolate the Input and Output.
                    tmpString = telnetFeedback[0].Substring(telnetFeedback[0].IndexOf("O", 4));
                    tmpMatrix = new Matrix();
                    tmpMatrix.matrixInput = Convert.ToInt32(tmpString.Substring(3, 1));
                    tmpMatrix.matrixOuput = Convert.ToInt32(tmpString.Substring(1, 1));
                    this.currentInput = "0"+tmpMatrix.matrixInput.ToString(); 
                    if (tmpMatrix.matrixOuput == 1) 
                    {
                        //Get the selected input for output 1. 
                        //Output 1 is the main boardroom TV, Output 1 and 2 should always be sync'd.
                        
                        tmpTV = tvList.Where(n => n.location == "boroombig" && n.name == "TV1").FirstOrDefault();
                        //Send a status update to the ipad that tells the iPad if it can ping the TV and which input is selected on the matrix.
                        _socket.Send("StatusUp|{\"htmlId\":\"" + tmpTV.htmlId + "\",\"status\":\"" + tmpTV.isConnected + "\",\"selectedInput\":\"" + tmpMatrix.matrixInput + "\"}");
                    }
                    matrixList.Add(tmpMatrix);
                }

            //**********************************************************************************//
            //************* CHECK FOR OUTPUT 3 *************************************************//
            //************* OUTPUT 3 is the small side of the boardroom ************************//
            //**********************************************************************************//
            
            //tmpString = telnetFeedback[2].Substring(telnetFeedback[2].IndexOf("O", 4));
            _logger.Info("The value of telnetFeedback[2] is " + telnetFeedback[2]);
            tmpMatrix = new Matrix();
            tmpMatrix.matrixInput = Convert.ToInt32(telnetFeedback[2].Substring(3, 1));
            tmpMatrix.matrixOuput = Convert.ToInt32(telnetFeedback[2].Substring(1, 1));
            tmpTV = tvList.Where(n => n.location == "boroomsmall" && n.name == "TV").FirstOrDefault();
            //Send a status update to the ipad that tells the iPad if it can ping the TV and which input is selected on the matrix.
            _socket.Send("StatusUp|{\"htmlId\":\"" + tmpTV.htmlId + "\",\"status\":\"" + tmpTV.isConnected + "\",\"selectedInput\":\"" + tmpMatrix.matrixInput + "\"}");
            // matrixList.Add(tmpMatrix);
            if (tmpTV.isConnected == false || tmpMatrix.matrixInput == 8)
            {
                changeVolume(JsonConvert.DeserializeObject("{\"Which\":\"boroomsmall\",\"Action\":\"mute\",\"Message\":\"Volume\"}"));
            }
                
            //**********************************************************************************//
            //************* CHECK FOR OUTPUT 4 *************************************************//
            //************* OUTPUT 4 is Admin Conference Room A ********************************//
            //**********************************************************************************//

            //tmpString = telnetFeedback[3].Substring(telnetFeedback[3].IndexOf("O", 4));
            tmpMatrix = new Matrix();
            tmpMatrix.matrixInput = Convert.ToInt32(telnetFeedback[3].Substring(3, 1));
            tmpMatrix.matrixOuput = Convert.ToInt32(telnetFeedback[3].Substring(1, 1));
            tmpTV = tvList.Where(n => n.location == "adminconf1" && n.name == "TV").FirstOrDefault();
            //Send a status update to the ipad that tells the iPad if it can ping the TV and which input is selected on the matrix.
            _socket.Send("StatusUp|{\"htmlId\":\"" + tmpTV.htmlId + "\",\"status\":\"" + tmpTV.isConnected + "\",\"selectedInput\":\"" + tmpMatrix.matrixInput + "\"}");
            //matrixList.Add(tmpMatrix);


            //**********************************************************************************//
            //************* CHECK FOR OUTPUT 5 *************************************************//
            //************* OUTPUT 5 is Admin Conference Room B ********************************//
            //**********************************************************************************//

            //tmpString = telnetFeedback[4].Substring(telnetFeedback[4].IndexOf("O", 4));
            tmpMatrix = new Matrix();
            tmpMatrix.matrixInput = Convert.ToInt32(telnetFeedback[4].Substring(3, 1));
            tmpMatrix.matrixOuput = Convert.ToInt32(telnetFeedback[4].Substring(1, 1));
            tmpTV = tvList.Where(n => n.location == "adminconf2" && n.name == "TV").FirstOrDefault();
            //Send a status update to the ipad that tells the iPad if it can ping the TV and which input is selected on the matrix.
            _socket.Send("StatusUp|{\"htmlId\":\"" + tmpTV.htmlId + "\",\"status\":\"" + tmpTV.isConnected + "\",\"selectedInput\":\"" + tmpMatrix.matrixInput + "\"}");
            //matrixList.Add(tmpMatrix);

            //**********************************************************************************//
            //************* CHECK FOR OUTPUT 6 *************************************************//
            //************* OUTPUT 6 is Research Room  *****************************************//
            //**********************************************************************************//

            //tmpString = telnetFeedback[5].Substring(telnetFeedback[5].IndexOf("O", 4));
            tmpMatrix = new Matrix();
            tmpMatrix.matrixInput = Convert.ToInt32(telnetFeedback[5].Substring(3, 1));
            tmpMatrix.matrixOuput = Convert.ToInt32(telnetFeedback[5].Substring(1, 1));
            tmpTV = tvList.Where(n => n.location == "researchroom" && n.name == "TV").FirstOrDefault();
            //Send a status update to the ipad that tells the iPad if it can ping the TV and which input is selected on the matrix.
            _socket.Send("StatusUp|{\"htmlId\":\"" + tmpTV.htmlId + "\",\"status\":\"" + tmpTV.isConnected + "\",\"selectedInput\":\"" + tmpMatrix.matrixInput + "\"}");
            //matrixList.Add(tmpMatrix);

            //We then need to query each of the Matrixs in the small conference rooms and collect their input and output status
            //This information will let us know if the buttons for the other displays should be lit up.
            Array.Clear(telnetFeedback, 0, telnetFeedback.Count());
            tc.Disconnect();
        }
        #endregion

        public void changeVolume(dynamic received)
        {
            _logger.Info("Starting changeVolume Function");
            _logger.Info("The receive variable is (inside the changeVolume Function) " + received);
            string whichDevice = received.Which.ToString().ToLower();
            
            string action = received.Action.ToString().ToLower();
            string message = received.Message.ToString().ToLower();

            _logger.Info("Finished Setting the variables in changeVolume for whichdeivce " + whichDevice);

            switch (whichDevice)
            {
                case "boroombig":
                    _logger.Info("Executing boroombig select case inside changevolum function");
                    string includeSmall = received.IncludeSmall.ToString().ToLower();
                    foreach (connectedClients cc in _connectedClients)
                    {
                        _logger.Debug("The name of the connected client is " + cc.devicename);
                        if (cc.devicename.IndexOf("boardroomamp") > -1)
                        {
                            _logger.Info("I found the amp device and I am snding msgs");
                            foreach (IWebSocketConnection _soc in sockets)
                            {
                                _logger.Info("I am looping through looking to see if " + _soc.ConnectionInfo.Id.ToString() + "==" + cc.socketId);
                                if (_soc.ConnectionInfo.Id.ToString() == cc.socketId)
                                {
                                    _logger.Info("I am sending a message to socket ID " + cc.socketId);
                                    if (action == "mute")
                                    {
                                        this.isMuted = true;
                                    }
                                    if (action == "unmute")
                                    {
                                        this.isMuted = false;
                                    }
                                    if (includeSmall == "true")
                                    {
                                        _logger.Info("I am sending the following message to the amp computer that includes small "  + message + " " + action);
                                        _soc.Send("Please set " + message + " " + action + " include small");
                                    }
                                    else
                                    {
                                        _logger.Info("I am sending the following message to the amp computer that does not includes small " + message + " " + action);
                                        _soc.Send("Please set " + message + " " + action);
                                    }
                                }
                            }
                        }
                    }
                break;

                case "boroomsmall":
                    _logger.Info("Changing volume for the board room small section");
                    foreach (connectedClients cc in _connectedClients)
                    {
                        if (cc.devicename.IndexOf("boardroomamp") > -1)
                        {
                            foreach (IWebSocketConnection _soc in sockets)
                            {
                                if (_soc.ConnectionInfo.Id.ToString() == cc.socketId)
                                {
                                    _soc.Send("Please set " + message + " " + action + " only small");
                                }
                            }
                        }
                    }
                break;
            }
        }
        public bool changeMatrix(dynamic received, IWebSocketConnection _socket)
        {
            _logger.Info("Line 306");
            _logger.Info(received);
            string whichDevice = received.Which.ToString().ToLower();
            string action = received.Action.ToString().ToLower();
            string message = received.Message.ToString().ToLower();
            string includeSmall = "false";
            //_logger.Info("Checking to see if I can detect if a json properity is passed or not by looking for the index of includesmall " + );
            if (received.ToString().ToLower().IndexOf("includesmall") > 1)
            {
                //Checks to see if a json properity of "Include Small" is part of the passed json string.
                //We have to be careful to not try and set the value if the property is not there as it causes an exception error.
                includeSmall = "true";
                
            }
            List<Device> tvs = new List<Device>();
            switch (whichDevice)
            {
                case "boroombig":
                    _logger.Info("Line 315");
                    tvs = tvList.Where(n => n.location == whichDevice).ToList();
                    foreach (Device tv in tvs)
                    {
                        _logger.Info("The value of is tv connect is " + tv.isConnected + " and the TV is " + tv.name);
                        int connectAttempt = 0;
                        tv.testConnectionSync();
                        while (tv.isConnected == false)
                        {
                            _logger.Info("Line 322");
                            if (connectAttempt < 6)
                            {
                                _logger.Info("Line 325");
                                connectAttempt++;
                                tv.testConnectionSync();
                                WOL.WakeOnLan(tv.macAddress);
                                _logger.Info("Trying to wake up " + tv.ipAddress);
                            }
                            else
                            {
                                _logger.Info("Line 333");
                                _logger.Info("Unable to connect, ending");
                                break;
                            }
                        }
                    }
                    foreach (connectedClients cc in _connectedClients)
                    {
                        _logger.Info("Line 341");
                        if (cc.devicename.IndexOf("board") > -1)
                        {
                            foreach (IWebSocketConnection _soc in sockets)
                            {
                                if (_soc.ConnectionInfo.Id.ToString() == cc.socketId)
                                {
                                    _logger.Info("Line 348");
                                    _soc.Send("Please set your TV to HDMI1");
                                }
                            }
                        }
                    }
                    break;
                case "boroomsmall":
                    _logger.Info("Line 362");
                    tvs = tvList.Where(n => n.location == whichDevice).ToList();
                    
                    foreach (Device tv in tvs)
                    {
                        _logger.Info("The value of is tv connect is " + tv.isConnected + " and the TV is " + tv.name);
                        int connectAttempt = 0;
                        tv.testConnectionSync();
                        while (tv.isConnected == false)
                        {
                            _logger.Info("Line 371");
                            if (connectAttempt < 6)
                            {
                                _logger.Info("Line 374");
                                connectAttempt++;
                                tv.testConnectionSync();
                                WOL.WakeOnLan(tv.macAddress);
                                _logger.Info("Trying to wake up " + tv.ipAddress);
                            }
                            else
                            {
                                _logger.Info("Line 382");
                                _logger.Info("Unable to connect, ending");
                                break;
                            }
                        }
                    }
                    foreach (connectedClients cc in _connectedClients)
                    {
                        _logger.Info("Line 390");
                        if (cc.devicename.IndexOf("board") > -1)
                        {
                            foreach (IWebSocketConnection _soc in sockets)
                            {
                                if (_soc.ConnectionInfo.Id.ToString() == cc.socketId)
                                {
                                    _logger.Info("Line 397");
                                    _soc.Send("Please set your TV to HDMI1");
                                }
                            }
                        }
                    }
                    break;
            }

            _logger.Info("Changing Matrix to " + whichDevice);

            if (action == "add")
            {
                this.currentAction = "add";
            }
            else
            {
                this.currentAction = "remove";
            }

            string tmpDisplayID = "";
            _logger.Info("The switch case message");
            switch (message)
            {
                case "input":
                    _logger.Info("The switch case message -- Triggered on input");
                    _logger.Info("The switch case whichDevice ");
                    switch (whichDevice)
                    {
                        case "computer":
                            _logger.Info("The switch case whichDevice -- Triggered Computer");
                            this.currentInput = "01";
                            break;
                        case "apple":
                            _logger.Info("The switch case whichDevice -- Triggered Apple");
                            this.currentInput = "02";
                            break;
                        case "tblhdmi":
                            _logger.Info("The switch case whichDevice -- Triggered Table HDMI");
                            this.currentInput = "03";
                            break;
                        case "comcast":
                            _logger.Info("The switch case whichDevice -- Triggered Comcast");
                            this.currentInput = "04";
                            break;
                    }
                    _logger.Info("Calling the Processaction Function");
                    this.processAction(_socket, this.currentInput, this.currentAction,"input",includeSmall);
                    return true;
                case "display":
                    _logger.Info("The switch case message -- Triggered on display");
                    _logger.Info("The switch case whichDevice ");
                    switch (whichDevice)
                    {
                        case "boroombig":
                            _logger.Info("The switch case whichDevice -- boroombig ");
                            tmpDisplayID = "01";
                            this.currentInput = "01";
                            break;
                        case "boroomsmall":
                            _logger.Info("The switch case whichDevice -- boroomsmall ");
                            tmpDisplayID = "03";
                            _logger.Info("the value if recieved is " + received);
                            string muteZone = received.MuteZone;
                            _logger.Debug("I made it past mutezone");
                            dynamic tmpRec;
                            if (this.currentAction == "add")
                            {
                                if (muteZone == "true")
                                {
                                   tmpRec = JsonConvert.DeserializeObject("{\"Which\":\"boroomsmall\",\"Action\":\"mute\",\"Message\":\"Volume\"}");
                                }
                                else
                                {
                                    tmpRec = JsonConvert.DeserializeObject("{\"Which\":\"boroomsmall\",\"Action\":\"unmute\",\"Message\":\"Volume\"}");
                                }
                            }
                            else
                            {
                                tmpRec = JsonConvert.DeserializeObject("{\"Which\":\"boroomsmall\",\"Action\":\"mute\",\"Message\":\"Volume\"}");
                            }
                            changeVolume(tmpRec);
                            break;
                        case "adminconfa":
                            _logger.Info("The switch case whichDevice -- adminconfa ");
                            tmpDisplayID = "04";
                            break;
                        case "adminconfb":
                            _logger.Info("The switch case whichDevice -- adminconfb ");
                            tmpDisplayID = "05";
                            break;
                        case "research":
                            _logger.Info("The switch case whichDevice -- research ");
                            tmpDisplayID = "06";
                            break;
                    }

                    this.processAction(_socket, tmpDisplayID,action, "display");
                    _logger.Info("Change Matrix Returned True");
                    return true;
            }
            _logger.Info("Change Matrix Returned False");
            return false;
        }
        private void processInput(string device, string action)
        {

        }
        private bool processAction(IWebSocketConnection _socket,string passedId, string addOrRemove,string inputOrDisplay,string includeSmall = "false")
        {
            _logger.Info("Attempting to Telenet into the Matrix");
            TelnetConnection tc = new TelnetConnection("172.16.0.33", 5000);
            _socket.Send("Starting Input Change");
            _logger.Info("Process action has been called. The passed values are passedId " + passedId + " addOrRemove " + addOrRemove + " inputorDisplay " + inputOrDisplay + " includeSmall " + includeSmall);
            if (inputOrDisplay == "display")
            {
                _logger.Info("The value of addOrRemove is " + addOrRemove + " and the value of passedDisplayID is " + passedId);
                if (addOrRemove.ToLower() == "add")
                {
                    if (passedId == "01")
                    {
                        Thread.Sleep(500);
                        tc.WriteLine("MT00SW" + this.currentInput + "01NT;MT00SW" + this.currentInput + "02NT");
                    }
                    else
                    {
                        _logger.Info("I am running the else statement that should have did a telnet");
                        Thread.Sleep(500);
                        tc.WriteLine("MT00SW" + this.currentInput + passedId + "NT;");
                        _logger.Info("I just sent the following to the matrix " + "MT00SW" + this.currentInput + passedId + "NT;");
                    }
                }
                if (addOrRemove.ToLower() == "remove")
                {
                    _logger.Info("I am recieved a request to remove the display and the display ID " + passedId);
                    if (passedId == "01")
                    {
                        _logger.Info("I am running the passedDisplay 1 if statement");
                        Thread.Sleep(500);
                        tc.WriteLine("MT00SW0801NT;MT00SW0802NT");
                    }
                    else
                    {
                        _logger.Info("I am running the passedDisplay 1  else statement");
                        _logger.Info("Passing the following to the matrix " + "MT00SW08" + passedId + "NT");
                        tc.WriteLine("MT00SW08" + passedId + "NT");
                    }
                }
                Thread.Sleep(250);
                _socket.Send("Finished Changing Display");

                tc.Disconnect();
                _logger.Info(" Process Action Returned True");
                return true;
            }
            else
            {
                _logger.Info("The value of addOrRemove is " + addOrRemove + " and the value of passedDisplayID is " + passedId);
                if (addOrRemove.ToLower() == "add")
                {

                    if (includeSmall == "false")
                    {
                        tc.WriteLine("MT00SW" + passedId + "01NT;MT00SW" + passedId + "02NT");
                    }
                    if (includeSmall == "true")
                    {
                        tc.WriteLine("MT00SW" + passedId + "01NT;MT00SW" + passedId + "02NT");
                        Thread.Sleep(500);
                        tc.WriteLine("MT00SW" + passedId + "03NT");
                    }
                }
                if (addOrRemove.ToLower() == "remove")
                {
                    _logger.Info("I am recieved a request to remove the input ID " + passedId);
                    if (includeSmall == "false")
                    {
                        tc.WriteLine("MT00SW0801NT;MT00SW0802NT");
                    }
                    if (includeSmall == "true")
                    {
                        tc.WriteLine("MT00SW0801NT;MT00SW0802NT");
                        Thread.Sleep(500);
                        tc.WriteLine("MT00SW" + passedId + "03NT");
                    }
                }
                Thread.Sleep(250);
                _socket.Send("Finished Changing Display");

                tc.Disconnect();
                _logger.Info(" Process Action Returned True");
                return true;
            }
        }


        public void createDeviceList()
        {
            foreach (var itemElement in xDoc.Element("Settings").Elements("DeviceList").Elements("Device"))
            {
                Device tmpDevice = new Device(itemElement.Element("name").Value, itemElement.Element("location").Value,itemElement.Element("ipAddress").Value, itemElement.Element("macAddress").Value, itemElement.Element("deviceType").Value, Convert.ToBoolean(itemElement.Element("enabled").Value),itemElement.Element("htmlId").Value);
                tvList.Add(tmpDevice);
            }
            
            foreach (Device dvc in tvList)
            {
                SetInterval(dvc.testConnection);
            }
        }
        public System.Timers.Timer SetInterval(Action Act)
        {
            System.Timers.Timer tmr = new System.Timers.Timer();
            tmr.Elapsed += (sender, args) => Act();
            tmr.AutoReset = true;
            tmr.Interval = 5000;
            tmr.Start();

            return tmr;
        }
    }

    class ScannerClient
    {
        public string IPAddress { get; set; }
        public string LocationId { get; set; }
        public string RoomId { get; set; }

        public ScannerClient(string ip, string location, string room)
        {
            this.IPAddress = ip;
            this.LocationId = location;
            this.RoomId = room;
        }


    }
    public static class WOL
    {

        public static async Task WakeOnLan(string macAddress)
        {
            byte[] magicPacket = BuildMagicPacket(macAddress);
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces().Where((n) =>
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.OperationalStatus == OperationalStatus.Up))
            {
                IPInterfaceProperties iPInterfaceProperties = networkInterface.GetIPProperties();
                foreach (MulticastIPAddressInformation multicastIPAddressInformation in iPInterfaceProperties.MulticastAddresses)
                {
                    IPAddress multicastIpAddress = multicastIPAddressInformation.Address;
                    if (multicastIpAddress.ToString().StartsWith("ff02::1%", StringComparison.OrdinalIgnoreCase)) // Ipv6: All hosts on LAN (with zone index)
                    {
                        UnicastIPAddressInformation unicastIPAddressInformation = iPInterfaceProperties.UnicastAddresses.Where((u) =>
                            u.Address.AddressFamily == AddressFamily.InterNetworkV6 && !u.Address.IsIPv6LinkLocal).FirstOrDefault();
                        if (unicastIPAddressInformation != null)
                        {
                            await SendWakeOnLan(unicastIPAddressInformation.Address, multicastIpAddress, magicPacket);
                            break;
                        }
                    }
                    else if (multicastIpAddress.ToString().Equals("224.0.0.1")) // Ipv4: All hosts on LAN
                    {
                        UnicastIPAddressInformation unicastIPAddressInformation = iPInterfaceProperties.UnicastAddresses.Where((u) =>
                            u.Address.AddressFamily == AddressFamily.InterNetwork && !iPInterfaceProperties.GetIPv4Properties().IsAutomaticPrivateAddressingActive).FirstOrDefault();
                        if (unicastIPAddressInformation != null)
                        {
                            await SendWakeOnLan(unicastIPAddressInformation.Address, multicastIpAddress, magicPacket);
                            break;
                        }
                    }
                }
            }
        }

        static byte[] BuildMagicPacket(string macAddress) // MacAddress in any standard HEX format
        {
            macAddress = Regex.Replace(macAddress, "[: -]", "");
            byte[] macBytes = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                macBytes[i] = Convert.ToByte(macAddress.Substring(i * 2, 2), 16);
            }

            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    for (int i = 0; i < 6; i++)  //First 6 times 0xff
                    {
                        bw.Write((byte)0xff);
                    }
                    for (int i = 0; i < 16; i++) // then 16 times MacAddress
                    {
                        bw.Write(macBytes);
                    }
                }
                return ms.ToArray(); // 102 bytes magic packet
            }
        }

        static async Task SendWakeOnLan(IPAddress localIpAddress, IPAddress multicastIpAddress, byte[] magicPacket)
        {
            using (UdpClient client = new UdpClient(new IPEndPoint(localIpAddress, 0)))
            {
                await client.SendAsync(magicPacket, magicPacket.Length, multicastIpAddress.ToString(), 9);
            }
        }
    }
    public class Communcations
    {
        public string Message { get; set; }
        public string Computer { get; set; }
        public string Action { get; set; }
        public string ElementId { get; set; }

    }
}
