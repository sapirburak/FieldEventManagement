namespace FieldEventManagement.Core.Exceptions;

/// <summary>
/// מציין כי ניסוי לבצע מעבר מצב לא חוקי על אובייקט FieldEvent.
/// </summary>
public sealed class InvalidFieldEventStateException : Exception
{
    /// <summary>
    /// מאתחל חריגה חדשה עם הודעה מותאמת.
    /// </summary>
    /// <param name="message">הודעת השגיאה.</param>
    public InvalidFieldEventStateException(string message)
        : base(message)
    {
    }
}
