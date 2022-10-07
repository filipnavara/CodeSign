using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Melanzana.MachO.BinaryFormat;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public class MachSymbolTable : MachLoadCommand
    {
        MachObjectFile objectFile;

        public MachSymbolTable(MachObjectFile objectFile)
        {
            ArgumentNullException.ThrowIfNull(objectFile);

            this.objectFile = objectFile;
            SymbolTableData = new MachLinkEditData();
            StringTableData = new MachLinkEditData();
            objectFile.LinkEditData.Add(SymbolTableData);
            objectFile.LinkEditData.Add(StringTableData);
        }

        internal MachSymbolTable(
            MachObjectFile objectFile,
            MachLinkEditData symbolTableData,
            MachLinkEditData stringTableData)
        {
            ArgumentNullException.ThrowIfNull(objectFile);
            ArgumentNullException.ThrowIfNull(symbolTableData);
            ArgumentNullException.ThrowIfNull(stringTableData);

            this.objectFile = objectFile;
            SymbolTableData = symbolTableData;
            StringTableData = stringTableData;
        }

        public MachLinkEditData SymbolTableData { get; private init; }

        public MachLinkEditData StringTableData { get; private init; }

        public IEnumerable<MachSymbol> GetReader()
        {
            var sectionMap = new Dictionary<byte, MachSection>();
            byte sectionIndex = 1;
            foreach (var section in objectFile.Segments.SelectMany(segment => segment.Sections))
            {
                sectionMap.Add(sectionIndex++, section);
                Debug.Assert(sectionIndex != 0);
            }

            byte[] stringTable = new byte[StringTableData.Size];
            using var stringTableStream = StringTableData.GetReadStream();
            stringTableStream.ReadFully(stringTable);

            int symbolSize = SymbolHeader.BinarySize + (objectFile.Is64Bit ? 8 : 4);
            byte[] symbolBuffer = new byte[symbolSize];
            using var symbolTableStream = SymbolTableData.GetReadStream();
            while (symbolTableStream.Position < symbolTableStream.Length)
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

        public MachSymbolTableWriter GetWriter()
        {
            return new MachSymbolTableWriter(objectFile, this);
        }
    }
}