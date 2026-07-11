namespace FieldEventManagement.Core.Exceptions;

/// <summary>
/// Indicates that an attempt was made to perform an invalid state transition on a FieldEvent object.
/// </summary>
public sealed class InvalidFieldEventStateException : Exception
{
    /// <summary>
    /// Initializes a new exception with a custom message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public InvalidFieldEventStateException(string message)
        : base(message)
    {
    }
}
