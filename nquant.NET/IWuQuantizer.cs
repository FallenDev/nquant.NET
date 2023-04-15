namespace nquant.NET;

public interface IWuQuantizer
{
    Image QuantizeImage(Bitmap image, int alphaThreshold, int alphaFader);
}