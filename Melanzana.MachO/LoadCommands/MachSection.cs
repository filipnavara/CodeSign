using System.ComponentModel;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public class MachSection
    {
        private Stream? dataStream;
        private ulong size;

        public MachSection()
        {
        }

        public MachSection(Stream stream)
        {
            dataStream = stream;
        }

        public string SectionName { get; set; } = string.Empty;

        public string SegmentName { get; set; } = string.Empty;

        public ulong Address { get; set; }

        public ulong Size
        {
            get => FileOffset != 0 && dataStream != null ? (ulong)dataStream.Length : size;
            set => size = value;
        }

        public uint FileOffset { get; set; }

        public uint Alignment { get; set; }

        public uint RelocationOffset { get; set; }

        public uint NumberOfReloationEntries { get; set; }

        public uint Flags { get; set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public uint Reserved1 { get; set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public uint Reserved2 { get; set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public uint Reserved3 { get; set; }

        public MachSectionType Type => (MachSectionType)(Flags & 0xff);

        public bool IsInFile => Size > 0 && Type != MachSectionType.ZeroFill && Type != MachSectionType.GBZeroFill && Type != MachSectionType.ThreadLocalZeroFill;

        internal bool HasContentChanged { get; set; }

        public Stream GetReadStream()
        {
            if (Size == 0 || dataStream == null)
            {
                return Stream.Null;
            }

            return dataStream.Slice(0, (long)this.Size);
        }

        public Stream GetWriteStream()
        {
            HasContentChanged = true;
            dataStream = new UnclosableMemoryStream();
            return dataStream;
        }
    }
}