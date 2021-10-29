namespace CodeSign.MachO
{
    public enum MachMagic : uint
    {
        MachHeaderLitteEndian = 0xcefaedfe,
        MachHeaderBigEndian = 0xfeedface,
        MachHeader64LitteEndian = 0xcffaedfe,
        MachHeader64BigEndian = 0xfeedfacf,
        FatMagicLittleEndian = 0xbebafeca,
        FatMagicBigEndian = 0xcafebabe,
    }
}