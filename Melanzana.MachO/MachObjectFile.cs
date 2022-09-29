using Melanzana.Streams;
using Melanzana.MachO.BinaryFormat;

namespace Melanzana.MachO
{
    public class MachObjectFile
    {
        private readonly Stream stream;

        public MachObjectFile(Stream stream)
        {
            this.stream = stream;
        }

        public bool Is64Bit => CpuType.HasFlag(MachCpuType.Architecture64);

        public bool IsLittleEndian { get; set; }

        public MachCpuType CpuType { get; set; }

        public uint CpuSubType { get; set; }

        public MachFileType FileType { get; set; }

        public MachHeaderFlags Flags { get; set; }

        public IList<MachLoadCommand> LoadCommands { get; } = new List<MachLoadCommand>();

        /// <summary>
        /// Get the lowest file offset of any section in the file. This allows calculating space that is
        /// reserved for adding new load commands (header pad).
        /// </summary>
        public ulong GetLowestSectionFileOffset()
        {
            ulong lowestFileOffset = (ulong)stream.Length;

            foreach (var segment in LoadCommands.OfType<MachSegment>())
            {
                foreach (var section in segment.Sections)
                {
                    if (section.IsInFile &&
                        section.FileOffset < lowestFileOffset)
                    {
                        lowestFileOffset = section.FileOffset;
                    }
                }
            }

            return lowestFileOffset;
        }

        public ulong GetSize()
        {
            // Assume the size is the highest file offset+size of any segment
            return LoadCommands.OfType<MachSegment>().Max(s => s.FileOffset + s.FileSize);
        }

        public ulong GetSigningLimit()
        {
            var codeSignature = LoadCommands.OfType<MachCodeSignature>().FirstOrDefault();
            if (codeSignature != null)
            {
                // If code signature is present it has to be at the end of the file
                return codeSignature.FileOffset;
            }
            else
            {
                // If no code signature is present then we return the whole file size
                return GetSize();
            }
        }

        /// <summary>
        /// Gets a stream for a given part of range of the file.
        /// </summary>
        /// <remarks>
        /// The range must be fully contained in a single section or segment with no sections.
        /// Accessing file header or link commands through this API is currently not supported.
        /// </remarks>
        public Stream GetStreamAtFileOffset(uint fileOffset, uint fileSize)
        {
            // FIXME: Should we dispose the original stream? At the moment it would be no-op
            // anyway since it's always SliceStream or UnclosableMemoryStream.

            foreach (var segment in LoadCommands.OfType<MachSegment>())
            {
                if (fileOffset >= segment.FileOffset &&
                    fileOffset <= segment.FileOffset + segment.FileSize)
                {
                    if (segment.Sections.Count == 0)
                    {
                        return segment.GetReadStream().Slice(
                            (long)(fileOffset - segment.FileOffset),
                            fileSize);
                    }

                    foreach (var section in segment.Sections)
                    {
                        if (fileOffset >= section.FileOffset &&
                            fileOffset <= section.FileOffset + section.Size)
                        {
                            return section.GetReadStream().Slice(
                                (long)(fileOffset - section.FileOffset),
                                fileSize);
                        }
                    }

                    return Stream.Null;
                }
            }

            return Stream.Null;
        }

        public Stream GetStreamAtVirtualAddress(ulong address, uint length)
        {
            // FIXME: Should we dispose the original stream? At the moment it would be no-op
            // anyway since it's always SliceStream or UnclosableMemoryStream.

            foreach (var segment in LoadCommands.OfType<MachSegment>())
            {
                if (address >= segment.VirtualAddress &&
                    address <= segment.VirtualAddress + segment.Size)
                {
                    if (segment.Sections.Count == 0)
                    {
                        return segment.GetReadStream().Slice(
                            (long)(address - segment.VirtualAddress),
                            (long)Math.Min(length, segment.VirtualAddress + segment.FileSize - address));
                    }

                    foreach (var section in segment.Sections)
                    {
                        if (address >= section.VirtualAddress &&
                            address <= section.VirtualAddress + section.Size)
                        {
                            return section.GetReadStream().Slice(
                                (long)(address - section.VirtualAddress),
                                (long)Math.Min(length, section.VirtualAddress + section.Size - address));
                        }
                    }

                    return Stream.Null;
                }
            }

            return Stream.Null;
        }

        public Stream GetOriginalStream()
        {
            if (stream == null)
                return Stream.Null;

            return stream.Slice(0, stream.Length);
        }
    }
}