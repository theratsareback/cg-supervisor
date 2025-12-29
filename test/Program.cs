using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

using furnace;
using furnace.profile;
using furnace.eurotherm;
using furnace.camera;

class Program
{
    static void Main(string[] args)
    {
        
        string ip = "192.168.168.222";
        var testInit = new FurnaceInit("PbMO4 furnace 1", 1, 502, ip, 1, ip, ip, 1, 1);

        string label = "PbMoO4";

        var profileHandler = new ProfileHandler();
        var furnaceScheduler = new FurnaceScheduler();
        ProfileDef activeProfile;
        FurnaceSet testSet = default(FurnaceSet);
        

        foreach (var profileDef in profileHandler.profiles)
        {
            if (profileDef.Label == label)
            {
                activeProfile = profileDef;
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
        
        Capture cap = new Capture("invalid", 10294);

        while (true)
        {
            furnaceScheduler.Push();
            furnaceScheduler.Pull();
        }

    }
}
