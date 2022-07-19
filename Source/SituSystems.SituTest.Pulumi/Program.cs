using System.Threading.Tasks;
using Pulumi;

namespace SituSystems.SituTest.Pulumi
{
    internal class Program
    {
        private static Task<int> Main()
        {
            return Deployment.RunAsync<MyStack>();
        }
    }
}