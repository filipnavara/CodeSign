using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Melanzana.MachO.BinaryFormat;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public class MachSymbolTable : MachLoadCommand
    {
        public uint SymbolTableOffset { get; set; }

        public uint NumberOfSymbols { get; set; }

        public uint StringTableOffset { get; set; }

        public uint StringTableSize { get; set; }

        internal MachSection? SymbolTableSection { get; set; }

        internal MachSection? StringTableSection { get; set; }

        public IEnumerable<MachSymbol> GetReader(MachObjectFile objectFile)
        {
            var sectionMap = new Dictionary<byte, MachSection>();
            byte sectionIndex = 1;
            foreach (var section in objectFile.Segments.SelectMany(segment => segment.Sections))
            {
                sectionMap.Add(sectionIndex++, section);
                Debug.Assert(sectionIndex != 0);
            }

            byte[] stringTable = new byte[StringTableSize];
            var stringTableStream = objectFile.GetStreamAtFileOffset(StringTableOffset, StringTableSize);
            stringTableStream.ReadFully(stringTable);

            int symbolSize = SymbolHeader.BinarySize + (objectFile.Is64Bit ? 8 : 4);
            byte[] symbolBuffer = new byte[symbolSize];
            var symbolTableStream = objectFile.GetStreamAtFileOffset(SymbolTableOffset, (uint)(symbolSize * NumberOfSymbols));
            for (int i = 0; i < NumberOfSymbols; i++)
            {
                symbolTableStream.ReadFully(symbolBuffer);
                var symbolHeader = SymbolHeader.Read(symbolBuffer, objectFile.IsLittleEndian, out var _);
                ulong symbolValue;
                if (objectFile.IsLittleEndian)
                {
                    symbolValue = objectFile.Is64Bit ?
                        BinaryPrimitives.ReadUInt64LittleEndian(symbolBuffer.AsSpan(SymbolHeader.BinarySize)) :
                        BinaryPrimitives.ReadUInt32LittleEndian(symbolBuffer.AsSpan(SymbolHeader.BinarySize));
                }
                else
                {
                    symbolValue = objectFile.Is64Bit ?
                        BinaryPrimitives.ReadUInt64BigEndian(symbolBuffer.AsSpan(SymbolHeader.BinarySize)) :
                        BinaryPrimitives.ReadUInt32BigEndian(symbolBuffer.AsSpan(SymbolHeader.BinarySize));
                }

                string name = string.Empty;
                if (symbolHeader.NameIndex != 0)
                {
                    int nameLength = stringTable.AsSpan((int)symbolHeader.NameIndex).IndexOf((byte)0);
                    Debug.Assert(nameLength >= 0);
                    name = Encoding.UTF8.GetString(stringTable.AsSpan((int)symbolHeader.NameIndex, nameLength));
                }

                yield return new MachSymbol
                {
                    Name = name,
                    Descriptor = (MachSymbolDescriptor)symbolHeader.Descriptor,
                    Section = symbolHeader.Section == 0 ? null : sectionMap[symbolHeader.Section],
                    Type = (MachSymbolType)symbolHeader.Type,
                    Value = symbolValue,
                };
            }
        }

        public MachSymbolTableWriter GetWriter(MachObjectFile objectFile)
        {
            ArgumentNullException.ThrowIfNull(objectFile);

            // We currently only support writing symbols to object files
            // and only if the symbol table is part of the unlinked section.
            if (objectFile.FileType != MachFileType.Object)
            {
                throw new NotImplementedException();
            }

            objectFile.EnsureUnlinkedSegmentExists();
            Debug.Assert(objectFile.UnlinkedSegment != null);

            if (SymbolTableSection == null)
            {
                Debug.Assert(StringTableSection == null);

                if (NumberOfSymbols == 0)
                {
                    // Create new section
                    SymbolTableSection = new MachSection { Type = MachSectionType.Regular };
                    objectFile.UnlinkedSegment.Sections.Add(SymbolTableSection);
                    StringTableSection = new MachSection { Type = MachSectionType.Regular };
                    objectFile.UnlinkedSegment.Sections.Add(StringTableSection);
                }
                else
                {
                    // Find existing section
                    SymbolTableSection = objectFile.UnlinkedSegment.Sections.First(s => s.FileOffset == SymbolTableOffset);
                    StringTableSection = objectFile.UnlinkedSegment.Sections.First(s => s.FileOffset == StringTableOffset);
                }
            }
            else
            {
                Debug.Assert(StringTableSection != null);
            }

            return new MachSymbolTableWriter(objectFile, this, SymbolTableSection, StringTableSection);
        }

        internal override void UpdateLayout(MachObjectFile objectFile)
        {
            if (SymbolTableSection != null)
            {
                SymbolTableOffset = SymbolTableSection.FileOffset;
            }

            if (StringTableSection != null)
            {
                StringTableOffset = StringTableSection.FileOffset;
            }

            base.UpdateLayout(objectFile);
        }
    }
}