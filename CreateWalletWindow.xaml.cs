using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
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
    /// Interaction logic for CreateWalletWindow.xaml
    /// </summary>
    public partial class CreateWalletWindow : Window
    {
        MainWindow mw;
        DB db;

        public CreateWalletWindow(MainWindow mw, DB db)
        {
            InitializeComponent();
            this.mw = mw;
            this.db = db;
        }

        public void Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void CreateWallet(object sender, RoutedEventArgs e)
        {
            String pwd = Password.Password;
            if (!pwd.Equals(PasswordRepeat.Password))
            {
                System.Windows.Forms.MessageBox.Show("Passwords do not match", "Password Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                Wallet w = new(0, Name.Text, Email.Text, null, null, 0, pwd);
                db.AddWallet(w);

                WalletInfoWindow wiw = new(mw, w);
                wiw.Visibility = Visibility.Visible;
                mw.infos.Add(wiw);

                mw.RefreshOwnedWallets();

                Close();
            } catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message, "Wallet Creation Error",
                       MessageBoxButtons.OK, MessageBoxIcon.Error);
                ConsoleWindow.WriteLine(ex);
            }
        }
    }
}
