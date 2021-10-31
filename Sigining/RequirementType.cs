namespace CodeSign.Signing
{
    enum RequirementType : uint
    {
        Host = 1u,
        Guest,
        Designated,
        Library,
        Plugin,
        Invalid
    }
}
