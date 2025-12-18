using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using furnace;

class Program
{
    static void Main(string[] args)
    {
        
        string ip = "192.168.168.222";
        var testInit = new FurnaceInit("PbMO4 furnace 1", 1, 502, ip, 1, ip, ip, 1, 1);

        string label = "PbMoO4";

        var profileHandler = new ProfileHandler();
        var furnaceScheduler = new FurnaceScheduler();
        Profile activeProfile;
        FurnaceSet testSet = default(FurnaceSet);
        
        Console.WriteLine("Setup started");

        foreach (var profile in profileHandler.profiles)
        {
            if (profile.Label == label)
            {
                activeProfile = profile;
                testSet.setProfile = activeProfile;
                Console.WriteLine(testSet.setProfile.Label);
            }
        }
        testSet.setpoint = 0;
        testSet.trim = 0;
        testSet.manualSetpoint = false;
        testSet.enable = true;
        testSet.state = ProcessState.Continue;
        
        furnaceScheduler.setValues[0] = testSet;
        Console.WriteLine("Setup finished");
        furnaceScheduler.Push();


        while (true)
        {
            furnaceScheduler.Pull();
            Console.WriteLine($"SP is {furnaceScheduler.stateValues[0].setpoint}");
        }

    }
}
