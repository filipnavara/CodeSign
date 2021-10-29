using System.Diagnostics;
using System.Security.Cryptography;
using Claunia.PropertyList;
using CodeSign.MachO;

class Signer
{
    public Signer()
    {
    }

    public void Sign(Bundle bundle)
    {
        if (bundle.MainExecutable != null)
        {
            /*if (MachOReader.TryLoadFat(File.OpenRead(Path.Combine(bundle.BundlePath, bundle.MainExecutable)), true, out var machOs) != MachOResult.NotMachO)
            {
                foreach (var machO in machOs)
                {
                    var cdBuilder = new CodeDirectoryBuilder(machO);
                    cdBuilder.Build();
                }
            }*/
            var s = File.OpenRead(Path.Combine(bundle.BundlePath, bundle.MainExecutable));
            foreach (var machO in MachOReader.Read(s))
            {
                var cdBuilder = new CodeDirectoryBuilder(machO, "", "");
                cdBuilder.Build();
            }
        }
        var resourceSeal = BuildResourceSeal(bundle);
        File.WriteAllText("seal.txt", resourceSeal.ToXmlPropertyList());
    }

    private static NSDictionary BuildResourceRulesPList(IEnumerable<ResourceRule> rules)
    {
        var rulesPList = new NSDictionary();

        foreach (var rule in rules)
        {
            if (rule.Weight == 1 && !rule.IsNested && !rule.IsOmitted && !rule.IsOptional)
            {
                rulesPList.Add(rule.Pattern, true);
            }
            else
            {
                var rulePList = new NSDictionary();
                if (rule.Weight != 1)
                    rulePList.Add("weight", (double)rule.Weight);
                if (rule.IsOmitted)
                    rulePList.Add("omit", true);
                if (rule.IsOptional)
                    rulePList.Add("optional", true);
                if (rule.IsNested)
                    rulePList.Add("nested", true);
                rulesPList.Add(rule.Pattern, rulePList);
            }
        }

        return rulesPList;
    }

    private static NSDictionary BuildResourceSeal(Bundle bundle)
    {
        var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];

        var resourceBuilder = new ResourceBuilder();
        bundle.AddResourceRules(resourceBuilder, useV2Rules: true);

        var rules2 = BuildResourceRulesPList(resourceBuilder.Rules);
        var files2 = new NSDictionary();
        foreach (var resourceAndRule in resourceBuilder.Scan(bundle.BundlePath))
        {
            var files2Value = new NSDictionary(2);

            Debug.Assert(resourceAndRule.Rule.IsNested == resourceAndRule.Info is DirectoryInfo);

            if (resourceAndRule.Info is DirectoryInfo)
            {
                // TODO: Nested signature on macOS (eg. Framework)
                throw new NotImplementedException();
            }
            else
            {
                if (resourceAndRule.Rule.IsOptional)
                {
                    files2Value.Add("optional", true);
                }

                using (var fileStream = File.OpenRead(resourceAndRule.Info.FullName))
                {
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer)) > 0)
                    {
                        sha1.AppendData(buffer.AsSpan(0, bytesRead));
                        sha256.AppendData(buffer.AsSpan(0, bytesRead));
                    }
                }

                files2Value.Add("hash", new NSData(sha1.GetHashAndReset()));
                files2Value.Add("hash2", new NSData(sha256.GetHashAndReset()));
            }

            files2.Add(resourceAndRule.Path, files2Value);
        };

        // Version 1 resources
        resourceBuilder = new ResourceBuilder();
        bundle.AddResourceRules(resourceBuilder, useV2Rules: false);

        var rules = BuildResourceRulesPList(resourceBuilder.Rules);
        var files = new NSDictionary();
        foreach (var resourceAndRule in resourceBuilder.Scan(bundle.BundlePath))
        {
            Debug.Assert(resourceAndRule.Info is FileInfo);

            if (files2.TryGetValue(resourceAndRule.Path, out var files2Value))
            {
                files.Add(resourceAndRule.Path, ((NSDictionary)files2Value)["hash"]);
            }
            else
            {
                using (var fileStream = File.OpenRead(resourceAndRule.Info.FullName))
                {
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer)) > 0)
                    {
                        sha1.AppendData(buffer.AsSpan(0, bytesRead));
                    }
                }

                files.Add(resourceAndRule.Path, new NSData(sha1.GetHashAndReset()));
            }
        }

        // Write down the rules and hashes
        return new NSDictionary { { "files", files }, { "files2", files2 }, { "rules", rules }, { "rules2", rules2 } };
    }
}
