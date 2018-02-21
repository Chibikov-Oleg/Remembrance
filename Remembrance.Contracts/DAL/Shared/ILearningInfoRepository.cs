using JetBrains.Annotations;
using Remembrance.Contracts.DAL.Model;
using Scar.Common.DAL;

namespace Remembrance.Contracts.DAL.Shared
{
    public interface ILearningInfoRepository : ITrackedRepository<LearningInfo, TranslationEntryKey>, ISharedRepository
    {
        [CanBeNull]
        LearningInfo GetMostSuitable();

        [NotNull]
        LearningInfo GetOrInsert([NotNull] TranslationEntryKey translationEntryKey);
    }
}