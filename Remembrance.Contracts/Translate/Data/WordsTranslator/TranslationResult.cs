﻿using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

// ReSharper disable NotNullMemberIsNotInitialized

namespace Remembrance.Contracts.Translate.Data.WordsTranslator
{
    public sealed class TranslationResult
    {
        [NotNull]
        [UsedImplicitly]
        public PartOfSpeechTranslation[] PartOfSpeechTranslations { get; set; }

        [NotNull]
        public IList<PriorityWord> GetDefaultWords()
        {
            return PartOfSpeechTranslations.Select(x => x.TranslationVariants.First()).Cast<PriorityWord>().ToList();
        }
    }
}