using OpenCvSharp;

namespace furnace.camera;

public class ImageProcessor : IDisposable
{
    private Point estimate;
    private readonly int width;
    private readonly int height;
    private readonly Mat weightMap;

    public ImageProcessor(Size imageSize, double sigma = 50.0, double strength = 0.2)
    {

        width = imageSize.Width;
        height = imageSize.Height;

        int mapWidth = 2 * width - 1;
        int mapHeight = 2 * height - 1;

        int mapCenterX = width - 1;
        int mapCenterY = height - 1;

        weightMap = new Mat(
            mapHeight,
            mapWidth,
            MatType.CV_32FC1);

        double denominator = 2.0 * sigma * sigma;

        for (int y = 0; y < mapHeight; y++)
        {
            double dy = y - mapCenterY;

            for (int x = 0; x < mapWidth; x++)
            {
                double dx = x - mapCenterX;

                double gaussian = Math.Exp(
                    -(dx * dx + dy * dy) / denominator);

                float weight = (float)(
                    1.0 - strength + strength * gaussian);

                weightMap.Set(y, x, weight);
            }
        }
    }

    public Point FindBrightestPoint(Mat input)
    {
        using var imFloat = new Mat();
        using var weighted = new Mat();

        input.ConvertTo(imFloat, MatType.CV_32FC1);

        // shift the weight map
        int roiX = width - 1 - estimate.X;
        int roiY = height - 1 - estimate.Y;

        var roiRect = new Rect(roiX, roiY, width, height);

        // create cropped view of weight map inside rectangle of interest
        using var weights = new Mat(weightMap, roiRect);

        Cv2.Multiply(imFloat, weights, weighted);

        Cv2.MinMaxLoc(
            weighted,
            out _,
            out _,
            out _,
            out Point brightestPoint);

        estimate = brightestPoint;

        return brightestPoint;
    }

    public ValueTask ProcessFrameAsync(Mat bgrFrame, FrameInfo frameInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Console.WriteLine(
            $"Frame {frameInfo.Sequence}: " +
            $"{bgrFrame.Width}x{bgrFrame.Height}, " +
            $"received {frameInfo.ReceivedAtUtc:O}");

        var gray = new Mat();
        Cv2.CvtColor(bgrFrame, gray, ColorConversionCodes.BGR2GRAY);

        Console.WriteLine(FindBrightestPoint(gray));

        return ValueTask.CompletedTask;
    }


    public void Dispose()
    {
        weightMap.Dispose();
    }
}