using Microsoft.AspNetCore.Routing;

namespace furnace.diameter;

// TODO migrate this to be part of the Furnace worker, and find a better way to route information to/from it and the weight sensor service
public sealed class DiameterControl
{
    private readonly object _sync = new();

    private readonly List<DataNode> _raw = [];
    private readonly List<DataPoint> _filtered = [];
    private double _pullRate;
    private double _density;
    private double _radiusTarget;
    private double _capAngle;
    private double _kp;
    private bool _enabled = false;

    private readonly ScalarKalmanFilter _kalman = new(
        initialEstimate: 0.0,
        initialErrorCovariance: 1.0,
        processNoise: 0.01,
        measurementNoise: 0.1
    );

    private uint _latestTimestamp = 0;
    private uint _startTimestamp = 0;

    // Keep only recent data needed by the Kalman filter / PID loop.
    // Interpret this according to your timestamp unit.
    private readonly uint _retentionWindow = 60_000;
    private readonly uint _derivativeWindow = 60_000;

    public IReadOnlyList<DataPoint> FilteredSnapshot
    {
        get
        {
            lock (_sync)
            {
                return _filtered.ToArray();
            }
        }
    }

    public DataPoint? LatestFiltered
    {
        get
        {
            lock (_sync)
            {
                return _filtered.Count == 0 ? null : _filtered[^1];
            }
        }
    }

    /// <param name="pullRate">Speed in mm/hour</param>
    /// <param name="density">Density in g/cm^3</param>
    /// <param name="radiusTarget">Target radius, in mm</param>
    /// <param name="capAngle">Angle from vertical, in degrees, of the conical cap</param>
    /// <param name="proportionalGain">Gain for the control loop</param>
    public DiameterControl(double pullRate, double density, double radiusTarget, double capAngle, double proportionalGain)
    {
        _pullRate = pullRate / 3600000; //convert to mm/millisecond
        _density = density / 1000; // convert to g/mm^3
        _radiusTarget = radiusTarget;
        _capAngle = capAngle;
        _kp = proportionalGain;
    }

    public async void AddData(uint[] timeArray, double[] massArray)
    {
        // Disregard any old data. This is a contingency and not expected to occur in practice
        if (timeArray[0] <= _raw[^1].Timestamps[^1])
        {
            return;
        }

        lock (_sync)
        {
            var node = new DataNode(timeArray, massArray);
            _raw.Add(node);
        }
    }

    public void SetGain(double proportionalGain)
    {
        lock (_sync) 
        {
            _kp = proportionalGain;
        }
    }

    private void PruneOldData()
    {
        uint cutoff = _latestTimestamp > _retentionWindow
            ? _latestTimestamp - _retentionWindow
            : 0;

        _raw.RemoveAll(node => node.End < cutoff);
    }

    public double GetTrim()
    {   
        lock (_sync) 
        {
            int start = _raw.BinarySearch(new DataNode(_latestTimestamp)); // start from the node containing the oldest unfiltered data
            for (int i = start; i < _raw.Count; i++)
            {
                DataNode node = _raw[i];
                for (int q = 0; q < node.Timestamps.Length; q++)
                {
                    if (node.Timestamps[q] > _latestTimestamp)
                    {
                        double filteredValue = _kalman.Update(node.Values[q]);
                        _filtered.Add(new DataPoint(node.Timestamps[q], filteredValue));
                    }
                }
            }
            _latestTimestamp = _raw[^1].Timestamps[^1];
            PruneOldData();

            if (_enabled){

                int derivativeStart = _raw.BinarySearch(new DataNode(_latestTimestamp - _derivativeWindow));

                return _kp * (CalculateRadius(_filtered[derivativeStart..]) - Math.Min(_radiusTarget, (Math.Tan(_capAngle * Math.PI / 180)*_pullRate*_latestTimestamp) - _startTimestamp));
            }

            return 0;
        }
    }

    private double CalculateRadius(List<DataPoint> dataPoints)
    {
        double sumX = 0;
        double sumY = 0;
        double sumXY = 0;
        double sumXSquared = 0;

        int n = dataPoints.Count;

        for (int i = 0; i < n; i++)
        {
            double x = dataPoints[i].Timestamp;
            double y = dataPoints[i].Value;

            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumXSquared += x * x;
        }

        double denominator = n * sumXSquared - sumX * sumX;

        double dmdt = (n * sumXY - sumX * sumY) / denominator;

        return Math.Sqrt(dmdt / (_pullRate * _density * Math.PI));
    }

    public void Start()
    {
        lock (_sync) 
        {
            _startTimestamp = _raw[^1].Timestamps[^1];
            _enabled = true;
        }
    }
}

public readonly record struct DataPoint(uint Timestamp, double Value);

public sealed class DataNode: IComparable
{
    public double[] Values { get; }
    public uint[] Timestamps { get; }

    public uint Start => Timestamps[0];
    public uint End => Timestamps[^1];

    public DataNode(uint[] timeArr, double[] valueArr)
    {
        Timestamps = timeArr;
        Values = valueArr;
    }

    public DataNode(uint time)
    {
        Values = [];
        Timestamps = [time];
    }

    public bool Contains(uint timestamp)
    {
        return Start <= timestamp && timestamp <= End;
    }

    public int CompareTo(object? obj)
    {
        if (obj == null) return 1;
        DataNode x = (DataNode) obj;
        if (x.Start > End)
        {
            return -1;
        }
        else if (x.Start < Start)
        {
            return 1;
        }
        else
        {
            return 0;
        }
    }
}


public sealed class ScalarKalmanFilter
{
    private double _estimate;
    private double _errorCovariance;
    public double Estimate => _estimate;
    private readonly double _processNoise;
    private readonly double _measurementNoise;

    public ScalarKalmanFilter(
        double initialEstimate,
        double initialErrorCovariance,
        double processNoise,
        double measurementNoise)
    {
        _estimate = initialEstimate;
        _errorCovariance = initialErrorCovariance;
        _processNoise = processNoise;
        _measurementNoise = measurementNoise;
    }

    public double Update(double measurement)
    {
        // Predict
        _errorCovariance += _processNoise;

        // Update
        double kalmanGain = _errorCovariance / (_errorCovariance + _measurementNoise);
        _estimate += kalmanGain * (measurement - _estimate);
        _errorCovariance *= 1.0 - kalmanGain;

        return _estimate;
    }
}