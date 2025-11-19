using System.Collections.Generic;
using Newtonsoft.Json;

namespace icti_emc_event_handler
{
    public class Participant
    {
        public string role { get; set; } = string.Empty;
        public string platform_agent_id { get; set; }
        public string external_user_id { get; set; }
    }

    public class Payload
    {
        [JsonProperty("custom.call.ani")]
        public string customcallani { get; set; } = string.Empty;

        [JsonProperty("custom.call.direction")]
        public string customcalldirection { get; set; } = string.Empty;

        [JsonProperty("custom.call.topic")]
        public string customcalltopic { get; set; } = string.Empty;

        [JsonProperty("custom.call.ucid")]
        public string customcallucid { get; set; } = string.Empty;
        [JsonProperty("custom.call.decodeducid")]
        public string customcalldecodeducid { get; set; } = string.Empty;

        [JsonProperty("custom.call.queueid")]
        public string customcallqueueid { get; set; } = string.Empty;

        [JsonProperty("custom.call.queuename")]
        public string customcallqueuename { get; set; } = string.Empty;

        [JsonProperty("custom.call.metadata")]
        public string customcallmetadata { get; set; } = string.Empty;

        [JsonProperty("system.voice.event_server")]
        public string systemvoiceeventserver { get; set; } = "SIPREC_SERVER";
    }

    public class CrestaPayload
    {
        public SessionEvent session_event { get; set; }
    }

    public class SessionEvent
    {
        public List<Participant> participants { get; set; }
        public string event_type { get; set; } = string.Empty;
        public Payload payload { get; set; }
        public string platform_call_id { get; set; } = string.Empty;
        public bool close_active_calls { get; set; } = true;
    }
}
