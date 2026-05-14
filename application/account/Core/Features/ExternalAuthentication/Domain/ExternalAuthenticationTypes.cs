using JetBrains.Annotations;

namespace Account.Features.ExternalAuthentication.Domain;

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExternalProviderType
{
    Google,
    Facebook
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExternalLoginType
{
    Login,
    Signup,
    Link
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExternalLoginResult
{
    Success,
    IdentityProviderError,
    InvalidState,
    LoginReplayDetected,
    SessionNotFound,
    FlowIdMismatch,
    SessionHijackingDetected,
    LoginExpired,
    LoginAlreadyCompleted,
    CodeExchangeFailed,
    NonceMismatch,
    IdentityMismatch,
    UserNotFound,
    AccountAlreadyExists
}
