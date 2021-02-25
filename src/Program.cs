using System.Threading.Tasks;
using Datafordelen.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Datafordelen
{
    class Program
    {
        public static async Task Main(string[] args)
        {

            using var serviceProvider = Setup.Configure();
            var startup = serviceProvider.GetService<Startup>();
            await startup.StartAsync();
        }
    }
}
