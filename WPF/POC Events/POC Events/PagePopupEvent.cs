using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace POC_Events
{
    public class PageTransitionEventArgs: EventArgs
    {
        public Page PageToBeLoaded { get;}
        public object PayLoad { get; }

        public PageTransitionEventArgs(Page page, object payload = null)
        {
            PageToBeLoaded = page;
            PayLoad = payload;
        }
    }

    public class PageSwitchNotifier
    {
        public static event EventHandler<PageTransitionEventArgs> PageSwitchChanged;

        /// <summary>
        /// Invoke the Page Swith Change
        /// </summary>
        /// <param name="pageInstance"></param>
        public static void RaisePageSwitchChanged(Page pageInstance, object payLoad = null)
        {
            PageSwitchChanged?.Invoke(typeof(PageSwitchNotifier), new PageTransitionEventArgs(pageInstance, payLoad));
        }
    }
}
