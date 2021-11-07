# Project "Melanzana"

Implements framework for Apple Code Signing of bundles and Mach-O files in .NET.

## Status

The code is very much work in progress.

It can sign simple application bundles and Mach-O executables. Deep signing and signing of nested content (Plugins, Helpers, Frameworks) is not implemented yet. On iOS it can be accomplished on application level but on macOS the nested content needs to be sealed in bundle's resources and that bit is missing.

There's a simple command line tool for testing that can sign with ad-hoc signatures, certificates from system keychain, or certificates from Azure Key Vault. For Azure Key Vault the only supported authentication is logging in with the Azure CLI tool.
