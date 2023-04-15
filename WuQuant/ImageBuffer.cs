//using System.Drawing;
//using System.Drawing.Imaging;

namespace WuQuant;

class ImageBuffer<TPixel> where TPixel : struct, IPixel<TPixel>
{
    public ImageBuffer(Image<TPixel> image)
    {
        Image = image;
    }

    public Image<TPixel> Image { get; private set; }

    public IEnumerable<Pixel[]> PixelLines
    {
        get
        {
            //var bitDepth = Image.PixelType.BitsPerPixel;
            //if (bitDepth != 32)
            //    throw new QuantizationException(string.Format("The image you are attempting to quantize does not contain a 32 bit ARGB palette. This image has a bit depth of {0} with {1} colors.", bitDepth, Image.Palette.Entries.Length));

            //TODO: check if 32 bit

            int width = Image.Width;
            int height = Image.Height;
            int[] buffer = new int[width];
            Pixel[] pixels = new Pixel[width];
            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                for(var i = 0; i < width; i++)
                {
                    Rgba32 rgba32 = default(Rgba32);
                    Image[rowIndex, i].ToRgba32(ref rgba32);
                    pixels[i] = new Pixel(rgba32.A, rgba32.R, rgba32.G, rgba32.B);
                }
                yield return pixels;
            }
        }
    }

    //public void UpdatePixelIndexes(List<byte[]> lineIndexes)
    //{
    //    int width = Image.Width;
    //    int height = Image.Height;
    //    var img = Image as Image<Gray8>;
    //    for (int rowIndex = 0; rowIndex < height; rowIndex++)
    //    {
    //        for(var i=0; i < width; i++)
    //        {
    //            img[rowIndex, i] = new Gray8(lineIndexes[rowIndex][i]);
    //        }
    //    }
    //}

    public void UpdatePixelIndexes(List<byte[]> lineIndexes, PaletteColorHistory[] paletteColorHistories)
    {
        int width = Image.Width;
        int height = Image.Height;
        var img = Image as Image<Rgba32>;
        for (int rowIndex = 0; rowIndex < height; rowIndex++)
        {
            for (var i = 0; i < width; i++)
            {
                var color = paletteColorHistories[lineIndexes[rowIndex][i]];
                if (color.Sum > 0)
                {
                    byte r = (byte)(color.Red / color.Sum);
                    byte g = (byte)(color.Green / color.Sum);
                    byte b = (byte)(color.Blue / color.Sum);
                    byte a = (byte)(color.Alpha / color.Sum);
                    img[rowIndex, i] = new Rgba32(r, g, b, a);
                }
            }
        }
    }
}