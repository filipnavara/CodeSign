using System.Buffers.Binary;
using Melanzana.MachO.Commands;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public static class MachOReader
    {
        private static MachObjectFile ReadSingle(FatArchHeader? fatArchHeader, MachMagic magic, Stream stream)
        {
            Span<byte> headerBuffer = stackalloc byte[Math.Max(MachHeader.BinarySize, MachHeader64.BinarySize)];
            MachObjectFile machO;
            bool isLittleEndian;

            switch (magic)
            {
                case MachMagic.MachHeaderLitteEndian:
                case MachMagic.MachHeaderBigEndian:
                    stream.Read(headerBuffer.Slice(0, MachHeader.BinarySize));
                    isLittleEndian = magic == MachMagic.MachHeaderLitteEndian;
                    machO = new MachObjectFile(null, MachHeader.Read(headerBuffer, isLittleEndian: isLittleEndian, out var _), isLittleEndian, stream);
                    break;

                case MachMagic.MachHeader64LitteEndian:
                case MachMagic.MachHeader64BigEndian:
                    stream.Read(headerBuffer.Slice(0, MachHeader64.BinarySize));
                    isLittleEndian = magic == MachMagic.MachHeader64LitteEndian;
                    machO = new MachObjectFile(null, MachHeader64.Read(headerBuffer, isLittleEndian: isLittleEndian, out var _), isLittleEndian, stream);
                    break;

                default:
                    throw new NotSupportedException();
            }

            // Read load commands
            //
            // Mach-O uses the load command both to describe the segments/sections and content
            // within them. The commands, like "Code Signature" overlap with the segments. For
            // code signature in particular it will overlap with the LINKEDIT segment.
            var loadCommands = new byte[machO.Header.SizeOfCommands];
            Span<byte> loadCommandPtr = loadCommands;
            stream.ReadFully(loadCommands);
            for (int i = 0; i < machO.Header.NumberOfCommands; i++)
            {
                var loadCommandHeader = LoadCommandHeader.Read(loadCommandPtr, isLittleEndian, out var _);
                switch (loadCommandHeader.CommandType)
                {
                    case LoadCommandType.Segment:
                        var segmentHeader = SegmentHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);
                        var sectionHeaders = new SectionHeader[segmentHeader.NumberOfSections];
                        for (int s = 0; s < segmentHeader.NumberOfSections; s++)
                            sectionHeaders[s] = SectionHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize + SegmentHeader.BinarySize + s * SectionHeader.BinarySize), isLittleEndian, out var _);
                        machO.LoadCommands.Add(new Segment(loadCommandHeader, segmentHeader, sectionHeaders));
                        break;

                    case LoadCommandType.Segment64:
                        var segment64Header = Segment64Header.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);
                        var section64Headers = new Section64Header[segment64Header.NumberOfSections];
                        for (int s = 0; s < segment64Header.NumberOfSections; s++)
                            section64Headers[s] = Section64Header.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize + Segment64Header.BinarySize + s * Section64Header.BinarySize), isLittleEndian, out var _);
                        machO.LoadCommands.Add(new Segment64(loadCommandHeader, segment64Header, section64Headers));
                        break;

                    case LoadCommandType.CodeSignature:
                    case LoadCommandType.DylibCodeSigningDRs:
                    case LoadCommandType.SegmentSplitInfo:
                    case LoadCommandType.FunctionStarts:
                    case LoadCommandType.DataInCode:
                    case LoadCommandType.LinkerOptimizationHint:
                    case LoadCommandType.DyldExportsTrie:
                    case LoadCommandType.DyldChainedFixups:
                        var linkEditHeader = LinkEditHeader.Read(loadCommandPtr.Slice(LoadCommandHeader.BinarySize), isLittleEndian, out var _);
                        machO.LoadCommands.Add(new LinkEdit(loadCommandHeader, linkEditHeader));
                        break;

                    // TODO: Support more standard sections

                    default:
                        machO.LoadCommands.Add(new UnsupportedLoadCommand(loadCommandHeader, loadCommandPtr.Slice(LoadCommandHeader.BinarySize, (int)loadCommandHeader.CommandSize - LoadCommandHeader.BinarySize).ToArray()));
                        break;
                }
                loadCommandPtr = loadCommandPtr.Slice((int)loadCommandHeader.CommandSize);
            }


            return machO;
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