# Project "Melanzana"

Implements Apple Code Signing of bundles and Mach-O files in .NET.

## Status

The code is very much work in progress but here's a checklist:

- [x] Implement framework for reading and rewriting Mach-O files (incomplete but good enough for code signing)
- [x] Generate Mach-O code signatures
- [x] Generate resource seals for app bundles
- [ ] Support for embedded entitlements
- [ ] Ad-hoc signatures
- [ ] Signing of frameworks and other nested code
- [ ] Comprehensive set of options (preserving old values, choosing hash types, setting requirements and entitlements)
- [ ] Standalone command line tool

