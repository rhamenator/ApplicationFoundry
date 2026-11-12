using System.ComponentModel.DataAnnotations;

namespace ApplicationFoundry.Data;

public sealed class OptionalEmailAddressAttribute : ValidationAttribute
{
    private readonly EmailAddressAttribute email = new();
    public override bool IsValid(object? value) => value is null or "" || email.IsValid(value);
}

public sealed class OptionalPhoneAttribute : ValidationAttribute
{
    private readonly PhoneAttribute phone = new();
    public override bool IsValid(object? value) => value is null or "" || phone.IsValid(value);
}
