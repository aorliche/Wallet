using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;
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
    public class UIException : ArgumentException
    {
        public UIException(String msg="Generic UI Exception") : base(msg) { }
    }

    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public readonly DB db;
        public readonly Web web;
        public readonly ConsoleWindow cons;
        public static BitmapImage locked32, unlocked32;
        public List<WalletInfoWindow> infos = new();
        public SendMoneyWindow sendWin = null;
        public FilterWindow filt;

        public MainWindow()
        {
            InitializeComponent();
            Title = String.Format("{0} v{1} ({2})", 
                Constants.PROG_NAME, Constants.PROG_VERSION, Constants.PROG_PLATFORM); 

            cons = new();
            cons.Visibility = Visibility.Hidden;
            ConsoleWindow.WriteLine(Title + " starting...");

            filt = new(this);
            filt.Visibility = Visibility.Hidden;

            db = new DB();
            web = new Web(db);

            LoadImages();
            RefreshOtherWallets();
            RefreshOwnedWallets();
            ConsoleWindow.WriteLine("Finished initialization");
        }

        public void About(object sender, RoutedEventArgs e)
        {
            AboutWindow about = new AboutWindow();
            about.Visibility = Visibility.Visible;
        }

        public void CreateWallet(object sender, RoutedEventArgs e)
        {
            CreateWalletWindow cww = new(this, db);
            cww.Visibility = Visibility.Visible;
        }

        public void Delete(object sender, RoutedEventArgs e)
        {
            if (OwnedWallets.SelectedItem == null)
            {
                System.Windows.Forms.MessageBox.Show("No owned wallet selected", "No Wallet Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }
            Wallet w = ((WalletControl)OwnedWallets.SelectedItem).wallet;
            String name = (w.name.Equals("")) ? "No Name" : w.name;
            DialogResult res = System.Windows.Forms.MessageBox.Show("Are you sure you want to delete wallet " + name + "?"
                + " You may lose all money stored on that wallet.", "Password Error",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (res == System.Windows.Forms.DialogResult.Yes)
            {
                try
                {
                    db.RemoveWallet(w);
                    RefreshOwnedWallets();
                    RefreshInfos();
                } catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Failed to remove wallet", "Database Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ConsoleWindow.WriteLine(ex);
                }
            }
        }

        public void DoOpenAction(Wallet w)
        {
            WalletInfoWindow wiw = new(this, w);
            wiw.Visibility = Visibility.Visible;
            infos.Add(wiw);
            wiw.Closing += delegate (object sender, CancelEventArgs e)
            {
                foreach (WalletInfoWindow testWiw in infos)
                {
                    if (wiw == testWiw)
                    {
                        infos.Remove(wiw);
                        break;
                    }
                }
            };
        }

        public void ExportOtherPublic(object sender, RoutedEventArgs e)
        {
            if (OtherWallets.SelectedItem == null)
            {
                System.Windows.Forms.MessageBox.Show("No owned wallet selected", "No Wallet Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }
            ExportWallet(((WalletControl)OtherWallets.SelectedItem).wallet, true);
        }

        public void ExportOwnedPrivate(object sender, RoutedEventArgs e)
        {
            if (OwnedWallets.SelectedItem == null)
            {
                System.Windows.Forms.MessageBox.Show("No owned wallet selected", "No Wallet Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }
            ExportWallet(((WalletControl)OwnedWallets.SelectedItem).wallet, false);
        }

        public void ExportOwnedPublic(object sender, RoutedEventArgs e)
        {
            if (OwnedWallets.SelectedItem == null)
            {
                System.Windows.Forms.MessageBox.Show("No owned wallet selected", "No Wallet Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }
            ExportWallet(((WalletControl)OwnedWallets.SelectedItem).wallet, true);
        }

        public static void ExportWallet(Wallet w, bool publicNotPrivate)
        {
            SaveFileDialog sfd = new();
            sfd.DefaultExt = "wallet";
            sfd.AddExtension = true;
            sfd.OverwritePrompt = true;
            sfd.FileName = w.name + ".wallet";
            DialogResult res = sfd.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }
            String fname = sfd.FileName;
            if (fname != null)
            {
                String privKeyPkcs8 = w.privKeyPkcs8;
                try
                {
                    if (publicNotPrivate)
                    {
                        w.privKeyPkcs8 = "";
                    }
                    DB.SaveWalletToFile(w, fname);
                } catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Failed to save wallet: " + ex.Message, "Save Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ConsoleWindow.WriteLine(ex);
                } finally
                {
                    w.privKeyPkcs8 = privKeyPkcs8;
                }
            }
        }

        public void Filter(object sender, RoutedEventArgs e)
        {
            filt.Visibility = Visibility.Visible;
        }

        public void ImportWallet(object sender, RoutedEventArgs e)
        {
            bool refreshOther = false;
            bool refreshOwned = false;
            OpenFileDialog ofd = new();
            ofd.ShowDialog();
            ofd.Multiselect = true;
            foreach (String fname in ofd.FileNames)
            {
                try
                {
                    ConsoleWindow.WriteLine("Loading wallet from " + fname);
                    Wallet w = DB.LoadWalletFromFile(fname);
                    db.AddWallet(w);
                    if (!w.privKeyPkcs8.Equals(""))
                    {
                        refreshOwned = true;
                    } else
                    {
                        refreshOther = true;
                    }
                } catch (Exception ex)
                {
                    ConsoleWindow.WriteLine(ex);
                }
            }
            try
            {
                if (refreshOwned)
                    RefreshOwnedWallets();
                if (refreshOther)
                    RefreshOtherWallets();
            } catch (Exception ex)
            {
                ConsoleWindow.WriteLine(ex);
            }
        }

        public static void LoadImages()
        {
            locked32 = new(new Uri("pack://application:,,,/locked32.png"));
            unlocked32 = new(new Uri("pack://application:,,,/unlocked32.png"));
        }

        public void OpenDeveloperConsole(object sender, RoutedEventArgs e)
        {
            cons.Visibility = Visibility.Visible;
        }

        public void OpenWallet(System.Windows.Controls.ListView view)
        {
            WalletControl wc = (WalletControl)view.SelectedItem;
            if (wc == null)
                return;
            DoOpenAction(wc.wallet);
        }

        public void OpenOtherWallet(object sender, RoutedEventArgs e)
        {
            OpenWallet(OtherWallets);
        }

        public void OpenOwnedWallet(object sender, RoutedEventArgs e)
        {
            OpenWallet(OwnedWallets);
        }

        public void Quit(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        public void Refresh(object sender, RoutedEventArgs e)
        {
            try
            {
                GetWalletsPacket wlp = new GetWalletsPacket(db.wallets);
                RefreshCallback rcb = new RefreshCallback(this);
                web.SendToNode(wlp, db.nodes[0], rcb);
            } catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(ex.Message, "Network Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                ConsoleWindow.WriteLine(ex);
            }
        }

        public void RefreshInfos()
        {
            foreach (WalletInfoWindow wiw in infos)
            {
                wiw.Balance.Content = Wallet.FormatBalance(wiw.wallet.balance);
                if (wiw.wallet.privKeyPkcs8.Equals(""))
                {
                    continue;
                }
                if (wiw.wallet.unlocked)
                {
                    wiw.Lock.Source = unlocked32;
                } else
                {
                    wiw.Lock.Source = locked32;
                }
            }
            if (sendWin != null)
            {
                if (sendWin.From.wallet != null)
                {
                    sendWin.From.Balance.Content = Wallet.FormatBalance(sendWin.From.wallet.balance);
                    if (!sendWin.From.wallet.privKeyPkcs8.Equals(""))
                    {
                        if (sendWin.From.wallet.unlocked)
                            sendWin.From.Lock.Source = MainWindow.unlocked32;
                        else
                            sendWin.From.Lock.Source = MainWindow.locked32;
                    }
                }
                if (sendWin.To.wallet != null)
                {
                    sendWin.To.Balance.Content = Wallet.FormatBalance(sendWin.To.wallet.balance);
                    if (!sendWin.To.wallet.privKeyPkcs8.Equals(""))
                    {
                        if (sendWin.To.wallet.unlocked)
                            sendWin.To.Lock.Source = MainWindow.unlocked32;
                        else
                            sendWin.To.Lock.Source = MainWindow.locked32;
                    }
                }
            }
        }

        public void RefreshOtherWallets()
        {
            RefreshWallets(false);
        }

        public void RefreshOwnedWallets()
        {
            RefreshWallets(true);
        }

        public void RefreshWallets(bool ownedNotOther)
        {
            int countAll = 0;
            long balance = 0;
            System.Windows.Controls.ListView Wallets = (ownedNotOther) ? OwnedWallets : OtherWallets;
            System.Windows.Controls.Label visibleLabel = (ownedNotOther) ? OwnedWalletsVisible : OtherWalletsVisible;
            Wallets.Items.Clear();
            foreach (Wallet w in db.wallets)
            {
                if (w.privKeyPkcs8.Equals("") && ownedNotOther)
                {
                    continue;
                } else if (!w.privKeyPkcs8.Equals("") && !ownedNotOther)
                {
                    continue;
                }
                if (ownedNotOther)
                    balance += w.balance;
                countAll++;
                // Show nodes
                if (!filt.displayNodes)
                {
                    if (!ownedNotOther && db.FindNodes(null, w.pubKey).Count > 0)
                    {
                        continue;
                    }
                }
                // Wallet name
                if (!filt.filterName.Equals(""))
                {
                    if (!w.name.Contains(filt.filterName, StringComparison.CurrentCultureIgnoreCase))
                        continue;
                }
                // Wallet email
                if (!filt.filterEmail.Equals(""))
                {
                    if (!w.email.Contains(filt.filterEmail, StringComparison.CurrentCultureIgnoreCase))
                        continue;
                }
                // Wallet public key
                if (!filt.filterPubKey.Equals(""))
                {
                    if (!w.pubKey.Contains(filt.filterPubKey))
                        continue;
                }
                WalletControl wc = new(this, w);
                wc.MouseDoubleClick += delegate (object sender, MouseButtonEventArgs e)
                {
                    DoOpenAction(w);
                };
                Wallets.Items.Add(wc);
            }
            if (ownedNotOther)
                Balance.Content = Wallet.FormatBalance(balance);
            visibleLabel.Content = "(" + Wallets.Items.Count + "/" + countAll + ")";
        }

        public void Remove(object sender, RoutedEventArgs e)
        {
            if (OtherWallets.SelectedItem == null)
            {
                System.Windows.Forms.MessageBox.Show("No other wallet selected", "No Wallet Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.None);
                return;
            }
            Wallet w = ((WalletControl)OtherWallets.SelectedItem).wallet;
            String name = (w.name.Equals("")) ? "No Name" : w.name;
            DialogResult res = System.Windows.Forms.MessageBox.Show("Are you sure you want to remove wallet " + name + "?", 
                "Password Error", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (res == System.Windows.Forms.DialogResult.Yes)
            {
                try
                {
                    db.RemoveWallet(w);
                    RefreshOtherWallets();
                    RefreshInfos();
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show("Failed to remove wallet", "Database Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ConsoleWindow.WriteLine(ex);
                }
            }
        }

        public void SendMoney(object sender, RoutedEventArgs e)
        {
            SendMoneyWindow smw = new(this);
            smw.Visibility = Visibility.Visible;
        }

        public void Shutdown(object sender, CancelEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    public class RefreshCallback : WebCallback
    {
        public MainWindow mw;

        public RefreshCallback(MainWindow mw)
        {
            this.mw = mw;
        }

        public override void Run(String json, Node n, Web web)
        {
            WalletListPacket wlp = (WalletListPacket)Packet.FromJson(json);
            if (!n.w.Verify(wlp))
            {
                System.Windows.Forms.MessageBox.Show("WalletListPacket from node " + n.uri + 
                    " failed verification (possible spoofing)", 
                    "Network Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ConsoleWindow.WriteLine("WalletListPacket failed verification");
                return;
            }
            int nFound = 0;
            foreach (Wallet w in mw.db.wallets)
            {
                bool found = false;
                foreach (Wallet wr in wlp.wallets)
                {
                    if (w.pubKey.Equals(wr.pubKey))
                    {
                        found = true;
                        nFound++;
                        mw.db.UpdateMetaAndBalance(w, null, null, wr.balance);
                        break;
                    }
                }
                if (!found && w.balance != 0)
                {
                    Console.WriteLine("Anomalous balance " + Wallet.FormatBalance(w.balance) + " for wallet " + w.name);
                }
            }
            System.Windows.Forms.MessageBox.Show("Retrieved balances for " + nFound + " wallets",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.None);
            ConsoleWindow.WriteLine("Updated " + nFound + " wallets");
            if (nFound > 0)
            {
                mw.RefreshOwnedWallets();
                mw.RefreshOtherWallets();
                mw.RefreshInfos();
            }
        }
    }
}
