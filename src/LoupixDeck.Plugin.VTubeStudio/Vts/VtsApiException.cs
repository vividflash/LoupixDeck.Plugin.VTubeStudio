namespace LoupixDeck.Plugin.VTubeStudio.Vts;

/// <summary>VTube Studio answered a request with an <c>APIError</c> message.</summary>
internal sealed class VtsApiException : Exception
{
    internal int ErrorId { get; }

    internal VtsApiException(int errorId, string message) : base(message)
    {
        ErrorId = errorId;
    }
}
