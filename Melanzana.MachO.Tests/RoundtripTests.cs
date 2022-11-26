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
            var objectFile = MachReader.Read(aOutStream).Single();

            using (MemoryStream cloneStream = new MemoryStream((int)aOutStream.Length))
            using (var outputStream = new ValidatingStream(cloneStream))
            {
                aOutStream.Seek(0, SeekOrigin.Begin);
                aOutStream.CopyTo(cloneStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                MachWriter.Write(outputStream, objectFile);
            }
        }

        private static void TestFatRoundtrip(Stream aOutStream)
        {
            var objectFiles = MachReader.Read(aOutStream).ToList();

            using (MemoryStream cloneStream = new MemoryStream((int)aOutStream.Length))
            using (var outputStream = new ValidatingStream(cloneStream))
            {
                aOutStream.Seek(0, SeekOrigin.Begin);
                aOutStream.CopyTo(cloneStream);
                outputStream.Seek(0, SeekOrigin.Begin);

                MachWriter.Write(outputStream, objectFiles);
        }
        }

        [Fact]
        public void BasicRoundtrip()
        {
            var aOutStream = typeof(RoundtripTests).Assembly.GetManifestResourceStream("Melanzana.MachO.Tests.Data.a.out")!;
            TestRoundtrip(aOutStream);
        }

        [Fact]
        public void FatRoundtrip()
        {
            var aFatOutStream = typeof(RoundtripTests).Assembly.GetManifestResourceStream("Melanzana.MachO.Tests.Data.a.fat.out")!;
            TestFatRoundtrip(aFatOutStream);
        }

        [Fact]
        public void ObjectFileRoundtrip()
        {
            var aOutStream = typeof(RoundtripTests).Assembly.GetManifestResourceStream("Melanzana.MachO.Tests.Data.a.o")!;
            TestRoundtrip(aOutStream);
        }

        [Fact]
        public void ExecutableRoundtrip()
        {
            var aOutStream = typeof(RoundtripTests).Assembly.GetManifestResourceStream("Melanzana.MachO.Tests.Data.rpath.out")!;
            TestRoundtrip(aOutStream);
        }
    }
}
