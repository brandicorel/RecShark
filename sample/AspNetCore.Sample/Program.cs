namespace RecShark.AspNetCore.Sample
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;
    using Serilog;

    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
            => Host.CreateDefaultBuilder(args)
                   .ConfigureWebHostDefaults(
                        webBuilder =>
                        {
                            webBuilder.UseSerilog()
                                      .UseStartup<Startup>();
                        });
    }
}
