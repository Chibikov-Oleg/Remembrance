using System.Windows;
using JetBrains.Annotations;
using Remembrance.Contracts.CardManagement;
using Remembrance.Resources;
using Scar.Common.View.Contracts;
using Scar.Common.WPF.View.Contracts;

namespace Remembrance.ViewModel
{
    [UsedImplicitly]
    internal sealed class WindowPositionAdjustmentManager : IWindowPositionAdjustmentManager
    {
        public void AdjustAnyWindowPosition(IDisplayable window)
        {
            if (!(window is IWindow wpfWindow))
            {
                return;
            }

            wpfWindow.Draggable = false;
            wpfWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            if (wpfWindow.AdvancedWindowStartupLocation == AdvancedWindowStartupLocation.Default)
            {
                wpfWindow.AdvancedWindowStartupLocation = AdvancedWindowStartupLocation.BottomRight;
            }

            wpfWindow.ShowActivated = false;
            wpfWindow.Topmost = true;
        }

        public void AdjustDetailsCardWindowPosition(IDisplayable window)
        {
            if (!(window is IWindow wpfWindow))
            {
                return;
            }

            wpfWindow.AdvancedWindowStartupLocation = AdvancedWindowStartupLocation.TopRight;
            wpfWindow.AutoCloseTimeout = AppSettings.TranslationCardCloseTimeout;
        }
    }
}
