using HuntTelemetry.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace HuntTelemetry
{

    [Flags]
    public enum FrequencySpreadReduce
    {
        NA,
        RemoveBigGaps,
        Linear
    }

    public class PathCalculatorProperties
    {
        public double LessThanFrequencyIgnore { get; set; } = 0;
        public double MaxPixelSquareSize { get; set; } = 3;
        public bool TreatOverlappingDotsAsDifferentEvents { get; set; } = false;
        public double CivilWarOffset { get; set; } = 0.5;
        public TimeSpan CampingCutOff { get; set; } = new TimeSpan(0, 0, 15);
        public bool IgnoreSpawnPointEvents { get; set; } = true;
        public Int32Rect Image { get; set; }
        public FrequencySpreadReduce FrequencyStrategy { get; set; } = FrequencySpreadReduce.RemoveBigGaps;


    }
    public class PathCalculator
    {

        private readonly PathCalculatorProperties _properties;

        public PathCalculator(PathCalculatorProperties properties)
        {
            _properties = properties ?? throw new ArgumentNullException();
        }

        public List<Dot> GetPaths(List<LevelLog> logs)
        {
            Point previousPoint = default;
            DateTime previousPointTimeStamp = default;
            Dictionary<int, List<(int index, int count, DateTime timestamp)>> positionFrequencies = CalculatePositionFrequencies(logs, ref previousPoint, ref previousPointTimeStamp);


            var useSet = positionFrequencies.SelectMany(x => x.Value).ToList();


            useSet = ApplyFrequencySquashStrategy(useSet);


            //if the overlapping is not done, then do no sorting to keep time flow
            var sort = !_properties.TreatOverlappingDotsAsDifferentEvents ? useSet.OrderBy(x => x.count) : useSet.OrderBy(x => x.timestamp);

            return CalculateDots(sort);
        }

        private List<(int index, int count, DateTime timestamp)> ApplyFrequencySquashStrategy(List<(int index, int count, DateTime timestamp)> useSet)
        {
            if (_properties.FrequencyStrategy > 0)
            {
                var uniqueCounts = useSet.Select(x => x.count).Distinct().OrderBy(x => x).ToList();
                var min = uniqueCounts[0];
                var max = uniqueCounts[^1];
                Dictionary<int, int> frequencyMap = new Dictionary<int, int>();
                var smallStep = 1;
                int bigStep = default;

                if ((_properties.FrequencyStrategy & FrequencySpreadReduce.RemoveBigGaps) > 0)
                {
                    bigStep = Math.Max(1, (int)((max - min) / 100));
                }
                else if ((_properties.FrequencyStrategy & FrequencySpreadReduce.Linear) > 0)
                {
                    bigStep = 1;
                }
                if (bigStep != default)
                {
                    var currValue = min;
                    var lastValue = 0;
                    foreach (var i in uniqueCounts)
                    {
                        if ((i - currValue) / (max - min) > 0.05)
                        {
                            currValue += bigStep;
                            frequencyMap.Add(i, currValue);
                        }
                        else
                        {
                            currValue += smallStep;
                            frequencyMap.Add(i, currValue);
                        }
                        lastValue = i;
                    }
                    return useSet.Select(x => (x.index, frequencyMap[x.count], x.timestamp)).ToList();
                }
            }
            return useSet;
        }

        private List<Dot> CalculateDots(IOrderedEnumerable<(int index, int count, DateTime timestamp)> positionList)
        {
            List<Dot> dots = new List<Dot>();
            //the max is the brightest and biggest dot on map
            var max = positionList.Max(x => x.count);
            var min = positionList.Min(x => x.count);

            foreach (var (pixelIndex, currCount, timestamp) in positionList)
            {
                if (currCount < max * _properties.LessThanFrequencyIgnore)
                {
                    continue;
                }

                Point pixelLocation = Utilities.CalculatePixelLocation(pixelIndex, _properties.Image);
                var normalizedFrequency = currCount / (double)max;

                #region bad attempts to make frequency spread more beaufiul
                //\log\left(x+0.15\right)+0.83
                //var relativeFrequency = Math.Log(normalizedFrequency + 0.15) + 0.83;
                //var relativeFrequency = Math.Asin(2 * (normalizedFrequency - 0.5) * 0.999 + Math.PI / 2) / Math.PI
                //var relativeFrequency = Math.Cos(Math.PI * (( - min)) - 1)) / 2 + 0.5;

                #endregion
                var relativeFrequency = normalizedFrequency;

                Dot dot = CalculateDotProperties(pixelLocation, relativeFrequency, !_properties.TreatOverlappingDotsAsDifferentEvents);
                dots.Add(dot);
            }
            return dots;
        }

        private Dictionary<int, List<(int index, int count, DateTime timestamp)>> CalculatePositionFrequencies(List<LevelLog> logs, ref Point previousPoint, ref DateTime previousPointTimeStamp)
        {
            var pixelCount = new Dictionary<int, List<(int index, int count, DateTime timestamp)>>();

            foreach (var workingLog in logs)
            {
                var xOffset = workingLog.Level == "civilwar" ? _properties.CivilWarOffset : 0;

                PerformanceEvent startingEvent = null;
                var startingPosition = default(Point);
                previousPoint = default;
                previousPointTimeStamp = default;


                var lastPosition = default(Point);
                var lastEvent = workingLog.Events[^1];

                var endingx = (int)((_properties.Image.Width - 2) * lastEvent.X);
                var endingy = (int)((_properties.Image.Height - 2) * lastEvent.Y);
                lastPosition = new Point(endingx, endingy);

                foreach (var evt in workingLog.Events)
                {
                    //eliminate loading events which are at the corner of the map
                    if (evt.Y > 0.998 || evt.Y < 0.002)
                    {
                        continue;
                    }


                    var relativeX = evt.X / 2 + xOffset;
                    var relativeY = 1 - evt.Y;


                    if (_properties.IgnoreSpawnPointEvents && startingEvent == null)
                    {
                        var startingx = (int)((_properties.Image.Width - 2) * relativeX);
                        var startingy = (int)((_properties.Image.Height - 2) * relativeY);
                        startingPosition = new Point(startingx, startingy);
                        startingEvent = evt;
                        previousPoint = startingPosition;
                        continue;
                    }


                    var x = (int)((_properties.Image.Width - 2) * relativeX);
                    var y = (int)((_properties.Image.Height - 2) * relativeY);

                    var newPoint = new Point(x, y);
                    if (newPoint == startingPosition)
                    {
                        continue;
                    }
                    if (previousPoint == newPoint)
                    {
                        if ((evt.TimeStamp - previousPointTimeStamp) > _properties.CampingCutOff)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        previousPoint = newPoint;
                        previousPointTimeStamp = evt.TimeStamp;
                    }


                    var index = (int)(_properties.Image.Width * y + x);


                    if (pixelCount.TryGetValue(index, out List<(int index, int count, DateTime timestamp)> val))
                    {
                        if (_properties.TreatOverlappingDotsAsDifferentEvents)
                        {
                            var lastVal = val[^1];
                            val.Add((index, lastVal.count - 1, evt.TimeStamp));
                        }
                        else
                        {
                            val[0] = (index, val[0].count + 1, evt.TimeStamp);
                        }
                    }
                    else
                    {
                        pixelCount.Add(index, new List<(int index, int count, DateTime timespan)>() { (index, 1, evt.TimeStamp) });
                    }
                }
            }

            return pixelCount;
        }

        public Dot CalculateDotProperties(Point pixelLocation, double relativeSize, bool resizePixel)
        {
            var sqSz = (int)Math.Round(resizePixel? _properties.MaxPixelSquareSize * relativeSize : 1);
            if (sqSz < 1)
            {
                sqSz = 1;
            }
            var size = new Int32Rect((int)pixelLocation.X, (int)pixelLocation.Y, sqSz, sqSz);

            //paint on the relative scale of green->yellow->red->violet->blue->light blue
            //HSL is the most usable for that
            //this just calculates the color on that scale
            var value = 128 - 278 * relativeSize;
            if (value < 0)
            {
                value += 360;
                if (value < 210)
                {
                    value = 210;
                }
            }
            value /= 360;

            var newColor = Utilities.HSL2RGB(value, 1.0, 0.5);
            return new Dot() {Color = newColor, Rectangle = size };
        }
    }

    public class Utilities
    {
        /// <summary>
        /// returns RBG color from HSL color properties
        /// </summary>
        /// <param name="h"></param>
        /// <param name="sl"></param>
        /// <param name="l"></param>
        /// <returns></returns>
        public static Color HSL2RGB(double h, double sl, double l)
        {
            double v;
            double r, g, b;

            r = l;   // default to gray
            g = l;
            b = l;
            v = (l <= 0.5) ? (l * (1.0 + sl)) : (l + sl - l * sl);

            if (v > 0)
            {
                double m;
                double sv;
                int sextant;
                double fract, vsf, mid1, mid2;

                m = l + l - v;
                sv = (v - m) / v;
                h *= 6.0;
                sextant = (int)h;
                fract = h - sextant;
                vsf = v * sv * fract;
                mid1 = m + vsf;
                mid2 = v - vsf;

                switch (sextant)
                {
                    case 0:
                        r = v;
                        g = mid1;
                        b = m;
                        break;
                    case 1:
                        r = mid2;
                        g = v;
                        b = m;
                        break;
                    case 2:
                        r = m;
                        g = v;
                        b = mid1;
                        break;
                    case 3:
                        r = m;
                        g = mid2;
                        b = v;
                        break;
                    case 4:
                        r = mid1;
                        g = m;
                        b = v;
                        break;
                    case 5:
                        r = v;
                        g = m;
                        b = mid2;
                        break;
                }
            }
            Color rgb;
            rgb.R = Convert.ToByte(r * 255.0f);
            rgb.G = Convert.ToByte(g * 255.0f);
            rgb.B = Convert.ToByte(b * 255.0f);
            rgb.A = 255;
            return rgb;
        }

        /// <summary>
        /// calculates pixel coordinates by it's index
        /// </summary>
        /// <param name="pixelIndex"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        public static Point CalculatePixelLocation(int pixelIndex, Int32Rect image)
        {
            var pixelLocationX = (pixelIndex % image.Width) - 4;
            var pixelLocationY = ((pixelIndex - pixelLocationX) / image.Width) - 4;
            var pixelLocation = new Point(pixelLocationX, pixelLocationY);
            return pixelLocation;
        }
    }

    public struct Dot
    {
        public DateTime TimeStamp;
        public Color Color;
        public Int32Rect Rectangle;
    }
}
