using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VirtualWallet
{
    /// <summary>
    /// Interaction logic for WalletInfoWindow.xaml
    /// </summary>
    public partial class WalletInfoWindow : Window
    {
        DB db;
        public Wallet wallet;
        MainWindow mw;

        public WalletInfoWindow(MainWindow mw, Wallet w)
        {
            InitializeComponent();
            this.mw = mw;
            wallet = w;
            this.db = mw.db;

            if (!w.privKeyPkcs8.Equals(""))
            {
                Lock.Source = (w.unlocked) ? MainWindow.unlocked32 : MainWindow.locked32;
                Lock.MouseDown += delegate (object sender, MouseButtonEventArgs e)
                {
                    WalletControl.LockUnlock(mw, w);
                };
            }

            Name.Content = (w.name.Equals("")) ? "No name" : w.name;
            Email.Content = (w.email.Equals("")) ? "No email" : w.email;
            Balance.Content = Wallet.FormatBalance(w.balance);
            PubKey.AppendText(Wallet.FormatPublicKey(w.pubKey));
            PubKey.IsReadOnly = true;
            Image.Source = WalletControl.GenerateImage(w, (int)Image.Width, (int)Image.Height);
        }

        public void ChangePassword(object sender, RoutedEventArgs e)
        {
            String pwd = NewPassword.Password;
            if (!pwd.Equals(RepeatNewPassword.Password))
            {
                System.Windows.Forms.MessageBox.Show("Passwords do not match", "Password Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                bool locked = !wallet.unlocked;
                wallet.ImportPrivateKey(CurrentPassword.Password);
                wallet.ChangePassword(pwd);
                db.UpdatePrivateKey(wallet);
                if (locked)
                    wallet.ImportPublicKey();
                System.Windows.Forms.MessageBox.Show("Password successfully changed", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.None);
            } catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message, "Password Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                ConsoleWindow.WriteLine(ex);
                return;
            }
        }
    }
}
