namespace EtabsConnector;

internal sealed class ConnectorException : Exception
{
    public ConnectorException(string message) : base(message)
    {
    }

    public ConnectorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
