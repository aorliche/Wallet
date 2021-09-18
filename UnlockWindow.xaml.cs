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
    /// Interaction logic for LockUnlockWindow.xaml
    /// </summary>
    public partial class UnlockWindow : Window
    {
        Wallet wallet;
        Image Lock;
        MainWindow mainWindow;

        public UnlockWindow(MainWindow mw, Wallet w)
        {
            InitializeComponent();
            mainWindow = mw;
            wallet = w;
            String namePart = (w.name.Equals("")) ? "No Name" : " " + w.name;
            String emailPart = (w.email.Equals("")) ? " (No Email)" : " (" + w.email + ") ";
            Info.Content = namePart + emailPart;
            Image.Source = WalletControl.GenerateImage(w, (int)Image.Width, (int)Image.Height);
        }

        public void Cancel(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void Unlock(object sender, RoutedEventArgs e)
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
                wallet.ImportPrivateKey(pwd);
                mainWindow.RefreshOwnedWallets();
                mainWindow.RefreshInfos();

                Close();
            } catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Incorrect Password", "Password Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                ConsoleWindow.WriteLine(ex);
            }
        }
    }
}
