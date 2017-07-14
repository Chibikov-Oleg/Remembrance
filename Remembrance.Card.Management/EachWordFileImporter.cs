﻿using System;
using System.Collections.Generic;
using Common.Logging;
using GalaSoft.MvvmLight.Messaging;
using JetBrains.Annotations;
using Remembrance.Card.Management.Contracts;
using Remembrance.Card.Management.Data;
using Remembrance.DAL.Contracts;
using Remembrance.DAL.Contracts.Model;
using Remembrance.Resources;
using Remembrance.Translate.Contracts.Interfaces;

namespace Remembrance.Card.Management
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
        private readonly ILanguageDetector languageDetector;

        public EachWordFileImporter([NotNull] ITranslationEntryRepository translationEntryRepository, [NotNull] ILog logger, [NotNull] IWordsAdder wordsAdder, [NotNull] IMessenger messenger, [NotNull] ITranslationDetailsRepository translationDetailsRepository, [NotNull] ILanguageDetector languageDetector)
            : base(translationEntryRepository, logger, wordsAdder, messenger, translationDetailsRepository)
        {
            this.languageDetector = languageDetector ?? throw new ArgumentNullException(nameof(languageDetector));
        }

        protected override TranslationEntryKey GetKey(EachWordExchangeEntry exchangeEntry)
        {
            var sourceLanguage = languageDetector.DetectLanguageAsync(exchangeEntry.Text).Result.Language ?? Constants.EnLanguage;
            var targetLanguage = WordsAdder.GetDefaultTargetLanguage(sourceLanguage);
            return new TranslationEntryKey(exchangeEntry.Text, sourceLanguage, targetLanguage);
        }

        protected override ICollection<string> GetPriorityTranslations(EachWordExchangeEntry exchangeEntry)
        {
            return exchangeEntry.Translation?.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}