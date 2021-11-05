using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Melanzana.MachO.BinaryFormat;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public static class MachWriter
    {
        private static void WriteSegment(MachSegment segment, bool isLittleEndian, bool is64bit, Stream stream)
        {
            if (is64bit)
            {
                Span<byte> sectionBuffer = stackalloc byte[Section64Header.BinarySize];
                Span<byte> segmentBuffer = stackalloc byte[Segment64Header.BinarySize];

                var segmentHeader = new Segment64Header
                {
                    Name = segment.Name,
                    Address = segment.Address,
                    Size = segment.Size,
                    FileOffset = (ulong)segment.FileOffset,
                    FileSize = (ulong)segment.FileSize,
                    MaximalProtection = segment.MaximalProtection,
                    InitialProtection = segment.InitialProtection,
                    NumberOfSections = (uint)segment.Sections.Count,
                    Flags = segment.Flags,
                };
                segmentHeader.Write(segmentBuffer, isLittleEndian, out var _);
                stream.Write(segmentBuffer);

                foreach (var section in segment.Sections)
                {
                    var sectionHeader = new Section64Header
                    {
                        SectionName = section.SectionName,
                        SegmentName = section.SegmentName,
                        Address = section.Address,
                        Size = section.Size,
                        FileOffset = section.FileOffset,
                        Alignment = section.Alignment,
                        RelocationOffset = section.RelocationOffset,
                        NumberOfReloationEntries = section.NumberOfReloationEntries,
                        Flags = section.Flags,
                        Reserved1 = section.Reserved1,
                        Reserved2 = section.Reserved2,
                        Reserved3 = section.Reserved3,
                    };
                    sectionHeader.Write(sectionBuffer, isLittleEndian, out var _);
                    stream.Write(sectionBuffer);
                }
            }
            else
            {
                Span<byte> sectionBuffer = stackalloc byte[SectionHeader.BinarySize];
                Span<byte> segmentBuffer = stackalloc byte[SegmentHeader.BinarySize];

                // FIXME: Validation

                var segmentHeader = new SegmentHeader
                {
                    Name = segment.Name,
                    Address = (uint)segment.Address,
                    Size = (uint)segment.Size,
                    FileOffset = (uint)segment.FileOffset,
                    FileSize = (uint)segment.FileSize,
                    MaximalProtection = segment.MaximalProtection,
                    InitialProtection = segment.InitialProtection,
                    NumberOfSections = (uint)segment.Sections.Count,
                    Flags = segment.Flags,
                };
                segmentHeader.Write(segmentBuffer, isLittleEndian, out var _);
                stream.Write(segmentBuffer);

                foreach (var section in segment.Sections)
                {
                    var sectionHeader = new SectionHeader
                    {
                        SectionName = section.SectionName,
                        SegmentName = section.SegmentName,
                        Address = (uint)section.Address,
                        Size = (uint)section.Size,
                        FileOffset = section.FileOffset,
                        Alignment = section.Alignment,
                        RelocationOffset = section.RelocationOffset,
                        NumberOfReloationEntries = section.NumberOfReloationEntries,
                        Flags = section.Flags,
                        Reserved1 = section.Reserved1,
                        Reserved2 = section.Reserved2,
                    };
                    sectionHeader.Write(sectionBuffer, isLittleEndian, out var _);
                    stream.Write(sectionBuffer);
                }
            }
        }

        private static void WriteDylibCommand(MachDylibCommand dylibCommand, uint commandSize, bool isLittleEndian, Stream stream)
        {
            Span<byte> dylibCommandHeaderBuffer = stackalloc byte[DylibCommandHeader.BinarySize];
            var dylibCommandHeader = new DylibCommandHeader
            {
                NameOffset = LoadCommandHeader.BinarySize + DylibCommandHeader.BinarySize,
                Timestamp = dylibCommand.Timestamp,
                CurrentVersion = dylibCommand.CurrentVersion,
                CompatibilityVersion = dylibCommand.CompatibilityVersion,
            };
            dylibCommandHeader.Write(dylibCommandHeaderBuffer, isLittleEndian, out var _);
            stream.Write(dylibCommandHeaderBuffer);
            byte[] nameBytes = Encoding.UTF8.GetBytes(dylibCommand.Name);
            stream.Write(nameBytes);
            // The name is always written with terminating `\0` and aligned to platform
            // pointer size.
            stream.WritePadding(commandSize - dylibCommandHeader.NameOffset - nameBytes.Length);
        }

        public static void Write(MachObjectFile objectFile, Stream stream)
        {
            long initialOffset = stream.Position;
            bool isLittleEndian = objectFile.IsLittleEndian;
            var machMagicBuffer = new byte[4];
            var machHeaderBuffer = new byte[Math.Max(MachHeader.BinarySize, MachHeader64.BinarySize)];

            uint magic = isLittleEndian ?
                (objectFile.Is64Bit ? (uint)MachMagic.MachHeader64LittleEndian : (uint)MachMagic.MachHeaderLittleEndian) :
                (objectFile.Is64Bit ? (uint)MachMagic.MachHeader64BigEndian : (uint)MachMagic.MachHeaderBigEndian);
            BinaryPrimitives.WriteUInt32BigEndian(machMagicBuffer, magic);

            if (objectFile.Is64Bit)
            {
                var machHeader = new MachHeader64
                {
                    CpuType = objectFile.CpuType,
                    CpuSubType = objectFile.CpuSubType,
                    FileType = objectFile.FileType,
                    NumberOfCommands = (uint)objectFile.LoadCommands.Count,
                    SizeOfCommands = (uint)objectFile.LoadCommands.Sum(c => c.GetCommandSize(objectFile)),
                    Flags = objectFile.Flags,
                    Reserved = 0, // TODO
                };

                stream.Write(machMagicBuffer);
                machHeader.Write(machHeaderBuffer, isLittleEndian, out int bytesWritten);
                stream.Write(machHeaderBuffer.AsSpan(0, bytesWritten));
            }
            else
            {
                var machHeader = new MachHeader
                {
                    CpuType = objectFile.CpuType,
                    CpuSubType = objectFile.CpuSubType,
                    FileType = objectFile.FileType,
                    NumberOfCommands = (uint)objectFile.LoadCommands.Count,
                    SizeOfCommands = (uint)objectFile.LoadCommands.Sum(c => c.GetCommandSize(objectFile)),
                    Flags = objectFile.Flags,
                };

                stream.Write(machMagicBuffer);
                machHeader.Write(machHeaderBuffer, isLittleEndian, out int bytesWritten);
                stream.Write(machHeaderBuffer.AsSpan(0, bytesWritten));
            }

            foreach (var loadCommand in objectFile.LoadCommands)
            {
                var loadCommandHeaderBuffer = new byte[LoadCommandHeader.BinarySize];
                var loadCommandHeader = new LoadCommandHeader
                {
                    CommandType = loadCommand.GetCommandType(objectFile),
                    CommandSize = (uint)loadCommand.GetCommandSize(objectFile),
                };
                loadCommandHeader.Write(loadCommandHeaderBuffer, isLittleEndian, out var _);
                stream.Write(loadCommandHeaderBuffer);

                switch (loadCommand)
                {
                    case MachSegment segment:
                        WriteSegment(segment, isLittleEndian, objectFile.Is64Bit, stream);
                        break;

                    case MachLinkEdit linkEdit:
                        var linkEditHeaderBuffer = new byte[LinkEditHeader.BinarySize];
                        var linkEditHeader = new LinkEditHeader
                        {
                            FileOffset = linkEdit.FileOffset,
                            FileSize = linkEdit.FileSize,
                        };
                        linkEditHeader.Write(linkEditHeaderBuffer, isLittleEndian, out var _);
                        stream.Write(linkEditHeaderBuffer);
                        break;

                    case MachDylibCommand dylibCommand:
                        WriteDylibCommand(dylibCommand, loadCommandHeader.CommandSize, isLittleEndian, stream);
                        break;

                    case MachEntrypointCommand entrypointCommand:
                        var mainCommandHeaderBuffer = new byte[MainCommandHeader.BinarySize];
                        var mainCommandHeader = new MainCommandHeader
                        {
                            FileOffset = entrypointCommand.FileOffset,
                            StackSize = entrypointCommand.StackSize,
                        };
                        mainCommandHeader.Write(mainCommandHeaderBuffer, isLittleEndian, out var _);
                        stream.Write(mainCommandHeaderBuffer);
                        break;

                    case MachUnsupportedLoadCommand unsupportedLoadCommand:
                        stream.Write(unsupportedLoadCommand.Data);
                        break;
                }
            }

            // Save the current position within the Mach-O file. Now we need to output the segments
            // and fill in the gaps as we go.
            ulong currentOffset = (ulong)(stream.Position - initialOffset);
            var orderedSegments = objectFile.LoadCommands.OfType<MachSegment>().OrderBy(s => s.FileOffset).ToList();

            foreach (var segment in orderedSegments)
            {
                if (segment.FileSize != 0)
                {
                    if (segment.Sections.Count == 0)
                    {
                        Debug.Assert(segment.FileOffset >= currentOffset);

                        if (segment.FileOffset > currentOffset)
                        {
                            ulong paddingSize = segment.FileOffset - currentOffset;
                            stream.WritePadding((long)paddingSize);
                            currentOffset += paddingSize;
                        }

                        using var segmentStream = segment.GetReadStream();
                        segmentStream.CopyTo(stream);
                        currentOffset += (ulong)segmentStream.Length;
                    }
                    else
                    {
                        foreach (var section in segment.Sections)
                        {
                            if (section.IsInFile)
                            {
                                Debug.Assert(section.FileOffset >= currentOffset);

                                if (section.FileOffset > currentOffset)
                                {
                                    ulong paddingSize = section.FileOffset - currentOffset;
                                    stream.WritePadding((long)paddingSize);
                                    currentOffset += paddingSize;
                                }

                                using var sectionStream = section.GetReadStream();
                                sectionStream.CopyTo(stream);
                                currentOffset += (ulong)sectionStream.Length;
                            }
                        }
                    }
                }
            }
        }

        public static void Write(IList<MachObjectFile> objectFiles, Stream stream)
        {
            if (objectFiles.Count == 1)
            {
                Write(objectFiles[0], stream);
            }
            else if (objectFiles.Count > 1)
            {
                var fatMagic = new byte[4];
                var fatHeader = new FatHeader { NumberOfFatArchitectures = (uint)objectFiles.Count };
                var fatHeaderBytes = new byte[FatHeader.BinarySize];
                var fatArchHeaderBytes = new byte[FatArchHeader.BinarySize];

                BinaryPrimitives.WriteUInt32BigEndian(fatMagic, (uint)MachMagic.FatMagicBigEndian);
                fatHeader.Write(fatHeaderBytes, isLittleEndian: false, out var _);
                stream.Write(fatMagic);
                stream.Write(fatHeaderBytes);

                uint offset = (uint)(FatHeader.BinarySize + objectFiles.Count * FatArchHeader.BinarySize);
                uint alignment = 0x4000;
                foreach (var objectFile in objectFiles)
                {
                    uint size = (uint)objectFile.GetSize();

                    offset = (offset + alignment - 1) & ~(alignment - 1);
                    var fatArchHeader = new FatArchHeader
                    {
                        CpuType = objectFile.CpuType,
                        CpuSubType = objectFile.CpuSubType,
                        Offset = offset,
                        Size = size,
                        Alignment = (uint)Math.Log2(alignment),
                    };

                    fatArchHeader.Write(fatArchHeaderBytes, isLittleEndian: false, out var _);
                    stream.Write(fatArchHeaderBytes);

                    offset += size;
                }

                offset = (uint)(FatHeader.BinarySize + objectFiles.Count * FatArchHeader.BinarySize);
                foreach (var objectFile in objectFiles)
                {
                    uint size = (uint)objectFile.GetSize();
                    uint alignedOffset = (offset + alignment - 1) & ~(alignment - 1);
                    stream.WritePadding(alignedOffset - offset);
                    Write(objectFile, stream);
                    offset = alignedOffset + size;
                }
            }
        }
    }
}
