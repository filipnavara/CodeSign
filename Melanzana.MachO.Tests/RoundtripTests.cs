using Xunit;
using System.IO;
using System.Linq;
using Melanzana.Streams;

namespace Melanzana.MachO.Tests
{
    public class RoundtripTests
    {
        private static void TestRoundtrip(Stream aOutStream)
        {
            var outputStream = new MemoryStream();
            var objectFiles = MachReader.Read(aOutStream).ToList();
            MachWriter.Write(outputStream, objectFiles);

            aOutStream.Position = 0;
            outputStream.Position = 0;
            Assert.Equal(aOutStream.Length, outputStream.Length);

            var input = new byte[aOutStream.Length];
            var output = new byte[aOutStream.Length];
            aOutStream.ReadFully(input);
            outputStream.ReadFully(output);
            Assert.Equal(input, output);
        }

        [Fact]
        public void BasicRoundtrip()
        {
            var aOutStream = typeof(RoundtripTests).Assembly.GetManifestResourceStream("Melanzana.MachO.Tests.Data.a.out")!;
            TestRoundtrip(aOutStream);
        }

        //[Fact]
        public void FatRoundtrip()
        {
            // FIXME: This doesn't work yet because the x86-64 object file pads the code in __TEXT section with
            // 0x90 byte that we don't roundtrip since it's not part of the section. The executable still works
            // after rewriting though.
            var aFatOutStream = typeof(RoundtripTests).Assembly.GetManifestResourceStream("Melanzana.MachO.Tests.Data.a.fat.out")!;
            TestRoundtrip(aFatOutStream);
        }
    }
}
