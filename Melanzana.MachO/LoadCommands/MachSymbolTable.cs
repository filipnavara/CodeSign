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

        // TODO: Reader, Writer
        public IEnumerable<MachSymbol> GetReader(MachObjectFile objectFile)
        {
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
                    Value = symbolValue,
                };
            }
        }
    }
}