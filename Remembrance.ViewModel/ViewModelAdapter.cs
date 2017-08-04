﻿using System;
using Autofac;
using JetBrains.Annotations;
using Mapster;
using Remembrance.Contracts.DAL.Model;
using Remembrance.Contracts.Translate.Data.WordsTranslator;
using Remembrance.ViewModel.Translation;

namespace Remembrance.ViewModel
{
    [UsedImplicitly]
    public sealed class ViewModelAdapter
    {
        public ViewModelAdapter([NotNull] ILifetimeScope lifetimeScope)
        {
            if (lifetimeScope == null)
                throw new ArgumentNullException(nameof(lifetimeScope));

            TypeAdapterConfig<TranslationEntryViewModel, TranslationEntry>.NewConfig()
                .ConstructUsing(
                    src => new TranslationEntry
                    {
                        Key = new TranslationEntryKey
                        {
                            Text = src.Text,
                            SourceLanguage = src.Language,
                            TargetLanguage = src.TargetLanguage
                        }
                    })
                .Compile();
            TypeAdapterConfig<Word, WordViewModel>.NewConfig().ConstructUsing(src => lifetimeScope.Resolve<WordViewModel>()).Compile();
            TypeAdapterConfig<PriorityWord, PriorityWordViewModel>.NewConfig().ConstructUsing(src => lifetimeScope.Resolve<PriorityWordViewModel>()).Compile();
            TypeAdapterConfig<TranslationVariant, TranslationVariantViewModel>.NewConfig().ConstructUsing(src => lifetimeScope.Resolve<TranslationVariantViewModel>()).Compile();
            TypeAdapterConfig<PartOfSpeechTranslation, PartOfSpeechTranslationViewModel>.NewConfig().ConstructUsing(src => lifetimeScope.Resolve<PartOfSpeechTranslationViewModel>()).Compile();
            TypeAdapterConfig<TranslationEntry, TranslationEntryViewModel>.NewConfig()
                .ConstructUsing(src => lifetimeScope.Resolve<TranslationEntryViewModel>())
                .Map(dest => dest.Text, src => src.Key.Text)
                .Map(dest => dest.Language, src => src.Key.SourceLanguage)
                .Map(dest => dest.TargetLanguage, src => src.Key.TargetLanguage)
                .AfterMapping(
                    (src, dest) =>
                    {
                        foreach (var translation in dest.Translations)
                        {
                            translation.Language = dest.TargetLanguage;
                            translation.TranslationEntryId = dest.Id;
                        }
                    });
            TypeAdapterConfig<TranslationInfo, TranslationDetailsViewModel>.NewConfig()
                .ConstructUsing(src => new TranslationDetailsViewModel(lifetimeScope.Resolve<TranslationResultViewModel>()))
                .Map(dest => dest.TranslationResult, src => src.TranslationDetails.TranslationResult)
                .Map(dest => dest.Id, src => src.TranslationDetails.Id)
                .Map(dest => dest.TranslationEntryId, src => src.TranslationDetails.TranslationEntryId)
                .AfterMapping(
                    (src, dest) =>
                    {
                        foreach (var partOfSpeechTranslation in dest.TranslationResult.PartOfSpeechTranslations)
                        {
                            partOfSpeechTranslation.Language = src.Key.SourceLanguage;
                            foreach (var translationVariant in partOfSpeechTranslation.TranslationVariants)
                            {
                                translationVariant.Language = src.Key.TargetLanguage;
                                translationVariant.TranslationEntryId = dest.TranslationEntryId;
                                if (translationVariant.Synonyms != null)
                                    foreach (var synonym in translationVariant.Synonyms)
                                    {
                                        synonym.Language = src.Key.TargetLanguage;
                                        synonym.TranslationEntryId = dest.TranslationEntryId;
                                    }
                                if (translationVariant.Meanings != null)
                                    foreach (var meaning in translationVariant.Meanings)
                                        meaning.Language = src.Key.SourceLanguage;
                            }
                        }
                    })
                .Compile();
        }

        public TDestination Adapt<TDestination>(object source)
        {
            return source.Adapt<TDestination>();
        }

        public TDestination Adapt<TSource, TDestination>(TSource source, TDestination destination)
        {
            return source.Adapt(destination, TypeAdapterConfig.GlobalSettings);
        }
    }
}