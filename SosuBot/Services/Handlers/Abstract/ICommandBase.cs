using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.Services.Handlers.Abstract
{
    public interface ICommandBase<TUpdateType> where TUpdateType : class
    {
        public void SetContext(ICommandContext<TUpdateType> context);
        public abstract Task ExecuteAsync();
    }
}
