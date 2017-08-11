﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using Remembrance.Contracts;
using Remembrance.Contracts.CardManagement;
using Remembrance.Contracts.DAL;
using Remembrance.Contracts.DAL.Model;
using Remembrance.Contracts.Translate;
using Remembrance.Core.CardManagement.Data;
using Remembrance.Resources;

namespace Remembrance.Core.Exchange
{
    [UsedImplicitly]
    internal sealed class EachWordFileImporter : BaseFileImporter<EachWordExchangeEntry>
    {
        private static readonly char[] Separator =
        {
            ',',
            ';',
            '/',
            '\\'
        };

        [NotNull]
        private readonly ILanguageDetector _languageDetector;

        public EachWordFileImporter(
            [NotNull] ITranslationEntryRepository translationEntryRepository,
            [NotNull] ILog logger,
            [NotNull] IWordsProcessor wordsProcessor,
            [NotNull] IMessenger messenger,
            [NotNull] ITranslationDetailsRepository translationDetailsRepository,
            [NotNull] ILanguageDetector languageDetector,
            [NotNull] IEqualityComparer<IWord> wordsEqualityComparer,
            [NotNull] IWordPriorityRepository wordPriorityRepository)
            : base(translationEntryRepository, logger, wordsProcessor, messenger, translationDetailsRepository, wordsEqualityComparer, wordPriorityRepository)
        {
            _languageDetector = languageDetector ?? throw new ArgumentNullException(nameof(languageDetector));
        }

        protected override TranslationEntryKey GetKey(EachWordExchangeEntry exchangeEntry, CancellationToken token)
        {
            var sourceLanguage = _languageDetector.DetectLanguageAsync(exchangeEntry.Text, token).Result.Language ?? Constants.EnLanguage;
            var targetLanguage = WordsProcessor.GetDefaultTargetLanguage(sourceLanguage);
            return new TranslationEntryKey(exchangeEntry.Text, sourceLanguage, targetLanguage);
        }

        protected override ICollection<ExchangeWord> GetPriorityTranslations(EachWordExchangeEntry exchangeEntry)
        {
            return exchangeEntry.Translation?.Split(Separator, StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    x => new ExchangeWord
                    {
                        Text = x
                    })
                .ToArray();
        }
    }
}