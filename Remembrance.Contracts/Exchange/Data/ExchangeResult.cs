using System.Collections.Generic;
using JetBrains.Annotations;

namespace Remembrance.Contracts.Exchange.Data
{
    public sealed class ExchangeResult
    {
        public ExchangeResult(bool success, [CanBeNull] ICollection<string> errors, int count)
        {
            Success = success;
            Errors = errors;
            Count = count;
        }

        public bool Success { get; }

        [CanBeNull]
        public ICollection<string> Errors { get; }

        public int Count { get; }
    }
}