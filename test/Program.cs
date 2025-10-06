using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using furnace;

class Program
{
    static void Main(string[] args)
    {
        // string ip = "192.168.168.222"; // find a way to do this dynamically
        // int port = 502;
        // byte slaveId = 250;

        // // var eurotherm = new Eurotherm(ip, port);
        // // eurotherm.Connect();
        // // eurotherm.Heater.Enable();
        // // eurotherm.Disconnect();
        // var furnace1 = new Furnace(1, "PbMO4 furnace 1", 502, ip, 1, ip, ip, 1, 1);
        // furnace1.Enable();
        // while (true)
        // {
        //     float pv = furnace1.GetProcessValue();
        //     furnace1.SetSetpoint(pv + 50.0f);
        // }

        // // while (true)
        // // {
        // //     try
        // //     {
        // //         eurotherm.Connect();
        // //         float pv = eurotherm.Heater.PV();

        // //         float newSetpoint = pv + 10.0f;
        // //         eurotherm.Heater.SP(newSetpoint);

        // //         Thread.Sleep(50);

        // //     }
        // //     catch (Exception ex)
        // //     {
        // //         Console.WriteLine("Error: " + ex.Message);
        // //     }
        // //     finally
        // //     {
        // //         eurotherm.Disconnect();
        // //     }
        // // }

        string ip = "192.168.168.222";
        var testFurnace = new Furnace(1, "PbMO4 furnace 1", 502, ip, 1, ip, ip, 1, 1);

        string label = "PbMoO4";

        var profileHandler = new ProfileHandler();

        foreach (var profile in profileHandler.profiles)
        {
            if (profile.Label == label)
            {
                profileHandler.Select(profile);
            }
        }
        
        profileHandler.Stop();
        profileHandler.Start();

        //testFurnace.Enable();
        while (true)
        {
            testFurnace.SetSetpoint(profileHandler.activeProfile.GetSetpoint());
        }
    }
}
