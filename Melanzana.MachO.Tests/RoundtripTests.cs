using Xunit;
using System.IO;
using System.Linq;
using Melanzana.Streams;

namespace Melanzana.MachO.Tests
{
    public class RoundtripTests
    {
        [Fact]
        public void BasicRoundtrip()
        {
            var aOutStream = typeof(RoundtripTests).Assembly.GetManifestResourceStream("Melanzana.MachO.Tests.Data.a.out");
            var outputStream = new MemoryStream();
            var objectFile = MachReader.Read(aOutStream).First();
            MachWriter.Write(outputStream, objectFile);

            aOutStream.Position = 0;
            outputStream.Position = 0;
            Assert.Equal(aOutStream.Length, outputStream.Length);

            var input = new byte[aOutStream.Length];
            var output = new byte[aOutStream.Length];
            aOutStream.ReadFully(input);
            outputStream.ReadFully(output);
            Assert.Equal(input, output);
        }
    }
}
