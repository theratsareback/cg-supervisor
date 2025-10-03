namespace furnace;

using System;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using NModbus;

public class StepperController
{
    private string _ipAddress;
    private int _port;
    private byte _pullerSlaveId;
    private byte _rotaterSlaveId;
    private TcpClient _tcpClient;
    private IModbusMaster _modbusMaster;

    public StepperController(string ipAddress, byte pullerSlaveId, byte rotaterSlaveId, int port = 502)
    {
        _ipAddress = ipAddress;
        _port = port;
        _pullerSlaveId = pullerSlaveId;
        _rotaterSlaveId = rotaterSlaveId;
    }

    public void SetRotSpeed(float speed)
    {
        return;
    }

    public void SetPullSpeed(float speed)
    {
        return;
    }
}