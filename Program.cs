// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using CodeSign.Signing;

Console.WriteLine("Hello, World!");

var provisioningProfile = new ProvisioningProfile("/Users/filipnavara/Library/MobileDevice/Provisioning Profiles/fd169b8e-5183-4fa1-af17-e9086a2adbcc.mobileprovision");
//var certificate = store.Certificates.Find(X509FindType.FindByIssuerName)
X509Certificate2 developerCertificate = null;
// Find certificate with private key
var store = new X509Store(StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadOnly);
/*foreach (var certificate in provisioningProfile.DeveloperCertificates)
{
    developerCertificate = store.Certificates.Find(X509FindType.FindBySubjectName, certificate.SubjectName, true).FirstOrDefault();
    if (developerCertificate != null)
        break;
}*/
developerCertificate = store.Certificates.Find(
    X509FindType.FindByThumbprint, 
    "D69F66D03D0DD5E7B2270300E60844898B70A213",
    //new byte[] { 0xD6, 0x9F, 0x66, 0xD0, 0x3D, 0x0D, 0xD5, 0xE7, 0xB2, 0x27, 0x03, 0x00, 0xE6, 0x08, 0x44, 0x89, 0x8B, 0x70, 0xA2, 0x13 },
    true).First();

Debug.Assert(developerCertificate != null);

var signer = new Signer(provisioningProfile, developerCertificate);
//var stopwatch = new Stopwatch();
//stopwatch.Start();
signer.Sign(new Bundle(/*"/Applications/Airmail.localized/Airmail.app"*/ "/Users/filipnavara/Downloads/gc/MailClient.Mobile.iOS.app/"));
//stopwatch.Stop();
//Console.WriteLine("Stops: " + stopwatch.Elapsed);