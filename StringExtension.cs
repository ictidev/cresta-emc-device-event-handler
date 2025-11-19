using Newtonsoft.Json;
using System;

namespace icti_emc_event_handler
{
    /// <summary>
    /// Extension methods must be defined in a static class
    /// </summary>
    public static class StringExtension
    {
        /// <summary>
        /// Converts object into JSON in order to log the entire object.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static string ToJson(this object value)
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                return JsonConvert.SerializeObject(value, Formatting.Indented, settings);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }
}
