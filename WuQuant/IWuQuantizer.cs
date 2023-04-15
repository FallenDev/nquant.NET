using static System.Net.Mime.MediaTypeNames;

namespace WuQuant;

public interface IWuQuantizer
{
    Image<Rgba32> QuantizeImage(Image<Rgba32> image, int alphaThreshold, int alphaFader);
}