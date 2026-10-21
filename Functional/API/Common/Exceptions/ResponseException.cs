namespace API.Common.Exceptions;

public class ResponseException : Exception
{
    public ResponseException(string message) : base(message) { }
}
