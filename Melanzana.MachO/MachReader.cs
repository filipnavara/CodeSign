using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Melanzana.MachO.BinaryFormat;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public static class MachReader
    {
        private static MachSegment ReadSegment(ReadOnlySpan<byte> loadCommandPtr, bool isLittleEndian, bool isObjectFile, Stream stream)
        {
            var segmentHeader = SegmentHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);

            var machSegment = segmentHeader.NumberOfSections == 0 && segmentHeader.FileSize != 0 ? 
                new MachSegment(stream.Slice(segmentHeader.FileOffset, segmentHeader.FileSize)) :
                new MachSegment();
            machSegment.FileOffset = segmentHeader.FileOffset;
            machSegment.OriginalFileSize = segmentHeader.FileSize;
            machSegment.IsObjectFile = isObjectFile;
            machSegment.Name = segmentHeader.Name;
            machSegment.VirtualAddress = segmentHeader.Address;
            machSegment.Size = segmentHeader.Size;
            machSegment.MaximumProtection = segmentHeader.MaximumProtection;
            machSegment.InitialProtection = segmentHeader.InitialProtection;
            machSegment.Flags = segmentHeader.Flags;

            for (int s = 0; s < segmentHeader.NumberOfSections; s++)
            {
                var sectionHeader = SectionHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize + SegmentHeader.BinarySize + s * SectionHeader.BinarySize), isLittleEndian, out var _);
                var sectionType = (MachSectionType)(sectionHeader.Flags & 0xff);
                MachSection section;

                if (sectionHeader.Size != 0 &&
                    sectionType != MachSectionType.ZeroFill &&
                    sectionType != MachSectionType.GBZeroFill &&
                    sectionType != MachSectionType.ThreadLocalZeroFill)
                {
                    section = new MachSection(stream.Slice(sectionHeader.FileOffset, sectionHeader.Size));
                }
                else
                {
                    section = new MachSection { Size = sectionHeader.Size };
                }

                section.SectionName = sectionHeader.SectionName;
                section.SegmentName = sectionHeader.SegmentName;
                section.VirtualAddress = sectionHeader.Address;
                section.FileOffset = sectionHeader.FileOffset;
                section.Log2Alignment = sectionHeader.Log2Alignment;
                section.RelocationOffset = sectionHeader.RelocationOffset;
                section.NumberOfReloationEntries = sectionHeader.NumberOfReloationEntries;
                section.Flags = sectionHeader.Flags;
                section.Reserved1 = sectionHeader.Reserved1;
                section.Reserved2 = sectionHeader.Reserved2;
                machSegment.Sections.Add(section);
            }

            return machSegment;
        }

        private static MachSegment ReadSegment64(ReadOnlySpan<byte> loadCommandPtr, bool isLittleEndian, Stream stream)
        {
            var segmentHeader = Segment64Header.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);

            var machSegment = segmentHeader.NumberOfSections == 0 && segmentHeader.FileSize != 0 ? 
                new MachSegment(stream.Slice((long)segmentHeader.FileOffset, (long)segmentHeader.FileSize)) :
                new MachSegment();
            machSegment.FileOffset = segmentHeader.FileOffset;
            machSegment.OriginalFileSize = segmentHeader.FileSize;
            machSegment.Name = segmentHeader.Name;
            machSegment.VirtualAddress = segmentHeader.Address;
            machSegment.Size = segmentHeader.Size;
            machSegment.MaximumProtection = segmentHeader.MaximumProtection;
            machSegment.InitialProtection = segmentHeader.InitialProtection;
            machSegment.Flags = segmentHeader.Flags;

            for (int s = 0; s < segmentHeader.NumberOfSections; s++)
            {
                var sectionHeader = Section64Header.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize + Segment64Header.BinarySize + s * Section64Header.BinarySize), isLittleEndian, out var _);
                var sectionType = (MachSectionType)(sectionHeader.Flags & 0xff);
                MachSection section;

                if (sectionHeader.Size != 0 &&
                    sectionType != MachSectionType.ZeroFill &&
                    sectionType != MachSectionType.GBZeroFill &&
                    sectionType != MachSectionType.ThreadLocalZeroFill)
                {
                    section = new MachSection(stream.Slice(sectionHeader.FileOffset, (long)sectionHeader.Size));
                }
                else
                {
                    section = new MachSection { Size = sectionHeader.Size };
                }

                section.SectionName = sectionHeader.SectionName;
                section.SegmentName = sectionHeader.SegmentName;
                section.VirtualAddress = sectionHeader.Address;
                section.FileOffset = sectionHeader.FileOffset;
                section.Log2Alignment = sectionHeader.Log2Alignment;
                section.RelocationOffset = sectionHeader.RelocationOffset;
                section.NumberOfReloationEntries = sectionHeader.NumberOfReloationEntries;
                section.Flags = sectionHeader.Flags;
                section.Reserved1 = sectionHeader.Reserved1;
                section.Reserved2 = sectionHeader.Reserved2;
                section.Reserved3 = sectionHeader.Reserved3;
                machSegment.Sections.Add(section);
            }

            return machSegment;
        }

        private static T ReadLinkEdit<T>(ReadOnlySpan<byte> loadCommandPtr, bool isLittleEndian)
            where T : MachLinkEdit, new()
        {
            var linkEditHeader = LinkEditHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);
            return new T
            {
                FileOffset = linkEditHeader.FileOffset,
                FileSize = linkEditHeader.FileSize,
            };
        }

        private static T ReadDylibCommand<T>(ReadOnlySpan<byte> loadCommandPtr, uint commandSize, bool isLittleEndian)
            where T : MachDylibCommand, new()
        {
            var dylibCommandHeader = DylibCommandHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);

            Debug.Assert(dylibCommandHeader.NameOffset == LoadCommandHeader.BinarySize + DylibCommandHeader.BinarySize);
            var nameSlice = loadCommandPtr.Slice((int)dylibCommandHeader.NameOffset, (int)commandSize - (int)dylibCommandHeader.NameOffset);
            int zeroIndex = nameSlice.IndexOf((byte)0);
            string name = zeroIndex >= 0 ? Encoding.UTF8.GetString(nameSlice.Slice(0, zeroIndex)) : Encoding.UTF8.GetString(nameSlice);

            return new T
            {
                Name = name,
                Timestamp = dylibCommandHeader.Timestamp,
                CurrentVersion = dylibCommandHeader.CurrentVersion,
                CompatibilityVersion = dylibCommandHeader.CompatibilityVersion,
            };
        }

        private static MachEntrypointCommand ReadMainCommand(ReadOnlySpan<byte> loadCommandPtr, bool isLittleEndian)
        {
            var mainCommandHeader = MainCommandHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);

            return new MachEntrypointCommand
            {
                FileOffset = mainCommandHeader.FileOffset,
                StackSize = mainCommandHeader.StackSize,
            };
        }

        private static Version ConvertVersion(uint version)
            => new Version((int)(version >> 16), (int)((version >> 8) & 0xff), (int)(version & 0xff));

        private static T ReadVersionMinCommand<T>(ReadOnlySpan<byte> loadCommandPtr, bool isLittleEndian)
            where T : MachBuildVersionBase, new()
        {
            var versionMinCommandHeader = VersionMinCommandHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);

            return new T
            {
                MinimumPlatformVersion = ConvertVersion(versionMinCommandHeader.MinimumPlatformVersion),
                SdkVersion = ConvertVersion(versionMinCommandHeader.SdkVersion),
            };
        }

        private static MachBuildVersion ReadBuildVersion(ReadOnlySpan<byte> loadCommandPtr, bool isLittleEndian)
        {
            var buildVersionCommandHeader = BuildVersionCommandHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);
            var buildVersion = new MachBuildVersion
            {
                TargetPlatform = buildVersionCommandHeader.Platform,
                MinimumPlatformVersion = ConvertVersion(buildVersionCommandHeader.MinimumPlatformVersion),
                SdkVersion = ConvertVersion(buildVersionCommandHeader.SdkVersion),
            };


            loadCommandPtr = loadCommandPtr.Slice(LoadCommandHeader.BinarySize + BuildVersionCommandHeader.BinarySize);
            for (int i = 0; i < buildVersionCommandHeader.NumberOfTools; i++)
            {
                var buildToolVersionHeader = BuildToolVersionHeader.Read(loadCommandPtr, isLittleEndian, out var _);
                buildVersion.ToolVersions.Add(new MachBuildToolVersion
                {
                    BuildTool = buildToolVersionHeader.BuildTool,
                    Version = ConvertVersion(buildToolVersionHeader.Version),
                });
                loadCommandPtr = loadCommandPtr.Slice(BuildToolVersionHeader.BinarySize);
            }

            return buildVersion;
        }

        private static MachSymbolTable ReadSymbolTable(ReadOnlySpan<byte> loadCommandPtr, bool isLittleEndian)
        {
            var symbolTableHeader = SymbolTableCommandHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);

            return new MachSymbolTable
            {
                SymbolTableOffset = symbolTableHeader.SymbolTableOffset,
                NumberOfSymbols = symbolTableHeader.NumberOfSymbols,
                StringTableOffset = symbolTableHeader.StringTableOffset,
                StringTableSize = symbolTableHeader.StringTableSize,
            };
        }

        private static MachDynamicLinkEditSymbolTable ReadDynamicLinkEditSymbolTable(ReadOnlySpan<byte> loadCommandPtr, bool isLittleEndian)
        {
            var dynamicSymbolTableHeader = DynamicSymbolTableCommandHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);

            // TODO: Clean up
            return new MachDynamicLinkEditSymbolTable(dynamicSymbolTableHeader);
        }

        private static MachObjectFile ReadSingle(FatArchHeader? fatArchHeader, MachMagic magic, Stream stream)
        {
            Span<byte> headerBuffer = stackalloc byte[Math.Max(MachHeader.BinarySize, MachHeader64.BinarySize)];
            MachObjectFile objectFile;
            IMachHeader machHeader;
            bool isLittleEndian;

            switch (magic)
            {
                case MachMagic.MachHeaderLittleEndian:
                case MachMagic.MachHeaderBigEndian:
                    stream.Read(headerBuffer.Slice(0, MachHeader.BinarySize));
                    isLittleEndian = magic == MachMagic.MachHeaderLittleEndian;
                    machHeader = MachHeader.Read(headerBuffer, isLittleEndian, out var _);
                    Debug.Assert(!machHeader.CpuType.HasFlag(MachCpuType.Architecture64));
                    objectFile = new MachObjectFile(stream);
                    break;

                case MachMagic.MachHeader64LittleEndian:
                case MachMagic.MachHeader64BigEndian:
                    stream.Read(headerBuffer.Slice(0, MachHeader64.BinarySize));
                    isLittleEndian = magic == MachMagic.MachHeader64LittleEndian;
                    machHeader = MachHeader64.Read(headerBuffer, isLittleEndian, out var _);
                    Debug.Assert(machHeader.CpuType.HasFlag(MachCpuType.Architecture64));
                    objectFile = new MachObjectFile(stream);
                    break;

                default:
                    throw new NotSupportedException();
            }

            objectFile.IsLittleEndian = isLittleEndian;
            objectFile.CpuType = machHeader.CpuType;
            objectFile.CpuSubType = machHeader.CpuSubType;
            objectFile.FileType = machHeader.FileType;
            objectFile.Flags = machHeader.Flags;

            // Read load commands
            //
            // Mach-O uses the load command both to describe the segments/sections and content
            // within them. The commands, like "Code Signature" overlap with the segments. For
            // code signature in particular it will overlap with the LINKEDIT segment.
            var loadCommands = new byte[machHeader.SizeOfCommands];
            Span<byte> loadCommandPtr = loadCommands;
            stream.ReadFully(loadCommands);
            for (int i = 0; i < machHeader.NumberOfCommands; i++)
            {
                var loadCommandHeader = LoadCommandHeader.Read(loadCommandPtr, isLittleEndian, out var _);
                objectFile.LoadCommands.Add(loadCommandHeader.CommandType switch
                {
                    MachLoadCommandType.Segment => ReadSegment(loadCommandPtr, isLittleEndian, objectFile.FileType == MachFileType.Object, stream),
                    MachLoadCommandType.Segment64 => ReadSegment64(loadCommandPtr, isLittleEndian, stream),
                    MachLoadCommandType.CodeSignature => ReadLinkEdit<MachCodeSignature>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.DylibCodeSigningDRs => ReadLinkEdit<MachDylibCodeSigningDirs>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.SegmentSplitInfo => ReadLinkEdit<MachSegmentSplitInfo>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.FunctionStarts => ReadLinkEdit<MachFunctionStarts>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.DataInCode => ReadLinkEdit<MachDataInCode>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.LinkerOptimizationHint => ReadLinkEdit<MachLinkerOptimizationHint>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.DyldExportsTrie => ReadLinkEdit<MachDyldExportsTrie>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.DyldChainedFixups => ReadLinkEdit<MachDyldChainedFixups>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.LoadDylib => ReadDylibCommand<MachLoadDylibCommand>(loadCommandPtr, loadCommandHeader.CommandSize, isLittleEndian),
                    MachLoadCommandType.LoadWeakDylib => ReadDylibCommand<MachLoadWeakDylibCommand>(loadCommandPtr, loadCommandHeader.CommandSize, isLittleEndian),
                    MachLoadCommandType.ReexportDylib => ReadDylibCommand<MachReexportDylibCommand>(loadCommandPtr, loadCommandHeader.CommandSize, isLittleEndian),
                    MachLoadCommandType.Main => ReadMainCommand(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.VersionMinMacOS => ReadVersionMinCommand<MachVersionMinMacOS>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.VersionMinIPhoneOS => ReadVersionMinCommand<MachVersionMinIOS>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.VersionMinTvOS => ReadVersionMinCommand<MachVersionMinTvOS>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.VersionMinWatchOS => ReadVersionMinCommand<MachVersionMinWatchOS>(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.BuildVersion => ReadBuildVersion(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.SymbolTable => ReadSymbolTable(loadCommandPtr, isLittleEndian),
                    MachLoadCommandType.DynamicLinkEditSymbolTable => ReadDynamicLinkEditSymbolTable(loadCommandPtr, isLittleEndian),
                    _ => new MachCustomLoadCommand(loadCommandHeader.CommandType, loadCommandPtr.Slice(LoadCommandHeader.BinarySize, (int)loadCommandHeader.CommandSize - LoadCommandHeader.BinarySize).ToArray()),
                });
                loadCommandPtr = loadCommandPtr.Slice((int)loadCommandHeader.CommandSize);
            }

            if (objectFile.FileType == MachFileType.Object)
            {
                // Create a segment to represent the unlinked data
                var unlinkedSegment = new MachSegment();
                unlinkedSegment.FileOffset = objectFile.GetSize();

                // Relocation tables
                int tempName = 1;
                foreach (var section in objectFile.LoadCommands.OfType<MachSegment>().SelectMany(segment => segment.Sections))
                {
                    if (section.NumberOfReloationEntries > 0)
                    {
                        var relocationSection = new MachSection(stream.Slice(section.RelocationOffset, section.NumberOfReloationEntries * 8));
                        relocationSection.Type = MachSectionType.Regular;
                        relocationSection.FileOffset = section.RelocationOffset;
                        relocationSection.SectionName = "$reloc" + tempName;
                        unlinkedSegment.Sections.Add(relocationSection);
                    }
                }

                tempName = 1;
                foreach (var dataInCode in objectFile.LoadCommands.OfType<MachDataInCode>())
                {
                    var dataInCodeSection = new MachSection(stream.Slice(dataInCode.FileOffset, dataInCode.FileSize));
                    dataInCodeSection.Type = MachSectionType.Regular;
                    dataInCodeSection.FileOffset = dataInCode.FileOffset;
                    dataInCodeSection.SectionName = "$dataincode" + tempName;
                    unlinkedSegment.Sections.Add(dataInCodeSection);
                }

                // Linker optimization hints
                foreach (var linkerOptimizationHint in objectFile.LoadCommands.OfType<MachLinkerOptimizationHint>())
                {
                    var lohSection = new MachSection(stream.Slice(linkerOptimizationHint.FileOffset, linkerOptimizationHint.FileSize));
                    lohSection.Type = MachSectionType.Regular;
                    lohSection.FileOffset = linkerOptimizationHint.FileOffset;
                    lohSection.SectionName = "$loh";
                    unlinkedSegment.Sections.Add(lohSection);
                }

                // Symbol table, string table
                foreach (var symbolTable in objectFile.LoadCommands.OfType<MachSymbolTable>())
                {
                    long symbolTableSize =
                        (long)symbolTable.NumberOfSymbols *
                        (long)(SymbolHeader.BinarySize + (objectFile.Is64Bit ? 8 : 4));

                    var symbolTableSection = new MachSection(stream.Slice(symbolTable.SymbolTableOffset, symbolTableSize));
                    symbolTableSection.Type = MachSectionType.Regular;
                    symbolTableSection.FileOffset = symbolTable.SymbolTableOffset;
                    symbolTableSection.SectionName = "$symtab";
                    unlinkedSegment.Sections.Add(symbolTableSection);

                    var stringTableSection = new MachSection(stream.Slice(symbolTable.StringTableOffset, symbolTable.StringTableSize));
                    stringTableSection.Type = MachSectionType.Regular;
                    stringTableSection.FileOffset = symbolTable.StringTableOffset;
                    stringTableSection.SectionName = "$stringtab";
                    unlinkedSegment.Sections.Add(stringTableSection);
                }

                unlinkedSegment.OriginalFileSize = unlinkedSegment.Sections.Select(s => s.FileOffset + s.Size).Max() - unlinkedSegment.FileOffset;
                objectFile.UnlinkedSegment = unlinkedSegment;
            }

            return objectFile;
        }

        public static IEnumerable<MachObjectFile> Read(Stream stream)
        {
            var magicBuffer = new byte[4];
            stream.ReadFully(magicBuffer);

            var magic = (MachMagic)BinaryPrimitives.ReadUInt32BigEndian(magicBuffer);
            if (magic == MachMagic.FatMagicLittleEndian || magic == MachMagic.FatMagicBigEndian)
            {
                var headerBuffer = new byte[Math.Max(FatHeader.BinarySize, FatArchHeader.BinarySize)];
                stream.ReadFully(headerBuffer.AsSpan(0, FatHeader.BinarySize));
                var fatHeader = FatHeader.Read(headerBuffer, isLittleEndian: magic == MachMagic.FatMagicLittleEndian, out var _);
                for (int i = 0; i < fatHeader.NumberOfFatArchitectures; i++)
                {
                    stream.ReadFully(headerBuffer.AsSpan(0, FatArchHeader.BinarySize));
                    var fatArchHeader = FatArchHeader.Read(headerBuffer, isLittleEndian: magic == MachMagic.FatMagicLittleEndian, out var _);

                    var machOSlice = stream.Slice(fatArchHeader.Offset, fatArchHeader.Size);
                    machOSlice.ReadFully(magicBuffer);
                    magic = (MachMagic)BinaryPrimitives.ReadUInt32BigEndian(magicBuffer);
                    yield return ReadSingle(fatArchHeader, magic, machOSlice);
                }
            }
            else
            {
                yield return ReadSingle(null, magic, stream);
            }
        }
    }
}