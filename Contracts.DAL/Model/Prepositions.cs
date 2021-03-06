using System.Collections.Generic;

namespace Mémoire.Contracts.DAL.Model
{
    public sealed class Prepositions
    {
        public IReadOnlyCollection<string>? Texts { get; set; }

        public override string ToString()
        {
            return Texts != null ? string.Join("/", Texts) : string.Empty;
        }
    }
}
