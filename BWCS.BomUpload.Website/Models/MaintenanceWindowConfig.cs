using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BWCS.BomUpload.Website.Models
{
    public class MaintenanceWindowConfig
    {
        public DayOfWeek StartDay { get; set; }
        public TimeSpan StartTime { get; set; }
        public DayOfWeek EndDay { get; set; }
        public TimeSpan EndTime { get; set; }
        public string TimeZone { get; set; }

        public MaintenanceWindowConfig()
        {
            TimeZone = "Central Standard Time";
        }
    }

    public class MaintenanceWindowConfigLoader
    {
        public static MaintenanceWindowConfig LoadFromConfig()
        {
            try
            {
                var config = new MaintenanceWindowConfig();

                // Read Start Day
                string startDayStr = System.Configuration.ConfigurationManager.AppSettings["MaintenanceWindow:StartDay"];
                if (!string.IsNullOrEmpty(startDayStr) && Enum.TryParse<DayOfWeek>(startDayStr, out DayOfWeek startDay))
                {
                    config.StartDay = startDay;
                }
                else
                {
                    config.StartDay = DayOfWeek.Saturday;
                }

                // Read Start Time
                string startTimeStr = System.Configuration.ConfigurationManager.AppSettings["MaintenanceWindow:StartTime"];
                if (!string.IsNullOrEmpty(startTimeStr) && TimeSpan.TryParse(startTimeStr, out TimeSpan startTime))
                {
                    config.StartTime = startTime;
                }
                else
                {
                    config.StartTime = TimeSpan.Parse("22:30");
                }

                // Read End Day
                string endDayStr = System.Configuration.ConfigurationManager.AppSettings["MaintenanceWindow:EndDay"];
                if (!string.IsNullOrEmpty(endDayStr) && Enum.TryParse<DayOfWeek>(endDayStr, out DayOfWeek endDay))
                {
                    config.EndDay = endDay;
                }
                else
                {
                    config.EndDay = DayOfWeek.Sunday;
                }

                // Read End Time
                string endTimeStr = System.Configuration.ConfigurationManager.AppSettings["MaintenanceWindow:EndTime"];
                if (!string.IsNullOrEmpty(endTimeStr) && TimeSpan.TryParse(endTimeStr, out TimeSpan endTime))
                {
                    config.EndTime = endTime;
                }
                else
                {
                    config.EndTime = TimeSpan.Parse("05:00");
                }

                // Read Timezone
                string timeZone = System.Configuration.ConfigurationManager.AppSettings["MaintenanceWindow:TimeZone"];
                if (!string.IsNullOrEmpty(timeZone))
                {
                    config.TimeZone = timeZone;
                }

                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading maintenance window config: {ex.Message}");
                return new MaintenanceWindowConfig();
            }
        }
    }
}