namespace furnace;
using System;
using System.Net.Sockets;
using System.Threading;
using NModbus;

public class Eurotherm
{
    private string _ipAddress;
    private int _port;
    private byte _slaveId;
    private TcpClient _tcpClient;
    private IModbusMaster _modbusMaster;

    public Heater Heater { get; private set; }
    public List<Alarm> alarms;
    public Alarm OverrangeAlarm { get; private set; }
    public Alarm UnderrangeAlarm { get; private set; }
    public Alarm SensorBreakAlarm { get; private set; }
    public Alarm RemoteSetpointFailureAlarm { get; private set; }

    public Eurotherm(string ipAddress, int port = 502)
    {
        _ipAddress = ipAddress;
        _port = port;
        _slaveId = 255; // slave id can be anything in 0-255 and still work
    }

    public void Connect()
    {
        _tcpClient = new TcpClient();
        _tcpClient.Connect(_ipAddress, _port);
        var factory = new ModbusFactory();
        _modbusMaster = factory.CreateMaster(_tcpClient);

        Heater = new Heater(_modbusMaster, _slaveId);

        OverrangeAlarm = new Alarm(_modbusMaster, _slaveId, 2113);
        UnderrangeAlarm = new Alarm(_modbusMaster, _slaveId, 2137);
        SensorBreakAlarm = new Alarm(_modbusMaster, _slaveId, 2161);
        RemoteSetpointFailureAlarm = new Alarm(_modbusMaster, _slaveId, 2209);

        alarms = [OverrangeAlarm, UnderrangeAlarm, SensorBreakAlarm, RemoteSetpointFailureAlarm];
    }

    public void Disconnect()
    {
        _tcpClient?.Close();
    }
}

public class Heater
{
    private readonly IModbusMaster _modbusMaster;
    private readonly byte _slaveId;

    public Heater(IModbusMaster modbusMaster, byte slaveId)
    {
        _modbusMaster = modbusMaster;
        _slaveId = slaveId;
    }

    public float PV()
    {
        ushort[] registers = _modbusMaster.ReadHoldingRegisters(_slaveId, 1, 2);
        return registers[0] / 10.0f;
    }

    public void SP(double setpoint)
    {
        setpoint *= 10.0; // Eurotherm considers an input of 123 to be 12.3, 1234 to be 123.4, etc.

        try
        {
            _modbusMaster.WriteSingleRegister(_slaveId, 277, (ushort)setpoint);
        }
        catch (SlaveException ex) when (ex.FunctionCode == 144 && ex.SlaveExceptionCode == 2)
        {
            Thread.Sleep(10);
            ushort[] result = _modbusMaster.ReadHoldingRegisters(_slaveId, 2, 2);
            double delta = Math.Abs(result[0] - setpoint);
            if (delta > 0.25f)
            {
                Console.WriteLine($"Warning: Setpoint not successfully written. Error of {delta}");
            }
        }
    }

    public void Enable()
    {
        WriteHeaterControl(1);
    }

    public void Disable()
    {
        WriteHeaterControl(0);
    }

    public FurnaceStatus CheckHeaterStatus()
    {
        ushort[] alarminterlocks = _modbusMaster.ReadHoldingRegisters(_slaveId, 8120, 1);
        if (alarminterlocks[0] == 1) { return FurnaceStatus.Alarm; }
        
        ushort[] enableinterlocks = _modbusMaster.ReadHoldingRegisters(_slaveId, 1990, 1);
        if (enableinterlocks[0] == 1) { return FurnaceStatus.Disabled; }

        else { return FurnaceStatus.Enabled; }
    }

    private void WriteHeaterControl(ushort value)
    {
        try
        {
            _modbusMaster.WriteSingleRegister(_slaveId, 2050, value);
        }
        catch (SlaveException ex) when (ex.FunctionCode == 144 && ex.SlaveExceptionCode == 2)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}

public class Alarm
{
    private readonly IModbusMaster _modbusMaster;
    private readonly byte _slaveId;
    private readonly ushort _registerAddress;

    public Alarm(IModbusMaster modbusMaster, byte slaveId, ushort registerAddress)
    {
        _modbusMaster = modbusMaster;
        _slaveId = slaveId;
        _registerAddress = registerAddress;
    }

    public AlarmStatus GetStatus()
    {
        return (AlarmStatus)_modbusMaster.ReadHoldingRegisters(_slaveId, _registerAddress, 1)[0];
        //0 for off, 1 for active but acknowledged, 2 for inactive not acknowledged, 3 for active not acknowledged
    }

    public void Acknowledge()
    {
        try
        {
            _modbusMaster.WriteSingleRegister(_slaveId, (ushort)(_registerAddress+13), 1);
        }
        catch (SlaveException ex) when (ex.FunctionCode == 144 && ex.SlaveExceptionCode == 2)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
