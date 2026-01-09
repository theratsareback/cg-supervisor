using furnace;

class Program
{
    static void Main(string[] args)
    {
        string label = "PbMoO4";

        var coordinator = new Coordinator();
        ProfileDef activeProfile;
        FurnaceSet testSet = default(FurnaceSet);
        

        foreach (var profileDef in coordinator.GetProfileDefs())
        {
            if (profileDef.Label == label)
            {
                activeProfile = profileDef;
                coordinator.SetFurnaceProfile(0, activeProfile);
            }
        }
        
        testSet.setpoint = 0;
        testSet.trim = 0;
        testSet.manualSetpoint = false;
        testSet.enable = true;
        
        coordinator.SetSetValues(0, testSet);
        
        //Capture cap = new Capture("invalid", 10294);

        while (true)
        {
            coordinator.Update();
        }

    }
}