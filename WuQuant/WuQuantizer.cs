namespace WuQuant;

public class WuQuantizer : WuQuantizerBase, IWuQuantizer
{
    private static List<byte[]> IndexedPixels(ImageBuffer<Rgba32> image, Pixel[] lookups, int alphaThreshold, PaletteColorHistory[] paletteHistogram)
    {
        var result = new List<byte[]>();
            
        var lookup = new PaletteLookup(lookups);
        foreach (var pixelLine in image.PixelLines)
        {
            var lineIndexes = new byte[image.Image.Width];
            for (int pixelIndex = 0; pixelIndex < pixelLine.Length; pixelIndex++)
            {
                Pixel pixel = pixelLine[pixelIndex];
                byte bestMatch = AlphaColor;
                if (pixel.Alpha > alphaThreshold)
                {
                    bestMatch = lookup.GetPaletteIndex(pixel);
                    paletteHistogram[bestMatch].AddPixel(pixel);
                }
                lineIndexes[pixelIndex] = bestMatch;
            }
            result.Add(lineIndexes);
        }
        return result;
    }

    internal override Image<Rgba32> GetQuantizedImage(ImageBuffer<Rgba32> image, int colorCount, Pixel[] lookups, int alphaThreshold)
    {
        var result = new Image<Rgba32>(image.Image.Width, image.Image.Height);
        var resultBuffer = new ImageBuffer<Rgba32>(result);
        var paletteHistogram = new PaletteColorHistory[colorCount + 1];
        var pixels = IndexedPixels(image, lookups, alphaThreshold, paletteHistogram);
        resultBuffer.UpdatePixelIndexes(pixels, paletteHistogram);
        //result.PixelType.Palette = BuildPalette(result.Palette, paletteHistogram);
        return result;
    }

    //private static ColorPalette BuildPalette(ColorPalette palette, PaletteColorHistory[] paletteHistogram)
    //{
    //    for (int paletteColorIndex = 0; paletteColorIndex < paletteHistogram.Length; paletteColorIndex++)
    //    {
    //        palette.Entries[paletteColorIndex] = paletteHistogram[paletteColorIndex].ToNormalizedColor();
    //    }
    //    return palette;
    //}
}