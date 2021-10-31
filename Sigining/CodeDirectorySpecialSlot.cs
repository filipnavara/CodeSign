namespace CodeSign.Signing
{
    enum CodeDirectorySpecialSlot
    {
        InfoPlist = 1,
        Requirements = 2,
        ResourceDirectory = 3,
        TopDirectory = 4,
        Entitlements = 5,
        RepresentationSpecific = 6,
        EntitlementsDer = 7,

        CodeDirectory = 0,
        CodeDirectory2 = 0x1000,
        CmsWrapper = 0x10000,
    }
}
