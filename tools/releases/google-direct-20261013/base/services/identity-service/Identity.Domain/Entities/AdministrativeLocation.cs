using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

public sealed class Province : Entity<string>
{
    public string Name { get; private set; } = string.Empty;

    private Province() { }

    private Province(string id, string name) : base(id)
    {
        Name = name;
    }

    public static Province Create(string id, string name) => new(id, name);
}

public sealed class District : Entity<string>
{
    public string ProvinceId { get; private set; } = string.Empty;
    public Province Province { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;

    private District() { }

    private District(string id, string provinceId, string name) : base(id)
    {
        ProvinceId = provinceId;
        Name = name;
    }

    public static District Create(string id, string provinceId, string name) => new(id, provinceId, name);
}
