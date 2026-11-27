namespace MiniErp.Domain;

public enum FailureKind
{
    Invalid,
    Missing,
    Conflict,
    Forbidden
}

public sealed class ErpException(FailureKind kind, string code, string message) : Exception(message)
{
    public FailureKind Kind { get; } = kind;
    public string Code { get; } = code;

    public static ErpException Invalid(string code, string message) => new(FailureKind.Invalid, code, message);

    public static ErpException Missing(string code, string message) => new(FailureKind.Missing, code, message);

    public static ErpException Conflict(string code, string message) => new(FailureKind.Conflict, code, message);

    public static ErpException Forbidden(string code, string message) => new(FailureKind.Forbidden, code, message);
}
