using System;
using System.Windows;
using Autofac;
using Common.Logging;
using JetBrains.Annotations;
using Remembrance.Contracts.DAL;
using Remembrance.Contracts.DAL.Model;
using Scar.Common.WPF.Localization;
using Scar.Common.WPF.View.Contracts;

namespace Remembrance.Core.CardManagement
{
    [UsedImplicitly]
    internal abstract class BaseCardManager
    {
        [NotNull]
        protected readonly ILifetimeScope LifetimeScope;

        [NotNull]
        protected readonly ILog Logger;

        [NotNull]
        protected readonly ISettingsRepository SettingsRepository;

        protected BaseCardManager([NotNull] ILifetimeScope lifetimeScope, [NotNull] ISettingsRepository settingsRepository, [NotNull] ILog logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            LifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
            SettingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        }

        public void ShowCard(TranslationInfo translationInfo, IWindow ownerWindow)
        {
            //TODO: Sync context?
            Application.Current.Dispatcher.Invoke(
                () =>
                {
                    var window = TryCreateWindow(translationInfo, ownerWindow);
                    if (window == null)
                    {
                        Logger.Trace($"No window to show for {translationInfo}");
                        return;
                    }

                    CultureUtilities.ChangeCulture(SettingsRepository.Get().UiLanguage);
                    window.Draggable = false;
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    if (window.AdvancedWindowStartupLocation == AdvancedWindowStartupLocation.Default)
                        window.AdvancedWindowStartupLocation = AdvancedWindowStartupLocation.BottomRight;
                    Logger.Info($"Showing {translationInfo}...");
                    window.ShowActivated = false;
                    window.Topmost = true;
                    window.Restore();
                });
        }

        [CanBeNull]
        protected abstract IWindow TryCreateWindow([NotNull] TranslationInfo translationInfo, IWindow ownerWindow);
    }
}