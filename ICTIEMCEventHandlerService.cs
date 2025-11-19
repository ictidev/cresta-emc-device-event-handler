// ***********************************************************************
// Assembly         : ICTIEMCEventHandlerService
// Author           : InfinityCTI
// Created          : 07-15-2025
//
// Last Modified By : User
// Last Modified On : 07-25-2025
// ***********************************************************************
// <copyright file="ICTIEMCEventHandlerService.cs" company="InfinityCTI                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 ">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using AgileSoftware.Developer;
using AgileSoftware.Developer.CSTA;
using AgileSoftware.Developer.CSTA.PrivateData;
using icti_emc_event_handler.Properties;
using Newtonsoft.Json;
using RestSharp;
using Serilog;
using SimpleServices;

namespace icti_emc_event_handler
{
    public class ICTIEMCEventHandlerService : IWindowsService
    {
        public ApplicationContext AppContext { get; set; }
        private Timer XmlClientReconnectTimer { get; set; }
        private ConcurrentDictionary<string, CrestaPayload> MyCallList { get; set; }
        /// <summary>
        /// Gets or sets my device list.
        /// </summary>
        /// <value>My device list.</value>
        private ConcurrentDictionary<string, Device> MyDeviceList { get; set; }
        private ConcurrentDictionary<string, string> MyOutboundUCIDList { get; set; }
        /// <summary>
        /// Gets or sets my agent list.
        /// </summary>
        /// <value>My agent list.</value>
        private ConcurrentDictionary<string, Agent> MyAgentList { get; set; }
        private ConcurrentDictionary<string, Queue> MyQueueList { get; set; }
        private ConcurrentDictionary<string, string> MyQQueuedCallList { get; set; }
        /// <summary>
        /// Gets or sets the XML client to utilize telephony through XML server.
        /// </summary>
        /// <value>
        /// The XML client.
        /// </value>
        private ASXMLClient XmlClient { get; set; }

        public void Start(string[] args)
        {
            string prodInfoVer = General.GetProductName + " " + General.GetProductVersion;
            const string methodName = "Start";

            try
            {
                //Serilog.Debugging.SelfLog.Enable(Console.WriteLine);
                MyCallList = new ConcurrentDictionary<string, CrestaPayload>();
                MyQueueList = new ConcurrentDictionary<string, Queue>();
                MyOutboundUCIDList = new ConcurrentDictionary<string, string>();
                MyQQueuedCallList = new ConcurrentDictionary<string, string>();
                Log.Information("Starting {prodInfoVer} service. Connection properties - TLink: {LinkName}; XML Server IP: {XMLServerIP}; XML Server Port: {XMLServerPort}",
                    prodInfoVer, Settings.Default.LinkName, Settings.Default.XMLServerIP,
                                            Settings.Default.XMLServerPort);
                
                // Connect to XML server
                this.PrepareXMLClient();
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private void XmlClientReconnectTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                XmlClientReconnectTimer.Enabled = false;
                XmlClientReconnectTimer.Stop();
                var connRetVal = this.XmlClient.Connect();
                Log.Information("Connection to XML Server return value: {connRetVal}", connRetVal);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "XMLReconnect_Elap");
            }
        }

        public void Stop()
        {
            const string methodName = "Stop";

            try
            {
                Log.Information("Stopping {GetProductName}", General.GetProductName);

                if (this.XmlClient != null)
                {
                    XmlClientReconnectTimer.Stop();
                    XmlClientReconnectTimer.Dispose();
                    Log.Information("Number of monitored devices: {Count}", this.XmlClient.CSTAMonitorList.Count);
                    if (this.XmlClient.CSTAMonitorList.Count > 0)
                    {
                        var deviceEnumerator = this.XmlClient.CSTAMonitorList.GetEnumerator();
                        while (deviceEnumerator.MoveNext())
                        {
                            var device = (CSTAMonitor)deviceEnumerator.Current;
                            if (device != null)
                            {
                                Log.Information("Request to unmonitor device {ID} with monitor reference {MonitorCrossRefID} and device type {Type}", device.DeviceObject.ID,
                                                            device.MonitorCrossRefID, device.DeviceObject.Type);
                                var retVal = this.XmlClient.CSTAMonitorStop(device.MonitorCrossRefID);
                                Log.Information("Unmonitor device {ID} with result value: {retVal}", device.DeviceObject.ID, retVal);
                                Task.Delay(200);
                            }
                        }
                    }

                    // Disconnect xml client
                    this.XmlClient.Disconnect();
                    this.XmlClient.Dispose();
                    Log.Information("XML client has been disconnected and disposed");
                }

                Log.Information("{GetProductName} Stopped", General.GetProductName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        /// <summary>
        /// Prepares the XML client configuration and connectivity.
        /// </summary>
        private void PrepareXMLClient()
        {
            const string methodName = "PrepareXMLClient";

            try
            {
                Log.Information("Preparing xml client reconnect timer");
                // Create the reconnect timer for the xml server in case it is unable to establish connection at start
                XmlClientReconnectTimer = new Timer();
                // Subscribe to elapsed event
                XmlClientReconnectTimer.Elapsed += XmlClientReconnectTimer_Elapsed;
                XmlClientReconnectTimer.Interval = 30000;
                XmlClientReconnectTimer.AutoReset = true;
                XmlClientReconnectTimer.Enabled = false;

                Log.Information("Preparing XML client object configuration");
                this.XmlClient = new ASXMLClient();
                this.XmlClient.AutoRecoverDelayInterval = 15;
                this.XmlClient.AutoRecovery = true;
                this.XmlClient.RaiseEventInSameThread = false;
                this.XmlClient.LogTraceToFile = Settings.Default.EnableXMLClientTrace;
                this.XmlClient.TraceFilePathName = Settings.Default.XMLClientTraceFilePath;

                // Setup the XML server primary/secondary IP and ports
                this.XmlClient.TServerLinkName = Settings.Default.LinkName;
                this.XmlClient.ServerIP = Settings.Default.XMLServerIP;
                this.XmlClient.ServerPort = Settings.Default.XMLServerPort;
                this.XmlClient.TServerLinkNameSecondary = Settings.Default.LinkName; //Settings.Default.SecondaryLinkName;
                this.XmlClient.ServerIPSecondary = Settings.Default.XMLServerIP; // Settings.Default.SecondaryXMLServerIP;
                this.XmlClient.ServerPortSecondary = Settings.Default.XMLServerPort; //Settings.Default.SecondaryXMLServerPort;
                Log.Information("XML Client telephony properties has been set. AgentEventIncluded: {AgentEventIncluded}", this.XmlClient.AgentEventIncluded);

                // Create XML client event handlers
                this.XmlClient.CSTAConnectionCleared += XmlClient_CSTAConnectionCleared;
                this.XmlClient.CSTAAgentLoggedOff += XmlClient_CSTAAgentLoggedOff;
                this.XmlClient.CSTAAgentLoggedOn += XmlClient_CSTAAgentLoggedOn;
                this.XmlClient.CSTATransfered += XmlClient_CSTATransfered;
                this.XmlClient.CSTAConferenced += XmlClient_CSTAConferenced;
                this.XmlClient.CSTADelivered += XmlClient_CSTADelivered;
                this.XmlClient.CSTADiverted += XmlClient_CSTADiverted;
                this.XmlClient.CSTAErrorReturned += XmlClient_CSTAErrorReturned;
                this.XmlClient.CSTAEstablished += XmlClient_CSTAEstablished;
                this.XmlClient.CSTAFailed += XmlClient_CSTAFailed;
                this.XmlClient.CSTAHeld += XmlClient_CSTAHeld;
                this.XmlClient.CSTARetrieved += XmlClient_CSTARetrieved;
                this.XmlClient.CSTAMonitorEnded += XmlClient_CSTAMonitorEnded;
                this.XmlClient.CSTAMonitorStartResponse += XmlClient_CSTAMonitorStartResponse;
                this.XmlClient.CSTAMonitorStopResponse += XmlClient_CSTAMonitorStopResponse;
                this.XmlClient.CSTAOriginated += XmlClient_CSTAOriginated;
                this.XmlClient.CSTAServiceInitiated += XmlClient_CSTAServiceInitiated;
                this.XmlClient.CSTAQueued += XmlClient_CSTAQueued;
                this.XmlClient.InternalError += XmlClient_InternalError;
                this.XmlClient.ServerClosed += XmlClient_ServerClosed;
                this.XmlClient.SocketConnected += XmlClient_SocketConnected;
                this.XmlClient.StreamConnected += XmlClient_StreamConnected;
                this.XmlClient.CSTAQueryDeviceNameResponse += XmlClient_CSTAQueryDeviceNameResponse;    

                Log.Information("XML Client event handlers has been created. Attempting to connect to the XML Server");

                var connRetVal = this.XmlClient.Connect();
                Log.Information("Connection to XML Server return value: {connRetVal}", connRetVal);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        /// <summary>Monitors hunt group list or single agent device.</summary>
        /// <param name="deviceToMonitor">If specified, the device will be monitor. Otherwise, the list of hunt groups will be monitored.</param>
        /// <returns>System.Threading.Tasks.Task.</returns>
        private async Task MonitorDevice(string deviceToMonitor = "")
        {
            const string methodName = "MonitorDevice";

            try
            {
                await Task.Run(() =>
                {
                    Log.Information("MonitorDevice method has been called with device to monitor: {deviceToMonitor}", deviceToMonitor);
                    if (!string.IsNullOrEmpty(deviceToMonitor))
                    {
                        var deviceMonitorFilter = new CSTAMonitorFilter(false, true, true, true, true, true, false, true, false, true, false, true, true,
                                                     true, false, false, false, false, false, false, false);
                        var monitorInvokeId = this.XmlClient.CSTAMonitorStart(new CSTADeviceID(deviceToMonitor, enDeviceIDType.deviceNumber),
                                                                                    enMonitorType.device, deviceMonitorFilter);
                        Log.Information("Request to monitor agent device {deviceToMonitor} with invoke id {monitorInvokeId}", deviceToMonitor, monitorInvokeId);
                    }
                    else
                    {
                        var huntGroupMonitorFilter = new CSTAMonitorFilter(false, false, false, false, false, false, false, false, false, false, false, false, false,
                                                    false, false, false, true, true, false, false, false);
                        this.MyAgentList = new ConcurrentDictionary<string, Agent>();
                        
                        // Parse the splitskill list
                        if (!string.IsNullOrEmpty(Settings.Default.SplitSkillList))
                            this.ParseDeviceList(Settings.Default.SplitSkillList, huntGroupMonitorFilter, enDeviceIDType.deviceNumber);

                        // Now start the monitoring
                        if (this.MyDeviceList.Count > 0)
                        {
                            Parallel.ForEach(this.MyDeviceList, async (deviceItem) =>
                            {
                                var monitorInvokeId = this.XmlClient.CSTAMonitorStart(new CSTADeviceID(deviceItem.Value.DeviceId, deviceItem.Value.Type),
                                                                                        enMonitorType.device, deviceItem.Value.monitorFilter);
                                Log.Information("Request to monitor hunt group {DeviceId} with invoke id {monitorInvokeId}", deviceItem.Value.DeviceId, monitorInvokeId);
                                await Task.Delay(100);
                            });
                            Log.Information("Completed device monitor {GetProductName}", General.GetProductName);
                        }
                        else
                        {
                            Log.Information("There are no devices to monitor");
                        }
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        /// <summary>
        /// Creates the cresta payload.
        /// </summary>
        /// <param name="callKey">The call key.</param>
        /// <param name="device">The device.</param>
        /// <param name="ucid">The ucid.</param>
        /// <param name="externalNumber">The external number.</param>
        /// <param name="queueId">The queue identifier.</param>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="callDirection">The call direction.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        private bool CreateCrestaPayload(string callKey, string device, string ucid, 
            string externalNumber, string queueId, string queueName, string callDirection)
        {
            bool retVal = false;
            const string methodName = "CreateCrestaPayload";

            try
            {
                Log.Information("Create Cresta payload. Active call records: {Count}. callKey: {callKey}; device: {device}" +
                    "; ucid: {ucid}; externalNumber: {externalNumber}; queueId: {queueId}; " +
                    "queueName: {queueName}; callDirection: {callDirection}", 
                    this.MyCallList.Count, callKey, device, ucid, externalNumber, queueId, queueName, callDirection);
                // Check if the queue exist in our list so we have a matching queue name 
                if (!string.IsNullOrEmpty(queueId))
                {
                    // Check if already in the queue list
                    //if (MyQueueList.ContainsKey(queueId))
                    if (string.IsNullOrEmpty(queueName))
                    {                        
                        // Add the queue to the list
                        Queue queueToadd = new Queue
                        {
                            ID = queueId,
                            Name = queueName
                        };
                        var isQAdded = MyQueueList.TryAdd(queueId, queueToadd);
                        // Since we don't have the queue in the list, lets query EMC to get the queue name
                        var queryReturn = XmlClient.CSTAQueryDeviceName(queueId);
                        Log.Information("Query device name for queue id {queueId}: {isQAdded}. returned: {queryReturn}",
                            isQAdded, queueId, queryReturn);
                    }
                    else
                    {
                        Log.Information("The queue exist in the List. Queue name is {queueName}", queueName);
                    }
                }

                // Check if there is agent that is logged based on the AnsweringDevice
                var agentItem = MyAgentList.FirstOrDefault(x => x.Value.DeviceId.Equals(device));
                var isAgentItemNull = agentItem.Value == null;
                // Check if agentItem is null. If yes then we cannot proceed since we don't have an agent id to send to Cresta
                if (!this.MyCallList.ContainsKey(callKey))
                {
                    var encodedUcid = General.EncodeUCID(ucid);
                    //Create the Cresta payload
                    var newCrestaPayload = new CrestaPayload();
                    newCrestaPayload.session_event = new SessionEvent
                    {
                        event_type = CrestaCallEventType.Started,
                        platform_call_id = encodedUcid
                    };
                    Log.Information("Created new session event. Is agent object null?: {isAgentItemNull}", isAgentItemNull);

                    // Add participants: Agent
                    var participant = new Participant()
                    {
                        role = CrestaRole.Agent,
                        platform_agent_id = agentItem.Value.AgentId
                    };
                    Log.Information("Created new participant");

                    newCrestaPayload.session_event.participants = new List<Participant>();
                    newCrestaPayload.session_event.participants.Add(participant);

                    Log.Information("Adding new participant to the list");
                    // Add visitor participant
                    participant = new Participant()
                    {
                        role = CrestaRole.Vistor,
                        external_user_id = externalNumber
                    };
                    newCrestaPayload.session_event.participants.Add(participant);
                    Log.Information("Added new participant to the list");
                    // Add payload
                    newCrestaPayload.session_event.payload = new Payload()
                    {
                        customcallani = device,
                        customcalldirection = callDirection,
                        customcallucid = encodedUcid,
                        customcalldecodeducid = ucid,
                    };

                    newCrestaPayload.session_event.payload.customcallqueueid = queueId;
                    newCrestaPayload.session_event.payload.customcallqueuename = queueName;

                    var itemAdded = this.MyCallList.TryAdd(callKey, newCrestaPayload);
                    Log.Information("Call item with call id {callKey} and UCID {ucid} has been added: {itemAdded}. Active call records: {Count}",
                                                callKey, ucid, itemAdded, this.MyCallList.Count);
                    retVal = true;
                }
                else
                {
                    Log.Warning("Call item with call id {callKey} with UCID {platform_call_id} already exist in the list. This call UCID {ucid}",
                                                callKey, this.MyCallList[callKey].session_event.platform_call_id, ucid);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }

            return retVal;
        }

        /// <summary>Processes the transfer call.</summary>
        /// <param name="callKey">The call key.</param>
        /// <param name="transferedMetadata">The transfered metadata.</param>
        private void ProcessTransferCall(string callKey, string transferedMetadata)
        {
            const string methodName = "ProcessTransferCall";
            CrestaPayload oldCallInfo = null;

            try
            {
                Log.Information("{methodName} has been called for {callKey}", methodName, callKey);

                if (MyCallList.ContainsKey(callKey))
                {
                    MyCallList[callKey].session_event.event_type = CrestaCallEventType.Ended;
                    SendEventToCresta(MyCallList[callKey], callKey, transferedMetadata);
                    // Before removing the cresta payload from the call list, we need to check the list again
                    // to make sure it still exist as another thread might have removed it already
                    if (MyCallList.ContainsKey(callKey))
                    {
                        // Remove the primary old call information from the call list                        
                        var isOldRemoved = MyCallList.TryRemove(callKey, out oldCallInfo);
                        Log.Information("Call key {callKey} and ucid {customcalldecodeducid}({platform_call_id}) has been removed from the call list: {isOldRemoved}",
                                                    callKey, oldCallInfo.session_event.payload.customcalldecodeducid,
                                                    oldCallInfo.session_event.platform_call_id, isOldRemoved);
                    }
                    else
                    {
                        Log.Warning("The call key {callKey} does not exist in the call list anymore. It might have been removed by another thread", callKey);
                    }
                }
                else
                {
                    Log.Warning("{methodName} - The call id {callKey} does not exist in the call list", methodName, callKey);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private async void SendEventToCresta(CrestaPayload crestaPayloadToSend, string callKey, string eventMetadata = "")
        {
            const string methodName = "SendEventToCresta";

            try
            {
                Log.Information("{methodName} has been called for {callKey}", methodName, callKey);
                // First we need to see if the queue name for inbound call is set 
                var queueId = crestaPayloadToSend.session_event.payload.customcallqueueid;
                var callDirection = crestaPayloadToSend.session_event.payload.customcalldirection;
                if (MyQueueList.ContainsKey(queueId) && callDirection.Equals(CrestaCallDirection.Inbound))
                {
                    crestaPayloadToSend.session_event.payload.customcallqueuename = MyQueueList[queueId].Name;
                    Log.Information("The queue name has been set to {Name}", MyQueueList[queueId].Name);
                }

                crestaPayloadToSend.session_event.payload.customcallmetadata = eventMetadata;
                
                // Now serialize the cresta payload
                string jsonPayload = JsonConvert.SerializeObject(crestaPayloadToSend);
                Log.Information("Cresta event payload to send {callKey}: {jsonPayload}", callKey, jsonPayload);
                var client = new RestClient(Settings.Default.CrestaAPIEndpoint);
                Log.Information("Client object has been set {callKey}", callKey);
                var request = new RestRequest();
                request.Method = Method.Post;
                request.AddHeader("Content-Type", "application/json");
                request.AddHeader("Authorization", $"ApiKey {Settings.Default.CrestaAPIKey}");
                request.AddJsonBody(jsonPayload);
                
                var response = await client.ExecutePostAsync(request);
                var isResponseNull = response == null;
                Log.Information("Cresta REST API execution completed {callKey}. Is response null: {isResponseNull}", callKey, isResponseNull);

                var content = response.Content ?? string.Empty;
                if (response.ErrorException != null)
                {
                    Log.Error(response.ErrorException, "Error occurred during API call {callKey}: {ErrorMessage}", callKey, response.ErrorMessage);
                }
                //- If StatusCode == 0 and ResponseStatus == ResponseStatus.Error,
                //it likely means a transport-level issue (e.g., DNS, SSL, timeout).
                Log.Information("Completed Cresta REST API execution. {callKey} Response StatusCode: {StatusCode}, ResponseStatus: {ResponseStatus}, Content: {content}",
                    callKey, response.StatusCode, response.ResponseStatus, content);
                //Log.Information("Completed Cresta REST API execution: {ToJson}", response.ToJson());
                //Log.Information("Completed Cresta REST API execution. Cresta API response status code: {StatusCode}; Content: {Content}", response.StatusCode, response.Content);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }


        /// <summary>
        /// Parses the device list.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="deviceMonitorFilter">The device monitor filter.</param>
        /// <param name="deviceType">Type of the device.</param>
        private void ParseDeviceList(string input, CSTAMonitorFilter deviceMonitorFilter, enDeviceIDType deviceType)
        {
            Device deviceToAdd = null;
            const string methodName = "ParseDeviceList";

            try
            {
                Log.Information("Parsing device extension list. Device extension list: {input}", input);
                if (MyDeviceList == null)
                {
                    MyDeviceList = new ConcurrentDictionary<string, Device>();
                }
                    
                string[] lines = input.Split(new char[] { ';', ',' });

                foreach (string line in lines)
                {
                    try
                    {
                        int temp = Int32.Parse(line);
                        deviceToAdd = new Device();
                        deviceToAdd.DeviceId = temp.ToString();
                        deviceToAdd.Type = deviceType;
                        deviceToAdd.monitorFilter = deviceMonitorFilter;
                        if (!MyDeviceList.ContainsKey(temp.ToString()))
                            MyDeviceList.TryAdd(deviceToAdd.DeviceId, deviceToAdd);
                    }
                    catch
                    {
                        string[] temp = line.Split(new char[] { '-' });
                        int a = Int32.Parse(temp[0]);
                        int b = Int32.Parse(temp[1]);
                        for (int i = a; i <= b; i++)
                        {
                            deviceToAdd = new Device();
                            deviceToAdd.DeviceId = temp.ToString();
                            deviceToAdd.Type = deviceType;
                            deviceToAdd.monitorFilter = deviceMonitorFilter;
                            if (MyDeviceList.ContainsKey(i.ToString()))
                                MyDeviceList.TryAdd(deviceToAdd.DeviceId, deviceToAdd);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }

            //return result;
            Log.Information("Device list count: {Count}", MyDeviceList.Count);
        }

        /// <summary>Determines whether [is device monitored] [the specified device identifier to check].</summary>
        /// <param name="deviceIdToCheck">The device identifier to check.</param>
        /// <returns>
        ///   <c>true</c> if [is device monitored] [the specified device identifier to check]; otherwise, <c>false</c>.</returns>
        private bool IsDeviceMonitored(string deviceIdToCheck)
        {
            bool retVal = false;
            const string methodName = "IsDeviceMonitored";

            try
            {
                var cstaMonitorList = this.XmlClient.CSTAMonitorList.Cast<CSTAMonitor>();
                var deviceObj = cstaMonitorList.FirstOrDefault(x => x.DeviceObject.ID == deviceIdToCheck);
                retVal = deviceObj != null;
                Log.Information("{IsDeviceMonitored}. Is device {deviceIdToCheck} monitored: {retVal}", methodName, deviceIdToCheck, retVal);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }

            return retVal;
        }

        /// <summary>Gets the device monitor reference identifier.</summary>
        /// <param name="deviceIdToCheck">The device identifier to check.</param>
        /// <returns>System.String.</returns>
        private string GetDeviceMonitorRefID(string deviceIdToCheck)
        {
            string retVal = string.Empty;
            const string methodName = "GetDeviceMonitorRefID";

            try
            {
                var cstaMonitorList = this.XmlClient.CSTAMonitorList.Cast<CSTAMonitor>();
                var deviceObj = cstaMonitorList.FirstOrDefault(x => x.DeviceObject.ID == deviceIdToCheck);
                if (deviceObj != null)
                {
                    retVal = deviceObj.MonitorCrossRefID;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
            return retVal;
        }

        /// <summary>Removes the queued from list.</summary>
        /// <param name="callIdToCheck">The call identifier to check.</param>
        private void RemoveQueuedFromList(string callIdToCheck)
        {
            const string methodName = "RemoveQueuedFromList";
            try
            {
                Log.Information("RemoveQueuedCallList called to check for call id {callIdToCheck}", callIdToCheck);
                if (MyQQueuedCallList.ContainsKey(callIdToCheck))
                {
                    var itemValueRemoved = string.Empty;
                    var isRemoved = this.MyQQueuedCallList.TryRemove(callIdToCheck, out itemValueRemoved);
                    Log.Information("The queued queue with call id {callIdToCheck} has been removed from the queued call list: {isRemoved}",
                                                callIdToCheck, isRemoved);
                }
                else
                {
                    Log.Information("The queued call id {callIdToCheck} does not exist in the queued call list", callIdToCheck);
                }
                
                Log.Information("Queued queue list count: {Count}", MyQQueuedCallList.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        /// <summary>Removes the outbound ucid from list.</summary>
        /// <param name="callKeyToCheck">The call key to check.</param>
        private void RemoveOutboundUCIDFromList(string callKeyToCheck)
        {
            const string methodName = "RemoveOutboundUCIDFromList";
            try
            {
                Log.Information("RemoveUCIDList called to check for call key {callKeyToCheck}", callKeyToCheck);
                if (MyOutboundUCIDList.ContainsKey(callKeyToCheck))
                {
                    var itemValueRemoved = string.Empty;
                    var isRemoved = this.MyOutboundUCIDList.TryRemove(callKeyToCheck, out itemValueRemoved);
                    Log.Information("The outbound UCID {callKeyToCheck} has been removed from the UCID list: {isRemoved}",
                                                callKeyToCheck, isRemoved);
                }
                else
                {
                    Log.Information("The outbound UCID {callKeyToCheck} does not exist in the UCID list", callKeyToCheck);
                }
                
                Log.Information("Outbound UCID list count: {Count}", MyOutboundUCIDList.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private async void XmlClient_CSTAConferenced(object sender, CSTAConferencedEventArgs arg)
        {
            const string methodName = "CSTAConferenced";

            await Task.Run(() =>
            {

                try
                {
                    Log.Information("CSTAConferenced. Arg: {ToJson}", arg.ToJson());                    
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        private async void XmlClient_CSTATransfered(object sender, CSTATransferedEventArgs arg)
        {
            const string methodName = "CSTATransfered";

            await Task.Run(() =>
            {
                try
                {
                    var transferedMetadata = arg.ToJson();
                    Log.Information("{methodName}. Arg: {transferedMetadata}", methodName, transferedMetadata);
                    
                    // Default call list key
                    var callKey = $"{arg.PrimaryOldCall.CallID}-{arg.PrimaryOldCall.DeviceID.ID}";
                    // Check the old UCID '00000000000000000000'
                    if (!arg.OldGlobalCallLinkageID.Equals("00000000000000000000"))
                    {
                        // This is a tranfer where the secondary call id is the actual intial call id
                        // So this is where we need to query the call list with proper key of the 
                        // primary (intial) call id
                        Log.Information("The old global call linkage id is all zeros. This is a transfer where the secondary call id is the actual intial call id");
                        // remove the UCID from the UCID list
                        RemoveOutboundUCIDFromList(callKey);
                        // set the call key to be the secondary call id as in this case, the secondary
                        // call id is the initial call id
                        callKey = $"{arg.SecondaryOldCall.CallID}-{arg.SecondaryOldCall.DeviceID.ID}";
                        // This is to send call end event to cresta for agent 1 who is transferring the call
                        if (MyCallList.ContainsKey(callKey))
                        {
                            ProcessTransferCall(callKey, transferedMetadata);
                            // We need to get call payload from the call list using primary old call id and transferred to device
                            // Note using the call key contains the second leg of the call cresta payload.
                            // Now that the call has been trasferred to agent 2, we must add the cresta payload of
                            // the second leg of the call to the call list
                            callKey = $"{arg.PrimaryOldCall.CallID}-{arg.TransferredToDevice.ID}";
                            if (MyCallList.ContainsKey(callKey))
                            {
                                // Add the cresta payload of the second leg (agent 1 calls agent 2) to the call list
                                // this is make sure the cresta payload is in the call list such that
                                // when the call that is transferred to agent 2 will have the cresta payload
                                var crestaPayloadToAdd = this.MyCallList[callKey];
                                // change call key to reflect original call id with the transferred device
                                callKey = $"{arg.SecondaryOldCall.CallID}-{arg.TransferredToDevice.ID}";
                                MyCallList.TryAdd(callKey, crestaPayloadToAdd);
                                Log.Information("Cresta payload with call key {callKey} has been added to the list", callKey);
                                // Now remove the call list of the outbound second leg of the call. This agent 1 initiating call (transfer consult)
                                callKey = $"{arg.PrimaryOldCall.CallID}-{arg.PrimaryOldCall.DeviceID.ID}";
                                var isOldRemoved = MyCallList.TryRemove(callKey, out var oldCallInfo);
                                Log.Information("Primary old call with call id {callKey} and ucid {customcalldecodeducid}({platform_call_id}) has been removed from the call list: {isOldRemoved}",
                                                            callKey, oldCallInfo.session_event.payload.customcalldecodeducid,
                                                            oldCallInfo.session_event.platform_call_id, isOldRemoved);
                            }
                            else
                            {
                                Log.Warning("The cresta payload with call key {callKey} does not exist in the call list", callKey);
                            }
                        }
                    }
                    else
                    {
                        // In this case, the primary old call id and device are the information that relates to the initial call
                        ProcessTransferCall(callKey, transferedMetadata);
                        // Need to delete the second leg of the call from the call list
                        // since in the establish event of the agent 2, the cresta payload is created which is the payload
                        // we need to remove when the transferred call to agent 2 is disconnected
                        callKey = $"{arg.SecondaryOldCall.CallID}-{arg.SecondaryOldCall.DeviceID.ID}";
                        if (MyCallList.ContainsKey(callKey))
                        {
                            var isOldRemoved = MyCallList.TryRemove(callKey, out var oldCallInfo);
                            Log.Information("Secondary old call with call id {callKey} and ucid {customcalldecodeducid}({platform_call_id}) has been removed from the call list: {isOldRemoved}",
                                                        callKey, oldCallInfo.session_event.payload.customcalldecodeducid,
                                                        oldCallInfo.session_event.platform_call_id, isOldRemoved);
                        }
                        else
                        {
                            Log.Warning("The secondary call id {callKey} does not exist in the call list", callKey);
                        }
                    }

                    // remove q queued call from the list using primary old call id
                    this.RemoveQueuedFromList(arg.PrimaryOldCall.CallID);
                    // remove q queued call from the list using secondary old call id
                    this.RemoveQueuedFromList(arg.SecondaryOldCall.CallID);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }
                
        private async void XmlClient_CSTAAgentLoggedOn(object sender, CSTAAgentLoggedOnEventArgs arg)
        {
            const string methodName = "CSTAAgentLoggedOn";

            await Task.Run(async () =>
            {
                try
                {
                    Log.Information("CSTAAgentLoggedOn: {ToJson}", arg.ToJson());
                    // Need to make sure the ACD group is set. Otherwise, ignore the event
                    if (string.IsNullOrEmpty(arg.ACDGroup.ID))
                    {
                        Log.Warning("Agent {AgentID} logged on without ACD group. Agent device: {ID}. Discarding additional process for this event", 
                            arg.AgentID, arg.AgentDevice.ID);
                    }
                    else
                    {
                        // check if the device the agent logged on is already in the list
                        if (MyAgentList.ContainsKey(arg.AgentDevice.ID))
                        {
                            // At this point, the device is already in the list. We need to check if the agent
                            // associated to this device is the same is just now logged in
                            if (MyAgentList[arg.AgentDevice.ID].AgentId != arg.AgentID)
                            {
                                Log.Warning("The device {ID} is already associated to agent id {AgentId} but now agent id {arg.AgentID} is logging in. Updating the agent id",
                                    arg.AgentDevice.ID, MyAgentList[arg.AgentDevice.ID].AgentId, arg.AgentID);
                                MyAgentList[arg.AgentDevice.ID].AgentId = arg.AgentID;
                            }
                            else
                            {
                                Log.Information("The device {ID} is already associated to agent id {AgentId}. No update is needed",
                                    arg.AgentDevice.ID, MyAgentList[arg.AgentDevice.ID].AgentId);
                            }
                        }
                        else
                        {
                            Log.Information("The device {ID} is not in the agent list. Proceed to add the agent id {AgentId} associated to this device",
                                arg.AgentDevice.ID, arg.AgentID);
                            // Add the agent to the list
                            var agentToAdd = new Agent()
                            {
                                AgentId = arg.AgentID,
                                DeviceId = arg.AgentDevice.ID
                            };
                            this.MyAgentList.TryAdd(arg.AgentDevice.ID, agentToAdd);
                            Log.Information("Agent {AgentID} is using device {ID} has been added to the list. Agent list count: {Count}",
                                                        arg.AgentID, arg.AgentDevice.ID, this.MyAgentList.Count);
                        }

                        // Check if agent extension is monitored
                        if (!IsDeviceMonitored(arg.AgentDevice.ID))
                        {
                            Log.Information("Agent {AgentID} is using device {ID} which is currently not monitored. Requesting to monitor the device", arg.AgentID, arg.AgentDevice.ID);
                            // Start to monitor the device list configured
                            await MonitorDevice(arg.AgentDevice.ID);
                        }
                        else
                        {
                            Log.Information("Agent {AgentID} is using device {ID} which is already monitored. No additional action is needed", arg.AgentID, arg.AgentDevice.ID);
                        }                        
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        private async void XmlClient_CSTAAgentLoggedOff(object sender, CSTAAgentLoggedOffEventArgs arg)
        {
            const string methodName = "CSTAAgentLoggedOff";

            await Task.Run(() =>
            {
                try
                {
                    Log.Information("{methodName}: {ToJson}", methodName, arg.ToJson());
                    // Need to make sure the ACD group is set. Otherwise, ignore the event
                    if (string.IsNullOrEmpty(arg.ACDGroup.ID))
                    {
                        Log.Warning("Agent {AgentID} logged off without ACD group. Agent device: {ID}. Discarding additional process for this event",
                            arg.AgentID, arg.AgentDevice.ID);
                    }
                    else
                    {
                        if (MyAgentList.ContainsKey(arg.AgentDevice.ID))
                        {
                            // Check if the agent id associated to the device matches the logged off agent id
                            if (MyAgentList[arg.AgentDevice.ID].AgentId.Equals(arg.AgentID))
                            {
                                // Just set the device and agent id association to empty string
                                MyAgentList[arg.AgentDevice.ID].AgentId = string.Empty;
                                Log.Information("The device {ID} association to agent id {AgentId} has been cleared",
                                    arg.AgentDevice.ID, arg.AgentID);
                            }
                            else
                            {
                                Log.Warning("The device {ID} is associated to agent id {AgentId} but now agent id {arg.AgentID} is logging off. No update is needed",
                                    arg.AgentDevice.ID, MyAgentList[arg.AgentDevice.ID].AgentId, arg.AgentID);
                            }
                        }                        
                    }                    
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }
                
        private void XmlClient_CSTAMonitorEnded(object sender, CSTAMonitorEndedEventArgs arg)
        {
            const string methodName = "CSTAMonitorEnded";

            try
            {
                Log.Warning("Monitor device ended. Cause: {Cause}; MonitorCrossRefID: {MonitorCrossRefID}. Monitored device list count: {Count}",
                                             arg.Cause, arg.MonitorCrossRefID, XmlClient.CSTAMonitorList.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        /// <summary>Handles the CSTAFailed event of the XmlClient control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="arg">The <see cref="CSTAFailedEventArgs" /> instance containing the event data.</param>
        private void XmlClient_CSTAFailed(object sender, CSTAFailedEventArgs arg)
        {
            const string methodName = "CSTAFailed";

            try
            {
                Log.Error("{methodName}: {ToJson}", methodName, arg.ToJson());
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        /// <summary>Handles the CSTAEstablished event of the XmlClient control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="arg">The <see cref="CSTAEstablishedEventArgs" /> instance containing the event data.</param>
        private async void XmlClient_CSTAEstablished(object sender, CSTAEstablishedEventArgs arg)
        {
            const string methodName = "CSTAEstablished";

            await Task.Run(() =>
            {
                try
                {
                    var establishedMetadata = arg.ToJson();
                    Log.Information("{methodName}. Active call records: {Count}; Arg: {establishedMetadata}",
                                               methodName, this.MyCallList.Count, establishedMetadata);
                    
                    // Create Cresta Payload
                    var queueId = string.Empty;
                    var queueName = string.Empty;
                    var callKey = $"{arg.EstablishedConnection.CallID}-{arg.AnsweringDevice.ID}";
                    var device = arg.AnsweringDevice.ID;
                    var ucid = arg.GlobalCallLinkageID;
                    var externalNumber = arg.CallingDevice.ID;

                    if (arg.CalledDevice.ID.Equals(arg.AnsweringDevice.ID))
                    {
                        // This is an outbound call since the called device is the same as the answering device
                        Log.Information("The called device {CalledDevice} is the same as the answering device {AnsweringDevice}. This is an outbound call. No cresta event sent on established event",
                                                        arg.CalledDevice.ID, arg.AnsweringDevice.ID);
                    }
                    else
                    {
                        // When the agent initiates an outbound call, the serviceiniated event is triggere
                        // in that event is where the ucid of the outbound is added to the outbound ucid list.
                        // If the ucid exist in the outbound ucid list, then this is an outbound call
                        // Otherwise, this is an inbound call
                        if (!MyOutboundUCIDList.ContainsKey(callKey))
                        {
                            if (MyQQueuedCallList.ContainsKey(arg.EstablishedConnection.CallID))
                            {
                                queueId = MyQQueuedCallList[arg.EstablishedConnection.CallID];
                                Log.Information("The queue id has been set to {queueId} for call id {callKey} from the queued call list", queueId, callKey);
                            }

                            // Check if the queue id for this call already exist in queue list
                            if (MyQueueList.ContainsKey(queueId))
                            {
                                queueName = MyQueueList[queueId].Name;
                                Log.Information("The queue name has been set to {Name} for call id {callKey}", queueName, callKey);
                            }
                            else
                            {
                                Log.Information("The queue name is not set in the list. Will attempt to query queue name for call id {callKey}", callKey);
                            }

                            if (CreateCrestaPayload(callKey, device, ucid, externalNumber, queueId, queueName, CrestaCallDirection.Inbound))
                            {
                                SendEventToCresta(this.MyCallList[callKey], callKey, establishedMetadata);
                            }
                            else
                            {
                                Log.Warning("Call with call id {callKey} failed to create Cresta payload. No event is sent to Cresta", callKey);
                            }
                        }
                        else
                        {
                            Log.Information("The call key {callKey} exist in the outbound UCID list. This is an outbound call. No cresta event sent on established event",
                                                            callKey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles the CSTAConnectionCleared event of the XmlClient control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="arg">The <see cref="CSTAConnectionClearedEventArgs"/> instance containing the event data.</param>
        private async void XmlClient_CSTAConnectionCleared(object sender, CSTAConnectionClearedEventArgs arg)
        {
            const string methodName = "CSTAConnectionCleared";

            await Task.Run(() =>
            {
                try
                {
                    var connectionClearedMetadata = arg.ToJson();
                    Log.Information("{methodName}. Active call records: {Count}. Arg: {connectionClearedMetadata}",
                                                methodName, this.MyCallList.Count, connectionClearedMetadata);
                    // Check if the connection cleared is from one of the agent's extensions
                    //var agentItem = MyAgentList.FirstOrDefault(x => x.Value.DeviceId.Equals(arg.DroppedConnection.DeviceID.ID));
                    
                    //if (agentItem.Value == null)
                    if (!MyAgentList.ContainsKey(arg.DroppedConnection.DeviceID.ID))
                    {
                        Log.Warning("The dropped device ID {ID} does not exist in the agent list", arg.DroppedConnection.DeviceID.ID);
                    }
                    else
                    {
                        // The key format is {CALLID}-{AGENTDEVICEID}
                        var callKey = $"{arg.DroppedConnection.CallID}-{arg.DroppedConnection.DeviceID.ID}";
                        if (MyCallList.ContainsKey(callKey))
                        {
                            MyCallList[callKey].session_event.event_type = CrestaCallEventType.Ended;
                            SendEventToCresta(MyCallList[callKey], callKey, connectionClearedMetadata);

                            // Delete the call item from the list
                            var callItemRemoved = this.MyCallList.TryRemove(callKey, out CrestaPayload callItemToDelete);
                            //Log.Information("Call item key to use: {keyToUse} removed: {callItemRemoved}");
                            if (callItemRemoved)
                            {
                                // Log the call item removed
                                Log.Information("Call item with call id {callKey}, ucid {customcalldecodeducid} ({platform_call_id}) has been removed from the call list: {callItemRemoved}. Active call records: {Count}",
                                                        callKey, callItemToDelete.session_event.payload.customcalldecodeducid, callItemToDelete.session_event.platform_call_id, callItemRemoved, this.MyCallList.Count);
                            }

                            // remove the UCID from the UCID list
                            RemoveOutboundUCIDFromList(callKey);

                            // remove q queued call from the list using drop call id
                            RemoveQueuedFromList(arg.DroppedConnection.CallID);
                        }
                        else
                        {
                            Log.Information("Call item with call id {callKey} does not exist in the call list. Active call records: {Count}",
                                                        callKey, this.MyCallList.Count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        private async void XmlClient_CSTADelivered(object sender, CSTADeliveredEventArgs arg)
        {
            const string methodName = "CSTADelivered";

            await Task.Run(() =>
            {
                try
                {
                    var deliveredMetadata = arg.ToJson();
                    var callListCount = this.MyCallList.Count;
                    Log.Information("{methodName}. Active call records: {callListCount}; Monitored List: {Count}; Arg: {deliveredMetadata}",
                                                methodName, callListCount, this.XmlClient.CSTAMonitorList.Count, deliveredMetadata);
                    
                    var keyToUse = arg.Connection.CallID;
                    if (!MyQQueuedCallList.ContainsKey(keyToUse))
                    {
                        var queueId = string.Empty;
                        PrivateDataDelivered privateDataDelivered = arg.PrivateData as PrivateDataDelivered;
                        if (privateDataDelivered != null && !string.IsNullOrEmpty(privateDataDelivered.SplitSkill))
                        {
                            queueId = privateDataDelivered.SplitSkill;
                            MyQQueuedCallList.TryAdd(keyToUse, queueId);
                            Log.Information("Delivered queue {queueId} with call id {keyToUse} has been added to the queued call list. Queued call list count: {Count}",
                                                    queueId, keyToUse, MyQQueuedCallList.Count);
                        }                        
                    }
                    else
                    {
                        Log.Information("The queued call list already contains call id {keyToUse}. No need to add again", keyToUse);
                    }                   
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        private async void XmlClient_CSTAQueued(object sender, CSTAQueuedEventArgs arg)
        {
            const string methodName = "CSTAQueued";

            await Task.Run(() =>
            {
                try
                {
                    Log.Information("{methodName}. Arg: {ToJson}", methodName, arg.ToJson());
                    var keyToUse = arg.QueuedConnection.CallID;

                    // This event only occurs if the call sets in the queue. If the call is queued
                    // and there is an available agent, this event is not fired. The receiving agent
                    // will get the CSTADelivered event. Therefore, we need to track the queued call in the
                    // CSTADelivered event
                    if (!MyQQueuedCallList.ContainsKey(keyToUse))
                    {
                        var queueId = arg.Queue.ID;
                        MyQQueuedCallList.TryAdd(keyToUse, arg.QueuedConnection.DeviceID.ID);
                        Log.Information("Queued queue {queueId} with call id {keyToUse} has been added to the queued call list. Queued call list count: {Count}",
                                                queueId, keyToUse, MyQQueuedCallList.Count);
                    }
                    else
                    {
                        Log.Information("The queued call list already contains call id {keyToUse}. No need to add again. List count: {Count}", 
                            keyToUse, MyQQueuedCallList.Count);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        private void XmlClient_CSTADiverted(object sender, CSTADivertedEventArgs arg)
        {
            const string methodName = "CSTADiverted";

            try
            {
                Log.Information("{methodName}. Arg: {ToJson}", methodName, arg.ToJson());
                var callKey = $"{arg.Connection.CallID}-{arg.DivertingDevice.ID}";
                // remove the UCID from the UCID list
                RemoveOutboundUCIDFromList(callKey);
                // remove q queued call from the list using primary old call id
                this.RemoveQueuedFromList(arg.Connection.CallID);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private void XmlClient_CSTAErrorReturned(object sender, CSTAErrorReturnedEventArgs arg)
        {
            const string methodName = "CSTAErrorReturned";

            try
            {
                Log.Error("{methodName}: {ToJson}", methodName, arg.ToJson());
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private async void XmlClient_StreamConnected(object sender, EventArgs e)
        {
            const string methodName = "StreamConnected";

            try
            {
                // All the CSTA or XML operations can be only performed after
                // this event fired. Any method invoked before this event will
                // return an integer value that can be casted to the
                // "NotConnected" of enASXMLClientError.
                Log.Information("{methodName}. XML server has created a stream connection with the TServer. IsConnected: {Connected}", methodName, XmlClient.Connected);
                // Start to monitor the device list configured
                await this.MonitorDevice();
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private void XmlClient_SocketConnected(object sender, EventArgs e)
        {
            const string methodName = "SocketConnected";

            try
            {
                // Fires when the Connect() method is called and the socket
                // connection is established with the XML server. It follows
                // with the StreamConnected event if it connected to the TServer
                // successfully.
                Log.Information("{SocketConnected}. XML client has established socket connection to the XML server", methodName);
                XmlClientReconnectTimer.Enabled = false;
                XmlClientReconnectTimer.Stop();
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private void XmlClient_ServerClosed(object sender, EventArgs e)
        {
            const string methodName = "ServerClosed";

            try
            {
                // Fires when connection with the Active XML Server is closed or
                // it fails to connect to the server.                
                Log.Error("{methodName}. The connection to the XML Server is closed. Attempting to reconnect in 30 seconds", methodName);
                // start the xmo server reconnect timer
                XmlClientReconnectTimer.Enabled = true;
                Log.Information("XML server reconnect timer is now enabled");
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private void XmlClient_InternalError(object sender, InternalErrorEventArgs arg)
        {
            const string methodName = "InternalError";

            try
            {
                // Fires when connection with the Active XML Server is closed or
                // it fails to connect to the server.                
                Log.Error("{methodName}. XML client internal error. ErrorMessage: {ErrorMessage}", methodName, arg.ErrorMessage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private async void XmlClient_CSTAMonitorStopResponse(object sender, CSTABasicResponseEventArgs arg)
        {
            const string methodName = "CSTAMonitorStopResponse";

            await Task.Run(() =>
            {
                try
                {
                    Log.Information("{methodName}. Monitor device stopped. InvokeID: {InvokeID};  Monitored devices count: {Count}",
                                    methodName, arg.InvokeID, XmlClient.CSTAMonitorList.Count);                   
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        private void XmlClient_CSTAMonitorStartResponse(object sender, CSTAMonitorStartResponseEventArgs arg)
        {
            const string methodName = "CSTAMonitorStartResponse";

            try
            {
                Log.Information("{methodName}. Succeeded to monitor device. InvokeID: {InvokeID}; MonitorCrossRefID: {MonitorCrossRefID}; Monitored devices count: {Count}",
                                            methodName, arg.InvokeID, arg.MonitorCrossRefID, XmlClient.CSTAMonitorList.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private async void XmlClient_CSTAHeld(object sender, CSTAHeldEventArgs arg)
        {
            const string methodName = "CSTAHeld";

            await Task.Run(() =>
            {
                try
                {
                    var heldMetadata = arg.ToJson();
                    Log.Information("{methodName}. Active call records: {Count}; Arg: {heldMetadata}",
                                                methodName, this.MyCallList.Count, heldMetadata);
                    var callKey = $"{arg.HeldConnection.CallID}-{arg.HoldingDevice.ID}";
                    if (MyCallList.ContainsKey(callKey))
                    {
                        MyCallList[callKey].session_event.event_type = CrestaCallEventType.Hold;
                        SendEventToCresta(MyCallList[callKey], callKey, heldMetadata);
                    }
                    else
                    {
                        Log.Information("Call item with call id {callKey} does not exist in the call list. Active call records: {Count}",
                                                    callKey, this.MyCallList.Count);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        private async void XmlClient_CSTARetrieved(object sender, CSTARetrievedEventArgs arg)
        {
            const string methodName = "CSTARetrieved";

            await Task.Run(() =>
            {
                try
                {
                    var retrievedMetadata = arg.ToJson();
                    Log.Information("{methodName}. Active call records: {Count}; Arg: {retrievedMetadata}",
                                                methodName, this.MyCallList.Count, retrievedMetadata);
                    var callKey = $"{arg.RetrievedConnection.CallID}-{arg.RetrievingDevice.ID}";
                    if (MyCallList.ContainsKey(callKey))
                    {
                        MyCallList[callKey].session_event.event_type = CrestaCallEventType.UnHold;
                        SendEventToCresta(MyCallList[callKey], callKey, retrievedMetadata);
                    }
                    else
                    {
                        Log.Information("Call item with call id {callKey} does not exist in the call list. Active call records: {Count}",
                                                    callKey, this.MyCallList.Count);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        private async void XmlClient_CSTAQueryDeviceNameResponse(object sender, CSTAQueryDeviceNameResponseEventArgs arg)
        {
            const string methodName = "CSTAQueryDeviceNameResponse";

            await Task.Run(() =>
            {
                try
                {
                    Log.Information("{methodName}. Queue list count: {Count}: {ToJson}", methodName, MyQueueList.Count, arg.ToJson());
                    // Update the queue with the device name
                    if (MyQueueList.ContainsKey(arg.DeviceID))
                    {
                        MyQueueList[arg.DeviceID].Name = arg.DeviceName;
                    }
                    else
                    {
                        Log.Warning("The queue Id {DeviceID} does not exist in the queue list", arg.DeviceID);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }

        private void XmlClient_CSTAServiceInitiated(object sender, CSTAServiceInitiatedEventArgs arg)
        {            
            const string methodName = "CSTAServiceInitiated";

            try
            {
                Log.Information("{methodName}: {ToJson}", methodName, arg.ToJson());
                var callKey = $"{arg.InitiatedConnection.CallID}-{arg.InitiatingDevice.ID}";
                //if (!MyUCIDList.ContainsKey(arg.InitiatedConnection.CallID))
                if (!MyOutboundUCIDList.ContainsKey(callKey))
                {
                    MyOutboundUCIDList.TryAdd(callKey, arg.GlobalCallLinkageID);
                    Log.Information("UCID {GlobalCallLinkageID} has been added to the UCID list for call key {callKey}. UCID list count: {Count}",
                                            arg.GlobalCallLinkageID, callKey, MyOutboundUCIDList.Count);
                }
                else
                {
                    Log.Information("UCID list already contains call key {callKey}. No need to add UCID {GlobalCallageID}",
                                            callKey, arg.GlobalCallLinkageID);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, methodName);
            }
        }

        private async void XmlClient_CSTAOriginated(object sender, CSTAOriginatedEventArgs arg)
        {
            const string methodName = "CSTAOriginated";

            await Task.Run(() =>
            {
                try
                {
                    var originatedMetadata = arg.ToJson();
                    Log.Information("CSTAOriginated: {originatedMetadata}", originatedMetadata);
                    // Check if the dial number is one of the agent's extension or agent id
                    // This is applicable only if the agent logged in to the physical phone manually
                    if (MyAgentList.Any(x => x.Value.DeviceId.Equals(arg.CalledDevice.ID) || x.Value.AgentId.Equals(arg.CalledDevice.ID)))
                    {
                        Log.Information("The dialed number {ID} is one of the agent's devices. No outbound message to Cresta is sent", arg.CalledDevice.ID);
                    }
                    else
                    {
                        Log.Information("The agent dialed {CalledDevice}. Going to create Cresta payload for outbound", arg.CalledDevice.ID);
                        var queueId = string.Empty;
                        var queueName = string.Empty;
                        var ucid = string.Empty;
                        var callKey = $"{arg.OriginatedConnection.CallID}-{arg.OriginatingDevice.ID}";
                        if (MyOutboundUCIDList.ContainsKey(callKey))
                        {
                            ucid = MyOutboundUCIDList[callKey];
                        }
                        
                        var device = arg.OriginatingDevice.ID;
                        //var ucid = arg.GlobalCallLinkageID;
                        var externalNumber = arg.CalledDevice.ID;
                        // Create Cresta Payload
                        if (CreateCrestaPayload(callKey, device, ucid, externalNumber, queueId, queueName, CrestaCallDirection.Outbound))
                        {
                            SendEventToCresta(this.MyCallList[callKey], callKey, originatedMetadata);
                        }
                        else
                        {
                            Log.Information("Call with call id {callKey} failed to create Cresta payload. No event is sent to Cresta", callKey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, methodName);
                }
            }).ConfigureAwait(false);
        }
    }
}