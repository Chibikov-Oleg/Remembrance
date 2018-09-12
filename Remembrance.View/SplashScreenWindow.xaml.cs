using JetBrains.Annotations;
using Remembrance.Contracts.View.Settings;

namespace Remembrance.View
{
    /// <summary>
    /// The splash screen window.
    /// </summary>
    [UsedImplicitly]
    internal sealed partial class SplashScreenWindow : ISplashScreenWindow
    {
        public SplashScreenWindow()
        {
            InitializeComponent();
        }
    }
}