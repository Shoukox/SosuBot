using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.Abstract;

public interface IReceiverService
{
    Task ReceiveAsync(CancellationToken stoppingToken);
}
