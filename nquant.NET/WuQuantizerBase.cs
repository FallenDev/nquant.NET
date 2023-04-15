namespace nquant.NET;

public class Histogram
{
    private const int SideSize = 33;
    internal readonly ColorMoment[,,,] Moments;

    public Histogram()
    {
        // 47,436,840 bytes
        Moments = new ColorMoment[SideSize, SideSize, SideSize, SideSize];
    }

    internal void Clear()
    {
        Array.Clear(Moments, 0, SideSize * SideSize * SideSize * SideSize);
    }
}

public abstract class WuQuantizerBase
{
    protected const byte AlphaColor = 255;
    private const int Alpha = 3;
    private const int Red = 2;
    private const int Green = 1;
    private const int Blue = 0;
    private const int SideSize = 33;
    private const int MaxSideIndex = 32;

    /// <summary>
    /// An Implementation of Wu's Color Quantization Algorithm
    /// Defaults Alpha Threshold to 10, and Alpha Fader to 70
    /// 
    /// alphaThreshold: This parameter sets a threshold value for the alpha channel.
    /// Any pixel in the image with an alpha value below this threshold will be considered as fully transparent.
    /// The value should be in the range of 0 to 255.
    /// Lower values will result in more transparent pixels, while higher values will result in fewer transparent pixels.
    ///
    /// alphaFader: This parameter is a multiplier that is applied to the alpha values of the quantized image.
    /// Values greater than 1 will increase the opacity of semi-transparent pixels, making them less transparent,
    /// while values less than 1 will decrease the opacity, making them more transparent.
    /// </summary>
    public Image QuantizeImage(Bitmap image)
    {
        return QuantizeImage(image, 10, 70);
    }

    /// <summary>
    /// An Implementation of Wu's Color Quantization Algorithm
    /// Defaults: alphaThreshold 10, alphaFader 70
    ///
    /// alphaThreshold: This parameter sets a threshold value for the alpha channel.
    /// Any pixel in the image with an alpha value below this threshold will be considered as fully transparent.
    /// The value should be in the range of 0 to 255.
    /// Lower values will result in more transparent pixels, while higher values will result in fewer transparent pixels.
    ///
    /// alphaFader: This parameter is a multiplier that is applied to the alpha values of the quantized image.
    /// Values greater than 1 will increase the opacity of semi-transparent pixels, making them less transparent,
    /// while values less than 1 will decrease the opacity, making them more transparent.
    /// </summary>
    public Image QuantizeImage(Bitmap image, int alphaThreshold, int alphaFader)
    {
        return QuantizeImage(image, alphaThreshold, alphaFader, null, 256);
    }

    /// <summary>
    /// An Implementation of Wu's Color Quantization Algorithm
    /// Defaults: alphaThreshold 10, alphaFader 70, histogram null, maxColors 256
    /// You may build your own Histogram and pass it, or let the library use defaults
    ///
    /// alphaThreshold: This parameter sets a threshold value for the alpha channel.
    /// Any pixel in the image with an alpha value below this threshold will be considered as fully transparent.
    /// The value should be in the range of 0 to 255.
    /// Lower values will result in more transparent pixels, while higher values will result in fewer transparent pixels.
    ///
    /// alphaFader: This parameter is a multiplier that is applied to the alpha values of the quantized image.
    /// Values greater than 1 will increase the opacity of semi-transparent pixels, making them less transparent,
    /// while values less than 1 will decrease the opacity, making them more transparent.
    /// </summary>
    public Image QuantizeImage(Bitmap image, int alphaThreshold, int alphaFader, Histogram histogram, int maxColors)
    {
        var buffer = new ImageBuffer(image);

        if (histogram == null)
            histogram = new Histogram();
        else
            histogram.Clear();

        BuildHistogram(histogram, buffer, alphaThreshold, alphaFader);
        CalculateMoments(histogram.Moments);
        var cubes = SplitData(ref maxColors, histogram.Moments);
        var lookups = BuildLookups(cubes, histogram.Moments);
        return GetQuantizedImage(buffer, maxColors, lookups, alphaThreshold);
    }

    private static void BuildHistogram(Histogram histogram, ImageBuffer sourceImage, int alphaThreshold, int alphaFader)
    {
        var moments = histogram.Moments;

        foreach (var pixelLine in sourceImage.PixelLines)
        {
            foreach (var pixel in pixelLine)
            {
                var pixelAlpha = pixel.Alpha;
                if (pixelAlpha <= alphaThreshold) continue;
                if (pixelAlpha < 255)
                {
                    var alpha = pixel.Alpha + (pixel.Alpha % alphaFader);
                    pixelAlpha = (byte)(alpha > 255 ? 255 : alpha);
                }

                var pixelRed = pixel.Red;
                var pixelGreen = pixel.Green;
                var pixelBlue = pixel.Blue;

                pixelAlpha = (byte)((pixelAlpha >> 3) + 1);
                pixelRed = (byte)((pixelRed >> 3) + 1);
                pixelGreen = (byte)((pixelGreen >> 3) + 1);
                pixelBlue = (byte)((pixelBlue >> 3) + 1);
                moments[pixelAlpha, pixelRed, pixelGreen, pixelBlue].Add(pixel);
            }
        }
    }

    private static void CalculateMoments(ColorMoment[,,,] moments)
    {
        var xarea = new ColorMoment[SideSize, SideSize];
        var area = new ColorMoment[SideSize];

        for (var alphaIndex = 1; alphaIndex < SideSize; alphaIndex++)
        {
            for (var redIndex = 1; redIndex < SideSize; redIndex++)
            {
                Array.Clear(area, 0, area.Length);
                for (var greenIndex = 1; greenIndex < SideSize; greenIndex++)
                {
                    var line = new ColorMoment();
                    for (var blueIndex = 1; blueIndex < SideSize; blueIndex++)
                    {
                        line.AddFast(ref moments[alphaIndex, redIndex, greenIndex, blueIndex]);
                        area[blueIndex].AddFast(ref line);
                        xarea[greenIndex, blueIndex].AddFast(ref area[blueIndex]);

                        var moment = moments[alphaIndex - 1, redIndex, greenIndex, blueIndex];
                        moment.AddFast(ref xarea[greenIndex, blueIndex]);
                        moments[alphaIndex, redIndex, greenIndex, blueIndex] = moment;
                    }
                }
            }
        }
    }

    private static ColorMoment Top(Box cube, int direction, int position, ColorMoment[,,,] moment)
    {
        return direction switch
        {
            Alpha => (moment[position, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] -
                      moment[position, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                      moment[position, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                      moment[position, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                     (moment[position, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                      moment[position, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                      moment[position, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                      moment[position, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]),
            Red => (moment[cube.AlphaMaximum, position, cube.GreenMaximum, cube.BlueMaximum] -
                    moment[cube.AlphaMaximum, position, cube.GreenMinimum, cube.BlueMaximum] -
                    moment[cube.AlphaMinimum, position, cube.GreenMaximum, cube.BlueMaximum] +
                    moment[cube.AlphaMinimum, position, cube.GreenMinimum, cube.BlueMaximum]) -
                   (moment[cube.AlphaMaximum, position, cube.GreenMaximum, cube.BlueMinimum] -
                    moment[cube.AlphaMaximum, position, cube.GreenMinimum, cube.BlueMinimum] -
                    moment[cube.AlphaMinimum, position, cube.GreenMaximum, cube.BlueMinimum] +
                    moment[cube.AlphaMinimum, position, cube.GreenMinimum, cube.BlueMinimum]),
            Green => (moment[cube.AlphaMaximum, cube.RedMaximum, position, cube.BlueMaximum] -
                      moment[cube.AlphaMaximum, cube.RedMinimum, position, cube.BlueMaximum] -
                      moment[cube.AlphaMinimum, cube.RedMaximum, position, cube.BlueMaximum] +
                      moment[cube.AlphaMinimum, cube.RedMinimum, position, cube.BlueMaximum]) -
                     (moment[cube.AlphaMaximum, cube.RedMaximum, position, cube.BlueMinimum] -
                      moment[cube.AlphaMaximum, cube.RedMinimum, position, cube.BlueMinimum] -
                      moment[cube.AlphaMinimum, cube.RedMaximum, position, cube.BlueMinimum] +
                      moment[cube.AlphaMinimum, cube.RedMinimum, position, cube.BlueMinimum]),
            Blue => (moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, position] -
                     moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, position] -
                     moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, position] +
                     moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, position]) -
                    (moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, position] -
                     moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, position] -
                     moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, position] +
                     moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, position]),
            _ => new ColorMoment()
        };
    }

    private static ColorMoment Bottom(Box cube, int direction, ColorMoment[,,,] moment)
    {
        return direction switch
        {
            Alpha => (-moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] +
                      moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                      moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                      moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                     (-moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] +
                      moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                      moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                      moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]),
            Red => (-moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] +
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                   (-moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                    moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] +
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                    moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]),
            Green => (-moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                      moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] +
                      moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                      moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -
                     (-moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                      moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] +
                      moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                      moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]),
            Blue => (-moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] +
                     moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                     moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                     moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]) -
                    (-moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] +
                     moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                     moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] -
                     moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]),
            _ => new ColorMoment()
        };
    }

    private static CubeCut Maximize(ColorMoment[,,,] moments, Box cube, int direction, byte first, byte last, ColorMoment whole)
    {
        var bottom = Bottom(cube, direction, moments);
        var result = 0.0f;
        byte? cutPoint = null;

        for (var position = first; position < last; ++position)
        {
            var half = bottom + Top(cube, direction, position, moments);
            if (half.Weight == 0) continue;

            var temp = half.WeightedDistance();

            half = whole - half;
            if (half.Weight == 0) continue;
            temp += half.WeightedDistance();

            if (!(temp > result)) continue;
            result = temp;
            cutPoint = position;
        }

        return new CubeCut(cutPoint, result);
    }

    private static bool Cut(ColorMoment[,,,] moments, ref Box first, ref Box second)
    {
        int direction;
        var whole = Volume(first, moments);
        var maxAlpha = Maximize(moments, first, Alpha, (byte)(first.AlphaMinimum + 1), first.AlphaMaximum, whole);
        var maxRed = Maximize(moments, first, Red, (byte)(first.RedMinimum + 1), first.RedMaximum, whole);
        var maxGreen = Maximize(moments, first, Green, (byte)(first.GreenMinimum + 1), first.GreenMaximum, whole);
        var maxBlue = Maximize(moments, first, Blue, (byte)(first.BlueMinimum + 1), first.BlueMaximum, whole);

        if ((maxAlpha.Value >= maxRed.Value) && (maxAlpha.Value >= maxGreen.Value) && (maxAlpha.Value >= maxBlue.Value))
        {
            direction = Alpha;
            if (maxAlpha.Position == null) return false;
        }
        else if ((maxRed.Value >= maxAlpha.Value) && (maxRed.Value >= maxGreen.Value) && (maxRed.Value >= maxBlue.Value))
            direction = Red;
        else
        {
            if ((maxGreen.Value >= maxAlpha.Value) && (maxGreen.Value >= maxRed.Value) && (maxGreen.Value >= maxBlue.Value))
                direction = Green;
            else
                direction = Blue;
        }

        second.AlphaMaximum = first.AlphaMaximum;
        second.RedMaximum = first.RedMaximum;
        second.GreenMaximum = first.GreenMaximum;
        second.BlueMaximum = first.BlueMaximum;

        switch (direction)
        {
            case Alpha:
                second.AlphaMinimum = first.AlphaMaximum = (byte)maxAlpha.Position;
                second.RedMinimum = first.RedMinimum;
                second.GreenMinimum = first.GreenMinimum;
                second.BlueMinimum = first.BlueMinimum;
                break;

            case Red:
                second.RedMinimum = first.RedMaximum = (byte)maxRed.Position;
                second.AlphaMinimum = first.AlphaMinimum;
                second.GreenMinimum = first.GreenMinimum;
                second.BlueMinimum = first.BlueMinimum;
                break;

            case Green:
                second.GreenMinimum = first.GreenMaximum = (byte)maxGreen.Position;
                second.AlphaMinimum = first.AlphaMinimum;
                second.RedMinimum = first.RedMinimum;
                second.BlueMinimum = first.BlueMinimum;
                break;

            case Blue:
                second.BlueMinimum = first.BlueMaximum = (byte)maxBlue.Position;
                second.AlphaMinimum = first.AlphaMinimum;
                second.RedMinimum = first.RedMinimum;
                second.GreenMinimum = first.GreenMinimum;
                break;
        }

        first.Size = (first.AlphaMaximum - first.AlphaMinimum) * (first.RedMaximum - first.RedMinimum) * (first.GreenMaximum - first.GreenMinimum) * (first.BlueMaximum - first.BlueMinimum);
        second.Size = (second.AlphaMaximum - second.AlphaMinimum) * (second.RedMaximum - second.RedMinimum) * (second.GreenMaximum - second.GreenMinimum) * (second.BlueMaximum - second.BlueMinimum);

        return true;
    }

    private static float CalculateVariance(ColorMoment[,,,] moments, Box cube)
    {
        var volume = Volume(cube, moments);
        return volume.Variance();
    }

    private static ColorMoment Volume(Box cube, ColorMoment[,,,] moment)
    {
        return (moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] -
                moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] -
                moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] +
                moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum] -
                moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMaximum] +
                moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMaximum] +
                moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMaximum] -
                moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMaximum]) -

               (moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMaximum, cube.BlueMinimum] -
                moment[cube.AlphaMaximum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] +
                moment[cube.AlphaMinimum, cube.RedMaximum, cube.GreenMinimum, cube.BlueMinimum] -
                moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMaximum, cube.BlueMinimum] +
                moment[cube.AlphaMaximum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum] -
                moment[cube.AlphaMinimum, cube.RedMinimum, cube.GreenMinimum, cube.BlueMinimum]);
    }

    private static Box[] SplitData(ref int colorCount, ColorMoment[,,,] moments)
    {
        --colorCount;
        var next = 0;
        var volumeVariance = new float[colorCount];
        var cubes = new Box[colorCount];
        cubes[0].AlphaMaximum = MaxSideIndex;
        cubes[0].RedMaximum = MaxSideIndex;
        cubes[0].GreenMaximum = MaxSideIndex;
        cubes[0].BlueMaximum = MaxSideIndex;
        for (var cubeIndex = 1; cubeIndex < colorCount; ++cubeIndex)
        {
            if (Cut(moments, ref cubes[next], ref cubes[cubeIndex]))
            {
                volumeVariance[next] = cubes[next].Size > 1 ? CalculateVariance(moments, cubes[next]) : 0.0f;
                volumeVariance[cubeIndex] = cubes[cubeIndex].Size > 1 ? CalculateVariance(moments, cubes[cubeIndex]) : 0.0f;
            }
            else
            {
                volumeVariance[next] = 0.0f;
                cubeIndex--;
            }

            next = 0;
            var temp = volumeVariance[0];

            for (var index = 1; index <= cubeIndex; ++index)
            {
                if (volumeVariance[index] <= temp) continue;
                temp = volumeVariance[index];
                next = index;
            }

            if (temp > 0.0) continue;
            colorCount = cubeIndex + 1;
            break;
        }
        return cubes.Take(colorCount).ToArray();
    }

    private static Pixel[] BuildLookups(Box[] cubes, ColorMoment[,,,] moments)
    {
        var lookups = new Pixel[cubes.Length];

        for (var cubeIndex = 0; cubeIndex < cubes.Length; cubeIndex++)
        {
            var volume = Volume(cubes[cubeIndex], moments);

            if (volume.Weight <= 0) continue;

            var lookup = new Pixel
            {
                Alpha = (byte)(volume.Alpha / volume.Weight),
                Red = (byte)(volume.Red / volume.Weight),
                Green = (byte)(volume.Green / volume.Weight),
                Blue = (byte)(volume.Blue / volume.Weight)
            };
            lookups[cubeIndex] = lookup;
        }
        return lookups;
    }

    protected abstract Image GetQuantizedImage(ImageBuffer image, int colorCount, Pixel[] lookups, int alphaThreshold);
}