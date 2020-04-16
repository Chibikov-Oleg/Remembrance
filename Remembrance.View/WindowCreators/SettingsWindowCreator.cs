using System;
using System.Threading;
using System.Threading.Tasks;
using Remembrance.Contracts.View.Settings;
using Scar.Common;

namespace Remembrance.View.WindowCreators
{
    sealed class SettingsWindowCreator : IWindowCreator<ISettingsWindow>
    {
        readonly IWindowFactory<IDictionaryWindow> _dictionaryWindowFactory;
        readonly Func<IDictionaryWindow?, ISettingsWindow> _settingsWindowFactory;

        public SettingsWindowCreator(IWindowFactory<IDictionaryWindow> dictionaryWindowFactory, Func<IDictionaryWindow?, ISettingsWindow> settingsWindowFactory)
        {
            _dictionaryWindowFactory = dictionaryWindowFactory ?? throw new ArgumentNullException(nameof(dictionaryWindowFactory));
            _settingsWindowFactory = settingsWindowFactory ?? throw new ArgumentNullException(nameof(settingsWindowFactory));
        }

        public async Task<ISettingsWindow> CreateWindowAsync(CancellationToken cancellationToken)
        {
            var dictionaryWindow = await _dictionaryWindowFactory.GetWindowIfExistsAsync(cancellationToken).ConfigureAwait(false);
            return _settingsWindowFactory(dictionaryWindow);
        }
    }
}
