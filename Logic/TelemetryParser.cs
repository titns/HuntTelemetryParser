using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Data;
using System.Windows.Media.Animation;

namespace HuntTelemetry.Logic
{
    public class TelemetryParser
    {

        public static List<LevelLog> ParseTelemtryFile(string filePath)
        {
            using var streamReader = new StreamReader(filePath);
            var contents = System.IO.File.ReadAllLines(filePath);

            var result = new List<LevelLog>();

            //events are sequential and one log contains separate levels continuously, therefore need to parse one level at a time
            LevelLog currentLog = new LevelLog();

            //parse each line

            while (!streamReader.EndOfStream)
            {
                var line = streamReader.ReadLine();
                
                var lineParts = line.Split(',').Select(x => x.Trim());

                var eventTypeStr = lineParts.FirstOrDefault();

                var ev = new PerformanceEvent();
                PerformanceEventType eventType = ParseEventType(eventTypeStr);


                ev.EventType = eventType;

                foreach (var linePart in lineParts.Skip(1))
                {
                    var keyval = linePart.Split(':');
                    var key = keyval[0];
                    var val = linePart.Remove(0, key.Length+1);

                    switch (key)
                    {
                        case "date":
                            ev.TimeStamp = DateTime.Parse(val);
                            break;
                        case "session_id":
                            ev.Session_Id = val;
                            break;
                        case "cam_pos":
                            var positions = val.Split(';');
                            ev.Stats.Add(key, val);
                            ev.Stats.Add("cam_pos_x", positions[0]);
                            ev.Stats.Add("cam_pos_y", positions[1]);
                            ev.Stats.Add("cam_pos_z", positions[2]);
                            break;
                        case "level":
                            ev.Stats.Add(key, val);
                            break;
                        case "cam_dir":

                            var camDirection = val.Split(';');
                            ev.Stats.Add(key, val);
                            ev.Stats.Add("cam_dir_x", camDirection[0]);
                            ev.Stats.Add("cam_dir_y", camDirection[1]);
                            ev.Stats.Add("cam_dir_z", camDirection[2]);
                            break;
                    }
                }

                switch (eventType){
                    case PerformanceEventType.NA:
                        break;
                    case PerformanceEventType.perf_session_start:
                        currentLog = new LevelLog();
                        currentLog.Session_Id = ev.Session_Id;
                        currentLog.Level = ev.Stats["level"];
                        if (!SkipEvent(ev)) { currentLog.Events.Add(ev); }
                        break;
                    case PerformanceEventType.perf_session_end:
                        if (!SkipEvent(ev)) { currentLog.Events.Add(ev); }
                        if (!SkipLog(currentLog)) { result.Add(currentLog); }
                        currentLog = null;
                        break;
                    default:
                        if (!SkipEvent(ev)) { currentLog.Events.Add(ev); }
                        break;
                }

            }

            return result;
        }

        private static PerformanceEventType ParseEventType(string eventTypeStr)
        {
            var eventType = PerformanceEventType.perf_general;
            switch (eventTypeStr)
            {
                case "perf_memory":
                    eventType = PerformanceEventType.perf_memory;
                    break;
                case "perf_general":
                    eventType = PerformanceEventType.perf_general;
                    break;
                case "perf_bandwidth":
                    eventType = PerformanceEventType.perf_bandwith;
                    break;
                case "perf_client_avg_stats":
                    eventType = PerformanceEventType.perf_client_avg_stats;
                    break;
                case "perf_host_info":
                    eventType = PerformanceEventType.NA;
                    break;
                case "perf_session_start":
                    eventType = PerformanceEventType.perf_session_start;
                    break;
                case "perf_session_end":
                    eventType = PerformanceEventType.perf_session_end;
                    break;
                case "perf_network_thread":
                    eventType = PerformanceEventType.perf_network_thread;
                    break;
                default:
                    throw new ArgumentException("unknown event type");
            }

            return eventType;
        }

        private static bool SkipEvent(PerformanceEvent evt)
        {
            if(evt.Stats.TryGetValue("cam_pos_x",out string cam_pos_x))
            {
                if (evt.X == 0 && evt.Y ==0)
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        private static bool SkipLog(LevelLog log)
        {
            if (log.Level == "menu")
            {
                return true;
            }
            if (log.Level != "civilwar" && log.Level != "cemetery" && log.Level != "tutorial")
            {
                throw new ArgumentException();
            }
            if(log.Events.Count == 0)
            {
                return true;
            }
            else if (log.EndTime-log.StartTime < new TimeSpan(0, 4, 0))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
