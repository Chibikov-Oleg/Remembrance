using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Common.Logging;
using Easy.MessageHub;
using PropertyChanged;
using Remembrance.Contracts.CardManagement;
using Remembrance.Contracts.DAL.Local;
using Remembrance.Contracts.DAL.Model;
using Remembrance.Contracts.DAL.Shared;
using Remembrance.Contracts.Languages;
using Remembrance.Contracts.Processing;
using Remembrance.Contracts.Processing.Data;
using Remembrance.Contracts.View;
using Remembrance.Contracts.View.Card;
using Remembrance.Contracts.View.Settings;
using Remembrance.Resources;
using Scar.Common;
using Scar.Common.DAL;
using Scar.Common.Localization;
using Scar.Common.MVVM.CollectionView;
using Scar.Common.MVVM.Commands;
using Scar.Common.View.Contracts;

namespace Remembrance.ViewModel
{
    [AddINotifyPropertyChangedInterface]
    public sealed class DictionaryViewModel : BaseViewModelWithAddTranslationControl
    {
        readonly Func<string, bool, ConfirmationViewModel> _confirmationViewModelFactory;

        readonly Func<ConfirmationViewModel, IConfirmationWindow> _confirmationWindowFactory;

        readonly ICultureManager _cultureManager;

        readonly IWindowFactory<IDictionaryWindow> _dictionaryWindowFactory;

        readonly ILearningInfoRepository _learningInfoRepository;

        readonly IMessageHub _messageHub;

        readonly IRateLimiter _rateLimiter;

        readonly IScopedWindowProvider _scopedWindowProvider;

        readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        readonly IWindowFactory<ISettingsWindow> _settingsWindowFactory;

        readonly IList<Guid> _subscriptionTokens = new List<Guid>();

        readonly SynchronizationContext _synchronizationContext;

        readonly Timer _timer;

        readonly ITranslationEntryRepository _translationEntryRepository;

        readonly Func<TranslationEntry, TranslationEntryViewModel> _translationEntryViewModelFactory;

        readonly ObservableCollection<TranslationEntryViewModel> _translationList;

        readonly IWindowPositionAdjustmentManager _windowPositionAdjustmentManager;

        int _count;

        bool _filterChanged;

        int _lastRecordedCount;

        bool _loaded;

        public DictionaryViewModel(
            ITranslationEntryRepository translationEntryRepository,
            ILocalSettingsRepository localSettingsRepository,
            ILanguageManager languageManager,
            ITranslationEntryProcessor translationEntryProcessor,
            ILog logger,
            IWindowFactory<IDictionaryWindow> dictionaryWindowFactory,
            IMessageHub messageHub,
            EditManualTranslationsViewModel editManualTranslationsViewModel,
            SynchronizationContext synchronizationContext,
            ILearningInfoRepository learningInfoRepository,
            Func<TranslationEntry, TranslationEntryViewModel> translationEntryViewModelFactory,
            IScopedWindowProvider scopedWindowProvider,
            IWindowFactory<ISettingsWindow> settingsWindowFactory,
            IRateLimiter rateLimiter,
            Func<string, bool, ConfirmationViewModel> confirmationViewModelFactory,
            Func<ConfirmationViewModel, IConfirmationWindow> confirmationWindowFactory,
            IWindowPositionAdjustmentManager windowPositionAdjustmentManager,
            ICultureManager cultureManager,
            ICommandManager commandManager,
            ICollectionViewSource collectionViewSource)
            : base(localSettingsRepository, languageManager, translationEntryProcessor, logger, commandManager)
        {
            EditManualTranslationsViewModel = editManualTranslationsViewModel ?? throw new ArgumentNullException(nameof(editManualTranslationsViewModel));
            _synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
            _learningInfoRepository = learningInfoRepository ?? throw new ArgumentNullException(nameof(learningInfoRepository));
            _translationEntryViewModelFactory = translationEntryViewModelFactory ?? throw new ArgumentNullException(nameof(translationEntryViewModelFactory));
            _scopedWindowProvider = scopedWindowProvider ?? throw new ArgumentNullException(nameof(scopedWindowProvider));
            _settingsWindowFactory = settingsWindowFactory ?? throw new ArgumentNullException(nameof(settingsWindowFactory));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _confirmationViewModelFactory = confirmationViewModelFactory ?? throw new ArgumentNullException(nameof(confirmationViewModelFactory));
            _confirmationWindowFactory = confirmationWindowFactory ?? throw new ArgumentNullException(nameof(confirmationWindowFactory));
            _windowPositionAdjustmentManager = windowPositionAdjustmentManager ?? throw new ArgumentNullException(nameof(windowPositionAdjustmentManager));
            _cultureManager = cultureManager ?? throw new ArgumentNullException(nameof(cultureManager));
            _messageHub = messageHub ?? throw new ArgumentNullException(nameof(messageHub));
            _dictionaryWindowFactory = dictionaryWindowFactory ?? throw new ArgumentNullException(nameof(dictionaryWindowFactory));
            _translationEntryRepository = translationEntryRepository ?? throw new ArgumentNullException(nameof(translationEntryRepository));
            _ = collectionViewSource ?? throw new ArgumentNullException(nameof(collectionViewSource));

            DeleteCommand = AddCommand<TranslationEntryViewModel>(DeleteAsync);
            OpenDetailsCommand = AddCommand<TranslationEntryViewModel>(OpenDetailsAsync);
            OpenSettingsCommand = AddCommand(OpenSettingsAsync);
            SearchCommand = AddCommand<string>(Search);
            WindowContentRenderedCommand = AddCommand(WindowContentRenderedAsync);

            Logger.Trace("Starting...");

            _translationList = new ObservableCollection<TranslationEntryViewModel>();
            _translationList.CollectionChanged += TranslationList_CollectionChanged;
            View = collectionViewSource.GetDefaultView(_translationList);

            Logger.Trace("Creating NextCardShowTime update timer...");

            _timer = new Timer(Timer_Tick, null, 0, 10000);

            Logger.Trace("Subscribing to the events...");

            _subscriptionTokens.Add(messageHub.Subscribe<TranslationEntry>(HandleTranslationEntryReceivedAsync));
            _subscriptionTokens.Add(messageHub.Subscribe<LearningInfo>(HandleLearningInfoReceivedAsync));
            _subscriptionTokens.Add(messageHub.Subscribe<IReadOnlyCollection<TranslationEntry>>(HandleTranslationEntriesBatchReceivedAsync));
            _subscriptionTokens.Add(messageHub.Subscribe<CultureInfo>(HandleUiLanguageChangedAsync));
            _subscriptionTokens.Add(messageHub.Subscribe<PriorityWordKey>(HandlePriorityChangedAsync));

            Logger.Debug("Started");
        }

        public int Count { get; private set; }

        public ICommand DeleteCommand { get; }

        public EditManualTranslationsViewModel EditManualTranslationsViewModel { get; }

        public ICommand OpenDetailsCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand SearchCommand { get; }

        public string? SearchText { get; set; }

        public ICollectionView View { get; }

        public ICommand WindowContentRenderedCommand { get; }

        protected override void Cleanup()
        {
            _translationList.CollectionChanged -= TranslationList_CollectionChanged;
            _timer.Dispose();
            foreach (var subscriptionToken in _subscriptionTokens)
            {
                _messageHub.Unsubscribe(subscriptionToken);
            }

            _subscriptionTokens.Clear();
        }

        protected override async Task<IDisplayable?> GetWindowAsync()
        {
            return await _dictionaryWindowFactory.GetWindowIfExistsAsync(CancellationTokenSource.Token).ConfigureAwait(false);
        }

        async Task DeleteAsync(TranslationEntryViewModel translationEntryViewModel)
        {
            _ = translationEntryViewModel ?? throw new ArgumentNullException(nameof(translationEntryViewModel));
            if (!await ConfirmDeletionAsync(translationEntryViewModel.ToString()))
            {
                return;
            }

            Logger.TraceFormat("Deleting {0} from the list...", translationEntryViewModel);

            await _semaphore.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false);
            var deleted = _translationList.Remove(translationEntryViewModel);
            _semaphore.Release();

            if (!deleted)
            {
                Logger.WarnFormat("{0} is not deleted from the list", translationEntryViewModel);
            }
            else
            {
                Logger.DebugFormat("{0} has been deleted from the list", translationEntryViewModel);
            }
        }

        async Task LoadTranslationsAsync()
        {
            await Task.Run(
                    async () =>
                    {
                        var pageNumber = 0;
                        bool result;
                        do
                        {
                            result = await LoadTranslationsPageAsync(pageNumber++).ConfigureAwait(false);
                        }
                        while (result);

                        _loaded = true;
                    },
                    CancellationTokenSource.Token)
                .ConfigureAwait(false);
        }

        async Task<bool> LoadTranslationsPageAsync(int pageNumber)
        {
            if (CancellationTokenSource.IsCancellationRequested)
            {
                return false;
            }

            Logger.TraceFormat("Receiving translations page {0}...", pageNumber);
            var translationEntries = _translationEntryRepository.GetPage(pageNumber, AppSettings.DictionaryPageSize, nameof(TranslationEntry.ModifiedDate), SortOrder.Descending);
            if (!translationEntries.Any())
            {
                return false;
            }

            await _semaphore.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false);
            var viewModels = translationEntries.Select(_translationEntryViewModelFactory);
            _synchronizationContext.Send(
                x =>
                {
                    foreach (var translationEntryViewModel in viewModels)
                    {
                        _translationList.Add(translationEntryViewModel);
                    }

                    UpdateCount();
                },
                null);

            _semaphore.Release();
            Logger.DebugFormat("{0} translations have been received", translationEntries.Count);
            return true;
        }

        async void HandleLearningInfoReceivedAsync(LearningInfo learningInfo)
        {
            _ = learningInfo ?? throw new ArgumentNullException(nameof(learningInfo));
            Logger.DebugFormat("Received {0} from external source", learningInfo);

            await Task.Run(
                    async () =>
                    {
                        await _semaphore.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false);
                        var translationEntryViewModel = _translationList.SingleOrDefault(x => x.Id.Equals(learningInfo.Id));
                        _semaphore.Release();
                        if (translationEntryViewModel == null)
                        {
                            Logger.DebugFormat("Translation entry is still not loaded for {0}", learningInfo);
                            return;
                        }

                        Logger.TraceFormat("Updating LearningInfo for {0} in the list...", translationEntryViewModel);
                        translationEntryViewModel.Update(learningInfo, translationEntryViewModel.ModifiedDate);
                        Logger.DebugFormat("LearningInfo for {0} has been updated in the list", translationEntryViewModel);
                        ScrollTo(translationEntryViewModel);
                    },
                    CancellationTokenSource.Token)
                .ConfigureAwait(false);
        }

        async void HandlePriorityChangedAsync(PriorityWordKey priorityWordKey)
        {
            _ = priorityWordKey ?? throw new ArgumentNullException(nameof(priorityWordKey));
            Logger.TraceFormat("Changing priority: {0}...", priorityWordKey);

            await Task.Run(
                    async () =>
                    {
                        await _semaphore.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false);
                        var translationEntryViewModel = _translationList.SingleOrDefault(x => x.Id.Equals(priorityWordKey.WordKey.TranslationEntryKey));
                        _semaphore.Release();

                        if (translationEntryViewModel == null)
                        {
                            Logger.WarnFormat("Cannot find {0} in translations list", priorityWordKey.WordKey);
                            return;
                        }

                        translationEntryViewModel.ProcessPriorityChange(priorityWordKey);
                        ScrollTo(translationEntryViewModel);
                    },
                    CancellationTokenSource.Token)
                .ConfigureAwait(false);
        }

        async void HandleTranslationEntriesBatchReceivedAsync(IReadOnlyCollection<TranslationEntry> translationEntries)
        {
            _ = translationEntries ?? throw new ArgumentNullException(nameof(translationEntries));
            Logger.DebugFormat("Received a batch of translations ({0} items) from the external source...", translationEntries.Count);

            await Task.Run(
                    async () =>
                    {
                        foreach (var translationEntry in translationEntries)
                        {
                            await OnTranslationEntryReceivedInternalAsync(translationEntry).ConfigureAwait(false);
                        }
                    },
                    CancellationTokenSource.Token)
                .ConfigureAwait(false);
        }

        async void HandleTranslationEntryReceivedAsync(TranslationEntry translationEntry)
        {
            _ = translationEntry ?? throw new ArgumentNullException(nameof(translationEntry));
            Logger.DebugFormat("Received {0} from external source", translationEntry);

            await Task.Run(async () => await OnTranslationEntryReceivedInternalAsync(translationEntry).ConfigureAwait(false), CancellationTokenSource.Token).ConfigureAwait(false);
        }

        async Task OnTranslationEntryReceivedInternalAsync(TranslationEntry translationEntry)
        {
            await _semaphore.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false);
            var existingTranslationEntryViewModel = _translationList.SingleOrDefault(x => x.Id.Equals(translationEntry.Id));
            _semaphore.Release();

            if (existingTranslationEntryViewModel != null)
            {
                Logger.TraceFormat("Updating {0} in the list...", existingTranslationEntryViewModel);
                var learningInfo = _learningInfoRepository.GetOrInsert(translationEntry.Id);
                existingTranslationEntryViewModel.Update(learningInfo, translationEntry.ModifiedDate);

                // no await here
                // ReSharper disable once AssignmentIsFullyDiscarded
                _ = existingTranslationEntryViewModel.ReloadTranslationsAsync(translationEntry);

                Logger.DebugFormat("{0} has been updated in the list", existingTranslationEntryViewModel);
                ScrollTo(existingTranslationEntryViewModel);
            }
            else
            {
                Logger.TraceFormat("Adding {0} to the list...", translationEntry);
                var translationEntryViewModel = _translationEntryViewModelFactory(translationEntry);
                await _semaphore.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false);
                _synchronizationContext.Send(x => _translationList.Insert(0, translationEntryViewModel), null);
                _semaphore.Release();

                Logger.DebugFormat("{0} has been added to the list...", translationEntryViewModel);
                ScrollTo(translationEntryViewModel);
            }
        }

        async void HandleUiLanguageChangedAsync(CultureInfo cultureInfo)
        {
            _ = cultureInfo ?? throw new ArgumentNullException(nameof(cultureInfo));
            Logger.TraceFormat("Changing UI language to {0}...", cultureInfo);

            await Task.Run(
                    async () =>
                    {
                        _cultureManager.ChangeCulture(cultureInfo);

                        await _semaphore.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false);
                        foreach (var translation in _translationList.SelectMany(translationEntryViewModel => translationEntryViewModel.Translations))
                        {
                            translation.ReRenderWord();
                        }

                        _semaphore.Release();
                    },
                    CancellationTokenSource.Token)
                .ConfigureAwait(false);
        }

        async Task OpenDetailsAsync(TranslationEntryViewModel translationEntryViewModel)
        {
            _ = translationEntryViewModel ?? throw new ArgumentNullException(nameof(translationEntryViewModel));
            Logger.TraceFormat("Opening details for {0}...", translationEntryViewModel);

            var translationEntry = _translationEntryRepository.GetById(translationEntryViewModel.Id);
            var translationDetails = await TranslationEntryProcessor
                .ReloadTranslationDetailsIfNeededAsync(translationEntry.Id, translationEntry.ManualTranslations, CancellationTokenSource.Token)
                .ConfigureAwait(false);
            var learningInfo = _learningInfoRepository.GetOrInsert(translationEntry.Id);
            var translationInfo = new TranslationInfo(translationEntry, translationDetails, learningInfo);
            var ownerWindow = await _dictionaryWindowFactory.GetWindowAsync(CancellationTokenSource.Token).ConfigureAwait(false);
            var window = await _scopedWindowProvider
                .GetScopedWindowAsync<ITranslationDetailsCardWindow, (IDisplayable, TranslationInfo)>((ownerWindow, translationInfo), CancellationTokenSource.Token)
                .ConfigureAwait(false);
            _synchronizationContext.Send(
                x =>
                {
                    _windowPositionAdjustmentManager.AdjustActivatedWindow(window);
                    window.Restore();
                },
                null);
        }

        async Task OpenSettingsAsync()
        {
            Logger.Trace("Opening settings...");

            await _settingsWindowFactory.ShowWindowAsync(CancellationTokenSource.Token).ConfigureAwait(false);
        }

        void ScrollTo(TranslationEntryViewModel translationEntryViewModel)
        {
            _synchronizationContext.Post(
                async x =>
                {
                    if (View.CurrentItem == translationEntryViewModel)
                    {
                        await _semaphore.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false);

                        if (_translationList.Any())
                        {
                            View.MoveCurrentTo(_translationList.First());
                        }

                        _semaphore.Release();
                    }

                    View.MoveCurrentTo(translationEntryViewModel);
                },
                null);
        }

        void Search(string? text)
        {
            Logger.TraceFormat("Searching for {0}...", text);

            if (string.IsNullOrWhiteSpace(text))
            {
                View.Filter = null;
            }
            else
            {
                View.Filter = obj =>
                {
                    var translationEntryViewModel = (TranslationEntryViewModel)obj;
                    return string.IsNullOrWhiteSpace(text)
                           || translationEntryViewModel.Id.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
                           || translationEntryViewModel.Translations.Any(translation => translation.Word.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
                };
            }

            _filterChanged = true;

            // Count = View.Cast<object>().Count();
        }

        async void Timer_Tick(object state)
        {
            await _semaphore.WaitAsync(CancellationTokenSource.Token).ConfigureAwait(false);
            foreach (var translation in _translationList)
            {
                var time = translation.LearningInfoViewModel.NextCardShowTime;
                if (time > DateTime.Now)
                {
                    translation.LearningInfoViewModel.ReRenderNextCardShowTime();
                }
            }

            _semaphore.Release();
        }

        void TranslationList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                    foreach (TranslationEntryViewModel translationEntryViewModel in e.OldItems)
                    {
                        Interlocked.Decrement(ref _count);
                        TranslationEntryProcessor.DeleteTranslationEntry(translationEntryViewModel.Id);
                    }

                    break;
                case NotifyCollectionChangedAction.Add:
                    foreach (var _ in e.NewItems)
                    {
                        Interlocked.Increment(ref _count);
                    }

                    break;
            }

            if (_loaded)
            {
                _rateLimiter.Throttle(TimeSpan.FromSeconds(1), UpdateCount);
            }
        }

        void UpdateCount()
        {
            if (!_filterChanged && _count == _lastRecordedCount)
            {
                return;
            }

            _filterChanged = false;
            _lastRecordedCount = _count;

            Count = View.Filter == null ? _count : View.Cast<object>().Count();
        }

        async Task WindowContentRenderedAsync()
        {
            await LoadTranslationsAsync().ConfigureAwait(false);
        }

        Task<bool> ConfirmDeletionAsync(string name)
        {
            var confirmationViewModel = _confirmationViewModelFactory(string.Format(Texts.AreYouSureDelete, name), true);

            _synchronizationContext.Send(
                x =>
                {
                    var confirmationWindow = _confirmationWindowFactory(confirmationViewModel);
                    confirmationWindow.ShowDialog();
                },
                null);
            return confirmationViewModel.UserInput;
        }
    }
}
