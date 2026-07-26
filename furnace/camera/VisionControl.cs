using OpenCvSharp;

namespace furnace.camera;

public static class ImageProcessor
{
    public static ValueTask ProcessFrameAsync(
        Mat bgrFrame,
        FrameInfo frameInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine("Frame captured");

        Console.WriteLine(
            $"Frame {frameInfo.Sequence}: " +
            $"{bgrFrame.Width}x{bgrFrame.Height}, " +
            $"received {frameInfo.ReceivedAtUtc:O}");

        return ValueTask.CompletedTask;
    }
}