using System;

namespace icti_emc_event_handler
{
    public class CallInfo
    {
        public string Direction { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets when the call was started for and outbound call.
        /// </summary>
        /// <value>
        /// The start date time.
        /// </value>
        public DateTime? StartDateTime { get; set; } = null;
        /// <summary>
        /// Gets or sets the end date time.
        /// </summary>
        /// <value>The end date time.</value>
        public DateTime? EndDateTime { get; set; } = null;
        /// <summary>
        /// Gets or sets the SplitSkill. Applied to CSTAQueued event.
        /// </summary>
        /// <value>
        /// The queue.
        /// </value>
        public string SplitSkill { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the call identifier of the active call.
        /// </summary>
        /// <value>
        /// The call identifier.
        /// </value>
        public string CallId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the originally called device. 
        /// </summary>
        /// <value>
        /// The called device.
        /// </value>
        public string CalledDevice { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the calling device.
        /// </summary>
        /// <value>
        /// The calling device.
        /// </value>
        public string CallingDevice { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the  user information that is related to the call.
        /// </summary>
        /// <value>
        /// The  user information that is related to the call.
        /// </value>
        public string UUI { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the Universal Call ID of the call.
        /// </summary>
        /// <value>
        /// The Universal Call ID.
        /// </value>
        public string UCID { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the agent identifier.
        /// </summary>
        /// <value>The agent identifier.</value>
        public string AgentId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the old ucid. Applies to transfer/conference event.
        /// </summary>
        /// <value>
        /// The old ucid.
        /// </value>
        public string OldUCID { get; set; } = string.Empty;

    }
}
