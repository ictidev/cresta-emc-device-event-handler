// ***********************************************************************
// Assembly         : Agent
// Author           : InfinityCTI
// Created          : 07-15-2025
//
// Last Modified By : InfinityCTI
// Last Modified On : 07-23-2025
// ***********************************************************************
// <copyright file="Agent.cs" company="InfinityCTI">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
namespace icti_emc_event_handler
{
    public class Agent
    {
        /// <summary>
        /// Gets or sets the agent identifier.
        /// </summary>
        /// <value>The agent identifier.</value>
        public string AgentId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>The device identifier.</value>
        public string DeviceId { get; set; } = string.Empty;
    }
}
