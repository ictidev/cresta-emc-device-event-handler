// ***********************************************************************
// Assembly         : icti-telephony-gateway
// Author           : InfinityCTI
// Created          : 07-15-2025
//
// Last Modified By : InfinityCTI
// Last Modified On : 07-23-2025
// ***********************************************************************
// <copyright file="General.cs" company="InfinityCTI, Inc.">
//     Copyright ©  2025
// </copyright>
// <summary></summary>
// ***********************************************************************
using System.Globalization;
using System;
using System.Reflection;
using Serilog;

namespace icti_emc_event_handler
{
    public class General
    {
        private static object[] assemblyAtributes;

        private General()
        {

        }

        public static string GetProductName
        {
            get
            {
                // Can't create this class
                // Retrieve assembly Product attribute
                // For Description, use AssemblyDescriptionAttribute
                // For Title, use AssemblyTitleAttribute
                assemblyAtributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                return ((AssemblyProductAttribute)assemblyAtributes[0]).Product;
            }
        }

        public static string GetProductVersion
        {
            get
            {
                // Can't create this class
                // Retrieve assembly Product attribute
                // For Description, use AssemblyDescriptionAttribute
                // For Title, use AssemblyTitleAttribute
                assemblyAtributes = Assembly.GetCallingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public static string EncodeUCID(string ucidToEncode)
        {
            string retVal = string.Empty;

            try
            {
                if (ucidToEncode.Length < 11)
                {
                    Log.Error("Given UCID is too short: {rawUcid}", ucidToEncode);
                }
                else
                {
                    try
                    {
                        long networkNode = long.Parse(ucidToEncode.Substring(0, 5), CultureInfo.InvariantCulture);
                        long sequenceNumber = long.Parse(ucidToEncode.Substring(5, 5), CultureInfo.InvariantCulture);
                        long timestamp = long.Parse(ucidToEncode.Substring(10), CultureInfo.InvariantCulture);

                        retVal = string.Format(CultureInfo.InvariantCulture, "{0:x4}{1:x4}{2:x6}",
                            networkNode, sequenceNumber, timestamp).ToLower();
                    }
                    catch (FormatException ex)
                    {
                        Log.Error($"Failed to parse UCID {ucidToEncode} segments from raw ucid: {ex}");
                    }
                }                
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

            return retVal;
        }
    }
}
