using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HuntTelemetry.Logic
{
    public class PerformanceEvent
    {
        public static int MaxWidth = 1550;
        public static int MaxHeight = 1550;

        private static IFormatProvider doubleFormat = System.Globalization.CultureInfo.GetCultureInfo(1033).NumberFormat;

        public PerformanceEventType EventType { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Session_Id { get; set; }

        private double? _posX;
        public double X
        {
            get
            {
                if (!_posX.HasValue)
                {
                    if(Stats.TryGetValue("cam_pos_x",out string pos))
                    {
                        _posX = double.Parse(pos, doubleFormat);
                        var magicXOffset = 500;

                        if (_posX > MaxWidth)
                        {
                            _posX = MaxWidth;
                        }
                        _posX -= magicXOffset;
                        if (_posX < 0)
                        {
                            _posX = 0;
                        }
                        _posX = _posX / (MaxWidth- magicXOffset);
                    }
                }
                return _posX.Value;
            }
        }
        private double? _posY;
        public double Y
        {
            get
            {
                if (!_posY.HasValue)
                {
                    if (Stats.TryGetValue("cam_pos_y", out string pos))
                    {
                        _posY = double.Parse(pos, doubleFormat);
                        var magicYOffset = 500;
                        _posY -= magicYOffset;
                        if (_posY > MaxHeight - magicYOffset)
                        {
                            _posY = MaxHeight - magicYOffset;
                        }
                        if (_posY < 0)
                        {
                            _posY = 0;
                        }
                        _posY = _posY / (MaxHeight - magicYOffset);
                    }
                }
                return _posY.Value;
            }
        }
        private double? _posZ;
        public double Z
        {
            get
            {
                if (!_posZ.HasValue)
                {
                    if (Stats.TryGetValue("cam_pos_z", out string pos))
                    {
                        _posZ = double.Parse(pos,doubleFormat);
                    }
                }
                return _posZ.Value;
            }
        }


        public Dictionary<string, string> Stats { get; set; } = new Dictionary<string, string>();
    }

    public enum PerformanceEventType
    {
        NA,
        perf_memory,
        perf_general,
        perf_bandwith,
        perf_client_avg_stats,
        perf_session_start,
        perf_session_end,
        perf_network_thread
    }
}
