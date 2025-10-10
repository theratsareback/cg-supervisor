using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using furnace;

class Program
{
    static void Main(string[] args)
    {
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

        testFurnace.Enable();
        while (true)
        {
            testFurnace.SetSetpoint(profileHandler.activeProfile.GetSetpoint());
        }
    }
}
