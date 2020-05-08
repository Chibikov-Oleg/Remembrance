using System;
using System.Collections.Generic;
using System.Linq;
using Easy.MessageHub;
using Mémoire.Contracts.DAL.Model;
using Mémoire.Contracts.DAL.SharedBetweenMachines;
using Mémoire.Contracts.Processing;
using Mémoire.Contracts.Processing.Data;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using Scar.Common.MVVM.Commands;
using Scar.Services.Contracts;
using Scar.Services.Contracts.Data.ExtendedTranslation;
using Scar.Services.Contracts.Data.Translation;

namespace Mémoire.ViewModel
{
    [AddINotifyPropertyChangedInterface]
    public sealed class TranslationVariantViewModel : PriorityWordViewModel, IWithExtendedExamples
    {
        public TranslationVariantViewModel(
            TranslationInfo translationInfo,
            TranslationVariant translationVariant,
            string parentText,
            ITextToSpeechPlayer textToSpeechPlayer,
            ITranslationEntryProcessor translationEntryProcessor,
            Func<Word, TranslationEntry, PriorityWordViewModel> priorityWordViewModelFactory,
            Func<Word, string, WordViewModel> wordViewModelFactory,
            Func<WordKey, string, WordImageViewerViewModel> wordImageViewerViewModelFactory,
            ILogger<PriorityWordViewModel> baseLogger,
            ITranslationEntryRepository translationEntryRepository,
            ICommandManager commandManager,
            ISharedSettingsRepository sharedSettingsRepository,
            IMessageHub messageHub) : base(
            translationInfo == null ? throw new ArgumentNullException(nameof(translationInfo)) : translationInfo.TranslationEntry,
            translationVariant,
            textToSpeechPlayer,
            translationEntryProcessor,
            baseLogger,
            wordViewModelFactory,
            translationEntryRepository,
            commandManager,
            sharedSettingsRepository,
            messageHub)
        {
            _ = translationInfo ?? throw new ArgumentNullException(nameof(translationInfo));
            _ = parentText ?? throw new ArgumentNullException(nameof(parentText));
            _ = translationVariant ?? throw new ArgumentNullException(nameof(translationVariant));
            _ = priorityWordViewModelFactory ?? throw new ArgumentNullException(nameof(priorityWordViewModelFactory));
            _ = wordImageViewerViewModelFactory ?? throw new ArgumentNullException(nameof(wordImageViewerViewModelFactory));

            Synonyms = translationVariant.Synonyms?.Select(synonym => priorityWordViewModelFactory(synonym, translationInfo.TranslationEntry)).ToArray();
            Meanings = translationVariant.Meanings?.Select(meaning => wordViewModelFactory(meaning, translationInfo.TranslationEntryKey.SourceLanguage)).ToArray();
            Examples = translationVariant.Examples;
            var translationVariantAndSynonymsHashSet = new HashSet<BaseWord>(translationVariant.GetTranslationVariantAndSynonyms());

            OrphanExtendedExamples = translationInfo.TranslationDetails.ExtendedTranslationResult?.ExtendedPartOfSpeechTranslations
                .Where(x => x.Translation.Text != null && translationVariantAndSynonymsHashSet.Contains(x.Translation))
                .SelectMany(x => x.ExtendedExamples).ToArray();
            WordImageViewerViewModel = wordImageViewerViewModelFactory(new WordKey(translationInfo.TranslationEntryKey, Word), parentText);
        }

        public IReadOnlyCollection<Example>? Examples { get; }

        public IReadOnlyCollection<ExtendedExample>? OrphanExtendedExamples { get; }

        public IReadOnlyCollection<WordViewModel>? Meanings { get; }

        public IReadOnlyCollection<PriorityWordViewModel>? Synonyms { get; }

        public WordImageViewerViewModel WordImageViewerViewModel { get; }
    }
}
