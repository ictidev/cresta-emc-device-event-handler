# Cresta EMC Device Event Handler

## Overview
**Cresta EMC Device Event Handler** is a Windows Service designed to handle telephony events using the **Avaya EMC Developer CSTA** framework. It connects to an XML telephony server (TServer) to monitor devices, agents, and queues, and integrates with **Cresta API** for real-time call event reporting and analytics.

---

**Components:**
- **Windows Service**: Core service that manages lifecycle and event handling.
- **XML Client**: Handles auto-reconnect, CSTA event subscriptions, and event processing.
- **REST API**: Sends serialized call event payloads to Cresta.
- **Cresta API**: Receives call analytics data for AI-driven insights.

---

## Features
- Avaya EMC XML Server Integration
- Event Handling for CSTA events
- Cresta API Integration
- Auto-Reconnect to EMC XML Server
- Thread-Safe Data Management

---

## Tech Stack
- **Language:** C#
- **Libraries:** Avaya EMC Developer CSTA, Newtonsoft.Json, RestSharp, Serilog, SimpleServices

---

## Setup Instructions
1. Configure `Settings.Default` for XML server and Cresta API.
2. Restore Nuget packages
3. Build and install as a Windows Service:
   ```powershell
   sc create ICTIEMCEventHandlerService binPath= "C:\Path\To\icti-emc-event-handler.exe"
   ```
4. Start service:
   ```powershell
   net start ICTIEMCEventHandlerService
   ```

---

## Usage
- Monitors agent devices and hunt groups for telephony events.
- Sends call event data to Cresta API for real-time analytics.
- Logs all operations using Serilog for troubleshooting and auditing.

---

## License
© InfinityCTI. All rights reserved.
