// ***********************************************************************
// Assembly         : Device
// Author           : InfinityCTI
// Created          : 07-15-2025
//
// Last Modified By : InfinityCTI
// Last Modified On : 07-23-2025
// ***********************************************************************
// <copyright file="Device.cs" company="InfinityCTI">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
using AgileSoftware.Developer.CSTA;

namespace icti_emc_event_handler
{
    public class Device
    {
        /// <summary>
        /// Gets or sets the device identifier.
        /// </summary>
        /// <value>The device identifier.</value>
        public string DeviceId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the monitor identifier.
        /// </summary>
        /// <value>The monitor identifier.</value>
        public int MonitorId { get; set; } = 0;
        /// <summary>
        /// Gets or sets the monitor filter.
        /// </summary>
        /// <value>The monitor filter.</value>
        public CSTAMonitorFilter monitorFilter { get; set; } = null;
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>
        public enDeviceIDType Type { get; set; } = enDeviceIDType.other;
    }
}
