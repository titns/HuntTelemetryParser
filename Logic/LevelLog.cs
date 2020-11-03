using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace HuntTelemetry.Logic
{
    public class LevelLog
    {
        public string Level { get; set; }
        public string Session_Id { get; set; }


        private DateTime? _startTime;
        public DateTime StartTime
        {
            get
            {
                if (_startTime.HasValue)
                {
                    return _startTime.Value;
                }
                _startTime = Events.Min(x => x.TimeStamp);
                return _startTime.Value;
            }
        }
        private DateTime? _endTime;
        public DateTime EndTime
        {
            get
            {
                if (_endTime.HasValue)
                {
                    return _endTime.Value;
                }
                _endTime = Events.Max(x => x.TimeStamp);
                return _endTime.Value;
            }
        }

        public List<PerformanceEvent> Events { get; set; } = new List<PerformanceEvent>();

        public void Reset()
        {
            _startTime = null;
            _endTime = null;
        }

        public override string ToString()
        {
            return $"{this.StartTime.ToString("yyyy-MM-dd hh:mm:ss")} {this.Level}";
        }

    }
}
