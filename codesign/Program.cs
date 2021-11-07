// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Melanzana.CodeSign;
using Melanzana.MachO;

/*
var objectFile = MachReader.Read(File.OpenRead("a.out")).First();
var symbolTable = objectFile.LoadCommands.OfType<MachSymbolTable>().First();
foreach (var symbol in symbolTable.GetReader(objectFile))
{
    Console.WriteLine($"{symbol.Name} {symbol.Value:x}");
}
return 0;*/

#if BUILDER
// Let's try to build a new macho!
var objectFile = new MachObjectFile(Stream.Null);

// Header
objectFile.CpuType = MachCpuType.Arm64;
objectFile.FileType = MachFileType.Execute;
objectFile.Flags = MachHeaderFlags.PIE | MachHeaderFlags.TwoLevel | MachHeaderFlags.DynamicLink | MachHeaderFlags.NoUndefinedReferences;

// Segments
var pageZeroSegement = new MachSegment
{
    Name = "__PAGEZERO",
    Address = 0,
    Size = 0x100000000
};
var textSegment = new MachSegment
{
    Name = "__TEXT",
    FileOffset = 0,
    Address = 0x100000000,
    Size = 0x4000,
    InitialProtection = MachVmProtection.Execute | MachVmProtection.Read,
    MaximalProtection = MachVmProtection.Execute | MachVmProtection.Read,
};
var textSection = new MachSection
{
    SectionName = "__text",
    SegmentName = "__TEXT",
    Alignment = 2,
    Type = MachSectionType.Regular,
    Attributes = MachSectionAttributes.SomeInstructions | MachSectionAttributes.PureInstructions,
};
using (var textWriter = textSection.GetWriteStream())
{
    textWriter.Write(new byte[] { 0x00, 0x00, 0x80, 0x52 }); // mov w0, #0
    textWriter.Write(new byte[] { 0xc0, 0x03, 0x5f, 0xd6 }); // ret
    textSection.FileOffset = 0x4000u - (uint)textWriter.Position;
    textSection.Address = textSegment.Address + textSection.FileOffset;
}
var linkEditSegment = new MachSegment
{
    Name = "__LINKEDIT",
    Address = textSection.Address + textSection.Size,
    // FileOffset = 
    // FileSize =
    InitialProtection = MachVmProtection.Read,
    MaximalProtection = MachVmProtection.Read,
};
#endif

/*
var machO = MachReader.Read(File.OpenRead("/Users/filipnavara/Downloads/gc/MailClient.Mobile.iOS.app/Xamarin.PreBuilt.iOS")).ToList();
//var codeSignAllocate = new CodeSignAllocate(machO, "rewritten");
//codeSignAllocate.Allocate();
using var output = File.OpenWrite("rewritten");
MachWriter.Write(machO.First(), output);
*/

Console.WriteLine("Hello, World!");

var provisioningProfile = new ProvisioningProfile("/Users/filipnavara/Library/MobileDevice/Provisioning Profiles/fd169b8e-5183-4fa1-af17-e9086a2adbcc.mobileprovision");
//var certificate = store.Certificates.Find(X509FindType.FindByIssuerName)
X509Certificate2? developerCertificate = null;
// Find certificate with private key
var store = new X509Store(StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadOnly);
/*foreach (var certificate in provisioningProfile.DeveloperCertificates)
{
    developerCertificate = store.Certificates.Find(X509FindType.FindBySubjectName, certificate.SubjectName, true).FirstOrDefault();
    if (developerCertificate != null)
        break;
}*/
/*developerCertificate = store.Certificates.Find(
    X509FindType.FindByThumbprint, 
    "D69F66D03D0DD5E7B2270300E60844898B70A213",
    //new byte[] { 0xD6, 0x9F, 0x66, 0xD0, 0x3D, 0x0D, 0xD5, 0xE7, 0xB2, 0x27, 0x03, 0x00, 0xE6, 0x08, 0x44, 0x89, 0x8B, 0x70, 0xA2, 0x13 },
    true).First();

Debug.Assert(developerCertificate != null);*/

var signer = new Signer(new CodeSignOptions {
    DeveloperCertificate = developerCertificate,
});
    //developerCertificate, null /*new Entitlements(provisioningProfile.Entitlements)*/);
//var stopwatch = new Stopwatch();
//stopwatch.Start();
signer.Sign(new Bundle(/*"/Applications/Airmail.localized/Airmail.app"*/ "/Users/filipnavara/Downloads/gc/MailClient.Mobile.iOS.app/"));
//stopwatch.Stop();
//Console.WriteLine("Stops: " + stopwatch.Elapsed);
