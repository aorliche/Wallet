using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VirtualWallet
{
    /// <summary>
    /// Interaction logic for FilterWindow.xaml
    /// </summary>
    public partial class FilterWindow : Window
    {
        public String filterName;
        public String filterEmail;
        public String filterPubKey;
        public bool displayNodes;
        public MainWindow mw;

        public FilterWindow(MainWindow mw)
        {
            InitializeComponent();
            GetValues();
            this.mw = mw;
        }

        public void Cancel(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
        }

        public void Filter(object sender, RoutedEventArgs e)
        {
            GetValues();
            mw.RefreshOwnedWallets();
            mw.RefreshOtherWallets();
            mw.RefreshInfos();
        }

        public void GetValues()
        {
            filterName = Name.Text;
            filterEmail = Email.Text;
            filterPubKey = PubKey.Text;
            displayNodes = (bool)DisplayNodes.IsChecked;
        }

        public void Hide(object sender, CancelEventArgs e)
        {
            Visibility = Visibility.Hidden;
            e.Cancel = true;
        }

        public void Reset(object sender, RoutedEventArgs e)
        {
            Name.Text = "";
            Email.Text = "";
            PubKey.Text = "";
            DisplayNodes.IsChecked = false;
            GetValues();
        }
    }
}
