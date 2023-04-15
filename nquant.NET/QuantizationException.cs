namespace nquant.NET;

[Serializable]
public class QuantizationException : ApplicationException
{
    public QuantizationException(string message) : base(message)
    {

    }
}