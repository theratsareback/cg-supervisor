namespace furnace.camera;

using System;
using System.Collections.Generic;
using Basler.Pylon;

public class BaslerCapture()
{
    public void Start()
    {
        List<ICameraInfo> cameras = CameraFinder.Enumerate(DeviceType.GigE);

        if (cameras.Count > 0)
        {
            using (Camera camera = new Camera(cameras[0]))
            {
                camera.Open();

                Console.WriteLine("Camera info: ", camera.CameraInfo);

                camera.Close();
            }
        }
    }
    
}