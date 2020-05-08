using System;
using Mémoire.Contracts.DAL.Local;
using Mémoire.Contracts.DAL.Model;
using Scar.Common.ApplicationLifetime.Contracts;
using Scar.Common.DAL.LiteDB;

namespace Mémoire.DAL.Local
{
    sealed class WordImageInfoRepository : LiteDbRepository<WordImageInfo, WordKey>, IWordImageInfoRepository
    {
        public WordImageInfoRepository(IAssemblyInfoProvider assemblyInfoProvider) : base(assemblyInfoProvider?.SettingsPath ?? throw new ArgumentNullException(nameof(assemblyInfoProvider)))
        {
            Collection.EnsureIndex(x => x.Id.Word.Text);
            Collection.EnsureIndex(x => x.Id.Word.PartOfSpeech);
            Collection.EnsureIndex(x => x.Id.TranslationEntryKey.Text);
            Collection.EnsureIndex(x => x.Id.TranslationEntryKey.SourceLanguage);
            Collection.EnsureIndex(x => x.Id.TranslationEntryKey.TargetLanguage);
        }

        public void ClearForTranslationEntry(TranslationEntryKey translationEntryKey)
        {
            Collection.Delete(
                x => (x.Id.TranslationEntryKey.Text == translationEntryKey.Text) &&
                     (x.Id.TranslationEntryKey.SourceLanguage == translationEntryKey.SourceLanguage) &&
                     (x.Id.TranslationEntryKey.TargetLanguage == translationEntryKey.TargetLanguage));
        }
    }
}