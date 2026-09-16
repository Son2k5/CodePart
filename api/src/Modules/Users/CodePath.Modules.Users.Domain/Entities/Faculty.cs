using CodePath.Domain.Common;

namespace CodePath.Modules.Users.Domain.Entities;

public sealed class Faculty : BaseEntity
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;

    public ICollection<Class> Classes { get; private set; } = new List<Class>();
    public ICollection<User> Users { get; private set; } = new List<User>();

    private Faculty() { }

    public static Faculty Create(string name, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new Faculty
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant()
        };
    }

    public void Update(string name, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
        Touch();
    }
}
