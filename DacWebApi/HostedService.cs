
using Dac.Interfaces;

namespace DacWebApi
{
    public class HostedService : IHostedService
    {
        private readonly ILogger _logger;
        private readonly IPlcService _plcSvc = null;

        public HostedService(ILogger<IHostedService> logger, IPlcService plcSvc)
        {
            _logger = logger;
            _plcSvc = plcSvc;
        }


        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Plc Service running.");
            DoWork();

            return Task.CompletedTask;
        }


        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Plc Service is stopping.");

            return Task.CompletedTask;
        }

        private void DoWork()
        {
            Thread thread = new Thread(() =>
            {
                do
                {
                    //斷線重連機制
                    if (!_plcSvc.IsConnected)
                    {
                        _plcSvc.Connect();
                    }
                    Thread.Sleep(2000);
                } while (true);
            });
            thread.Start();
        }


    }
}
