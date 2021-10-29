// See https://aka.ms/new-console-template for more information
using System.Diagnostics;

Console.WriteLine("Hello, World!");

var signer = new Signer();
var stopwatch = new Stopwatch();
stopwatch.Start();
signer.Sign(new Bundle("/Applications/Airmail.localized/Airmail.app" /*"/Users/filipnavara/Downloads/gc/MailClient.Mobile.iOS.app/"*/));
stopwatch.Stop();
Console.WriteLine("Stops: " + stopwatch.Elapsed);