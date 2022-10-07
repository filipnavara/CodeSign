using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public class MachSection
    {
        private MachObjectFile objectFile;
        private Stream? dataStream;
        private ulong size;

        public MachSection(MachObjectFile objectFile, string segmentName, string sectionName)
            : this(objectFile, segmentName, sectionName, null)
        {
        }

        public MachSection(MachObjectFile objectFile, string segmentName, string sectionName, Stream? stream)
            : this(objectFile, segmentName, sectionName, stream, new MachLinkEditData())
        {
        }

        internal MachSection(
            MachObjectFile objectFile,
            string segmentName,
            string sectionName,
            Stream? stream,
            MachLinkEditData relocationData)
        {
            ArgumentNullException.ThrowIfNull(objectFile);
            ArgumentNullException.ThrowIfNull(segmentName);
            ArgumentNullException.ThrowIfNull(sectionName);

            this.objectFile = objectFile;
            this.SegmentName = segmentName;
            this.SectionName = sectionName;
            this.dataStream = stream;
            this.RelocationData = relocationData;
        }

        /// <summary>
        /// Gets or sets the name of this section.
        /// </summary>
        public string SectionName { get; private init; }

        /// <summary>
        /// Gets or sets the name of the segment.
        /// </summary>
        /// <remarks>
        /// For fully linked executables or dynamic libraries this should always be the same as
        /// the name of the containing segment. However, intermediate object files
        /// (<see cref="MachFileType.Object"/>) use compact format where all sections are
        /// listed under single segment.
        /// </remarks>
        public string SegmentName { get; private init; }

        /// <summary>
        /// Gets or sets the virtual address of this section.
        /// </summary>
        public ulong VirtualAddress { get; set; }

        public ulong Size
        {
            get => dataStream != null ? (ulong)dataStream.Length : size;
            set
            {
                size = value;
                if (dataStream != null)
                {
                    if (!HasContentChanged)
                    {
                        HasContentChanged = true;
                        dataStream = new UnclosableMemoryStream();
                    }
                    dataStream.SetLength((long)size);
                }
            }
        }

        public uint FileOffset { get; set; }

        /// <summary>
        /// Gets or sets the alignment requirement of this section.
        /// </summary>
        public uint Log2Alignment { get; set; }

        /// <summary>
        /// Gets the file offset to relocation entries of this section.
        /// </summary>
        public uint RelocationOffset => RelocationData?.FileOffset ?? 0u;

        /// <summary>
        /// Gets or sets the number of relocation entries of this section.
        /// </summary>
        public uint NumberOfReloationEntries => (uint)((RelocationData?.Size ?? 0u) / 8);

        internal uint Flags { get; set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public uint Reserved1 { get; set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public uint Reserved2 { get; set; }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public uint Reserved3 { get; set; }

        public MachSectionAttributes Attributes
        {
            get => (MachSectionAttributes)(Flags & ~0xffu);
            set => Flags = (Flags & 0xffu) | (uint)value;
        }

        public MachSectionType Type
        {
            get => (MachSectionType)(Flags & 0xff);
            set => Flags = (Flags & ~0xffu) | (uint)value;
        }

        public bool IsInFile => Size > 0 && Type != MachSectionType.ZeroFill && Type != MachSectionType.GBZeroFill && Type != MachSectionType.ThreadLocalZeroFill;

        internal bool HasContentChanged { get; set; }

        public MachLinkEditData RelocationData { get; private init; }

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

        public IEnumerable<MachRelocation> GetRelocationReader()
        {
            if (RelocationData == null)
            {
                yield break;
            }

            var relocationStream = RelocationData.GetReadStream();
            var relocationBuffer = new byte[8];
            
            for (uint i = 0; i < NumberOfReloationEntries; i++)
            {
                relocationStream.ReadFully(relocationBuffer);

                int address =
                    objectFile.IsLittleEndian ?
                    BinaryPrimitives.ReadInt32LittleEndian(relocationBuffer) :
                    BinaryPrimitives.ReadInt32BigEndian(relocationBuffer);

                uint info =
                    objectFile.IsLittleEndian ?
                    BinaryPrimitives.ReadUInt32LittleEndian(relocationBuffer.AsSpan(4)) :
                    BinaryPrimitives.ReadUInt32BigEndian(relocationBuffer.AsSpan(4));

                yield return new MachRelocation
                {
                    Address = address,
                    SymbolOrSectionIndex = info & 0xff_ff_ff,
                    IsPCRelative = (info & 0x1_00_00_00) > 0,
                    Length = ((info >> 25) & 3) switch { 0 => 1, 1 => 2, 2 => 4, _ => 8 },
                    IsExternal = (info & 0x8_00_00_00) > 0,
                    RelocationType = (MachRelocationType)(info >> 28)
                };
            }
        }

        public MachRelocationWriter GetRelocationWriter()
        {
            if (RelocationData == null)
            {
                throw new InvalidOperationException("Section cannot have relocations");
            }

            return new MachRelocationWriter(objectFile, this, RelocationData);
        }
    }
}