using Xunit;
using System.IO;
using System.Linq;
using Melanzana.Streams;

namespace Melanzana.MachO.Tests
{
    public class ReadTests
    {
        [Fact]
        public void ReadExecutable()
        {
            var aOutStream = typeof(RoundtripTests).Assembly.GetManifestResourceStream("Melanzana.MachO.Tests.Data.a.out");
            var objectFile = MachReader.Read(aOutStream).First();

            var segments = objectFile.LoadCommands.OfType<MachSegment>().ToArray();
            Assert.Equal("__PAGEZERO", segments[0].Name);
            Assert.Equal("__TEXT", segments[1].Name);
            Assert.Equal("__LINKEDIT", segments[2].Name);

            var symbolTable = objectFile.LoadCommands.OfType<MachSymbolTable>().FirstOrDefault();
            Assert.NotNull(symbolTable);
            var symbols = symbolTable!.GetReader(objectFile).ToArray();
            Assert.Equal(2, symbols.Length);
            Assert.Equal("__mh_execute_header", symbols[0].Name);
            Assert.Equal(0x100000000u, symbols[0].Value);
            Assert.Equal("_main", symbols[1].Name);
            Assert.Equal(0x100003fa4u, symbols[1].Value);
        }
    }
}
