using System;
using System.Collections.Generic;
using System.Linq;
using Easy.MessageHub;
using Microsoft.Extensions.Logging;
using PropertyChanged;
using Remembrance.Contracts.DAL.Model;
using Remembrance.Contracts.DAL.SharedBetweenMachines;
using Remembrance.Contracts.Processing;
using Remembrance.Contracts.Processing.Data;
using Remembrance.Contracts.Translate;
using Remembrance.Contracts.Translate.Data.WordsTranslator;
using Scar.Common.MVVM.Commands;

namespace Remembrance.ViewModel
{
    [AddINotifyPropertyChangedInterface]
    public class PriorityWordViewModel : WordViewModel
    {
        readonly ILogger _logger;

        readonly IMessageHub _messageHub;

        readonly TranslationEntry _translationEntry;

        readonly ITranslationEntryRepository _translationEntryRepository;

        readonly WordKey _wordKey;

        public PriorityWordViewModel(
            TranslationEntry translationEntry,
            Word word,
            ITextToSpeechPlayer textToSpeechPlayer,
            IMessageHub messageHub,
            ITranslationEntryProcessor translationEntryProcessor,
            ILogger<PriorityWordViewModel> logger,
            Func<Word, string, WordViewModel> wordViewModelFactory,
            ITranslationEntryRepository translationEntryRepository,
            ICommandManager commandManager) : base(
            word,
            translationEntry?.Id.TargetLanguage ?? throw new ArgumentNullException(nameof(translationEntry)),
            textToSpeechPlayer,
            translationEntryProcessor,
            commandManager)
        {
            _ = wordViewModelFactory ?? throw new ArgumentNullException(nameof(wordViewModelFactory));
            IsPriority = translationEntry.PriorityWords?.Contains(word) ?? false;
            _translationEntry = translationEntry;
            _messageHub = messageHub ?? throw new ArgumentNullException(nameof(messageHub));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _translationEntryRepository = translationEntryRepository ?? throw new ArgumentNullException(nameof(translationEntryRepository));
            _wordKey = new WordKey(translationEntry.Id, word);
        }

        [DoNotNotify]
        public override bool CanEdit { get; } = true;

        [DoNotNotify]
        public override string Language => _translationEntry.Id.TargetLanguage;

        public void SetIsPriority(bool isPriority)
        {
            IsPriority = isPriority;
        }

        protected override void TogglePriority()
        {
            var isPriority = !IsPriority;
            _logger.LogTrace("Changing priority for {0} to {1}...", _wordKey, isPriority);
            if (isPriority)
            {
                if (_translationEntry.PriorityWords == null)
                {
                    _translationEntry.PriorityWords = new HashSet<BaseWord> { _wordKey.Word };
                }
                else
                {
                    _translationEntry.PriorityWords.Add(_wordKey.Word);
                }
            }
            else
            {
                if (_translationEntry.PriorityWords == null)
                {
                    throw new InvalidOperationException("PriorityWords should not be null when deleting");
                }

                _translationEntry.PriorityWords.Remove(_wordKey.Word);
                if (!(_translationEntry.PriorityWords.Count > 0))
                {
                    _translationEntry.PriorityWords = null;
                }
            }

            _translationEntryRepository.Update(_translationEntry);
            IsPriority = isPriority;
            _logger.LogInformation("Priority has been changed for {0}", _wordKey);

            var priorityWordKey = new PriorityWordKey(isPriority, _wordKey);
            _messageHub.Publish(priorityWordKey);
        }
    }
}
