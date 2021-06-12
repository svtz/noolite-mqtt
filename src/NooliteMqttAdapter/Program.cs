using System.Threading.Tasks;

namespace NooliteMqttAdapter
{
    static class Program
    {
        static async Task Main(string[] args)
        {
            await new AdapterService().Run();
        }
    }
}