namespace CodeSign.MachO
{
    public enum LoadCommandType : uint
    {
        Segment = 0x1,
        SymbolTable = 0x2,
        SymbolSegment = 0x3,
        Thread = 0x4,
        UnixThread = 0x5,
        LoadFixedVMSharedLibrary = 0x6,
        IdFixedVMSharedLibrary = 0x7,
        ObjectIdenfication = 0x8,
        FixedVMFileInclusion = 0x9,
        Prepage = 0xa,
        DynamicLinkEditSymbolTable = 0xb,
        LoadDylib = 0xc,
        IdDylib = 0xd,
        LoadDylinker = 0xe,
        IdDylinker = 0xf,
        PreboundDylib = 0x10,
        Routines = 0x11,
        SubFramework = 0x12,
        SubUmbrella = 0x13,
        SubClient = 0x14,
        SubLibrary = 0x15,
        TowLevelHints = 0x16,
        PrebindChecksum = 0x17,
        LoadWeakDylib = 0x18 | ReqDyld,
        Segment64 = 0x19,
        Routines64 = 0x1a,
        Uuid = 0x1b,
        Rpath = 0x1c | ReqDyld,
        CodeSignature = 0x1d,
        SegmentSplitInfo = 0x1e,
        ReexportDylib = 0x1f | ReqDyld,
        LazyLoadDylib = 0x20,
        EncryptionInfo = 0x21,
        DyldInfo = 0x22,
        DyldInfoOnly = 0x22 | ReqDyld,
        LoadUpwardDylib = 0x23 | ReqDyld,
        VersionMinMacOS = 0x24,
        VersionMinIPhoneOS = 0x25,
        FunctionStarts = 0x26,
        DyldEnvironment = 0x27,
        Main = 0x28 | ReqDyld,
        DataInCode = 0x29,
        SourceVersion = 0x2a,
        DylibCodeSigningDRs = 0x2b,
        EncryptionInfo64 = 0x2c,
        LinkerOption = 0x2d,
        LinkerOptimizationHint = 0x2e,
        VersionMinTvOS = 0x2f,
        VersionMinWatchOS = 0x30,
        Note = 0x31,
        BuildVersion = 0x32,
        DyldExportsTrie = 0x33 | ReqDyld,
        DyldChainedFixups = 0x34 | ReqDyld,
        FileSetEntry = 0x35,

        /// <summary>Flag that marks any command that needs to be understood by DyLD or load will fail</summary>
        ReqDyld = 0x80000000,
    }
}