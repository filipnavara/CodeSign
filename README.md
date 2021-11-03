# Project "Manzana"

Implements Apple Code Signing of bundles and Mach-O files in .NET.

## Status

Very crude prototype at the moment, too many things to solve to even make a comprehensive list.

It can generate all the necessary code signing structure for a Mach-O file and create a seal over resource files in the bundle. The implementation of the actual code signing uses System.Security.Cryptography.Pkcs and not BouncyCastle like some similar closed source projects.
