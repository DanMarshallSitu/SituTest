using Pulumi;
using Pulumi.AzureNative.Authorization;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Deployment = Pulumi.Deployment;

namespace SituSystems.SituTest.Pulumi
{
    internal class MyStack : Stack
    {
        public MyStack()
        {
            var config = new Config();
            var location = config.Require("location");
            var locationFull = config.Require("locationFull");
            var resourceName = config.Require("resourceName");
            var clientConfig = GetClientConfig.InvokeAsync().Result;
            var subscriptionId = clientConfig.SubscriptionId;

            var rgSituTestName = $"rg-situ-{resourceName}-{Deployment.Instance.StackName.ToLower()}";
            var rgSituTest = new ResourceGroup(rgSituTestName,
                new ResourceGroupArgs {Location = location, ResourceGroupName = rgSituTestName},
                new CustomResourceOptions {Protect = false});

            var storageAccountName = $"st{resourceName}{Deployment.Instance.StackName.ToLower()}";
            var storageSituTest = new StorageAccount(storageAccountName,
                new StorageAccountArgs
                {
                    AccountName = storageAccountName,
                    EnableHttpsTrafficOnly = true,
                    Encryption = new EncryptionArgs
                    {
                        KeySource = "Microsoft.Storage",
                        Services = new EncryptionServicesArgs
                        {
                            Blob = new EncryptionServiceArgs {Enabled = true, KeyType = "Account"},
                            File = new EncryptionServiceArgs {Enabled = true, KeyType = "Account"}
                        }
                    },
                    Kind = "Storage",
                    Location = location,
                    MinimumTlsVersion = "TLS1_2",
                    NetworkRuleSet =
                        new NetworkRuleSetArgs {Bypass = "AzureServices", DefaultAction = DefaultAction.Allow},
                    ResourceGroupName = rgSituTestName,
                    Sku = new SkuArgs {Name = "Standard_LRS"}
                },
                new CustomResourceOptions {Protect = false, DependsOn = rgSituTest});
        }
    }
}