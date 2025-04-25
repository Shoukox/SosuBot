using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SosuBot.Abstract;

namespace SosuBot.Services;

// Compose Polling and ReceiverService implementations
public class PollingService(IServiceProvider serviceProvider, ILogger<PollingService> logger) : PollingServiceBase<ReceiverService>(serviceProvider, logger);

